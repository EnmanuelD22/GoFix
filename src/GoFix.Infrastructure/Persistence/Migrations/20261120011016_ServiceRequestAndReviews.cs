using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoFix.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceRequestAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "services");

            migrationBuilder.EnsureSchema(
                name: "reviews");

            migrationBuilder.CreateTable(
                name: "SolicitudServicio",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EspecialidadId = table.Column<int>(type: "int", nullable: false),
                    SectorId = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FechaPreferida = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    FechaConfirmada = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: true),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    AceptadaAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    InicioAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FinAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudServicio", x => x.Id);
                    table.UniqueConstraint("AK_SolicitudServicio_Id_ClienteId", x => new { x.Id, x.ClienteId });
                    table.UniqueConstraint("AK_SolicitudServicio_Id_ClienteId_TecnicoId", x => new { x.Id, x.ClienteId, x.TecnicoId });
                    table.CheckConstraint("CK_SolicitudServicio_Borrado", "([IsDeleted] = 0 AND [DeletedAtUtc] IS NULL) OR ([IsDeleted] = 1 AND [Estado] = 1 AND [DeletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_SolicitudServicio_Estado", "[Estado] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_SolicitudServicio_AspNetUsers_ClienteId",
                        column: x => x.ClienteId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudServicio_Especialidad_EspecialidadId",
                        column: x => x.EspecialidadId,
                        principalSchema: "professionals",
                        principalTable: "Especialidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudServicio_PerfilTecnico_TecnicoId",
                        column: x => x.TecnicoId,
                        principalSchema: "professionals",
                        principalTable: "PerfilTecnico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SolicitudServicio_Sector_SectorId",
                        column: x => x.SectorId,
                        principalSchema: "professionals",
                        principalTable: "Sector",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HistorialSolicitud",
                schema: "services",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoEvento = table.Column<byte>(type: "tinyint", nullable: false),
                    EstadoPrevio = table.Column<byte>(type: "tinyint", nullable: true),
                    EstadoNuevo = table.Column<byte>(type: "tinyint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialSolicitud", x => x.Id);
                    table.CheckConstraint("CK_HistorialSolicitud_Estados", "([EstadoPrevio] IS NULL OR [EstadoPrevio] BETWEEN 1 AND 6) AND ([EstadoNuevo] IS NULL OR [EstadoNuevo] BETWEEN 1 AND 6)");
                    table.CheckConstraint("CK_HistorialSolicitud_TipoEvento", "[TipoEvento] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_HistorialSolicitud_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "identity",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistorialSolicitud_SolicitudServicio_SolicitudId",
                        column: x => x.SolicitudId,
                        principalSchema: "services",
                        principalTable: "SolicitudServicio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotenciaSolicitud",
                schema: "services",
                columns: table => new
                {
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Clave = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HashContenido = table.Column<byte[]>(type: "binary(32)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotenciaSolicitud", x => new { x.ClienteId, x.Clave });
                    table.ForeignKey(
                        name: "FK_IdempotenciaSolicitud_SolicitudServicio_SolicitudId_ClienteId",
                        columns: x => new { x.SolicitudId, x.ClienteId },
                        principalSchema: "services",
                        principalTable: "SolicitudServicio",
                        principalColumns: new[] { "Id", "ClienteId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Valoracion",
                schema: "reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitudId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Puntuacion = table.Column<byte>(type: "tinyint", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Valoracion", x => x.Id);
                    table.CheckConstraint("CK_Valoracion_Puntuacion", "[Puntuacion] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Valoracion_SolicitudServicio_SolicitudId_ClienteId_TecnicoId",
                        columns: x => new { x.SolicitudId, x.ClienteId, x.TecnicoId },
                        principalSchema: "services",
                        principalTable: "SolicitudServicio",
                        principalColumns: new[] { "Id", "ClienteId", "TecnicoId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistorialSolicitud_ActorId",
                schema: "services",
                table: "HistorialSolicitud",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_HistorialSolicitud_SolicitudId_CreatedAtUtc_Id",
                schema: "services",
                table: "HistorialSolicitud",
                columns: new[] { "SolicitudId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotenciaSolicitud_SolicitudId",
                schema: "services",
                table: "IdempotenciaSolicitud",
                column: "SolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotenciaSolicitud_SolicitudId_ClienteId",
                schema: "services",
                table: "IdempotenciaSolicitud",
                columns: new[] { "SolicitudId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudServicio_ClienteId_CreatedAtUtc_Id",
                schema: "services",
                table: "SolicitudServicio",
                columns: new[] { "ClienteId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudServicio_EspecialidadId",
                schema: "services",
                table: "SolicitudServicio",
                column: "EspecialidadId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudServicio_SectorId",
                schema: "services",
                table: "SolicitudServicio",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudServicio_TecnicoId_Estado_CreatedAtUtc_Id",
                schema: "services",
                table: "SolicitudServicio",
                columns: new[] { "TecnicoId", "Estado", "CreatedAtUtc", "Id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Valoracion_SolicitudId",
                schema: "reviews",
                table: "Valoracion",
                column: "SolicitudId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Valoracion_SolicitudId_ClienteId_TecnicoId",
                schema: "reviews",
                table: "Valoracion",
                columns: new[] { "SolicitudId", "ClienteId", "TecnicoId" });

            migrationBuilder.CreateIndex(
                name: "IX_Valoracion_TecnicoId_CreatedAtUtc_Id",
                schema: "reviews",
                table: "Valoracion",
                columns: new[] { "TecnicoId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialSolicitud",
                schema: "services");

            migrationBuilder.DropTable(
                name: "IdempotenciaSolicitud",
                schema: "services");

            migrationBuilder.DropTable(
                name: "Valoracion",
                schema: "reviews");

            migrationBuilder.DropTable(
                name: "SolicitudServicio",
                schema: "services");
        }
    }
}
