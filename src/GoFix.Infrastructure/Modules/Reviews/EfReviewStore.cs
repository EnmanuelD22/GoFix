using GoFix.Application.Modules.Reviews;
using GoFix.Application.Modules.Services;
using GoFix.Domain.Modules.Reviews;
using GoFix.Infrastructure.Modules.Professionals;
using GoFix.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Reviews;

internal sealed class EfReviewStore(GoFixDbContext db, IReviewQuery reputation) : IReviewStore
{
    public async Task<ReviewResult> CreateAsync(ReviewTarget target, CreateReview input, DateTime now, CancellationToken ct)
    {
        var review = new Valoracion
        {
            Id = Guid.NewGuid(), SolicitudId = target.SolicitudId, ClienteId = target.ClienteId,
            TecnicoId = target.TecnicoId, Puntuacion = (byte)input.Puntuacion,
            Comentario = string.IsNullOrEmpty(input.Comentario) ? null : input.Comentario, CreatedAtUtc = now
        };
        db.Add(review);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            if (!await db.Set<Valoracion>().AnyAsync(row => row.SolicitudId == target.SolicitudId, ct))
                throw;
            return new(Code: "valoracion_duplicada");
        }
        return new(new ReviewDetail(review.Id, review.Puntuacion, review.Comentario, now));
    }

    public Task<ReviewDetail?> GetAsync(Guid solicitudId, CancellationToken ct) =>
        db.Set<Valoracion>().AsNoTracking().Where(row => row.SolicitudId == solicitudId)
            .Select(row => new ReviewDetail(row.Id, row.Puntuacion, row.Comentario,
                DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc))).SingleOrDefaultAsync(ct);

    public async Task<ReviewPage?> ListAsync(Guid tecnicoId, int page, int pageSize, CancellationToken ct)
    {
        if (!await ProfessionalCoverage.Published(db).AnyAsync(row => row.Id == tecnicoId, ct))
            return null;
        var summaries = await reputation.GetReputationsAsync([tecnicoId], ct);
        var summary = summaries.GetValueOrDefault(tecnicoId);
        var rows = await (from review in db.Set<Valoracion>().AsNoTracking()
            join user in db.Users on review.ClienteId equals user.Id
            where review.TecnicoId == tecnicoId
            orderby review.CreatedAtUtc descending, review.Id descending
            select new { review.Id, Nombre = user.Nombre, review.Puntuacion, review.Comentario, review.CreatedAtUtc })
            .Skip(checked((page - 1) * pageSize)).Take(pageSize).ToArrayAsync(ct);
        var items = rows.Select(row => new PublicReview(row.Id,
            row.Nombre.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
            row.Puntuacion, row.Comentario, DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc))).ToArray();
        return new(items, page, pageSize, summary?.Cantidad ?? 0, summary?.Promedio ?? 0, summary?.Cantidad ?? 0);
    }
}
