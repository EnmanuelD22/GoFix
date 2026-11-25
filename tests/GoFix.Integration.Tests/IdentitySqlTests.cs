using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GoFix.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GoFix.Integration.Tests;

public sealed class IdentitySqlTests : IAsyncLifetime
{
    private const string Password = "ClaveDePruebas2026";
    private const string Phone = "+59176543210";
    private readonly string databaseName = $"GoFixIdentityTests_{Guid.NewGuid():N}";
    private readonly string connectionString;
    private readonly TestClock clock = new();
    private WebApplicationFactory<Program>? app;
    private bool ownsDatabase;

    public IdentitySqlTests()
    {
        var sql = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER"))
        {
            InitialCatalog = databaseName,
            AttachDBFilename = string.Empty
        };
        connectionString = sql.ConnectionString;
    }

    #region Ciclo de vida de la base aislada

    public async Task InitializeAsync()
    {
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:GoFix", connectionString);
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(clock));
            });
        });

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GoFixDbContext>();
        // Antes de migrar, verificamos cómo quedó configurado el host. Nunca se prueba sobre su base habitual.
        var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        var expected = new SqlConnectionStringBuilder(connectionString);
        Assert.Equal(expected.DataSource, actual.DataSource);
        Assert.Equal(databaseName, actual.InitialCatalog);
        Assert.Empty(actual.AttachDBFilename);
        Assert.NotEmpty(db.Database.GetMigrations());
        Assert.False(await db.Database.CanConnectAsync());
        ownsDatabase = true;
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (app is not null)
            await app.DisposeAsync();
        if (!ownsDatabase)
            return;

        var options = new DbContextOptionsBuilder<GoFixDbContext>().UseSqlServer(connectionString).Options;
        await using var db = new GoFixDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    #endregion

    #region Flujos de Identidad

    [SqlServerFact]
    public async Task RegistroSesionEdicionPropiaYRevocacionPersistenCorrectamente()
    {
        using var technician = CreateClient();
        using var customer = CreateClient();
        using var registration = await RegisterAsync(technician, "luz@example.test", "tecnico");
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        Assert.False(registration.Headers.Contains("Set-Cookie"));
        using var anonymous = await technician.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var anonymousToken = await GetCsrfTokenAsync(technician);

        using var techLogin = await LoginAsync(technician, "luz@example.test");
        Assert.Equal(HttpStatusCode.OK, techLogin.StatusCode);
        var authCookie = Assert.Single(techLogin.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("__Host-GoFix.Session=", StringComparison.Ordinal));
        Assert.Contains("secure", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", authCookie, StringComparison.OrdinalIgnoreCase);
        var techAccount = await ReadAccountAsync(technician);
        var accountId = techAccount.GetProperty("id").GetGuid();
        var profileId = techAccount.GetProperty("perfilTecnicoId").GetGuid();
        Assert.Equal("tecnico", techAccount.GetProperty("rol").GetString());
        Assert.NotEqual(Guid.Empty, profileId);
        Assert.NotEqual(accountId, profileId);

        using var clientRegistration = await RegisterAsync(customer, "noa@example.test", "cliente");
        Assert.Equal(HttpStatusCode.Created, clientRegistration.StatusCode);
        using var clientLogin = await LoginAsync(customer, "noa@example.test");
        Assert.Equal(HttpStatusCode.OK, clientLogin.StatusCode);
        var clientAccount = await ReadAccountAsync(customer);
        Assert.Equal("cliente", clientAccount.GetProperty("rol").GetString());
        Assert.Equal(JsonValueKind.Null, clientAccount.GetProperty("perfilTecnicoId").ValueKind);

        using var staleTokenRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/cuenta")
        {
            Content = JsonContent.Create(new { nombre = "Nombre Inválido", telefono = Phone })
        };
        staleTokenRequest.Headers.Add("X-CSRF-TOKEN", anonymousToken);
        using var staleTokenResponse = await technician.SendAsync(staleTokenRequest);
        Assert.Equal(HttpStatusCode.BadRequest, staleTokenResponse.StatusCode);
        Assert.Equal(techAccount.GetProperty("nombre").GetString(),
            (await ReadAccountAsync(technician)).GetProperty("nombre").GetString());

        using var edit = await SendMutationAsync(technician, HttpMethod.Put, "/api/v1/cuenta",
            new { nombre = "Luz Reforma", telefono = "+59171345678" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var updated = await ReadAccountAsync(technician);
        Assert.Equal("Luz Reforma", updated.GetProperty("nombre").GetString());
        Assert.Equal("+59171345678", updated.GetProperty("telefono").GetString());
        Assert.Equal("luz@example.test", updated.GetProperty("correo").GetString());
        Assert.Equal(profileId, updated.GetProperty("perfilTecnicoId").GetGuid());
        var unaffected = await ReadAccountAsync(customer);
        Assert.Equal(clientAccount.GetProperty("id").GetGuid(), unaffected.GetProperty("id").GetGuid());
        Assert.Equal(clientAccount.GetProperty("nombre").GetString(), unaffected.GetProperty("nombre").GetString());
        Assert.Equal(Phone, unaffected.GetProperty("telefono").GetString());

        using var logout = await SendMutationAsync(technician, HttpMethod.Post, "/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var signedOut = await technician.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);

        // La sesión debe quedar revocada en el servidor, además de borrar la cookie del navegador.
        using var priorSession = CreateClient();
        using var sessionCheck = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        sessionCheck.Headers.Add("Cookie", authCookie.Split(';', 2)[0]);
        using var revoked = await priorSession.SendAsync(sessionCheck);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    [SqlServerFact]
    public async Task ActividadNoProlongaLaVigenciaDeLaSesion()
    {
        using var client = CreateClient();
        using var registration = await RegisterAsync(client, "vigencia@example.test", "cliente");
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await LoginAsync(client, "vigencia@example.test");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        clock.Advance(TimeSpan.FromMinutes(40));
        using var active = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.False(active.Headers.Contains("Set-Cookie"));

        clock.Advance(TimeSpan.FromMinutes(21));
        using var expired = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
    }

    [SqlServerFact]
    public async Task QuintoFalloBloqueaAccesoSinDistinguirLaCuentaEnLaRespuesta()
    {
        const string email = "intentos@example.test";
        const string wrongPassword = "ClaveIncorrecta2026";
        using var client = CreateClient();
        using var successfulClient = CreateClient();
        using var registration = await RegisterAsync(client, email, "cliente");
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var success = await LoginAsync(successfulClient, email);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        using var unknown = await LoginAsync(client, "inexistente@example.test", wrongPassword);
        var genericError = await ReadCredentialErrorAsync(unknown);

        for (var i = 0; i < 4; i++)
        {
            using var failed = await LoginAsync(client, email, wrongPassword);
            Assert.Equal(genericError, await ReadCredentialErrorAsync(failed));
        }

        await using var scope = app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GoFixDbContext>();
        var accounts = db.Users.AsNoTracking().Where(user => user.Email == email);
        Assert.Null(await accounts.Select(user => user.LockoutEnd).SingleAsync());

        using var fifth = await LoginAsync(client, email, wrongPassword);
        Assert.Equal(genericError, await ReadCredentialErrorAsync(fifth));
        var lockoutEnd = await accounts.Select(user => user.LockoutEnd).SingleAsync();
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));

        using var locked = await LoginAsync(client, email);
        Assert.Equal(genericError, await ReadCredentialErrorAsync(locked));

        // Simula el vencimiento de la ventana persistida solo en la base desechable.
        await db.Users.Where(user => user.Email == email)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.LockoutEnd, DateTimeOffset.UtcNow.AddSeconds(-1)));
        using var recovered = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(0, await accounts.Select(user => user.AccessFailedCount).SingleAsync());
        Assert.True(await accounts.Select(user => user.LockoutEnd).SingleAsync() <= DateTimeOffset.UtcNow);
    }

    [SqlServerFact]
    public async Task CorreoNormalizadoEsUnicoAnteDosRegistrosSimultaneos()
    {
        using var first = CreateClient();
        using var second = CreateClient();
        var responses = await Task.WhenAll(RegisterAsync(first, "par@example.test", "cliente"),
            RegisterAsync(second, "PAR@example.test", "cliente"));
        using var accepted = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        using var rejected = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.True(problem.RootElement.TryGetProperty("errors", out _));
    }

    [SqlServerFact]
    public async Task FalloDePerfilRevierteCuentaYPermiteReintentarElRegistro()
    {
        await using var scope = app!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GoFixDbContext>();
        // Solo en esta base desechable, una restricción fuerza el fallo al guardar el perfil.
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE [professionals].[PerfilTecnico] ADD CONSTRAINT [Prueba_ForzarFalloPerfil]
            CHECK ([UsuarioId] IS NULL);
            """);

        using var client = CreateClient();
        using var failed = await RegisterAsync(client, "reintento@example.test", "tecnico");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);

        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE [professionals].[PerfilTecnico] DROP CONSTRAINT [Prueba_ForzarFalloPerfil];
            """);
        using var retry = await RegisterAsync(client, "reintento@example.test", "tecnico");
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        using var login = await LoginAsync(client, "reintento@example.test");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.NotEqual(Guid.Empty, (await ReadAccountAsync(client)).GetProperty("perfilTecnicoId").GetGuid());
    }

    #endregion

    #region Cliente HTTP para pruebas

    private HttpClient CreateClient() => app!.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string role) =>
        SendMutationAsync(client, HttpMethod.Post, "/api/v1/auth/register", new
        {
            nombre = "Persona Prueba", correo = email, telefono = Phone, contrasena = Password, rol = role
        });

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password) =>
        SendMutationAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { correo = email, contrasena = password });

    private static async Task<(string? Title, string? Code)> ReadCredentialErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (problem.RootElement.GetProperty("title").GetString(), problem.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadAccountAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> SendMutationAsync(
        HttpClient client, HttpMethod method, string route, object? data = null)
    {
        using var mutation = new HttpRequestMessage(method, route);
        mutation.Headers.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        if (data is not null)
            mutation.Content = JsonContent.Create(data);
        return await client.SendAsync(mutation);
    }

    private static async Task<string?> GetCsrfTokenAsync(HttpClient client)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("token").GetString();
    }

    #endregion

    private sealed class TestClock : TimeProvider
    {
        private long ticks = DateTimeOffset.UtcNow.Ticks;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);

        public void Advance(TimeSpan duration) => Interlocked.Add(ref ticks, duration.Ticks);
    }
}
