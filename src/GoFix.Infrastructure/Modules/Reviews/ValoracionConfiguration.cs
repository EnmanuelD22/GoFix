using GoFix.Domain.Modules.Reviews;
using GoFix.Domain.Modules.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Reviews;

internal sealed class ValoracionConfiguration : IEntityTypeConfiguration<Valoracion>
{
    public void Configure(EntityTypeBuilder<Valoracion> builder)
    {
        builder.ToTable("Valoracion", "reviews", table =>
            table.HasCheckConstraint("CK_Valoracion_Puntuacion", "[Puntuacion] BETWEEN 1 AND 5"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Puntuacion).HasColumnType("tinyint");
        builder.Property(x => x.Comentario).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2(7)");
        builder.HasIndex(x => x.SolicitudId).IsUnique();
        builder.HasIndex(x => new { x.TecnicoId, x.CreatedAtUtc, x.Id }).IsDescending(false, true, true);
        // La referencia conjunta impide que la opinión se atribuya a participantes de otra solicitud.
        builder.HasOne<SolicitudServicio>().WithMany()
            .HasForeignKey(x => new { x.SolicitudId, x.ClienteId, x.TecnicoId })
            .HasPrincipalKey(x => new { x.Id, x.ClienteId, x.TecnicoId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
