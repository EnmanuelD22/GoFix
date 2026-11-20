namespace GoFix.Domain.Modules.Reviews;

/// <summary>Opinión del cliente vinculada a los participantes de un servicio.</summary>
public sealed class Valoracion
{
    public required Guid Id { get; init; }
    public required Guid SolicitudId { get; init; }
    public required Guid ClienteId { get; init; }
    public required Guid TecnicoId { get; init; }
    public required byte Puntuacion { get; init; }
    public string? Comentario { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
