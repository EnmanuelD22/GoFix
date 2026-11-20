using GoFix.Application.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Identity;

internal sealed class IdentityConfiguration :
    IEntityTypeConfiguration<GoFixUser>,
    IEntityTypeConfiguration<IdentityRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserRole<Guid>>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityRoleClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>,
    IEntityTypeConfiguration<IdentitySession>
{
    public void Configure(EntityTypeBuilder<GoFixUser> builder)
    {
        builder.ToTable("AspNetUsers", "identity");
        builder.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(254);
        builder.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(254);
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(16);
        builder.HasIndex(x => x.NormalizedEmail).IsUnique();
    }

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        builder.ToTable("AspNetRoles", "identity");
        // Identificadores estables para los roles fijos entregados con el esquema.
        builder.HasData(
            new IdentityRole<Guid> { Id = Guid.Parse("6ecaefd1-33cb-4cfd-87ff-4986564b35c4"), Name = AccountRole.Customer, NormalizedName = "CLIENTE", ConcurrencyStamp = "1" },
            new IdentityRole<Guid> { Id = Guid.Parse("55bc9bb2-8440-4a3a-8511-591b6cc684d6"), Name = AccountRole.Technician, NormalizedName = "TECNICO", ConcurrencyStamp = "1" });
    }

    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        builder.ToTable("AspNetUserRoles", "identity");
        // RN-01: la base también impide asignar dos roles a la misma cuenta.
        builder.HasIndex(x => x.UserId).IsUnique();
    }

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("AspNetUserClaims", "identity");
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("AspNetUserLogins", "identity");
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("AspNetRoleClaims", "identity");
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("AspNetUserTokens", "identity");

    public void Configure(EntityTypeBuilder<IdentitySession> builder)
    {
        builder.ToTable("Sesiones", "identity");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne<GoFixUser>().WithMany().HasForeignKey(x => x.UserId);
    }
}
