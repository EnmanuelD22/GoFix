using GoFix.Application.Modules.Services;
using System.Text.Json.Serialization;

namespace GoFix.Application.Modules.Reviews;

public sealed record CreateReview([property: JsonNumberHandling(JsonNumberHandling.Strict)] int Puntuacion, string? Comentario);
public sealed record ReviewDetail(Guid Id, int Puntuacion, string? Comentario, DateTime CreatedAtUtc);
public sealed record PublicReview(Guid Id, string NombreCliente, int Puntuacion, string? Comentario, DateTime CreatedAtUtc);
public sealed record ReviewPage(IReadOnlyList<PublicReview> Items, int Page, int PageSize, int Total, decimal Promedio, int Cantidad);
public sealed record ReviewResult(ReviewDetail? Item = null, string? Code = null);

public interface IReviewStore
{
    Task<ReviewResult> CreateAsync(ReviewTarget target, CreateReview input, DateTime now, CancellationToken ct);
    Task<ReviewDetail?> GetAsync(Guid solicitudId, CancellationToken ct);
    Task<ReviewPage?> ListAsync(Guid tecnicoId, int page, int pageSize, CancellationToken ct);
}

public sealed class ReviewService(IReviewEligibility eligibility, IReviewStore store, TimeProvider clock)
{
    public async Task<ReviewResult> CreateAsync(Guid clienteId, Guid solicitudId, CreateReview input, CancellationToken ct)
    {
        input = input with { Comentario = input.Comentario?.Trim() };
        if (input.Puntuacion is < 1 or > 5 || input.Comentario is { Length: > 1000 })
            return new(Code: "valoracion_invalida");
        var target = await eligibility.GetOwnAsync(clienteId, solicitudId, ct);
        if (target is null)
            return new(Code: "solicitud_no_encontrada");
        if (!target.Completada)
            return new(Code: "estado_invalido");
        return await store.CreateAsync(target, input, clock.GetUtcNow().UtcDateTime, ct);
    }

    public async Task<ReviewResult> GetAsync(Guid clienteId, Guid solicitudId, CancellationToken ct)
    {
        if (await eligibility.GetOwnAsync(clienteId, solicitudId, ct) is null)
            return new(Code: "solicitud_no_encontrada");
        return new(await store.GetAsync(solicitudId, ct));
    }

    public Task<ReviewPage?> ListAsync(Guid tecnicoId, int page, int pageSize, CancellationToken ct) =>
        store.ListAsync(tecnicoId, page, pageSize, ct);
}
