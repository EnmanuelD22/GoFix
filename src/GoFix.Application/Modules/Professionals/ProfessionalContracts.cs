namespace GoFix.Application.Modules.Professionals;

public sealed record SpecialtyOption(int Id, string Nombre);
public sealed record SectorOption(int Id, string Nombre, string Municipio);

/// <summary>Ficha privada del perfil propio. La versión corresponde al rowversion completo del perfil.</summary>
public sealed record ProfessionalProfile(
    Guid Id, string Descripcion, int[] EspecialidadIds, int[] SectorIds, string Version, bool Publicado);

public sealed record ProfileEdit(string? Descripcion, int[]? EspecialidadIds, int[]? SectorIds, string? Version);

public enum ProfileSaveStatus { Saved, NotFound, Conflict, InvalidCatalog }

public sealed record ProfileSaveResult(ProfileSaveStatus Status, ProfessionalProfile? Profile = null,
    Dictionary<string, string[]>? Errors = null);

public interface IProfessionalStore
{
    Task<SpecialtyOption[]> ListSpecialtiesAsync(CancellationToken cancellationToken);
    Task<SectorOption[]> ListSectorsAsync(CancellationToken cancellationToken);
    Task<ProfessionalProfile?> FindProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileSaveResult> SaveProfileAsync(Guid userId, string description, int[] specialties, int[] sectors,
        byte[] version, CancellationToken cancellationToken);
}
