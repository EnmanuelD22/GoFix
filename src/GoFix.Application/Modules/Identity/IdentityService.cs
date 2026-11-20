using System.Net.Mail;
using System.Text.RegularExpressions;

namespace GoFix.Application.Modules.Identity;

public sealed partial class IdentityService(IIdentityStore store)
{
    public async Task<Dictionary<string, string[]>?> RegisterAsync(Registration input, CancellationToken cancellationToken)
    {
        var details = GetDetails(input.Nombre, input.Telefono);
        var errors = ValidateDetails(details);
        var email = input.Correo?.Trim();
        var password = input.Contrasena;
        var role = input.Rol?.Trim();

        if (email is not { Length: > 0 and <= 254 } || email.Normalize().ToUpperInvariant().Length > 254 ||
            !MailAddress.TryCreate(email, out var address) || !string.Equals(email, address.Address, StringComparison.Ordinal))
            errors.Add("correo", ["Introduce un correo válido de hasta 254 caracteres."]);
        if (password is not { Length: >= 12 and <= 128 } || !password.Any(char.IsLetter) || !password.Any(char.IsAsciiDigit))
            errors.Add("contrasena", ["Usa entre 12 y 128 caracteres, con al menos una letra y un número."]);
        if (role is not (AccountRole.Customer or AccountRole.Technician))
            errors.Add("rol", ["Selecciona cliente o técnico."]);
        if (errors.Count > 0)
            return errors;

        // La contraseña se conserva exactamente como la ingresó la persona.
        var normalized = new Registration(details.Nombre, email, details.Telefono, password, role);
        return await store.RegisterAsync(normalized, cancellationToken)
            ? null
            : new() { ["correo"] = ["Este correo ya está registrado."] };
    }

    public Task<Session?> LoginAsync(Credentials input, CancellationToken cancellationToken)
    {
        var email = input.Correo?.Trim();
        if (email is not { Length: > 0 and <= 254 } || input.Contrasena is not { Length: > 0 and <= 128 })
            return Task.FromResult<Session?>(null);

        return store.AuthenticateAsync(new(email, input.Contrasena), cancellationToken);
    }

    public Task<Account?> GetAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        store.FindAccountAsync(userId, cancellationToken);

    public async Task<AccountChange> UpdateAccountAsync(Guid userId, AccountDetails input, CancellationToken cancellationToken)
    {
        var details = GetDetails(input.Nombre, input.Telefono);
        var errors = ValidateDetails(details);
        return errors.Count > 0
            ? new(null, errors)
            : new(await store.UpdateAsync(userId, details, cancellationToken));
    }

    private static AccountDetails GetDetails(string? name, string? phone) => new(name?.Trim(), phone?.Trim());

    private static Dictionary<string, string[]> ValidateDetails(AccountDetails details)
    {
        var errors = new Dictionary<string, string[]>();
        if (details.Nombre is not { Length: >= 2 and <= 100 } name ||
            name.Any(c => c is '<' or '>' || char.IsControl(c)))
            errors.Add("nombre", ["Introduce un nombre de 2 a 100 caracteres."]);
        if (details.Telefono is not { Length: >= 10 and <= 16 } phone || !PhonePattern().IsMatch(phone))
            errors.Add("telefono", ["Introduce de 10 a 15 dígitos con prefijo + opcional."]);
        return errors;
    }

    [GeneratedRegex(@"\A\+?[0-9]{10,15}\z")]
    private static partial Regex PhonePattern();
}
