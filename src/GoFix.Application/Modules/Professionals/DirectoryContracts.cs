namespace GoFix.Application.Modules.Professionals;

/// <summary>Ficha pública. El identificador corresponde al perfil técnico.</summary>
public sealed record PublicProfessional(
    Guid Id, string Nombre, string Descripcion, SpecialtyOption[] Especialidades,
    SectorOption[] Sectores, decimal? Promedio, int CantidadValoraciones);

public sealed record DirectorySearch(int? EspecialidadId, int? SectorId, int Page = 1, int PageSize = 20);
public sealed record ProfessionalPage(PublicProfessional[] Items, int Page, int PageSize, int Total);

public interface IProfessionalDirectory
{
    Task<ProfessionalPage> SearchAsync(DirectorySearch query, CancellationToken cancellationToken);
    Task<PublicProfessional?> GetAsync(Guid id, CancellationToken cancellationToken);
}
