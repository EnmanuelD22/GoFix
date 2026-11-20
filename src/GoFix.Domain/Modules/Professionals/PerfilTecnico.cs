namespace GoFix.Domain.Modules.Professionals;

/// <summary>El perfil técnico nace incompleto y tiene un identificador distinto al de la cuenta.</summary>
public sealed class PerfilTecnico
{
    private PerfilTecnico() { }

    public PerfilTecnico(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("Se requiere una cuenta válida.", nameof(usuarioId));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Descripcion { get; private set; } = "";

    /// <summary>La descripción del perfil publicado es texto simple con longitud limitada.</summary>
    public void EditarDescripcion(string descripcion)
    {
        if (descripcion.Length is < 50 or > 1000)
            throw new ArgumentException("La descripción debe tener entre 50 y 1.000 caracteres.", nameof(descripcion));
        Descripcion = descripcion;
    }
}
