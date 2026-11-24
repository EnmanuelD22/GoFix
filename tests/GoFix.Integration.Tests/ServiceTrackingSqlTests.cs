using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GoFix.Integration.Tests;

public sealed partial class ServiceRequestSqlTests
{
    [SqlServerFact]
    public async Task SeguimientoCompletoConservaHistorialContactoYResumen()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var client = await RegisterAsync("cliente", "cliente");
        using var outsider = await RegisterAsync("cliente", "ajeno");
        var request = await CreateAsync(client, CreateData(techId));
        var path = $"{Requests}/{Id(request)}";
        Assert.Equal(JsonValueKind.Null, request.GetProperty("contacto").ValueKind);
        using var forbidden = await outsider.GetAsync(path + "/historial");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var wrongRole = await SendAsync(client, HttpMethod.Post, path + "/aceptar",
            new { version = Version(request), fechaConfirmada = DateTimeOffset.UtcNow.AddDays(2) });
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
        using var noToken = await tech.PostAsJsonAsync(path + "/aceptar",
            new { version = Version(request), fechaConfirmada = DateTimeOffset.UtcNow.AddDays(2) });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var pastDate = await SendAsync(tech, HttpMethod.Post, path + "/aceptar",
            new { version = Version(request), fechaConfirmada = DateTimeOffset.UtcNow.AddDays(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, pastDate.StatusCode);
        using var noVersion = await SendAsync(tech, HttpMethod.Post, path + "/iniciar", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        Assert.Single((await GetAsync(client, path + "/historial")).EnumerateArray());
        Assert.Equal(1, (await GetAsync(tech, "/api/v1/tecnicos/me/resumen")).GetProperty("pendientes").GetInt32());

        var accepted = await TransitionAsync(tech, path, "aceptar", Version(request));
        Assert.Equal("Aceptada", accepted.GetProperty("estado").GetString());
        Assert.NotEqual(Version(request), Version(accepted));
        Assert.NotEqual(JsonValueKind.Null, accepted.GetProperty("aceptadaAtUtc").ValueKind);
        Assert.Equal("+59176543210", accepted.GetProperty("contacto").GetProperty("telefono").GetString());
        Assert.Equal("Persona cliente", accepted.GetProperty("contacto").GetProperty("nombre").GetString());
        var clientView = await GetAsync(client, path);
        Assert.Equal("Persona tecnico", clientView.GetProperty("contacto").GetProperty("nombre").GetString());
        var summary = await GetAsync(tech, "/api/v1/tecnicos/me/resumen");
        Assert.Equal(0, summary.GetProperty("pendientes").GetInt32());
        Assert.Equal(1, summary.GetProperty("aceptadas").GetInt32());
        Assert.Equal(Id(request), Id(Assert.Single(summary.GetProperty("proximasVisitas").EnumerateArray())));
        using var stale = await SendAsync(tech, HttpMethod.Post, path + "/iniciar", new { version = Version(request) });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var started = await TransitionAsync(tech, path, "iniciar", Version(accepted));
        Assert.Equal("EnProceso", started.GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKind.Null, started.GetProperty("inicioAtUtc").ValueKind);
        Assert.Equal(1, (await GetAsync(tech, "/api/v1/tecnicos/me/resumen")).GetProperty("enProceso").GetInt32());
        using var cancel = await SendAsync(client, HttpMethod.Post, path + "/cancelar",
            new { version = Version(started), motivo = "Se necesita cancelar la visita." });
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        var completed = await TransitionAsync(tech, path, "completar", Version(started));
        Assert.Equal("Completada", completed.GetProperty("estado").GetString());
        Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("finAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, completed.GetProperty("contacto").ValueKind);
        using var reopen = await SendAsync(tech, HttpMethod.Post, path + "/iniciar", new { version = Version(completed) });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);

        var history = (await GetAsync(client, path + "/historial")).EnumerateArray().ToArray();
        Assert.Equal(new[] { "Creacion", "Transicion", "Transicion", "Transicion" },
            history.Select(row => row.GetProperty("tipoEvento").GetString()));
        Assert.Equal(history.OrderBy(row => row.GetProperty("createdAtUtc").GetDateTime())
            .ThenBy(row => row.GetProperty("id").GetInt64()).Select(row => row.GetProperty("id").GetInt64()),
            history.Select(row => row.GetProperty("id").GetInt64()));
        Assert.All(history, row =>
        {
            Assert.NotEqual(Guid.Empty, row.GetProperty("actorId").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("actorNombre").GetString()));
        });
        Assert.Equal("Trabajo finalizado con revisión.", history[^1].GetProperty("observacion").GetString());
    }

    [SqlServerFact]
    public async Task RechazoYCancelacionesAplicanMatrizYPrivacidad()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var otherTech = await RegisterAsync("tecnico", "ajeno");
        using var client = await RegisterAsync("cliente", "cliente");
        foreach (var action in new[] { "rechazar", "cancelar" })
        {
            var created = await CreateAsync(client, CreateData(techId));
            var path = $"{Requests}/{Id(created)}";
            using var outsider = await SendAsync(otherTech, HttpMethod.Post, path + "/rechazar",
                new { version = Version(created), motivo = "No puedo atender este servicio." });
            Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);
            using var shortReason = await SendAsync(action == "rechazar" ? tech : client, HttpMethod.Post, path + "/" + action,
                new { version = Version(created), motivo = "No" });
            Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);
            var final = await TransitionAsync(action == "rechazar" ? tech : client, path, action, Version(created));
            Assert.Equal(action == "rechazar" ? "Rechazada" : "Cancelada", final.GetProperty("estado").GetString());
            Assert.Equal(JsonValueKind.Null, final.GetProperty("contacto").ValueKind);
            using var terminal = await SendAsync(tech, HttpMethod.Post, path + "/aceptar",
                new { version = Version(final), fechaConfirmada = DateTimeOffset.UtcNow.AddDays(3) });
            Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode);
            Assert.Equal(2, (await GetAsync(client, path + "/historial")).GetArrayLength());
        }
        foreach (var actor in new[] { client, tech })
        {
            var created = await CreateAsync(client, CreateData(techId));
            var path = $"{Requests}/{Id(created)}";
            var accepted = await TransitionAsync(tech, path, "aceptar", Version(created));
            var cancelled = await TransitionAsync(actor, path, "cancelar", Version(accepted));
            Assert.NotEqual(JsonValueKind.Null, cancelled.GetProperty("contacto").ValueKind);
        }
        var pending = await CreateAsync(client, CreateData(techId));
        using var deletion = await SendAsync(client, HttpMethod.Delete, $"{Requests}/{Id(pending)}", null,
            "X-Resource-Version", Version(pending));
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        using var goneHistory = await tech.GetAsync($"{Requests}/{Id(pending)}/historial");
        Assert.Equal(HttpStatusCode.NotFound, goneHistory.StatusCode);
        var summary = await GetAsync(tech, "/api/v1/tecnicos/me/resumen");
        Assert.Equal(0, summary.GetProperty("pendientes").GetInt32());
        Assert.Equal(0, summary.GetProperty("aceptadas").GetInt32());
        using var deniedSummary = await client.GetAsync("/api/v1/tecnicos/me/resumen");
        Assert.Equal(HttpStatusCode.Forbidden, deniedSummary.StatusCode);
    }

    [SqlServerFact]
    public async Task AceptacionCompiteConCancelacionYEdicionSinHistorialDeExitoDuplicado()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var client = await RegisterAsync("cliente", "cliente");
        foreach (var action in new[] { "cancelar", "editar" })
        {
            var created = await CreateAsync(client, CreateData(techId));
            var id = Id(created);
            var path = $"{Requests}/{id}";
            var techToken = await CsrfAsync(tech);
            var clientToken = await CsrfAsync(client);
            var pair = await Task.WhenAll(
                SendAsync(tech, HttpMethod.Post, path + "/aceptar",
                    new { version = Version(created), fechaConfirmada = DateTimeOffset.UtcNow.AddDays(3) }, token: techToken),
                action == "cancelar"
                    ? SendAsync(client, HttpMethod.Post, path + "/cancelar",
                        new { version = Version(created), motivo = "Cambio de planes para esta visita." }, token: clientToken)
                    : SendAsync(client, HttpMethod.Put, path, EditData(Version(created), 1, "Revisar cableado principal"), token: clientToken));
            using var first = pair[0];
            using var second = pair[1];
            Assert.Single(pair, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(pair, response => response.StatusCode == HttpStatusCode.Conflict);
            var history = await GetAsync(client, path + "/historial");
            Assert.Equal(2, history.GetArrayLength());
            var current = await GetAsync(client, path);
            Assert.NotEqual(Version(created), Version(current));
            await using var db = OpenDatabase();
            Assert.Equal(2, (await EventTypesAsync(db, id)).Count);
        }
    }

    [SqlServerFact]
    public async Task MatrizCompletaDeTransicionesRespetaRolEstadoYVersion()
    {
        using var tech = await RegisterAsync("tecnico", "tecnico");
        var techId = await PublishAsync(tech);
        using var client = await RegisterAsync("cliente", "cliente");
        await using var db = OpenDatabase();
        foreach (var role in new[] { "cliente", "tecnico" })
        foreach (var state in Enumerable.Range(1, 6))
        foreach (var action in new[] { "aceptar", "rechazar", "cancelar", "iniciar", "completar" })
        {
            var created = await CreateAsync(client, CreateData(techId));
            var id = Id(created);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE services.SolicitudServicio SET Estado = {state} WHERE Id = {id}
                """);
            var path = $"{Requests}/{id}";
            var version = Version(await GetAsync(client, path));
            using var response = await SendAsync(role == "cliente" ? client : tech, HttpMethod.Post, path + "/" + action,
                new { version, fechaConfirmada = DateTimeOffset.UtcNow.AddDays(2),
                    motivo = "Motivo válido de cambio de la visita.", observacion = "Revisión completa." });
            var allowed = role == "tecnico"
                ? (state == 1 && (action == "aceptar" || action == "rechazar"))
                    || (state == 2 && (action == "cancelar" || action == "iniciar"))
                    || (state == 5 && action == "completar")
                : action == "cancelar" && (state == 1 || state == 2);
            var expected = allowed ? HttpStatusCode.OK
                : role == "cliente" && action != "cancelar" ? HttpStatusCode.Forbidden
                : HttpStatusCode.Conflict;
            Assert.True(response.StatusCode == expected,
                $"{role}, estado {state}, acción {action}: esperaba {expected}, obtuvo {response.StatusCode}.");
            Assert.Equal(allowed ? 2 : 1, (await EventTypesAsync(db, id)).Count);
        }
    }

    private static async Task<JsonElement> TransitionAsync(HttpClient actor, string path, string action, string version)
    {
        using var response = await SendAsync(actor, HttpMethod.Post, path + "/" + action,
            new { version, fechaConfirmada = DateTimeOffset.UtcNow.AddDays(3),
                motivo = "Se necesita cambiar la planificación.", observacion = "Trabajo finalizado con revisión." });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadBodyAsync(response);
    }
}
