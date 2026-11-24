namespace GoFix.Domain.Modules.Services;

/// <summary>Conserva la cobertura elegida y los participantes de una solicitud a lo largo de su historial.</summary>
public sealed class SolicitudServicio
{
    private SolicitudServicio() { }

    public SolicitudServicio(Guid id, Guid clienteId, Guid tecnicoId, int especialidadId, int sectorId,
        string titulo, string descripcion, string direccion, DateTimeOffset fechaPreferida, DateTime createdAtUtc)
    {
        Id = id;
        ClienteId = clienteId;
        TecnicoId = tecnicoId;
        EspecialidadId = especialidadId;
        CreatedAtUtc = createdAtUtc;
        Editar(sectorId, titulo, descripcion, direccion, fechaPreferida);
    }

    public void Editar(int sectorId, string titulo, string descripcion, string direccion, DateTimeOffset fechaPreferida)
    {
        VerificarPendiente();
        SectorId = sectorId;
        Titulo = titulo;
        Descripcion = descripcion;
        Direccion = direccion;
        FechaPreferida = fechaPreferida;
    }

    public void Eliminar(DateTime deletedAtUtc)
    {
        VerificarPendiente();
        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc;
    }

    public bool PuedeModificar => Estado == EstadoSolicitud.Pendiente && !IsDeleted;

    private void VerificarPendiente()
    {
        if (!PuedeModificar)
            throw new InvalidOperationException("Solo una solicitud pendiente permite cambios.");
    }

    #region Identidad y datos de la visita

    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public Guid TecnicoId { get; private set; }
    public int EspecialidadId { get; private set; }
    public int SectorId { get; private set; }
    public string Titulo { get; private set; } = "";
    public string Descripcion { get; private set; } = "";
    public string Direccion { get; private set; } = "";
    public DateTimeOffset FechaPreferida { get; private set; }
    public DateTimeOffset? FechaConfirmada { get; init; }

    #endregion

    #region Seguimiento y borrado lógico

    public EstadoSolicitud Estado { get; init; } = EstadoSolicitud.Pendiente;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AceptadaAtUtc { get; init; }
    public DateTime? InicioAtUtc { get; init; }
    public DateTime? FinAtUtc { get; init; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    #endregion
}
