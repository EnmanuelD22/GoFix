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
