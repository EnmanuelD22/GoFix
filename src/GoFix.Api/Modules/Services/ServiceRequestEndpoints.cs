using GoFix.Api.Modules.Identity;
using GoFix.Application.Modules.Identity;
using GoFix.Application.Modules.Services;
using GoFix.Domain.Modules.Services;

namespace GoFix.Api.Modules.Services;

internal static class ServiceRequestEndpoints
{
    internal static void MapServiceRequestEndpoints(this RouteGroupBuilder api)
    {
        var requests = api.MapGroup("/solicitudes").WithTags("Solicitudes")
            .RequireAuthorization(policy => policy.RequireRole(AccountRole.Customer, AccountRole.Technician))
            .WithDescription("Errores Problem Details con code y traceId. 400: solicitud_invalida, cobertura_invalida, version_invalida, clave_idempotencia_invalida, transicion_invalida. 404: solicitud_no_encontrada. 409: estado_invalido, conflicto_version, idempotencia_conflicto, recurso_eliminado. 401/403: sesion o rol incompatible.");
        requests.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        requests.MapGet("", ListAsync).Produces<ServiceRequestPage>()
            .ProducesValidationProblem().ProducesProblem(401).ProducesProblem(403);
        requests.MapGet("/{id:guid}", GetAsync)
            .WithDescription("Detalle propio o asignado. Contacto contiene nombre y telefono de la contraparte cuando existe aceptadaAtUtc y la solicitud sigue visible. Fechas de visita conservan su desplazamiento horario.")
            .Produces<ServiceRequestDetail>()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);


        requests.MapGet("/{id:guid}/historial", HistoryAsync).Produces<RequestHistoryItem[]>()
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        foreach (var action in new[] { "aceptar", "rechazar", "cancelar", "iniciar", "completar" })
        {
            var route = requests.MapPost("/{id:guid}/" + action,
                (Guid id, TransitionServiceRequest input, HttpContext context, ServiceRequestService service,
                    CancellationToken ct) => TransitionAsync(id, action, input, context, service, ct))
                .Produces<ServiceRequestDetail>().ProducesProblem(400).ProducesProblem(401)
                .ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
            if (action != "cancelar")
                route.RequireAuthorization(policy => policy.RequireRole(AccountRole.Technician));
        }
        api.MapGet("/tecnicos/me/resumen", SummaryAsync).WithTags("Seguimiento")
            .RequireAuthorization(policy => policy.RequireRole(AccountRole.Technician))
            .WithDescription("Conteos operativos propios por estado y hasta diez visitas aceptadas futuras, ordenadas por fecha confirmada e identificador. Excluye solicitudes eliminadas.")
            .Produces<TechnicianSummary>().ProducesProblem(401).ProducesProblem(403);

        var client = requests.MapGroup("").RequireAuthorization(policy => policy.RequireRole(AccountRole.Customer));
        client.MapPost("", CreateAsync).Produces<ServiceRequestDetail>(201).Produces<ServiceRequestDetail>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(409);
        client.MapPut("/{id:guid}", UpdateAsync).Produces<ServiceRequestDetail>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
            .ProducesProblem(409);
        client.MapDelete("/{id:guid}", DeleteAsync).Produces(204)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> ListAsync(HttpContext context, ServiceRequestService service,
        CancellationToken cancellationToken, string? estado = null, int page = 1, int pageSize = 20)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();

        EstadoSolicitud? status = null;
        var errors = new Dictionary<string, string[]>();
        if (estado is not null)
        {
            if (Enum.TryParse<EstadoSolicitud>(estado, true, out var parsed)
                && string.Equals(estado, Enum.GetName(parsed), StringComparison.OrdinalIgnoreCase))
                status = parsed;
            else
                errors["estado"] = ["Selecciona un estado válido."];
        }
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue)
            errors["page"] = ["Selecciona una página válida."];
        if (pageSize is < 1 or > 50)
            errors["pageSize"] = ["El tamaño de página debe estar entre 1 y 50."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        return Results.Ok(await service.ListAsync(userId, context.User.IsInRole(AccountRole.Technician),
            status, page, pageSize, cancellationToken));
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context,
        ServiceRequestService service, CancellationToken cancellationToken)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.GetAsync(userId, context.User.IsInRole(AccountRole.Technician), id, cancellationToken);
        return result.Code is null ? Results.Ok(result.Item) : ToProblem(result.Code);
    }

    private static async Task<IResult> CreateAsync(CreateServiceRequest input, HttpContext context,
        ServiceRequestService service, CancellationToken cancellationToken)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        if (!Guid.TryParseExact(context.Request.Headers["Idempotency-Key"].ToString(), "D", out var key) || key == Guid.Empty)
            return ToProblem("clave_idempotencia_invalida");

        var result = await service.CreateAsync(userId, key, input, cancellationToken);
        if (result.Code is { } code)
            return ToProblem(code);

        return result.Replay ? Results.Ok(result.Item)
            : Results.Created($"/api/v1/solicitudes/{result.Item!.Id}", result.Item);
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdateServiceRequest input,
        HttpContext context, ServiceRequestService service, CancellationToken cancellationToken)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.UpdateAsync(userId, id, input, cancellationToken);
        return result.Code is null ? Results.Ok(result.Item) : ToProblem(result.Code);
    }

    private static async Task<IResult> DeleteAsync(Guid id, HttpContext context,
        ServiceRequestService service, CancellationToken cancellationToken)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.DeleteAsync(userId, id, context.Request.Headers["X-Resource-Version"].ToString(),
            cancellationToken);
        return result.Code is null ? Results.NoContent() : ToProblem(result.Code);
    }


    private static async Task<IResult> TransitionAsync(Guid id, string action, TransitionServiceRequest input,
        HttpContext context, ServiceRequestService service, CancellationToken ct)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.TransitionAsync(userId, context.User.IsInRole(AccountRole.Technician), id,
            action, input, ct);
        return result.Code is null ? Results.Ok(result.Item) : ToProblem(result.Code);
    }

    private static async Task<IResult> HistoryAsync(Guid id, HttpContext context,
        ServiceRequestService service, CancellationToken ct)
    {
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.HistoryAsync(userId, context.User.IsInRole(AccountRole.Technician), id, ct);
        return result.Code is null ? Results.Ok(result.Items) : ToProblem(result.Code);
    }

    private static async Task<IResult> SummaryAsync(HttpContext context, ServiceRequestService service, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        return Results.Ok(await service.SummaryAsync(userId, ct));
    }

    private static IResult ToProblem(string code)
    {
        var (status, title) = code switch
        {
            "clave_idempotencia_invalida" => (400, "Envía una clave de idempotencia válida."),
            "solicitud_invalida" => (400, "Revisa los datos de la solicitud y la fecha preferida."),
            "version_invalida" => (400, "La versión de la solicitud es inválida."),
            "solicitud_no_encontrada" => (404, "No se encontró la solicitud."),
            "cobertura_invalida" => (400, "El técnico no tiene cobertura activa para esta especialidad y sector."),
            "estado_invalido" => (409, "La acción no está disponible en el estado actual de la solicitud."),
            "transicion_invalida" => (400, "Revisa la fecha confirmada, el motivo o la observación de la acción."),
            "conflicto_version" => (409, "La solicitud cambió. Consulta su versión actual antes de volver a guardar."),
            "idempotencia_conflicto" => (409, "La clave de idempotencia ya se usó con otros datos."),
            "recurso_eliminado" => (409, "La solicitud original ya fue eliminada."),
            _ => throw new InvalidOperationException($"Resultado de solicitud desconocido: {code}.")
        };
        return Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
    }
}
