namespace GoFix.Application.Modules.Professionals;

public sealed class ProfessionalService(IProfessionalStore store)
{
    public Task<SpecialtyOption[]> ListSpecialtiesAsync(CancellationToken cancellationToken) =>
        store.ListSpecialtiesAsync(cancellationToken);

    public Task<SectorOption[]> ListSectorsAsync(CancellationToken cancellationToken) =>
        store.ListSectorsAsync(cancellationToken);

    public Task<ProfessionalProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        store.FindProfileAsync(userId, cancellationToken);

    public async Task<ProfileSaveResult> UpdateProfileAsync(Guid userId, ProfileEdit input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var description = input.Descripcion?.Trim();
        if (description is not { Length: >= 50 and <= 1000 } ||
            description.Any(character => character is '<' or '>' || char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            errors["descripcion"] = ["Escribe una descripción de 50 a 1.000 caracteres como texto sin HTML."];

        ValidateIds(input.EspecialidadIds, "especialidadIds", errors);
        ValidateIds(input.SectorIds, "sectorIds", errors);
        var version = ParseVersion(input.Version);
        if (version is null)
            errors["version"] = ["Consulta la versión actual del perfil antes de guardar."];

        if (errors.Count > 0)
            return new(ProfileSaveStatus.InvalidCatalog, Errors: errors);

        return await store.SaveProfileAsync(userId, description!, input.EspecialidadIds!, input.SectorIds!,
            version!, cancellationToken);
    }

    private static void ValidateIds(int[]? ids, string field, Dictionary<string, string[]> errors)
    {
        if (ids is not { Length: > 0 } || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            errors[field] = ["Selecciona al menos una opción válida, sin repetir identificadores."];
    }

    private static byte[]? ParseVersion(string? value)
    {
        if (value is null || value.Length != 12)
            return null;
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
