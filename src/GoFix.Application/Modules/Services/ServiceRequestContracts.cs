using System.Text.Json.Serialization;
using GoFix.Domain.Modules.Services;

namespace GoFix.Application.Modules.Services;

public sealed record CreateServiceRequest(Guid TecnicoId, int EspecialidadId, int SectorId, string? Titulo,
    string? Descripcion, string? Direccion, DateTimeOffset FechaPreferida);

public sealed record UpdateServiceRequest(int SectorId, string? Titulo, string? Descripcion, string? Direccion,
    DateTimeOffset FechaPreferida, string? Version);

public sealed record ServiceRequestSummary(Guid Id, Guid TecnicoId, int EspecialidadId, int SectorId, string Titulo,
    DateTimeOffset FechaPreferida,
    [property: JsonConverter(typeof(JsonStringEnumConverter<EstadoSolicitud>))] EstadoSolicitud Estado,
    DateTime CreatedAtUtc);

public sealed record ServiceRequestDetail(Guid Id, Guid TecnicoId, int EspecialidadId, int SectorId, string Titulo,
    string Descripcion, string Direccion, DateTimeOffset FechaPreferida,
    [property: JsonConverter(typeof(JsonStringEnumConverter<EstadoSolicitud>))] EstadoSolicitud Estado,
    DateTime CreatedAtUtc, string Version, DateTimeOffset? FechaConfirmada = null,
    DateTime? AceptadaAtUtc = null, DateTime? InicioAtUtc = null, DateTime? FinAtUtc = null,
    RequestContact? Contacto = null);

public sealed record ServiceRequestPage(IReadOnlyList<ServiceRequestSummary> Items, int Page, int PageSize, int Total);

public sealed record ServiceRequestResult(ServiceRequestDetail? Item = null, string? Code = null, bool Replay = false);

public interface IRepositorioSolicitudes
{
    Task<ServiceRequestResult> TransitionAsync(Guid userId, bool isTechnician, Guid id, string action,
        TransitionServiceRequest input, byte[] version, DateTimeOffset now, CancellationToken ct);
    Task<RequestHistoryResult> HistoryAsync(Guid userId, bool isTechnician, Guid id, CancellationToken ct);
    Task<TechnicianSummary> SummaryAsync(Guid userId, DateTimeOffset now, CancellationToken ct);
    Task<ServiceRequestResult> CreateAsync(Guid userId, Guid key, CreateServiceRequest input, byte[] hash,
        DateTimeOffset now, CancellationToken ct);
    Task<ServiceRequestPage> ListAsync(Guid userId, bool isTechnician, EstadoSolicitud? estado, int page, int pageSize,
        CancellationToken ct);
    Task<ServiceRequestResult> GetAsync(Guid userId, bool isTechnician, Guid id, CancellationToken ct);
    Task<ServiceRequestResult> UpdateAsync(Guid userId, Guid id, UpdateServiceRequest input, byte[] version,
        DateTimeOffset now, CancellationToken ct);
    Task<ServiceRequestResult> DeleteAsync(Guid userId, Guid id, byte[] version, DateTimeOffset now, CancellationToken ct);
}


public sealed record TransitionServiceRequest(string? Version, DateTimeOffset? FechaConfirmada = null,
    string? Motivo = null, string? Observacion = null);

public sealed record RequestContact(string Nombre, string Telefono);
public sealed record RequestHistoryItem(long Id, Guid ActorId, string ActorNombre,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TipoEventoSolicitud>))] TipoEventoSolicitud TipoEvento,
    [property: JsonConverter(typeof(JsonStringEnumConverter<EstadoSolicitud>))] EstadoSolicitud? EstadoPrevio,
    [property: JsonConverter(typeof(JsonStringEnumConverter<EstadoSolicitud>))] EstadoSolicitud? EstadoNuevo,
    DateTime CreatedAtUtc, string? Observacion);
public sealed record UpcomingVisit(Guid Id, string Titulo, DateTimeOffset FechaConfirmada);
public sealed record TechnicianSummary(int Pendientes, int Aceptadas, int EnProceso, IReadOnlyList<UpcomingVisit> ProximasVisitas);
public sealed record RequestHistoryResult(IReadOnlyList<RequestHistoryItem>? Items = null, string? Code = null);
