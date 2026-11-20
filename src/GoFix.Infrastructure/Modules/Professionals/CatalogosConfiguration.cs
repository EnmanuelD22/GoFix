using GoFix.Domain.Modules.Professionals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoFix.Infrastructure.Modules.Professionals;

internal sealed class CatalogosConfiguration :
    IEntityTypeConfiguration<Especialidad>, IEntityTypeConfiguration<Sector>,
    IEntityTypeConfiguration<TecnicoEspecialidad>, IEntityTypeConfiguration<TecnicoSector>
{
    private const string Schema = "professionals";

    // Los códigos son estables, independientes de los nombres que muestra el directorio.
    private const string CodigoValido = """
        [Codigo] COLLATE Latin1_General_100_BIN2 LIKE '[a-z0-9]%'
        AND [Codigo] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%'
        AND DATALENGTH([Codigo]) = LEN([Codigo])
        """;

    public void Configure(EntityTypeBuilder<Especialidad> builder)
    {
        builder.ToTable("Especialidad", Schema, table =>
        {
            table.HasCheckConstraint("CK_Especialidad_Codigo", CodigoValido);
            table.HasCheckConstraint("CK_Especialidad_Nombre", "LEN(TRIM([Nombre])) > 0");
        });
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Codigo).IsRequired().HasMaxLength(64).IsUnicode(false);
        builder.Property(entry => entry.Nombre).IsRequired().HasMaxLength(100);
        builder.HasIndex(entry => entry.Codigo).IsUnique();
        builder.HasIndex(entry => entry.Nombre).IsUnique();
    }

    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.ToTable("Sector", Schema, table =>
        {
            table.HasCheckConstraint("CK_Sector_Codigo", CodigoValido);
            table.HasCheckConstraint("CK_Sector_NombreMunicipio", "LEN(TRIM([Nombre])) > 0 AND LEN(TRIM([Municipio])) > 0");
        });
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Codigo).IsRequired().HasMaxLength(64).IsUnicode(false);
        builder.Property(entry => entry.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(entry => entry.Municipio).IsRequired().HasMaxLength(100);
        builder.HasIndex(entry => entry.Codigo).IsUnique();
        builder.HasIndex(entry => new { entry.Municipio, entry.Nombre }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<TecnicoEspecialidad> builder)
    {
        builder.ToTable("TecnicoEspecialidad", Schema);
        builder.HasKey(entry => new { entry.PerfilTecnicoId, entry.EspecialidadId });
        builder.HasIndex(entry => new { entry.EspecialidadId, entry.PerfilTecnicoId });
        builder.HasOne<PerfilTecnico>().WithMany().HasForeignKey(entry => entry.PerfilTecnicoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Especialidad>().WithMany().HasForeignKey(entry => entry.EspecialidadId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TecnicoSector> builder)
    {
        builder.ToTable("TecnicoSector", Schema);
        builder.HasKey(entry => new { entry.PerfilTecnicoId, entry.SectorId });
        builder.HasIndex(entry => new { entry.SectorId, entry.PerfilTecnicoId });
        builder.HasOne<PerfilTecnico>().WithMany().HasForeignKey(entry => entry.PerfilTecnicoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sector>().WithMany().HasForeignKey(entry => entry.SectorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
