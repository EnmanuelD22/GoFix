using System.Data;
using GoFix.Application.Modules.Services;
using GoFix.Infrastructure.Modules.Professionals;
using GoFix.Domain.Modules.Services;
using GoFix.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Services;

internal sealed class EfServiceRequestStore(GoFixDbContext db) : IRepositorioSolicitudes
{
    public async Task<ServiceRequestResult> CreateAsync(Guid userId, Guid key, CreateServiceRequest input,
        byte[] hash, DateTimeOffset now, CancellationToken ct)
    {
        var previous = await ReadReplayAsync(userId, key, hash, ct);
        if (previous is not null)
            return previous;

        try
        {
            // La cobertura queda estable hasta guardar la solicitud. La PK cliente-clave arbitra la creación.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            if (input.FechaPreferida <= now)
                return new(Code: "solicitud_invalida");

            if (!await HasCoverageAsync(input.TecnicoId, input.EspecialidadId, input.SectorId, ct))
                return new(Code: "cobertura_invalida");

            var request = new SolicitudServicio(Guid.NewGuid(), userId, input.TecnicoId, input.EspecialidadId,
                input.SectorId, input.Titulo!, input.Descripcion!, input.Direccion!, input.FechaPreferida,
                now.UtcDateTime);
            db.Add(request);
            db.Add(Event(request, userId, TipoEventoSolicitud.Creacion, now));
            db.Add(new IdempotenciaSolicitud
            {
                ClienteId = userId,
                Clave = key,
                SolicitudId = request.Id,
                HashContenido = hash
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(Detail(request));
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Tras revertir la transacción, solo una clave existente confirma que se trata de un reenvío.
            db.ChangeTracker.Clear();
            var committed = await ReadReplayAsync(userId, key, hash, ct);
            if (committed is null)
                throw;
            return committed;
        }
    }

    public async Task<ServiceRequestPage> ListAsync(Guid userId, bool isTechnician, EstadoSolicitud? estado,
        int page, int pageSize, CancellationToken ct)
    {
        var requests = VisibleTo(userId, isTechnician);
        if (estado is { } state)
            requests = requests.Where(request => request.Estado == state);

        var total = await requests.CountAsync(ct);
        var rows = await requests.OrderByDescending(request => request.CreatedAtUtc)
            .ThenByDescending(request => request.Id)
            .Skip(checked((page - 1) * pageSize)).Take(pageSize)
            .Select(request => new ServiceRequestSummary(request.Id, request.TecnicoId, request.EspecialidadId,
                request.SectorId, request.Titulo, request.FechaPreferida, request.Estado,
                DateTime.SpecifyKind(request.CreatedAtUtc, DateTimeKind.Utc)))
            .ToArrayAsync(ct);
        return new(rows, page, pageSize, total);
    }

    public async Task<ServiceRequestResult> GetAsync(Guid userId, bool isTechnician, Guid id, CancellationToken ct)
    {
        var request = await VisibleTo(userId, isTechnician).SingleOrDefaultAsync(row => row.Id == id, ct);
        return request is null ? new(Code: "solicitud_no_encontrada") : new(Detail(request));
    }

    public async Task<ServiceRequestResult> UpdateAsync(Guid userId, Guid id, UpdateServiceRequest input,
        byte[] version, DateTimeOffset now, CancellationToken ct)
    {
        var request = await FindOwnedAsync(userId, id, ct);
        if (CheckMutation(request, version) is { } error)
            return new(Code: error);

        var fechaModificada = !input.FechaPreferida.EqualsExact(request!.FechaPreferida);
        if (fechaModificada && input.FechaPreferida <= now)
            return new(Code: "solicitud_invalida");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await HasCoverageAsync(request.TecnicoId, request.EspecialidadId, input.SectorId, ct))
            return new(Code: "cobertura_invalida");

        request.Editar(input.SectorId, input.Titulo!, input.Descripcion!, input.Direccion!, input.FechaPreferida);
        // Incluso una edición con los mismos datos comprueba y renueva la versión antes de añadir el evento.
        db.Entry(request).Property(row => row.Titulo).IsModified = true;
        if (fechaModificada)
            db.Entry(request).Property(row => row.FechaPreferida).IsModified = true;
        var result = await SaveMutationAsync(request, userId, TipoEventoSolicitud.Edicion, now, ct);
        if (result.Code is null)
            await transaction.CommitAsync(ct);

        return result;
    }

    public async Task<ServiceRequestResult> DeleteAsync(Guid userId, Guid id, byte[] version, DateTimeOffset now,
        CancellationToken ct)
    {
        var request = await FindOwnedAsync(userId, id, ct);
        if (CheckMutation(request, version) is { } error)
            return new(Code: error);

        request!.Eliminar(now.UtcDateTime);
        return await SaveMutationAsync(request, userId, TipoEventoSolicitud.Eliminacion, now, ct);
    }

    private IQueryable<SolicitudServicio> VisibleTo(Guid userId, bool isTechnician)
    {
        var requests = db.Set<SolicitudServicio>().AsNoTracking().Where(request => !request.IsDeleted);
        return isTechnician
            ? requests.Where(request => db.PerfilesTecnicos.Any(profile =>
                profile.Id == request.TecnicoId && profile.UsuarioId == userId))
            : requests.Where(request => request.ClienteId == userId);
    }

    private Task<SolicitudServicio?> FindOwnedAsync(Guid userId, Guid id, CancellationToken ct) =>
        db.Set<SolicitudServicio>().SingleOrDefaultAsync(row => row.Id == id && row.ClienteId == userId && !row.IsDeleted, ct);

    private Task<bool> HasCoverageAsync(Guid technician, int specialty, int sector, CancellationToken ct) =>
        ProfessionalCoverage.Published(db, specialty, sector).AnyAsync(profile => profile.Id == technician, ct);

    private async Task<ServiceRequestResult?> ReadReplayAsync(Guid userId, Guid key, byte[] hash, CancellationToken ct)
    {
        var existing = await (
            from entry in db.Set<IdempotenciaSolicitud>().AsNoTracking()
            join request in db.Set<SolicitudServicio>().AsNoTracking() on entry.SolicitudId equals request.Id
            where entry.ClienteId == userId && entry.Clave == key
            select new { entry.HashContenido, Request = request }).SingleOrDefaultAsync(ct);

        if (existing is null)
            return null;
        if (!existing.HashContenido.AsSpan().SequenceEqual(hash))
            return new(Code: "idempotencia_conflicto");
        return existing.Request.IsDeleted
            ? new(Code: "recurso_eliminado")
            : new(Detail(existing.Request), Replay: true);
    }

    private static string? CheckMutation(SolicitudServicio? request, byte[] version)
    {
        if (request is null)
            return "solicitud_no_encontrada";
        if (!request.PuedeModificar)
            return "estado_invalido";
        return request.RowVersion.AsSpan().SequenceEqual(version) ? null : "conflicto_version";
    }

    private async Task<ServiceRequestResult> SaveMutationAsync(SolicitudServicio request, Guid userId,
        TipoEventoSolicitud type, DateTimeOffset now, CancellationToken ct)
    {
        db.Add(Event(request, userId, type, now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return new(Code: "conflicto_version");
        }

        return type == TipoEventoSolicitud.Eliminacion ? new() : new(Detail(request));
    }

    private static HistorialSolicitud Event(SolicitudServicio request, Guid userId, TipoEventoSolicitud type,
        DateTimeOffset now) => new()
    {
        SolicitudId = request.Id,
        ActorId = userId,
        TipoEvento = type,
        EstadoNuevo = type == TipoEventoSolicitud.Creacion ? EstadoSolicitud.Pendiente : null,
        CreatedAtUtc = now.UtcDateTime
    };

    private static ServiceRequestDetail Detail(SolicitudServicio request) =>
        new(request.Id, request.TecnicoId, request.EspecialidadId, request.SectorId, request.Titulo,
            request.Descripcion, request.Direccion, request.FechaPreferida, request.Estado,
            DateTime.SpecifyKind(request.CreatedAtUtc, DateTimeKind.Utc), Convert.ToBase64String(request.RowVersion));
}
