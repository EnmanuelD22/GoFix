using GoFix.Domain.Modules.Professionals;
using GoFix.Infrastructure.Persistence;

namespace GoFix.Infrastructure.Modules.Professionals;

internal static class ProfessionalCoverage
{
    internal static IQueryable<PerfilTecnico> Published(GoFixDbContext db, int? specialty = null, int? sector = null) =>
        db.PerfilesTecnicos.Where(profile =>
            profile.Descripcion.Length >= 50 && profile.Descripcion.Length <= 1000 &&
            db.Set<TecnicoEspecialidad>().Any(link => link.PerfilTecnicoId == profile.Id &&
                (specialty == null || link.EspecialidadId == specialty) &&
                db.Set<Especialidad>().Any(option => option.Id == link.EspecialidadId && option.Activa)) &&
            db.Set<TecnicoSector>().Any(link => link.PerfilTecnicoId == profile.Id &&
                (sector == null || link.SectorId == sector) &&
                db.Set<Sector>().Any(option => option.Id == link.SectorId && option.Activo)));
}
