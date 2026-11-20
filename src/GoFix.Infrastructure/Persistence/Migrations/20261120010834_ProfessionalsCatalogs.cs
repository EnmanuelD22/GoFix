using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoFix.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfessionalsCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Especialidad",
                schema: "professionals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Especialidad", x => x.Id);
                    table.CheckConstraint("CK_Especialidad_Codigo", "[Codigo] COLLATE Latin1_General_100_BIN2 LIKE '[a-z0-9]%'\nAND [Codigo] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%'\nAND DATALENGTH([Codigo]) = LEN([Codigo])");
                    table.CheckConstraint("CK_Especialidad_Nombre", "LEN(TRIM([Nombre])) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Sector",
                schema: "professionals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Municipio = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sector", x => x.Id);
                    table.CheckConstraint("CK_Sector_Codigo", "[Codigo] COLLATE Latin1_General_100_BIN2 LIKE '[a-z0-9]%'\nAND [Codigo] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^a-z0-9-]%'\nAND DATALENGTH([Codigo]) = LEN([Codigo])");
                    table.CheckConstraint("CK_Sector_NombreMunicipio", "LEN(TRIM([Nombre])) > 0 AND LEN(TRIM([Municipio])) > 0");
                });

            migrationBuilder.CreateTable(
                name: "TecnicoEspecialidad",
                schema: "professionals",
                columns: table => new
                {
                    PerfilTecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspecialidadId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TecnicoEspecialidad", x => new { x.PerfilTecnicoId, x.EspecialidadId });
                    table.ForeignKey(
                        name: "FK_TecnicoEspecialidad_Especialidad_EspecialidadId",
                        column: x => x.EspecialidadId,
                        principalSchema: "professionals",
                        principalTable: "Especialidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TecnicoEspecialidad_PerfilTecnico_PerfilTecnicoId",
                        column: x => x.PerfilTecnicoId,
                        principalSchema: "professionals",
                        principalTable: "PerfilTecnico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TecnicoSector",
                schema: "professionals",
                columns: table => new
                {
                    PerfilTecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TecnicoSector", x => new { x.PerfilTecnicoId, x.SectorId });
                    table.ForeignKey(
                        name: "FK_TecnicoSector_PerfilTecnico_PerfilTecnicoId",
                        column: x => x.PerfilTecnicoId,
                        principalSchema: "professionals",
                        principalTable: "PerfilTecnico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TecnicoSector_Sector_SectorId",
                        column: x => x.SectorId,
                        principalSchema: "professionals",
                        principalTable: "Sector",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Especialidad_Codigo",
                schema: "professionals",
                table: "Especialidad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Especialidad_Nombre",
                schema: "professionals",
                table: "Especialidad",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sector_Codigo",
                schema: "professionals",
                table: "Sector",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sector_Municipio_Nombre",
                schema: "professionals",
                table: "Sector",
                columns: new[] { "Municipio", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TecnicoEspecialidad_EspecialidadId_PerfilTecnicoId",
                schema: "professionals",
                table: "TecnicoEspecialidad",
                columns: new[] { "EspecialidadId", "PerfilTecnicoId" });

            migrationBuilder.CreateIndex(
                name: "IX_TecnicoSector_SectorId_PerfilTecnicoId",
                schema: "professionals",
                table: "TecnicoSector",
                columns: new[] { "SectorId", "PerfilTecnicoId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TecnicoEspecialidad",
                schema: "professionals");

            migrationBuilder.DropTable(
                name: "TecnicoSector",
                schema: "professionals");

            migrationBuilder.DropTable(
                name: "Especialidad",
                schema: "professionals");

            migrationBuilder.DropTable(
                name: "Sector",
                schema: "professionals");
        }
    }
}
