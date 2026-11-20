using Microsoft.AspNetCore.Identity;

namespace GoFix.Infrastructure.Modules.Identity;

public sealed class GoFixUser : IdentityUser<Guid>
{
    public string Nombre { get; set; } = "";
}

internal sealed class IdentitySession
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}
