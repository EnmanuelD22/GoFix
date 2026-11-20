namespace GoFix.Domain.Modules.Services;

/// <summary>Relaciona la clave de creación de un cliente y su huella de contenido con una única solicitud.</summary>
public sealed class IdempotenciaSolicitud
{
    public required Guid ClienteId { get; init; }
    public required Guid Clave { get; init; }
    public required Guid SolicitudId { get; init; }

    /// <summary>SHA-256 binario del contenido canónico de la creación.</summary>
    public required byte[] HashContenido { get; init; }
}
