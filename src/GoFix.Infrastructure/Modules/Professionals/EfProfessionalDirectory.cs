using System.Data;
using GoFix.Application.Modules.Professionals;
using GoFix.Domain.Modules.Professionals;
using GoFix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Professionals;

internal sealed class EfProfessionalDirectory(GoFixDbContext db) : IProfessionalDirectory
{
    #region Búsqueda y detalle publicados

    public async Task<ProfessionalPage> SearchAsync(DirectorySearch query, CancellationToken cancellationToken)
    {
        // Conserva las filas y los catálogos leídos hasta completar la proyección pública.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var profiles = PublishedProfiles(query.EspecialidadId, query.SectorId);

        var total = await profiles.CountAsync(cancellationToken);
        var page = await profiles.OrderBy(profile => profile.Nombre).ThenBy(profile => profile.Id)
            .Skip(checked((query.Page - 1) * query.PageSize)).Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        var result = new ProfessionalPage(await AddCoverageAsync(page, cancellationToken), query.Page, query.PageSize, total);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PublicProfessional?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var profile = await PublishedProfiles().SingleOrDefaultAsync(profile => profile.Id == id, cancellationToken);
        var result = profile is null ? null : (await AddCoverageAsync([profile], cancellationToken))[0];
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    #endregion

    private IQueryable<DirectoryRow> PublishedProfiles(int? specialty = null, int? sector = null) =>
        from profile in ProfessionalCoverage.Published(db, specialty, sector).AsNoTracking()
        join user in db.Users.AsNoTracking() on profile.UsuarioId equals user.Id
        select new DirectoryRow { Id = profile.Id, Nombre = user.Nombre, Descripcion = profile.Descripcion };

    // Las asociaciones se consultan por lote solo para los perfiles de la página solicitada.
    private async Task<PublicProfessional[]> AddCoverageAsync(DirectoryRow[] page, CancellationToken cancellationToken)
    {
        if (page.Length == 0)
            return [];

        var ids = page.Select(profile => profile.Id).ToArray();
        var specialties = await (
            from link in db.Set<TecnicoEspecialidad>().AsNoTracking()
            join option in db.Set<Especialidad>().AsNoTracking() on link.EspecialidadId equals option.Id
            where ids.Contains(link.PerfilTecnicoId) && option.Activa
            orderby option.Nombre, option.Id
            select new { link.PerfilTecnicoId, Option = new SpecialtyOption(option.Id, option.Nombre) })
            .ToArrayAsync(cancellationToken);
        var sectors = await (
            from link in db.Set<TecnicoSector>().AsNoTracking()
            join option in db.Set<Sector>().AsNoTracking() on link.SectorId equals option.Id
            where ids.Contains(link.PerfilTecnicoId) && option.Activo
            orderby option.Municipio, option.Nombre, option.Id
            select new { link.PerfilTecnicoId, Option = new SectorOption(option.Id, option.Nombre, option.Municipio) })
            .ToArrayAsync(cancellationToken);

        var specialtyGroups = specialties.ToLookup(link => link.PerfilTecnicoId, link => link.Option);
        var sectorGroups = sectors.ToLookup(link => link.PerfilTecnicoId, link => link.Option);
        return page.Select(profile => new PublicProfessional(profile.Id, profile.Nombre, profile.Descripcion,
            specialtyGroups[profile.Id].ToArray(), sectorGroups[profile.Id].ToArray(), null, 0)).ToArray();
    }

    private sealed class DirectoryRow
    {
        public Guid Id { get; init; }
        public required string Nombre { get; init; }
        public required string Descripcion { get; init; }
    }
}
