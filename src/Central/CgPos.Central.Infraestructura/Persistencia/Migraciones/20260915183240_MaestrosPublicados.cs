using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MaestrosPublicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Sucursales",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Parametros",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UltimaDescargaEn",
                table: "EstadosSincronizacionCaja",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VersionMaestrosConfirmada",
                table: "EstadosSincronizacionCaja",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "VersionMaestrosEntregada",
                table: "EstadosSincronizacionCaja",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Empresas",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                table: "Cajas",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "MaestrosCentral",
                columns: table => new
                {
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ModificadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaestrosCentral", x => new { x.Tipo, x.Id });
                    table.ForeignKey(
                        name: "FK_MaestrosCentral_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaestrosCentral_CajaId",
                table: "MaestrosCentral",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_MaestrosCentral_Tipo_Codigo",
                table: "MaestrosCentral",
                columns: new[] { "Tipo", "Codigo" },
                unique: true,
                filter: "[Codigo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaestrosCentral_Version",
                table: "MaestrosCentral",
                column: "Version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaestrosCentral");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Parametros");

            migrationBuilder.DropColumn(
                name: "UltimaDescargaEn",
                table: "EstadosSincronizacionCaja");

            migrationBuilder.DropColumn(
                name: "VersionMaestrosConfirmada",
                table: "EstadosSincronizacionCaja");

            migrationBuilder.DropColumn(
                name: "VersionMaestrosEntregada",
                table: "EstadosSincronizacionCaja");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Empresas");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Cajas");
        }
    }
}
