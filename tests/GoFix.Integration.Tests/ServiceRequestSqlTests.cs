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

public sealed partial class ServiceRequestSqlTests : IAsyncLifetime
{
    private const string Requests = "/api/v1/solicitudes";
    private const string Profile = "/api/v1/tecnicos/me/perfil";
    private const string Password = "ClaveDePruebas2026";
    private readonly string databaseName = $"GoFixRequestTests_{Guid.NewGuid():N}";
    private readonly string connectionString;
    private WebApplicationFactory<Program>? app;
    private bool ownsDatabase;

    public ServiceRequestSqlTests()
    {
        connectionString = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER"))
        {
            InitialCatalog = databaseName,
            AttachDBFilename = string.Empty
        }.ConnectionString;
    }

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
            INSERT INTO professionals.Especialidad (Codigo, Nombre, Activa)
            VALUES ('prueba', N'Electricidad', 1), ('otra', N'Plomería', 1);
            INSERT INTO professionals.Sector (Codigo, Nombre, Municipio, Activo)
            VALUES ('centro', N'Centro', N'Municipio de prueba', 1),
                   ('sur', N'Sur', N'Municipio de prueba', 1),
                   ('norte', N'Norte', N'Municipio de prueba', 1);
            """);
    }

    public async Task DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
        if (!ownsDatabase) return;
        Assert.Matches("\\AGoFixRequestTests_[a-f0-9]{32}\\z", databaseName);
        await using var db = OpenDatabase();
        await db.Database.EnsureDeletedAsync();
    }

    [SqlServerFact]
    public async Task AltaConcurrenteReutilizaLaSolicitudYLaClavePerteneceAlCliente()
    {
        using var technician = await RegisterAsync("tecnico", "tecnico");
        var technicianId = await PublishAsync(technician);
        using var client = await RegisterAsync("cliente", "cliente");
        using var other = await RegisterAsync("cliente", "otro");
        var key = Guid.NewGuid();
        var data = CreateData(technicianId);
        var token = await CsrfAsync(client);

        using var missingToken = await client.PostAsJsonAsync(Requests, data);
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
        using var missingKey = await SendAsync(client, HttpMethod.Post, Requests, data);
        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        using var invalidCoverage = await SendAsync(client, HttpMethod.Post, Requests,
            data with { SectorId = 3 }, "Idempotency-Key", key.ToString());
        await AssertProblemAsync(invalidCoverage, "cobertura_invalida", HttpStatusCode.BadRequest);

        var pair = await Task.WhenAll(
            SendAsync(client, HttpMethod.Post, Requests, data, "Idempotency-Key", key.ToString(), token),
            SendAsync(client, HttpMethod.Post, Requests, data, "Idempotency-Key", key.ToString(), token));
        using var first = pair[0];
        using var second = pair[1];
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Created },
            pair.Select(response => response.StatusCode).Order());
        var firstBody = await ReadBodyAsync(first);
        var id = Id(firstBody);
        Assert.Equal(id, Id(await ReadBodyAsync(second)));
        Assert.Equal("Pendiente", firstBody.GetProperty("estado").GetString());
        using var changed = await SendAsync(client, HttpMethod.Post, Requests,
            data with { Titulo = "Otro título válido" }, "Idempotency-Key", key.ToString());
        await AssertProblemAsync(changed, "idempotencia_conflicto");
        await using var db = OpenDatabase();
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM services.SolicitudServicio").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM services.IdempotenciaSolicitud").SingleAsync());
        Assert.Equal(new[] { 1 }, await EventTypesAsync(db, id));

        using var otherResult = await SendAsync(other, HttpMethod.Post, Requests, data, "Idempotency-Key", key.ToString());
        Assert.Equal(HttpStatusCode.Created, otherResult.StatusCode);
        Assert.NotEqual(id, Id(await ReadBodyAsync(otherResult)));
    }

    [SqlServerFact]
    public async Task ConsultasAislanParticipantesYAplicanEstadoYPaginacion()
    {
        using var technician = await RegisterAsync("tecnico", "tecnico");
        var technicianId = await PublishAsync(technician);
        using var otherTechnician = await RegisterAsync("tecnico", "otro-tecnico");
        using var client = await RegisterAsync("cliente", "cliente");
        using var other = await RegisterAsync("cliente", "otro");
        using var visitor = Client();
        var firstId = Id(await CreateAsync(client, CreateData(technicianId)));
        var secondId = Id(await CreateAsync(client, CreateData(technicianId)));
        var otherId = Id(await CreateAsync(other, CreateData(technicianId)));
        var detailPath = $"{Requests}/{firstId}";

        using var anonymous = await visitor.GetAsync(Requests);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var outsider in new[] { other, otherTechnician })
        {
            using var denied = await outsider.GetAsync(detailPath);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        using var forbiddenCreate = await SendAsync(technician, HttpMethod.Post, Requests,
            CreateData(technicianId), "Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);
        using var forbiddenEdit = await SendAsync(technician, HttpMethod.Put, detailPath,
            EditData("AAAAAAAAAAA=", 1, "Revisar instalación"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEdit.StatusCode);

        var assigned = await GetAsync(technician, detailPath);
        Assert.Equal(firstId, Id(assigned));
        Assert.Equal("Calle de prueba, número 14", assigned.GetProperty("direccion").GetString());
        Assert.DoesNotContain("@example.test", assigned.GetRawText());
        Assert.DoesNotContain("+59176543210", assigned.GetRawText());

        var first = await GetAsync(client, $"{Requests}?estado=Pendiente&page=1&pageSize=1");
        var second = await GetAsync(client, $"{Requests}?estado=Pendiente&page=2&pageSize=1");
        Assert.Equal(2, first.GetProperty("total").GetInt32());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        var firstItem = Id(Assert.Single(first.GetProperty("items").EnumerateArray()));
        var secondItem = Id(Assert.Single(second.GetProperty("items").EnumerateArray()));
        Assert.Equal(new[] { firstId, secondId }.Order(), new[] { firstItem, secondItem }.Order());
        Assert.Equal(3, (await GetAsync(technician, $"{Requests}?estado=Pendiente")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await GetAsync(technician, $"{Requests}?estado=Aceptada")).GetProperty("total").GetInt32());
        Assert.Equal(otherId, Id(Assert.Single((await GetAsync(other, Requests)).GetProperty("items").EnumerateArray())));
        Assert.Equal(0, (await GetAsync(otherTechnician, Requests)).GetProperty("total").GetInt32());
        using var invalidFilter = await client.GetAsync($"{Requests}?estado=desconocido");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
    }

    [SqlServerFact]
    public async Task EditarYEliminarSonAtomicosYExigenVersionVigente()
    {
        using var technician = await RegisterAsync("tecnico", "tecnico");
        var technicianId = await PublishAsync(technician);
        using var client = await RegisterAsync("cliente", "cliente");
        using var other = await RegisterAsync("cliente", "otro");
        var original = CreateData(technicianId);
        var key = Guid.NewGuid();
        var created = await CreateAsync(client, original, key);
        var id = Id(created);
        var path = $"{Requests}/{id}";
        var version = Version(created);
        using var badSector = await SendAsync(client, HttpMethod.Put, path, EditData(version, 3, "Revisar instalación"));
        await AssertProblemAsync(badSector, "cobertura_invalida", HttpStatusCode.BadRequest);
        using var unrelated = await SendAsync(other, HttpMethod.Put, path, EditData(version, 1, "Revisar instalación"));
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);

        var token = await CsrfAsync(client);
        var pair = await Task.WhenAll(
            SendAsync(client, HttpMethod.Put, path, EditData(version, 2, "Reparar instalación"), token: token),
            SendAsync(client, HttpMethod.Put, path, EditData(version, 2, "Revisar cableado"), token: token));
        using var first = pair[0];
        using var second = pair[1];
        using var winner = Assert.Single(pair, response => response.StatusCode == HttpStatusCode.OK);
        using var conflict = Assert.Single(pair, response => response.StatusCode == HttpStatusCode.Conflict);
        await AssertProblemAsync(conflict, "conflicto_version");
        var edited = await ReadBodyAsync(winner);
        Assert.Equal(2, edited.GetProperty("sectorId").GetInt32());
        Assert.Equal(technicianId, edited.GetProperty("tecnicoId").GetGuid());
        Assert.Equal(1, edited.GetProperty("especialidadId").GetInt32());
        var currentVersion = Version(edited);
        Assert.NotEqual(version, currentVersion);

        using var staleDelete = await SendAsync(client, HttpMethod.Delete, path, null, "X-Resource-Version", version);
        await AssertProblemAsync(staleDelete, "conflicto_version");
        using var deleted = await SendAsync(client, HttpMethod.Delete, path, null, "X-Resource-Version", currentVersion);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        foreach (var participant in new[] { client, technician })
        {
            using var gone = await participant.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
            Assert.Equal(0, (await GetAsync(participant, Requests)).GetProperty("total").GetInt32());
        }
        using var replay = await SendAsync(client, HttpMethod.Post, Requests,
            original, "Idempotency-Key", key.ToString());
        await AssertProblemAsync(replay, "recurso_eliminado");

        var rejectedId = Id(await CreateAsync(client, original));
        var rejectedPath = $"{Requests}/{rejectedId}";
        await using var db = OpenDatabase();
        Assert.Equal(1, await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM services.SolicitudServicio
            WHERE Id = {id} AND IsDeleted = 1 AND DeletedAtUtc IS NOT NULL
            """).SingleAsync());
        Assert.Equal(new[] { 1, 2, 4 }, await EventTypesAsync(db, id));
        var expiredDate = DateTimeOffset.UtcNow.AddDays(-1).ToOffset(TimeSpan.FromHours(-4));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE services.SolicitudServicio SET FechaPreferida = {expiredDate} WHERE Id = {rejectedId}
            """);
        var expiredVersion = Version(await GetAsync(client, rejectedPath));
        using var invalidDate = await SendAsync(client, HttpMethod.Put, rejectedPath,
            EditData(expiredVersion, 1, "Revisar instalación", expiredDate.AddDays(-1)));
        await AssertProblemAsync(invalidDate, "solicitud_invalida", HttpStatusCode.BadRequest);
        using var keptDate = await SendAsync(client, HttpMethod.Put, rejectedPath,
            EditData(expiredVersion, 1, "Reparar instalación", expiredDate));
        Assert.Equal(HttpStatusCode.OK, keptDate.StatusCode);
        var preferredDate = (await ReadBodyAsync(keptDate)).GetProperty("fechaPreferida").GetDateTimeOffset();
        Assert.True(expiredDate.EqualsExact(preferredDate));

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE services.SolicitudServicio SET Estado = 3 WHERE Id = {rejectedId}
            """);
        var rejectedVersion = Version(await GetAsync(client, rejectedPath));
        using var wrongStateEdit = await SendAsync(client, HttpMethod.Put, rejectedPath,
            EditData(rejectedVersion, 1, "Reparar instalación"));
        await AssertProblemAsync(wrongStateEdit, "estado_invalido");
        using var wrongStateDelete = await SendAsync(client, HttpMethod.Delete, rejectedPath,
            null, "X-Resource-Version", rejectedVersion);
        await AssertProblemAsync(wrongStateDelete, "estado_invalido");
        Assert.Equal(new[] { 1, 2 }, await EventTypesAsync(db, rejectedId));
    }

    private GoFixDbContext OpenDatabase() =>
        new(new DbContextOptionsBuilder<GoFixDbContext>().UseSqlServer(connectionString).Options);

    private static Task<List<int>> EventTypesAsync(GoFixDbContext db, Guid id) =>
        db.Database.SqlQuery<int>($"""
            SELECT CAST(TipoEvento AS int) AS [Value]
            FROM services.HistorialSolicitud WHERE SolicitudId = {id} ORDER BY Id
            """).ToListAsync();

    private HttpClient Client() => app!.CreateClient(new()
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<HttpClient> RegisterAsync(string role, string alias)
    {
        var client = Client();
        try
        {
            var email = $"{alias}@example.test";
            using var registration = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/register",
                new { nombre = $"Persona {alias}", correo = email, telefono = "+59176543210", contrasena = Password, rol = role });
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            using var login = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login",
                new { correo = email, contrasena = Password });
            login.EnsureSuccessStatusCode();
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static async Task<Guid> PublishAsync(HttpClient technician)
    {
        var profile = await GetAsync(technician, Profile);
        using var update = await SendAsync(technician, HttpMethod.Put, Profile, new
        {
            descripcion = "Mantenimiento de instalaciones eléctricas para viviendas y comercios.",
            especialidadIds = new[] { 1 },
            sectorIds = new[] { 1, 2 },
            version = Version(profile)
        });
        update.EnsureSuccessStatusCode();
        Assert.True((await GetAsync(technician, Profile)).GetProperty("publicado").GetBoolean());
        return Id(profile);
    }

    private static async Task<JsonElement> CreateAsync(HttpClient client, Creation data, Guid? key = null)
    {
        using var response = await SendAsync(client, HttpMethod.Post, Requests, data,
            "Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadBodyAsync(response);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, object? data,
        string? header = null, string? value = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (data is not null) request.Content = JsonContent.Create(data);
        request.Headers.Add("X-CSRF-TOKEN", token ?? await CsrfAsync(client));
        if (header is not null) request.Headers.Add(header, value);
        return await client.SendAsync(request);
    }

    private static async Task<string> CsrfAsync(HttpClient client) =>
        (await GetAsync(client, "/api/v1/auth/csrf")).GetProperty("token").GetString()!;

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await ReadBodyAsync(response);
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string code, HttpStatusCode status = HttpStatusCode.Conflict)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ReadBodyAsync(response)).GetProperty("code").GetString());
    }

    private static Guid Id(JsonElement body) => body.GetProperty("id").GetGuid();
    private static string Version(JsonElement body) => body.GetProperty("version").GetString()!;

    private static Creation CreateData(Guid technicianId) =>
        new(technicianId, 1, 1, "Revisar instalación",
            "El interruptor de la sala necesita una revisión y mantenimiento.",
            "Calle de prueba, número 14", DateTimeOffset.UtcNow.AddDays(5));

    private static object EditData(string version, int sectorId, string title, DateTimeOffset? preferredDate = null) => new
    {
        version,
        sectorId,
        titulo = title,
        descripcion = "El interruptor de la sala necesita una revisión de mantenimiento.",
        direccion = "Calle de prueba, número 20",
        fechaPreferida = preferredDate ?? DateTimeOffset.UtcNow.AddDays(7)
    };

    private sealed record Creation(Guid TecnicoId, int EspecialidadId, int SectorId,
        string Titulo, string Descripcion, string Direccion, DateTimeOffset FechaPreferida);
}
