using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace GoFix.Api.Tests;

public sealed class IdentityHttpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public IdentityHttpTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/login")]
    public async Task CredencialesSinTokenAntifalsificacionSonRechazadas(string ruta)
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync(ruta, new
        {
            nombre = "Ana Prueba", correo = "ana@example.test", telefono = "7654321012",
            contrasena = "ClaveDePruebas2026", rol = "cliente"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("GET", "/api/v1/auth/me")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("PUT", "/api/v1/cuenta")]
    public async Task RutasPrivadasDevuelven401SinRedirigir(string metodo, string ruta)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (request.Method == HttpMethod.Put)
            request.Content = JsonContent.Create(new { nombre = "Ana Prueba", telefono = "7654321012" });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RegistroYAccesoCompartenLimiteDeDiezPeticiones()
    {
        // Cada host de prueba empieza con un contador independiente.
        using var isolatedFactory = new WebApplicationFactory<Program>();
        using var client = CreateClient(isolatedFactory);
        using var csrf = await client.GetAsync("/api/v1/auth/csrf");
        csrf.EnsureSuccessStatusCode();
        using var token = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());

        for (var i = 0; i < 5; i++)
        {
            // Datos vacíos ejercitan las rutas y su política compartida sin consultar SQL Server.
            using var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { correo = "" });
            Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
            using var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "" });
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/login", new { correo = "" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter?.Delta);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
    }

    private HttpClient CreateClient() => CreateClient(factory);

    private static HttpClient CreateClient(WebApplicationFactory<Program> host) => host.CreateClient(new()
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });
}
