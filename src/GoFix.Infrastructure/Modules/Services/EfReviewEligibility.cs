using GoFix.Application.Modules.Services;
using GoFix.Domain.Modules.Services;
using GoFix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Services;

internal sealed class EfReviewEligibility(GoFixDbContext db) : IReviewEligibility
{
    public Task<ReviewTarget?> GetOwnAsync(Guid clienteId, Guid solicitudId, CancellationToken ct) =>
        db.Set<SolicitudServicio>().AsNoTracking()
            .Where(row => row.Id == solicitudId && row.ClienteId == clienteId && !row.IsDeleted)
            .Select(row => new ReviewTarget(row.Id, row.ClienteId, row.TecnicoId, row.Estado == EstadoSolicitud.Completada))
            .SingleOrDefaultAsync(ct);
}
