using System.Security.Cryptography;
using System.Text.Json;
using GoFix.Domain.Modules.Services;

namespace GoFix.Application.Modules.Services;

public sealed class ServiceRequestService(IRepositorioSolicitudes store, TimeProvider clock)
{
    public Task<ServiceRequestResult> CreateAsync(Guid userId, Guid key, CreateServiceRequest input, CancellationToken ct)
    {
        input = input with
        {
            Titulo = input.Titulo?.Trim(),
            Descripcion = input.Descripcion?.Trim(),
            Direccion = input.Direccion?.Trim()
        };

        if (key == Guid.Empty || input.TecnicoId == Guid.Empty || input.EspecialidadId <= 0 ||
            !ValidFields(input.SectorId, input.Titulo, input.Descripcion, input.Direccion, input.FechaPreferida))
            return Error("solicitud_invalida");

        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        return store.CreateAsync(userId, key, input, hash, clock.GetUtcNow(), ct);
    }

    public Task<ServiceRequestPage> ListAsync(Guid userId, bool isTechnician, EstadoSolicitud? estado, int page,
        int pageSize, CancellationToken ct) => store.ListAsync(userId, isTechnician, estado, page, pageSize, ct);

    public Task<ServiceRequestResult> GetAsync(Guid userId, bool isTechnician, Guid id, CancellationToken ct) =>
        store.GetAsync(userId, isTechnician, id, ct);

    public Task<ServiceRequestResult> UpdateAsync(Guid userId, Guid id, UpdateServiceRequest input, CancellationToken ct)
    {
        input = input with
        {
            Titulo = input.Titulo?.Trim(),
            Descripcion = input.Descripcion?.Trim(),
            Direccion = input.Direccion?.Trim()
        };

        if (!ValidFields(input.SectorId, input.Titulo, input.Descripcion, input.Direccion, input.FechaPreferida))
            return Error("solicitud_invalida");

        var version = ParseVersion(input.Version);
        return version is null
            ? Error("version_invalida")
            : store.UpdateAsync(userId, id, input, version, clock.GetUtcNow(), ct);
    }

    public Task<ServiceRequestResult> DeleteAsync(Guid userId, Guid id, string version, CancellationToken ct)
    {
        var parsedVersion = ParseVersion(version);
        return parsedVersion is null
            ? Error("version_invalida")
            : store.DeleteAsync(userId, id, parsedVersion, clock.GetUtcNow(), ct);
    }


    public Task<ServiceRequestResult> TransitionAsync(Guid userId, bool isTechnician, Guid id, string action,
        TransitionServiceRequest input, CancellationToken ct)
    {
        input = input with { Motivo = input.Motivo?.Trim(), Observacion = input.Observacion?.Trim() };
        var version = ParseVersion(input.Version);
        if (version is null)
            return Error("version_invalida");
        if ((action is "rechazar" or "cancelar" && input.Motivo is not { Length: >= 10 and <= 500 })
            || (action == "completar" && input.Observacion is { Length: > 1000 })
            || (action == "aceptar" && (input.FechaConfirmada is null || input.FechaConfirmada <= clock.GetUtcNow())))
            return Error("transicion_invalida");
        return store.TransitionAsync(userId, isTechnician, id, action, input, version, clock.GetUtcNow(), ct);
    }

    public Task<RequestHistoryResult> HistoryAsync(Guid userId, bool isTechnician, Guid id, CancellationToken ct) =>
        store.HistoryAsync(userId, isTechnician, id, ct);

    public Task<TechnicianSummary> SummaryAsync(Guid userId, CancellationToken ct) =>
        store.SummaryAsync(userId, clock.GetUtcNow(), ct);

    private static bool ValidFields(int sector, string? title, string? description, string? address,
        DateTimeOffset date) => sector > 0 && title is { Length: >= 10 and <= 120 }
        && description is { Length: >= 20 and <= 2000 } && address is { Length: >= 10 and <= 300 }
        && date != default;

    private static byte[]? ParseVersion(string? value)
    {
        if (value?.Length != 12)
            return null;

        var version = new byte[8];
        return Convert.TryFromBase64String(value, version, out var count) && count == version.Length &&
            value == Convert.ToBase64String(version) ? version : null;
    }

    private static Task<ServiceRequestResult> Error(string code) => Task.FromResult(new ServiceRequestResult(Code: code));
}
