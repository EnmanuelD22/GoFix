using GoFix.Domain.Modules.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Services;

internal sealed class IdempotenciaSolicitudConfiguration : IEntityTypeConfiguration<IdempotenciaSolicitud>
{
    public void Configure(EntityTypeBuilder<IdempotenciaSolicitud> builder)
    {
        builder.ToTable("IdempotenciaSolicitud", "services");
        builder.HasKey(x => new { x.ClienteId, x.Clave });
        builder.Property(x => x.HashContenido).HasColumnType("binary(32)").IsRequired();
        builder.HasIndex(x => x.SolicitudId).IsUnique();
        // Una clave de idempotencia solo puede apuntar a una solicitud del cliente que la emitió.
        builder.HasOne<SolicitudServicio>().WithMany()
            .HasForeignKey(x => new { x.SolicitudId, x.ClienteId })
            .HasPrincipalKey(x => new { x.Id, x.ClienteId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
