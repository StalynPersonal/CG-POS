using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class OutboxYAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoEntidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AutorizadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMensajes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoMensaje = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AgregadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashContenido = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ProximoIntentoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    EnviadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ConfirmadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMensajes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Accion",
                table: "Auditoria",
                column: "Accion");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_OcurridoEn",
                table: "Auditoria",
                column: "OcurridoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_TipoEntidad_EntidadId",
                table: "Auditoria",
                columns: new[] { "TipoEntidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajes_AgregadoId",
                table: "OutboxMensajes",
                column: "AgregadoId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMensajes_Estado_ProximoIntentoEn",
                table: "OutboxMensajes",
                columns: new[] { "Estado", "ProximoIntentoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "OutboxMensajes");
        }
    }
}
