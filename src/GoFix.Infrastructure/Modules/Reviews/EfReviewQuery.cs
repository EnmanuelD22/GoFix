using GoFix.Application.Modules.Reviews;
using GoFix.Domain.Modules.Reviews;
using GoFix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Reviews;

internal sealed class EfReviewQuery(GoFixDbContext db) : IReviewQuery
{
    public async Task<IReadOnlyDictionary<Guid, Reputation>> GetReputationsAsync(
        Guid[] technicianIds, CancellationToken cancellationToken)
    {
        if (technicianIds.Length == 0)
            return new Dictionary<Guid, Reputation>();

        var summaries = await db.Set<Valoracion>().AsNoTracking()
            .Where(review => technicianIds.Contains(review.TecnicoId))
            .GroupBy(review => review.TecnicoId)
            .Select(group => new { Id = group.Key, Average = group.Average(review => (decimal)review.Puntuacion), Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        return summaries.ToDictionary(summary => summary.Id,
            summary => new Reputation(summary.Id, Math.Round(summary.Average, 1, MidpointRounding.AwayFromZero), summary.Count));
    }
}
