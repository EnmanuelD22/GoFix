namespace GoFix.Domain.Modules.Services;

/// <summary>Conserva la cobertura elegida y los participantes de una solicitud a lo largo de su historial.</summary>
public sealed class SolicitudServicio
{
    #region Identidad y datos de la visita

    public required Guid Id { get; init; }
    public required Guid ClienteId { get; init; }
    public required Guid TecnicoId { get; init; }
    public required int EspecialidadId { get; init; }
    public required int SectorId { get; init; }
    public required string Titulo { get; init; }
    public required string Descripcion { get; init; }
    public required string Direccion { get; init; }
    public required DateTimeOffset FechaPreferida { get; init; }
    public DateTimeOffset? FechaConfirmada { get; init; }

    #endregion

    #region Seguimiento y borrado lógico

    public EstadoSolicitud Estado { get; init; } = EstadoSolicitud.Pendiente;
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? AceptadaAtUtc { get; init; }
    public DateTime? InicioAtUtc { get; init; }
    public DateTime? FinAtUtc { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
    public byte[] RowVersion { get; private set; } = [];

    #endregion
}
