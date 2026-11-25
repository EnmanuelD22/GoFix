using System.Net;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GoFix.Integration.Tests;

public sealed partial class ServiceRequestSqlTests
{
    [SqlServerFact]
    public async Task MatrizDeRecursosAislaDosClientesDosTecnicosYVisitante()
    {
        using var owner = await RegisterAsync("cliente", "titular");
        using var other = await RegisterAsync("cliente", "otro");
        using var assigned = await RegisterAsync("tecnico", "asignado");
        using var stranger = await RegisterAsync("tecnico", "ajeno");
        using var visitor = Client();
        var profileId = await PublishAsync(assigned);
        await PublishAsync(stranger);
        var created = await CreateAsync(owner, CreateData(profileId));
        var id = Id(created);
        var path = $"{Requests}/{id}";
        var version = Version(created);

        foreach (var actor in new[] { other, stranger, visitor })
        {
            var expected = actor == visitor ? HttpStatusCode.Unauthorized : HttpStatusCode.NotFound;
            foreach (var route in new[] { path, path + "/historial" })
            {
                using var response = await actor.GetAsync(route);
                Assert.Equal(expected, response.StatusCode);
            }
        }
        foreach (var actor in new[] { other, stranger })
            Assert.Empty((await GetAsync(actor, Requests)).GetProperty("items").EnumerateArray());

        using (var clientSummary = await owner.GetAsync("/api/v1/tecnicos/me/resumen"))
            Assert.Equal(HttpStatusCode.Forbidden, clientSummary.StatusCode);
        Assert.Equal(1, (await GetAsync(assigned, "/api/v1/tecnicos/me/resumen")).GetProperty("pendientes").GetInt32());
        Assert.Equal(0, (await GetAsync(stranger, "/api/v1/tecnicos/me/resumen")).GetProperty("pendientes").GetInt32());

        foreach (var actor in new[] { other, assigned, stranger, visitor })
        {
            var expected = actor == visitor ? HttpStatusCode.Unauthorized
                : actor == other ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;
            using var edit = await SendAsync(actor, HttpMethod.Put, path, EditData(version, 1, "Edición ajena inválida"));
            Assert.Equal(expected, edit.StatusCode);
            using var delete = await SendAsync(actor, HttpMethod.Delete, path, null, "X-Resource-Version", version);
            Assert.Equal(expected, delete.StatusCode);
        }

        foreach (var action in new[] { "aceptar", "rechazar", "cancelar", "iniciar", "completar" })
        foreach (var actor in new[] { other, stranger, visitor })
        {
            var expected = actor == visitor ? HttpStatusCode.Unauthorized
                : actor == other && action != "cancelar" ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound;
            using var mutation = await SendAsync(actor, HttpMethod.Post, path + "/" + action,
                new { version, fechaConfirmada = DateTimeOffset.UtcNow.AddDays(2), motivo = "Motivo válido para prueba." });
            Assert.Equal(expected, mutation.StatusCode);
        }

        using (var ownReview = await other.GetAsync(path + "/valoracion"))
            Assert.Equal(HttpStatusCode.NotFound, ownReview.StatusCode);
        foreach (var actor in new[] { assigned, stranger, visitor })
        {
            using var ownReview = await actor.GetAsync(path + "/valoracion");
            Assert.Equal(actor == visitor ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, ownReview.StatusCode);
        }

        var profile = await GetAsync(assigned, Profile);
        using (var forbiddenProfile = await SendAsync(owner, HttpMethod.Put, Profile, new
        {
            descripcion = "Descripción de un perfil que el cliente intenta cambiar indebidamente.",
            especialidadIds = new[] { 1 }, sectorIds = new[] { 1 }, version = Version(profile)
        }))
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenProfile.StatusCode);
        Assert.Equal(profile.GetRawText(), (await GetAsync(assigned, Profile)).GetRawText());
        Assert.Equal(created.GetRawText(), (await GetAsync(owner, path)).GetRawText());
        await using var db = OpenDatabase();
        Assert.Equal(new[] { 1 }, await EventTypesAsync(db, id));
    }
}
