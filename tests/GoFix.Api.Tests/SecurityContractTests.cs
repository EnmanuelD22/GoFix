using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GoFix.Application.Modules.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GoFix.Api.Tests;

public sealed class SecurityContractTests
{
    [Fact]
    public async Task CookieSeguraVenceSinRenovarseYLaSesionRevocadaSeRechaza()
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        var cookie = await LoginAsync(client);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        Assert.False(options.SlidingExpiration);
        var value = cookie.Split(';')[0].Split('=', 2)[1];
        var ticket = options.TicketDataFormat.Unprotect(value);
        Assert.NotNull(ticket);
        Assert.Equal(factory.Clock.GetUtcNow().AddMinutes(60), ticket.Properties.ExpiresUtc);
        factory.Clock.Advance(TimeSpan.FromMinutes(59));
        using var before = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.False(before.Headers.Contains("Set-Cookie"));
        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var expired = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var another = Client(factory);
        await LoginAsync(another);
        factory.Store.Active = false;
        using var revoked = await another.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/auth/logout", "cliente")]
    [InlineData("PUT", "/api/v1/cuenta", "cliente")]
    [InlineData("POST", "/api/v1/solicitudes", "cliente")]
    [InlineData("PUT", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111", "cliente")]
    [InlineData("DELETE", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111", "cliente")]
    [InlineData("POST", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/cancelar", "cliente")]
    [InlineData("POST", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/valoracion", "cliente")]
    [InlineData("PUT", "/api/v1/tecnicos/me/perfil", "tecnico")]
    [InlineData("POST", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/aceptar", "tecnico")]
    [InlineData("POST", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/rechazar", "tecnico")]
    [InlineData("POST", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/iniciar", "tecnico")]
    [InlineData("POST", "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/completar", "tecnico")]
    public async Task MutacionesAutenticadasExigenToken(string method, string path, string role)
    {
        using var factory = new SecurityFactory();
        factory.Store.Role = role;
        using var client = Client(factory);
        await LoginAsync(client);
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) };
        using var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CabeceraIpDeOrigenNoConfiableNoEludeLimiteCompartido()
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        for (var i = 0; i < 10; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                i % 2 == 0 ? "/api/v1/auth/register" : "/api/v1/auth/login")
            { Content = JsonContent.Create(new { correo = "" }) };
            request.Headers.Add("X-Forwarded-For", "192.0.2." + (i + 1));
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "" });
        await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task ErrorInesperadoDevuelveCorrelacionSinRevelarDatosEnRespuestaOLogs()
    {
        using var factory = new SecurityFactory();
        factory.Store.ThrowOnLogin = true;
        using var client = Client(factory);
        var token = await CsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login?private=" + SecurityFactory.Secret)
        { Content = JsonContent.Create(new { correo = "ana@example.test", contrasena = SecurityFactory.Secret }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Headers.Add("Cookie", "private=" + SecurityFactory.Secret);
        using var response = await client.SendAsync(request);
        var trace = await AssertProblemAsync(response, HttpStatusCode.InternalServerError);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecurityFactory.Secret, text);
        Assert.Contains(factory.Logs.Entries, entry => entry.Contains(trace, StringComparison.Ordinal));
        Assert.DoesNotContain(factory.Logs.Entries, entry => entry.Contains(SecurityFactory.Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConflictoDevuelveCodigoYCorrelacionEnDiagnostico()
    {
        using var factory = new SecurityFactory();
        using var client = Client(factory);
        await LoginAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "/api/v1/solicitudes/11111111-1111-1111-1111-111111111111/valoracion")
        { Content = JsonContent.Create(new { puntuacion = 5, comentario = "Atención correcta." }) };
        request.Headers.Add("X-CSRF-TOKEN", await CsrfAsync(client));
        using var response = await client.SendAsync(request);
        var trace = await AssertProblemAsync(response, HttpStatusCode.Conflict);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("valoracion_duplicada", body.GetProperty("code").GetString());
        Assert.Contains(factory.Logs.Entries, entry => entry.Contains(trace, StringComparison.Ordinal));
    }

    private static HttpClient Client(SecurityFactory factory) => factory.CreateClient(new()
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var token = await CsrfAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        { Content = JsonContent.Create(new { correo = "ana@example.test", contrasena = "ClaveDePruebas2026" }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-GoFix.Session="));
    }

    private static async Task<string> CsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static async Task<string> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("code").GetString()));
        var trace = body.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(trace));
        return trace!;
    }

    private sealed class SecurityFactory : WebApplicationFactory<Program>
    {
        internal const string Secret = "PasswordAddressPhone-18095559876";
        internal readonly TestClock Clock = new();
        internal readonly TestIdentityStore Store;
        internal readonly CapturedLogs Logs = new();
        internal SecurityFactory() => Store = new(Clock);
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IIdentityStore>();
            services.AddSingleton<IIdentityStore>(Store);
            services.RemoveAll<GoFix.Application.Modules.Reviews.IReviewStore>();
            services.RemoveAll<GoFix.Application.Modules.Services.IReviewEligibility>();
            services.AddSingleton<GoFix.Application.Modules.Reviews.IReviewStore, DuplicateReview>();
            services.AddSingleton<GoFix.Application.Modules.Services.IReviewEligibility, DuplicateReview>();
            services.AddSingleton<ILoggerProvider>(Logs);
            services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = Clock);
        });
    }

    private sealed class DuplicateReview : GoFix.Application.Modules.Reviews.IReviewStore, GoFix.Application.Modules.Services.IReviewEligibility
    {
        public Task<GoFix.Application.Modules.Services.ReviewTarget?> GetOwnAsync(Guid customer, Guid request, CancellationToken ct) =>
            Task.FromResult<GoFix.Application.Modules.Services.ReviewTarget?>(new(request, customer, Guid.NewGuid(), true));
        public Task<GoFix.Application.Modules.Reviews.ReviewResult> CreateAsync(GoFix.Application.Modules.Services.ReviewTarget target,
            GoFix.Application.Modules.Reviews.CreateReview input, DateTime now, CancellationToken ct) =>
            Task.FromResult(new GoFix.Application.Modules.Reviews.ReviewResult(Code: "valoracion_duplicada"));
        public Task<GoFix.Application.Modules.Reviews.ReviewDetail?> GetAsync(Guid request, CancellationToken ct) =>
            Task.FromResult<GoFix.Application.Modules.Reviews.ReviewDetail?>(null);
        public Task<GoFix.Application.Modules.Reviews.ReviewPage?> ListAsync(Guid technician, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<GoFix.Application.Modules.Reviews.ReviewPage?>(null);
    }

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => now;
        internal void Advance(TimeSpan value) => now += value;
    }

    private sealed class TestIdentityStore(TestClock clock) : IIdentityStore
    {
        internal bool Active = true;
        internal bool ThrowOnLogin;
        internal string Role = AccountRole.Customer;
        private readonly Guid userId = Guid.NewGuid();
        private Account Account => new(userId, "Ana Prueba", "ana@example.test", "+18095550100", Role, null);
        public Task<bool> RegisterAsync(Registration input, CancellationToken ct) => Task.FromResult(true);
        public Task<Session?> AuthenticateAsync(Credentials input, CancellationToken ct) => ThrowOnLogin
            ? throw new InvalidOperationException(SecurityFactory.Secret)
            : Task.FromResult<Session?>(new(Guid.NewGuid(), Account, clock.GetUtcNow().AddMinutes(60)));
        public Task<Account?> FindAccountAsync(Guid id, CancellationToken ct) => Task.FromResult<Account?>(Account);
        public Task<Account?> UpdateAsync(Guid id, AccountDetails input, CancellationToken ct) => Task.FromResult<Account?>(Account);
        public Task<bool> HasSessionAsync(Guid id, Guid session, CancellationToken ct) => Task.FromResult(Active);
        public Task RevokeSessionAsync(Guid id, Guid session, CancellationToken ct) { Active = false; return Task.CompletedTask; }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        internal readonly ConcurrentQueue<string> Entries = new();
        public ILogger CreateLogger(string categoryName) => new Capture(Entries);
        public void Dispose() { }
        private sealed class Capture(ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(formatter(state, exception) + (exception?.ToString() ?? ""));
        }
    }
}
