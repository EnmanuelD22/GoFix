using GoFix.Domain.Modules.Professionals;
using GoFix.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Professionals;

internal sealed class PerfilTecnicoConfiguration : IEntityTypeConfiguration<PerfilTecnico>
{
    public void Configure(EntityTypeBuilder<PerfilTecnico> builder)
    {
        builder.ToTable("PerfilTecnico", "professionals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Descripcion).IsRequired().HasMaxLength(1000);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasOne<GoFixUser>().WithOne().HasForeignKey<PerfilTecnico>(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
