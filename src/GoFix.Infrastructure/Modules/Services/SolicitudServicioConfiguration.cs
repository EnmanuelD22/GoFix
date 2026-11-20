using GoFix.Domain.Modules.Professionals;
using GoFix.Domain.Modules.Services;
using GoFix.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Services;

internal sealed class SolicitudServicioConfiguration : IEntityTypeConfiguration<SolicitudServicio>
{
    public void Configure(EntityTypeBuilder<SolicitudServicio> builder)
    {
        builder.ToTable("SolicitudServicio", "services", table =>
        {
            table.HasCheckConstraint("CK_SolicitudServicio_Estado", "[Estado] BETWEEN 1 AND 6");
            table.HasCheckConstraint("CK_SolicitudServicio_Borrado",
                "([IsDeleted] = 0 AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [Estado] = 1 AND [DeletedAtUtc] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        // El cliente y el técnico asignados son inmutables y forman parte de las referencias históricas.
        builder.HasAlternateKey(x => new { x.Id, x.ClienteId });
        builder.HasAlternateKey(x => new { x.Id, x.ClienteId, x.TecnicoId });
        builder.Property(x => x.Titulo).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Descripcion).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Direccion).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Estado).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(x => x.FechaPreferida).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.FechaConfirmada).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(x => x.AceptadaAtUtc).HasColumnType("datetime2(7)");
        builder.Property(x => x.InicioAtUtc).HasColumnType("datetime2(7)");
        builder.Property(x => x.FinAtUtc).HasColumnType("datetime2(7)");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<GoFixUser>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PerfilTecnico>().WithMany().HasForeignKey(x => x.TecnicoId).OnDelete(DeleteBehavior.Restrict);
        // Referenciar el catálogo permite editar la cobertura actual sin romper solicitudes históricas.
        builder.HasOne<Especialidad>().WithMany().HasForeignKey(x => x.EspecialidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sector>().WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ClienteId, x.CreatedAtUtc, x.Id }).IsDescending(false, true, true);
        builder.HasIndex(x => new { x.TecnicoId, x.Estado, x.CreatedAtUtc, x.Id }).IsDescending(false, false, true, true);
    }
}
