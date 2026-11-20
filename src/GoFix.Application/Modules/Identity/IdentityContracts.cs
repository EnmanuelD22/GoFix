namespace GoFix.Application.Modules.Identity;

public static class AccountRole
{
    public const string Customer = "cliente";
    public const string Technician = "tecnico";
}

public sealed record Registration(string? Nombre, string? Correo, string? Telefono, string? Contrasena, string? Rol);

public sealed record Credentials(string? Correo, string? Contrasena);

public sealed record AccountDetails(string? Nombre, string? Telefono);

/// <summary>Representación privada de la cuenta. Solo se entrega al titular.</summary>
public sealed record Account(Guid Id, string Nombre, string Correo, string Telefono, string Rol, Guid? PerfilTecnicoId);

public sealed record Session(Guid Id, Account Cuenta, DateTimeOffset ExpiresAt);

public sealed record AccountChange(Account? Account, Dictionary<string, string[]>? Errors = null);

public interface IIdentityStore
{
    Task<bool> RegisterAsync(Registration input, CancellationToken cancellationToken);
    Task<Session?> AuthenticateAsync(Credentials input, CancellationToken cancellationToken);
    Task<Account?> FindAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<Account?> UpdateAsync(Guid userId, AccountDetails input, CancellationToken cancellationToken);
    Task<bool> HasSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}
