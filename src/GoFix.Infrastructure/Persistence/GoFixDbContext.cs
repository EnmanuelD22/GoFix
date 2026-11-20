using GoFix.Domain.Modules.Professionals;
using GoFix.Infrastructure.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Persistence;

/// <summary>Contexto relacional compartido por los módulos del monolito.</summary>
public sealed class GoFixDbContext(DbContextOptions<GoFixDbContext> options)
    : IdentityDbContext<GoFixUser, IdentityRole<Guid>, Guid>(options)
{
    internal DbSet<IdentitySession> Sessions => Set<IdentitySession>();
    public DbSet<PerfilTecnico> PerfilesTecnicos => Set<PerfilTecnico>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(GoFixDbContext).Assembly);
    }
}
