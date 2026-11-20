namespace GoFix.Domain.Modules.Services;

/// <summary>Valores de evento persistidos en el historial de una solicitud.</summary>
public enum TipoEventoSolicitud : byte
{
    Creacion = 1,
    Edicion = 2,
    Transicion = 3,
    Eliminacion = 4
}

/// <summary>Registra el hecho y su actor sin duplicar el contenido privado de la solicitud.</summary>
public sealed class HistorialSolicitud
{
    public long Id { get; private set; }
    public required Guid SolicitudId { get; init; }
    public required Guid ActorId { get; init; }
    public required TipoEventoSolicitud TipoEvento { get; init; }
    public EstadoSolicitud? EstadoPrevio { get; init; }
    public EstadoSolicitud? EstadoNuevo { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public string? Observacion { get; init; }
}
