namespace GoFix.Application.Modules.Reviews;

/// <summary>Resumen calculado de opiniones persistidas, sin datos privados de sus autores.</summary>
public sealed record Reputation(Guid TecnicoId, decimal Promedio, int Cantidad);

public interface IReviewQuery
{
    Task<IReadOnlyDictionary<Guid, Reputation>> GetReputationsAsync(Guid[] technicianIds, CancellationToken cancellationToken);
}
