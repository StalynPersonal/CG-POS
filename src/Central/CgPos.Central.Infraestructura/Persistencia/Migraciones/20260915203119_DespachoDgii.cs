using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DespachoDgii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EnviadoEn",
                table: "ComprobantesRecibidos",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IntentosEnvio",
                table: "ComprobantesRecibidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProximoIntentoEn",
                table: "ComprobantesRecibidos",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackId",
                table: "ComprobantesRecibidos",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprobantesRecibidos_EstadoDgii_ProximoIntentoEn",
                table: "ComprobantesRecibidos",
                columns: new[] { "EstadoDgii", "ProximoIntentoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ComprobantesRecibidos_EstadoDgii_ProximoIntentoEn",
                table: "ComprobantesRecibidos");

            migrationBuilder.DropColumn(
                name: "EnviadoEn",
                table: "ComprobantesRecibidos");

            migrationBuilder.DropColumn(
                name: "IntentosEnvio",
                table: "ComprobantesRecibidos");

            migrationBuilder.DropColumn(
                name: "ProximoIntentoEn",
                table: "ComprobantesRecibidos");

            migrationBuilder.DropColumn(
                name: "TrackId",
                table: "ComprobantesRecibidos");
        }
    }
}
