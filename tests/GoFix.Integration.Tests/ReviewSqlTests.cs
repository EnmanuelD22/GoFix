using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GoFix.Integration.Tests;

public sealed partial class ServiceRequestSqlTests
{
    [SqlServerFact]
    public async Task ValoracionExigeTitularServicioCompletadoYDatosValidos()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var client = await RegisterAsync("cliente", "cliente");
        using var other = await RegisterAsync("cliente", "ajeno");
        using var visitor = Client();
        var created = await CreateAsync(client, CreateData(techId));
        var path = $"{Requests}/{Id(created)}";
        var reviewPath = path + "/valoracion";
        using var absent = await client.GetAsync(reviewPath);
        Assert.Equal(HttpStatusCode.NoContent, absent.StatusCode);
        using var anonymous = await visitor.PostAsJsonAsync(reviewPath, new { puntuacion = 5 });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var forbidden = await SendAsync(tech, HttpMethod.Post, reviewPath, new { puntuacion = 5 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var foreign = await SendAsync(other, HttpMethod.Post, reviewPath, new { puntuacion = 5 });
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var foreignRead = await other.GetAsync(reviewPath);
        Assert.Equal(HttpStatusCode.NotFound, foreignRead.StatusCode);
        using var noToken = await client.PostAsJsonAsync(reviewPath, new { puntuacion = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var pending = await SendAsync(client, HttpMethod.Post, reviewPath, new { puntuacion = 5 });
        await AssertProblemAsync(pending, "estado_invalido");

        var accepted = await TransitionAsync(tech, path, "aceptar", Version(created));
        var started = await TransitionAsync(tech, path, "iniciar", Version(accepted));
        await TransitionAsync(tech, path, "completar", Version(started));
        foreach (var invalid in new object[]
        {
            new { puntuacion = 0 }, new { puntuacion = 6 }, new { puntuacion = -1 },
            new { puntuacion = 1.5 }, new { puntuacion = "5" }, new { puntuacion = (int?)null },
            new { comentario = "Sin puntuación" }, new { puntuacion = 5, comentario = new string('a', 1001) }
        })
        {
            using var rejected = await SendAsync(client, HttpMethod.Post, reviewPath, invalid);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using var saved = await SendAsync(client, HttpMethod.Post, reviewPath,
            new { puntuacion = 1, comentario = "  Revisión puntual y atención cuidadosa.  " });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var body = await ReadBodyAsync(saved);
        Assert.Equal(1, body.GetProperty("puntuacion").GetInt32());
        Assert.Equal("Revisión puntual y atención cuidadosa.", body.GetProperty("comentario").GetString());
        var immutable = await GetAsync(client, reviewPath);
        Assert.Equal(body.GetRawText(), immutable.GetRawText());
        using var duplicate = await SendAsync(client, HttpMethod.Post, reviewPath, new { puntuacion = 5 });
        await AssertProblemAsync(duplicate, "valoracion_duplicada");
        using var edit = await SendAsync(client, HttpMethod.Put, reviewPath, new { puntuacion = 5 });
        Assert.Equal(HttpStatusCode.MethodNotAllowed, edit.StatusCode);
        using var delete = await SendAsync(client, HttpMethod.Delete, reviewPath, null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
        Assert.Equal(immutable.GetRawText(), (await GetAsync(client, reviewPath)).GetRawText());
    }

    [SqlServerFact]
    public async Task DosOpinionesConcurrentesProducenUnaUnicaValoracionPersistida()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var client = await RegisterAsync("cliente", "cliente");
        var path = await CompletedRequestAsync(client, tech, techId);
        var token = await CsrfAsync(client);
        var pair = await Task.WhenAll(
            SendAsync(client, HttpMethod.Post, path + "/valoracion", new { puntuacion = 5 }, token: token),
            SendAsync(client, HttpMethod.Post, path + "/valoracion", new { puntuacion = 2 }, token: token));
        using var first = pair[0];
        using var second = pair[1];
        Assert.Single(pair, response => response.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(pair, response => response.StatusCode == HttpStatusCode.Conflict);
        await AssertProblemAsync(conflict, "valoracion_duplicada");
        await using var db = OpenDatabase();
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS [Value] FROM reviews.Valoracion").SingleAsync());
        var persisted = await GetAsync(client, path + "/valoracion");
        var saved = await ReadBodyAsync(Assert.Single(pair, response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(Id(saved), Id(persisted));
        Assert.Equal(saved.GetProperty("puntuacion").GetInt32(), persisted.GetProperty("puntuacion").GetInt32());
    }

    [SqlServerFact]
    public async Task OpinionesPublicasPaginanSinContactosYCalculanReputacion()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var client = await RegisterAsync("cliente", "cliente");
        using var visitor = Client();
        var route = $"/api/v1/tecnicos/{techId}/valoraciones";
        var empty = await GetAsync(visitor, route);
        Assert.Equal(0, empty.GetProperty("cantidad").GetInt32());
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        var ids = new List<Guid>();
        foreach (var score in new[] { 1, 5, 5 })
        {
            var path = await CompletedRequestAsync(client, tech, techId);
            using var response = await SendAsync(client, HttpMethod.Post, path + "/valoracion",
                new { puntuacion = score, comentario = score == 1 ? null : "Servicio de prueba." });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            ids.Add(Id(await ReadBodyAsync(response)));
        }
        var pageOne = await GetAsync(visitor, route + "?page=1&pageSize=2");
        var pageTwo = await GetAsync(visitor, route + "?page=2&pageSize=2");
        Assert.Equal(3, pageOne.GetProperty("total").GetInt32());
        Assert.Equal(3, pageOne.GetProperty("cantidad").GetInt32());
        Assert.Equal(3.7m, pageOne.GetProperty("promedio").GetDecimal());
        var rows = pageOne.GetProperty("items").EnumerateArray().Concat(pageTwo.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(ids.Order(), rows.Select(Id).Order());
        Assert.All(rows, row =>
        {
            Assert.Equal("Persona", row.GetProperty("nombreCliente").GetString());
            Assert.DoesNotContain("cliente", row.GetRawText());
            Assert.DoesNotContain("@", row.GetRawText());
            Assert.DoesNotContain("+591", row.GetRawText());
            Assert.False(row.TryGetProperty("clienteId", out _));
            Assert.False(row.TryGetProperty("solicitudId", out _));
        });
        Assert.Equal(pageOne.GetRawText(), (await GetAsync(visitor, route + "?page=1&pageSize=2")).GetRawText());
        var profile = await GetAsync(visitor, $"/api/v1/tecnicos/{techId}");
        Assert.Contains("3.7", profile.GetRawText());
        foreach (var query in new[] { "?page=0", "?pageSize=0", "?pageSize=51", "?page=2147483647&pageSize=50" })
        {
            using var bad = await visitor.GetAsync(route + query);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
        using var missing = await visitor.GetAsync($"/api/v1/tecnicos/{Guid.NewGuid()}/valoraciones");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var historicalPath = await CompletedRequestAsync(client, tech, techId);
        await using var db = OpenDatabase();
        await db.Database.ExecuteSqlRawAsync("UPDATE professionals.Especialidad SET Activa = 0 WHERE Id = 1;");
        using var hidden = await visitor.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var historical = await SendAsync(client, HttpMethod.Post, historicalPath + "/valoracion",
            new { puntuacion = 5 });
        Assert.Equal(HttpStatusCode.Created, historical.StatusCode);
    }

    private static async Task<string> CompletedRequestAsync(HttpClient client, HttpClient technician, Guid technicianId)
    {
        var created = await CreateAsync(client, CreateData(technicianId));
        var path = $"{Requests}/{Id(created)}";
        var accepted = await TransitionAsync(technician, path, "aceptar", Version(created));
        var started = await TransitionAsync(technician, path, "iniciar", Version(accepted));
        await TransitionAsync(technician, path, "completar", Version(started));
        return path;
    }
}
