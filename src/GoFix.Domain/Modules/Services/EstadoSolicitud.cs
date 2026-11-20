namespace GoFix.Domain.Modules.Services;

/// <summary>Los valores numéricos identifican estados persistidos y deben conservarse en futuras migraciones.</summary>
public enum EstadoSolicitud : byte
{
    Pendiente = 1,
    Aceptada = 2,
    Rechazada = 3,
    Cancelada = 4,
    EnProceso = 5,
    Completada = 6
}
