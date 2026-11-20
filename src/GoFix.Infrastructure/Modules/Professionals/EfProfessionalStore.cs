using System.Data;
using GoFix.Application.Modules.Professionals;
using GoFix.Domain.Modules.Professionals;
using GoFix.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Professionals;

internal sealed class EfProfessionalStore(GoFixDbContext db) : IProfessionalStore
{
    #region Catálogos públicos y consulta propia

    public Task<SpecialtyOption[]> ListSpecialtiesAsync(CancellationToken cancellationToken) =>
        db.Set<Especialidad>().AsNoTracking().Where(entry => entry.Activa)
            .OrderBy(entry => entry.Nombre).ThenBy(entry => entry.Id)
            .Select(entry => new SpecialtyOption(entry.Id, entry.Nombre)).ToArrayAsync(cancellationToken);

    public Task<SectorOption[]> ListSectorsAsync(CancellationToken cancellationToken) =>
        db.Set<Sector>().AsNoTracking().Where(entry => entry.Activo)
            .OrderBy(entry => entry.Municipio).ThenBy(entry => entry.Nombre).ThenBy(entry => entry.Id)
            .Select(entry => new SectorOption(entry.Id, entry.Nombre, entry.Municipio)).ToArrayAsync(cancellationToken);

    public async Task<ProfessionalProfile?> FindProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        // La ficha y sus asociaciones se leen de una misma versión confirmada.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var profile = await db.PerfilesTecnicos.SingleOrDefaultAsync(entry => entry.UsuarioId == userId, cancellationToken);
        var result = profile is null ? null : await ReadProfileAsync(profile, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    #endregion

    #region Escritura atómica y concurrencia

    public async Task<ProfileSaveResult> SaveProfileAsync(Guid userId, string description, int[] specialties,
        int[] sectors, byte[] version, CancellationToken cancellationToken)
    {
        try
        {
            return await SaveCoreAsync(userId, description, specialties, sectors, version, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(ProfileSaveStatus.Conflict);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            // SQL revierte la víctima de un interbloqueo; el cliente debe volver a consultar antes de escribir.
            db.ChangeTracker.Clear();
            return new(ProfileSaveStatus.Conflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            db.ChangeTracker.Clear();
            return new(ProfileSaveStatus.Conflict);
        }
    }

    private async Task<ProfileSaveResult> SaveCoreAsync(Guid userId, string description, int[] specialties,
        int[] sectors, byte[] version, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // UPDLOCK serializa ediciones de la misma ficha antes de comprobar su versión.
        var profile = await db.PerfilesTecnicos.FromSqlInterpolated(
                $"SELECT * FROM [professionals].[PerfilTecnico] WITH (UPDLOCK) WHERE [UsuarioId] = {userId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (profile is null)
            return new(ProfileSaveStatus.NotFound);
        if (!db.Entry(profile).Property<byte[]>("RowVersion").CurrentValue!.SequenceEqual(version))
            return new(ProfileSaveStatus.Conflict);

        // Serializable conserva los catálogos activos hasta confirmar la cobertura, incluso ante desactivaciones.
        var errors = new Dictionary<string, string[]>();
        if (await db.Set<Especialidad>().CountAsync(entry => specialties.Contains(entry.Id) && entry.Activa, cancellationToken) != specialties.Length)
            errors["especialidadIds"] = ["Las especialidades seleccionadas deben existir y estar activas."];
        if (await db.Set<Sector>().CountAsync(entry => sectors.Contains(entry.Id) && entry.Activo, cancellationToken) != sectors.Length)
            errors["sectorIds"] = ["Los sectores seleccionados deben existir y estar activos."];
        if (errors.Count > 0)
            return new(ProfileSaveStatus.InvalidCatalog, Errors: errors);

        profile.EditarDescripcion(description);
        // También cambia rowversion cuando la descripción coincide y solo se modifica la cobertura.
        db.Entry(profile).Property(entry => entry.Descripcion).IsModified = true;
        await db.SaveChangesAsync(cancellationToken);
        await db.Set<TecnicoEspecialidad>().Where(entry => entry.PerfilTecnicoId == profile.Id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<TecnicoSector>().Where(entry => entry.PerfilTecnicoId == profile.Id).ExecuteDeleteAsync(cancellationToken);
        db.Set<TecnicoEspecialidad>().AddRange(specialties.Select(id => new TecnicoEspecialidad(profile.Id, id)));
        db.Set<TecnicoSector>().AddRange(sectors.Select(id => new TecnicoSector(profile.Id, id)));
        await db.SaveChangesAsync(cancellationToken);

        var result = await ReadProfileAsync(profile, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(ProfileSaveStatus.Saved, result);
    }

    #endregion

    private async Task<ProfessionalProfile> ReadProfileAsync(PerfilTecnico profile, CancellationToken cancellationToken)
    {
        var specialties = await db.Set<TecnicoEspecialidad>().AsNoTracking()
            .Where(entry => entry.PerfilTecnicoId == profile.Id).OrderBy(entry => entry.EspecialidadId)
            .Select(entry => entry.EspecialidadId).ToArrayAsync(cancellationToken);
        var sectors = await db.Set<TecnicoSector>().AsNoTracking()
            .Where(entry => entry.PerfilTecnicoId == profile.Id).OrderBy(entry => entry.SectorId)
            .Select(entry => entry.SectorId).ToArrayAsync(cancellationToken);
        var hasActiveSpecialty = await db.Set<Especialidad>().AnyAsync(entry => specialties.Contains(entry.Id) && entry.Activa, cancellationToken);
        var hasActiveSector = await db.Set<Sector>().AnyAsync(entry => sectors.Contains(entry.Id) && entry.Activo, cancellationToken);
        var version = db.Entry(profile).Property<byte[]>("RowVersion").CurrentValue!;
        return new(profile.Id, profile.Descripcion, specialties, sectors, Convert.ToBase64String(version),
            profile.Descripcion.Length is >= 50 and <= 1000 && hasActiveSpecialty && hasActiveSector);
    }
}
