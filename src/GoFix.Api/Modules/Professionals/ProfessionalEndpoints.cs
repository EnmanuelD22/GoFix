using GoFix.Api.Modules.Identity;
using GoFix.Application.Modules.Identity;
using GoFix.Application.Modules.Professionals;

namespace GoFix.Api.Modules.Professionals;

internal static class ProfessionalEndpoints
{
    internal static void MapProfessionalEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/especialidades", (ProfessionalService service, CancellationToken token) =>
            service.ListSpecialtiesAsync(token)).WithTags("Catálogos").Produces<SpecialtyOption[]>();
        api.MapGet("/sectores", (ProfessionalService service, CancellationToken token) =>
            service.ListSectorsAsync(token)).WithTags("Catálogos").Produces<SectorOption[]>();

        api.MapGet("/tecnicos", SearchAsync).WithTags("Directorio")
            .Produces<ProfessionalPage>().ProducesValidationProblem();
        api.MapGet("/tecnicos/{id:guid}", PublicProfileAsync).WithTags("Directorio")
            .Produces<PublicProfessional>().ProducesProblem(404);

        var ownProfile = api.MapGroup("/tecnicos/me/perfil").WithTags("Perfil profesional")
            .RequireAuthorization(policy => policy.RequireRole(AccountRole.Technician));
        ownProfile.MapGet("", ReadAsync).Produces<ProfessionalProfile>().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        ownProfile.MapPut("", UpdateAsync).Produces<ProfessionalProfile>()
            .ProducesValidationProblem().ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> SearchAsync(DirectoryService directory, CancellationToken token,
        int? especialidadId = null, int? sectorId = null, int page = 1, int pageSize = 20)
    {
        var query = new DirectorySearch(especialidadId, sectorId, page, pageSize);
        var errors = DirectoryService.Validate(query);
        return errors.Count > 0 ? Results.ValidationProblem(errors)
            : Results.Ok(await directory.SearchAsync(query, token));
    }

    private static async Task<IResult> PublicProfileAsync(Guid id, DirectoryService directory, CancellationToken token)
    {
        var profile = await directory.GetAsync(id, token);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static async Task<IResult> ReadAsync(HttpContext context, ProfessionalService service, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var profile = await service.GetProfileAsync(userId, token);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    }

    private static async Task<IResult> UpdateAsync(ProfileEdit input, HttpContext context,
        ProfessionalService service, CancellationToken token)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IdentityEndpoints.TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();
        var result = await service.UpdateProfileAsync(userId, input, token);
        return result.Status switch
        {
            ProfileSaveStatus.Saved => Results.Ok(result.Profile),
            ProfileSaveStatus.NotFound => Results.NotFound(),
            ProfileSaveStatus.Conflict => Results.Problem(statusCode: 409,
                title: "El perfil cambió. Consulta su versión actual antes de volver a guardar."),
            _ => Results.ValidationProblem(result.Errors!)
        };
    }
}
