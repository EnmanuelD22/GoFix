namespace GoFix.Domain.Modules.Professionals;

/// <summary>Sector con nombre único dentro de su municipio.</summary>
public sealed class Sector
{
    public int Id { get; private set; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required string Municipio { get; init; }
    public bool Activo { get; init; } = true;
}
