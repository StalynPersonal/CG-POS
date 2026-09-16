using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ReembolsoDevolucion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReembolsadaEn",
                table: "Devoluciones",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Reembolso",
                table: "Devoluciones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReembolsoDetalle",
                table: "Devoluciones",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReembolsoReferencia",
                table: "Devoluciones",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReembolsadaEn",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "Reembolso",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "ReembolsoDetalle",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "ReembolsoReferencia",
                table: "Devoluciones");
        }
    }
}
