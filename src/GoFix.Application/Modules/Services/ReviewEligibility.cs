namespace GoFix.Application.Modules.Services;

public sealed record ReviewTarget(Guid SolicitudId, Guid ClienteId, Guid TecnicoId, bool Completada);

public interface IReviewEligibility
{
    Task<ReviewTarget?> GetOwnAsync(Guid clienteId, Guid solicitudId, CancellationToken ct);
}
