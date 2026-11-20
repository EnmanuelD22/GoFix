namespace GoFix.Domain.Modules.Professionals;

/// <summary>Entrada de especialidad con código estable para la carga de operación.</summary>
public sealed class Especialidad
{
    public int Id { get; private set; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public bool Activa { get; init; } = true;
}
