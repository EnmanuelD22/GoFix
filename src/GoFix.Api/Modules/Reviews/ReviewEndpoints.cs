using GoFix.Api.Modules.Identity;
using GoFix.Application.Modules.Identity;
using GoFix.Application.Modules.Reviews;

namespace GoFix.Api.Modules.Reviews;

internal static class ReviewEndpoints
{
    internal static void MapReviewEndpoints(this RouteGroupBuilder api)
    {
        var own = api.MapGroup("/solicitudes/{id:guid}/valoracion").WithTags("Valoraciones")
            .RequireAuthorization(policy => policy.RequireRole(AccountRole.Customer))
            .WithDescription("Una opinión inmutable por servicio completado propio. Errores Problem Details con code y traceId: valoracion_invalida (400), solicitud_no_encontrada (404), estado_invalido y valoracion_duplicada (409).");
        own.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        own.MapPost("", CreateAsync).Produces<ReviewDetail>(201).ProducesProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        own.MapGet("", GetAsync).Produces<ReviewDetail>().Produces(204)
            .WithDescription("Consulta de la valoración propia para confirmar un envío. Devuelve 204 cuando aún no existe.")
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        api.MapGet("/tecnicos/{id:guid}/valoraciones", ListAsync).WithTags("Valoraciones")
            .WithDescription("Opiniones de un perfil publicado, ordenadas por fecha e identificador descendentes. Publica solo nombre de pila, puntuación, comentario y fecha. Promedio redondeado a una cifra decimal.")
            .Produces<ReviewPage>().ProducesValidationProblem().ProducesProblem(404);
    }

    private static async Task<IResult> CreateAsync(Guid id, CreateReview input, HttpContext context,
        ReviewService service, CancellationToken ct)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.CreateAsync(userId, id, input, ct);
        return result.Code is null ? Results.Created($"/api/v1/solicitudes/{id}/valoracion", result.Item)
            : Problem(result.Code);
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, ReviewService service, CancellationToken ct)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.GetAsync(userId, id, ct);
        if (result.Code is { } code)
            return Problem(code);
        return result.Item is null ? Results.NoContent() : Results.Ok(result.Item);
    }

    private static async Task<IResult> ListAsync(Guid id, ReviewService service, CancellationToken ct,
        int page = 1, int pageSize = 20)
    {
        var errors = new Dictionary<string, string[]>();
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue)
            errors["page"] = ["Selecciona una página válida."];
        if (pageSize is < 1 or > 50)
            errors["pageSize"] = ["El tamaño de página debe estar entre 1 y 50."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);
        var result = await service.ListAsync(id, page, pageSize, ct);
        return result is null ? Problem("tecnico_no_encontrado") : Results.Ok(result);
    }

    private static IResult Problem(string code)
    {
        var (status, title) = code switch
        {
            "valoracion_invalida" => (400, "Elige una puntuación de 1 a 5 y un comentario de hasta 1.000 caracteres."),
            "solicitud_no_encontrada" => (404, "No se encontró la solicitud."),
            "tecnico_no_encontrado" => (404, "No se encontró el perfil público."),
            "estado_invalido" => (409, "Solo puedes valorar servicios completados."),
            "valoracion_duplicada" => (409, "Este servicio ya tiene una valoración."),
            _ => throw new InvalidOperationException($"Resultado de valoración desconocido: {code}.")
        };
        return Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
