using System.Net;
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

public sealed class SearchDirectorySqlTests : IAsyncLifetime
{
    private const string Description = "Servicios de mantenimiento y reparaciones profesionales para viviendas del municipio.";
    private readonly string databaseName = $"GoFixSearchTests_{Guid.NewGuid():N}";
    private readonly string connectionString;
    private WebApplicationFactory<Program>? app;
    private bool ownsDatabase;

    public SearchDirectorySqlTests()
    {
        connectionString = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER"))
        { InitialCatalog = databaseName, AttachDBFilename = string.Empty }.ConnectionString;
    }

    #region Base de datos y datos exclusivos de cada prueba

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
            INSERT INTO professionals.Especialidad (Codigo, Nombre, Activa) VALUES
            ('electricidad-test', N'Electricidad', 1), ('plomeria-test', N'Plomería', 1), ('inactiva-test', N'Retirada', 0);
            INSERT INTO professionals.Sector (Codigo, Nombre, Municipio, Activo) VALUES
            ('norte-test', N'Norte', N'Municipio de prueba', 1), ('sur-test', N'Sur', N'Municipio de prueba', 1),
            ('retirado-test', N'Retirado', N'Municipio de prueba', 0);
            """);

        for (var i = 1; i <= 7; i++)
        {
            var userId = Id(100 + i);
            var name = i switch { 1 or 2 => "Ana Técnica", 3 => "Bea Técnica", _ => "Cuenta privada" };
            var email = $"privado{i}@example.test";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [identity].AspNetUsers
                (Id, Nombre, Email, NormalizedEmail, PhoneNumber, EmailConfirmed, PhoneNumberConfirmed,
                 TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
                VALUES ({userId}, {name}, {email}, {email.ToUpperInvariant()}, '8095550199', 0, 0, 0, 0, 0);
                INSERT INTO [identity].AspNetUserRoles (UserId, RoleId)
                SELECT {userId}, Id FROM [identity].AspNetRoles WHERE Name = {(i == 7 ? "cliente" : "tecnico")};
                """);
            if (i == 7) continue;
            var profileId = Id(i);
            var description = i == 4 ? "Incompleto" : Description;
            var specialty = i switch { 3 => 2, 5 => 3, _ => 1 };
            var sector = i switch { 2 => 2, 6 => 3, _ => 1 };
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO professionals.PerfilTecnico (Id, UsuarioId, Descripcion) VALUES ({profileId}, {userId}, {description});
                INSERT INTO professionals.TecnicoEspecialidad (PerfilTecnicoId, EspecialidadId) VALUES ({profileId}, {specialty});
                INSERT INTO professionals.TecnicoSector (PerfilTecnicoId, SectorId) VALUES ({profileId}, {sector});
                """);
        }

        // Las valoraciones se insertan solo para comprobar la consulta. No se crean mediante un endpoint futuro.
        for (var i = 1; i <= 3; i++)
        {
            var requestId = Id(200 + i);
            var reviewId = Id(300 + i);
            var customerId = Id(107);
            var technicianId = Id(1);
            var score = i == 2 ? 5 : 4;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO services.SolicitudServicio
                (Id, ClienteId, TecnicoId, EspecialidadId, SectorId, Titulo, Descripcion, Direccion,
                 FechaPreferida, Estado, CreatedAtUtc, IsDeleted)
                VALUES ({requestId}, {customerId}, {technicianId}, 1, 1, N'Servicio de prueba', N'Descripción del servicio',
                 N'Dirección privada 123', SYSDATETIMEOFFSET(), 6, SYSUTCDATETIME(), 0);
                INSERT INTO reviews.Valoracion (Id, SolicitudId, ClienteId, TecnicoId, Puntuacion, Comentario, CreatedAtUtc)
                VALUES ({reviewId}, {requestId}, {customerId}, {technicianId}, {score}, N'Opinión de prueba', SYSUTCDATETIME());
                """);
        }

        // Una cobertura retirada no debe reaparecer en la ficha aunque quede alguna cobertura activa.
        var publishedId = Id(1);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO professionals.TecnicoEspecialidad (PerfilTecnicoId, EspecialidadId) VALUES ({publishedId}, 3);
            INSERT INTO professionals.TecnicoSector (PerfilTecnicoId, SectorId) VALUES ({publishedId}, 3);
            """);
    }

    public async Task DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
        if (!ownsDatabase) return;
        Assert.Matches("\\AGoFixSearchTests_[a-f0-9]{32}\\z", databaseName);
        await using var db = new GoFixDbContext(new DbContextOptionsBuilder<GoFixDbContext>().UseSqlServer(connectionString).Options);
        await db.Database.EnsureDeletedAsync();
    }

    #endregion

    #region Directorio y ficha pública

    [SqlServerFact]
    public async Task VisitanteVeSoloPublicadosConPaginacionEstableYFiltrosCombinados()
    {
        using var visitor = Client();
        var first = await ReadAsync(visitor, "/api/v1/tecnicos?page=1&pageSize=1");
        var second = await ReadAsync(visitor, "/api/v1/tecnicos?page=2&pageSize=1");
        var third = await ReadAsync(visitor, "/api/v1/tecnicos?page=3&pageSize=1");
        Assert.Equal(3, first.GetProperty("total").GetInt32());
        Assert.Equal(1, first.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(Id(1), first.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(Id(2), second.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(Id(3), third.GetProperty("items")[0].GetProperty("id").GetGuid());

        var combined = await ReadAsync(visitor, "/api/v1/tecnicos?especialidadId=1&sectorId=1");
        Assert.Equal(1, combined.GetProperty("total").GetInt32());
        Assert.Equal(Id(1), Assert.Single(combined.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var noMatch = await ReadAsync(visitor, "/api/v1/tecnicos?especialidadId=2&sectorId=2");
        Assert.Equal(0, noMatch.GetProperty("total").GetInt32());
        Assert.Empty(noMatch.GetProperty("items").EnumerateArray());
        var inactiveFilter = await ReadAsync(visitor, "/api/v1/tecnicos?especialidadId=3");
        Assert.Equal(0, inactiveFilter.GetProperty("total").GetInt32());
        var beyond = await ReadAsync(visitor, "/api/v1/tecnicos?page=5&pageSize=1");
        Assert.Equal(3, beyond.GetProperty("total").GetInt32());
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());
    }

    [SqlServerFact]
    public async Task DetalleYListadoExcluyenContactosYCalculanReputacionPersistida()
    {
        using var visitor = Client();
        var detail = await ReadAsync(visitor, $"/api/v1/tecnicos/{Id(1)}");
        Assert.Equal("Ana Técnica", detail.GetProperty("nombre").GetString());
        Assert.Equal(Description, detail.GetProperty("descripcion").GetString());
        Assert.Equal(3, detail.GetProperty("cantidadValoraciones").GetInt32());
        Assert.Equal(4.3m, detail.GetProperty("promedio").GetDecimal());
        Assert.Single(detail.GetProperty("especialidades").EnumerateArray());
        Assert.Single(detail.GetProperty("sectores").EnumerateArray());
        AssertPublicFields(detail);

        var noReviews = await ReadAsync(visitor, $"/api/v1/tecnicos/{Id(2)}");
        Assert.Equal(0, noReviews.GetProperty("cantidadValoraciones").GetInt32());
        Assert.Equal(JsonValueKind.Null, noReviews.GetProperty("promedio").ValueKind);
        var directory = await ReadAsync(visitor, "/api/v1/tecnicos");
        foreach (var profile in directory.GetProperty("items").EnumerateArray()) AssertPublicFields(profile);
    }

    [SqlServerFact]
    public async Task PerfilIncompletoRetiradoOInexistenteDevuelve404()
    {
        using var visitor = Client();
        foreach (var id in new[] { Id(4), Id(5), Id(6), Id(999) })
        {
            using var response = await visitor.GetAsync($"/api/v1/tecnicos/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        await ExecuteAsync("UPDATE professionals.Sector SET Activo = 0 WHERE Id = 1;");
        using var withdrawn = await visitor.GetAsync($"/api/v1/tecnicos/{Id(1)}");
        Assert.Equal(HttpStatusCode.NotFound, withdrawn.StatusCode);
        var after = await ReadAsync(visitor, "/api/v1/tecnicos?sectorId=1");
        Assert.Equal(0, after.GetProperty("total").GetInt32());
    }

    [SqlServerFact]
    public async Task FiltrosYPaginacionInvalidosDevuelvenValidacionSinErroresInternos()
    {
        using var visitor = Client();
        foreach (var query in new[] { "page=0", "page=-1", "page=abc", "page=2147483648", "pageSize=0", "pageSize=51", "especialidadId=0", "sectorId=-1", "page=2147483647&pageSize=50" })
        {
            using var response = await visitor.GetAsync($"/api/v1/tecnicos?{query}");
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"La consulta '{query}' debe producir 400 y produjo {(int)response.StatusCode}.");
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("traceId").GetString()));
        }
    }

    #endregion

    #region Ayudas de pruebas

    private HttpClient Client() => app!.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

    private static void AssertPublicFields(JsonElement profile)
    {
        var allowed = new[] { "id", "nombre", "descripcion", "especialidades", "sectores", "promedio", "cantidadValoraciones" };
        Assert.Equal(allowed.OrderBy(name => name), profile.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        var json = profile.GetRawText();
        Assert.DoesNotContain("example.test", json);
        Assert.DoesNotContain("8095550199", json);
        Assert.DoesNotContain("Dirección privada", json);
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
