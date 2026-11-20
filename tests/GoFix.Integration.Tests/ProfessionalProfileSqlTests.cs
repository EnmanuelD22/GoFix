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
using Xunit;

namespace GoFix.Integration.Tests;

public sealed class ProfessionalProfileSqlTests : IAsyncLifetime
{
    private const string ProfileRoute = "/api/v1/tecnicos/me/perfil";
    private const string Description = "Atención profesional de instalaciones y mantenimiento con experiencia en servicios domésticos.";
    private readonly string databaseName = $"GoFixProfileTests_{Guid.NewGuid():N}";
    private readonly string connectionString;
    private WebApplicationFactory<Program>? app;
    private bool ownsDatabase;

    public ProfessionalProfileSqlTests()
    {
        connectionString = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER"))
        { InitialCatalog = databaseName, AttachDBFilename = string.Empty }.ConnectionString;
    }

    #region Base SQL Server aislada

    public async Task InitializeAsync()
    {
        app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:GoFix", connectionString);
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GoFixDbContext>();
        var actual = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        Assert.Equal(databaseName, actual.InitialCatalog);
        Assert.Equal(new SqlConnectionStringBuilder(connectionString).DataSource, actual.DataSource);
        Assert.Empty(actual.AttachDBFilename);
        Assert.False(await db.Database.CanConnectAsync());
        ownsDatabase = true;
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO professionals.Especialidad(Codigo, Nombre, Activa) VALUES
            ('prueba-activa', N'Activa de prueba', 1), ('prueba-inactiva', N'Inactiva de prueba', 0),
            ('prueba-segunda', N'Segunda de prueba', 1);
            INSERT INTO professionals.Sector(Codigo, Nombre, Municipio, Activo) VALUES
            ('sector-activo', N'Sector activo', N'Municipio de prueba', 1),
            ('sector-inactivo', N'Sector inactivo', N'Municipio de prueba', 0);
            """);
    }

    public async Task DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
        if (!ownsDatabase) return;
        Assert.Matches("\\AGoFixProfileTests_[a-f0-9]{32}\\z", databaseName);
        await using var db = new GoFixDbContext(new DbContextOptionsBuilder<GoFixDbContext>().UseSqlServer(connectionString).Options);
        await db.Database.EnsureDeletedAsync();
    }

    #endregion

    #region Catálogos y permisos

    [SqlServerFact]
    public async Task CatalogosPublicosExcluyenInactivosYPerfilExigeRolTecnico()
    {
        using var visitor = Client();
        var specialties = await ReadAsync(visitor, "/api/v1/especialidades");
        Assert.Equal(2, specialties.GetArrayLength());
        Assert.DoesNotContain(specialties.EnumerateArray(), item => item.GetProperty("nombre").GetString() == "Inactiva de prueba");
        var sectors = await ReadAsync(visitor, "/api/v1/sectores");
        Assert.Equal(1, sectors.GetArrayLength());
        Assert.Equal("Municipio de prueba", sectors[0].GetProperty("municipio").GetString());
        using var anonymous = await visitor.GetAsync(ProfileRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var customer = await RegisteredAsync("cliente", "cliente@example.test");
        using var forbidden = await customer.GetAsync(ProfileRoute);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var forbiddenEdit = await SendAsync(customer, Data("AAAAAAAAAAA="));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEdit.StatusCode);
    }

    [SqlServerFact]
    public async Task PublicacionDependeDeCompletitudYEstadoActualDelCatalogo()
    {
        using var tech = await RegisteredAsync("tecnico", "perfil@example.test");
        var initial = await ReadAsync(tech, ProfileRoute);
        Assert.False(initial.GetProperty("publicado").GetBoolean());
        using var update = await SendAsync(tech, Data(Version(initial)));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var published = await ReadAsync(tech, ProfileRoute);
        Assert.True(published.GetProperty("publicado").GetBoolean());
        Assert.Equal(Description, published.GetProperty("descripcion").GetString());
        Assert.NotEqual(Version(initial), Version(published));

        // Desactivar el único sector no cambia la cobertura histórica, pero retira la publicación.
        await ExecuteAsync("UPDATE professionals.Sector SET Activo = 0 WHERE Codigo = 'sector-activo';");
        var unpublished = await ReadAsync(tech, ProfileRoute);
        Assert.False(unpublished.GetProperty("publicado").GetBoolean());
        Assert.Equal(1, unpublished.GetProperty("sectorIds").GetArrayLength());
    }

    #endregion

    #region Validación, aislamiento y concurrencia

    [SqlServerFact]
    public async Task EdicionInvalidaYTokenAusenteConservanPerfilAnterior()
    {
        using var tech = await RegisteredAsync("tecnico", "validacion@example.test");
        var initial = await ReadAsync(tech, ProfileRoute);
        using var accepted = await SendAsync(tech, Data(Version(initial)));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var before = await ReadAsync(tech, ProfileRoute);
        foreach (var invalid in new object[]
        {
            new { descripcion = "Corta", especialidadIds = new[] { 1 }, sectorIds = new[] { 1 }, version = Version(before) },
            new { descripcion = Description, especialidadIds = new[] { 2 }, sectorIds = new[] { 1 }, version = Version(before) },
            new { descripcion = Description, especialidadIds = new[] { 999 }, sectorIds = new[] { 1 }, version = Version(before) },
            new { descripcion = Description, especialidadIds = new[] { 1, 1 }, sectorIds = new[] { 1 }, version = Version(before) },
            new { descripcion = Description, especialidadIds = new[] { 1 }, sectorIds = new[] { 2 }, version = Version(before) },
            Data("no-es-base64")
        })
        {
            using var rejected = await SendAsync(tech, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            var after = await ReadAsync(tech, ProfileRoute);
            Assert.Equal(before.GetRawText(), after.GetRawText());
        }
        using var noToken = await tech.PutAsJsonAsync(ProfileRoute, Data(Version(before)));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(before.GetRawText(), (await ReadAsync(tech, ProfileRoute)).GetRawText());
    }

    [SqlServerFact]
    public async Task CambiosConcurrentesSoloDeCoberturaProducenUnGanadorYSonPropios()
    {
        using var first = await RegisteredAsync("tecnico", "concurrencia@example.test");
        using var second = Client();
        using var login = await MutateAsync(second, "/api/v1/auth/login", HttpMethod.Post,
            new { correo = "concurrencia@example.test", contrasena = "ClaveDePruebas2026" });
        login.EnsureSuccessStatusCode();
        using var other = await RegisteredAsync("tecnico", "otro@example.test");
        var otherBefore = await ReadAsync(other, ProfileRoute);
        var initial = await ReadAsync(first, ProfileRoute);
        using var created = await SendAsync(first, Data(Version(initial)));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var version = Version(await ReadAsync(first, ProfileRoute));
        var responses = await Task.WhenAll(SendAsync(first, Data(version, 1)), SendAsync(second, Data(version, 3)));
        using var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        using var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var result = await ReadAsync(first, ProfileRoute);
        Assert.NotEqual(version, Version(result));
        Assert.Single(result.GetProperty("especialidadIds").EnumerateArray());
        Assert.Equal(otherBefore.GetRawText(), (await ReadAsync(other, ProfileRoute)).GetRawText());
    }

    #endregion

    #region Clientes y datos de prueba

    private HttpClient Client() => app!.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static string Version(JsonElement profile) => profile.GetProperty("version").GetString()!;
    private static object Data(string version, int specialty = 1) => new
    { descripcion = Description, especialidadIds = new[] { specialty }, sectorIds = new[] { 1 }, version };
    private static Task<HttpResponseMessage> SendAsync(HttpClient client, object data) => MutateAsync(client, ProfileRoute, HttpMethod.Put, data);

    private async Task<HttpClient> RegisteredAsync(string role, string email)
    {
        var client = Client();
        using var registration = await MutateAsync(client, "/api/v1/auth/register", HttpMethod.Post,
            new { nombre = "Persona Prueba", correo = email, telefono = "+59176543210", contrasena = "ClaveDePruebas2026", rol = role });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await MutateAsync(client, "/api/v1/auth/login", HttpMethod.Post,
            new { correo = email, contrasena = "ClaveDePruebas2026" });
        login.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<HttpResponseMessage> MutateAsync(HttpClient client, string route, HttpMethod method, object data)
    {
        var token = await ReadAsync(client, "/api/v1/auth/csrf");
        using var request = new HttpRequestMessage(method, route) { Content = JsonContent.Create(data) };
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var db = new GoFixDbContext(new DbContextOptionsBuilder<GoFixDbContext>().UseSqlServer(connectionString).Options);
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    #endregion
}
