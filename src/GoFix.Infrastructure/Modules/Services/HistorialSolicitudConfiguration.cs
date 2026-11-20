using GoFix.Domain.Modules.Services;
using GoFix.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Services;

internal sealed class HistorialSolicitudConfiguration : IEntityTypeConfiguration<HistorialSolicitud>
{
    public void Configure(EntityTypeBuilder<HistorialSolicitud> builder)
    {
        builder.ToTable("HistorialSolicitud", "services", table =>
        {
            table.HasCheckConstraint("CK_HistorialSolicitud_TipoEvento", "[TipoEvento] BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_HistorialSolicitud_Estados",
                "([EstadoPrevio] IS NULL OR [EstadoPrevio] BETWEEN 1 AND 6) AND ([EstadoNuevo] IS NULL OR [EstadoNuevo] BETWEEN 1 AND 6)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).UseIdentityColumn();
        builder.Property(x => x.TipoEvento).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(x => x.EstadoPrevio).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(x => x.EstadoNuevo).HasConversion<byte>().HasColumnType("tinyint");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.Property(x => x.Observacion).HasMaxLength(1000);
        builder.HasOne<SolicitudServicio>().WithMany().HasForeignKey(x => x.SolicitudId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GoFixUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SolicitudId, x.CreatedAtUtc, x.Id });
    }
}
