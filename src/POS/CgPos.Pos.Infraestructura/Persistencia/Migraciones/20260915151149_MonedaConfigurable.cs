using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class MonedaConfigurable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "Ventas",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SimboloMoneda",
                table: "Ventas",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Moneda",
                table: "MovimientosCaja",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(3)",
                oldUnicode: false,
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "Devoluciones",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SimboloMoneda",
                table: "Devoluciones",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "CierresTurno",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Monedas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Simbolo = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Monedas", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Monedas_Codigo",
                table: "Monedas",
                column: "Codigo",
                unique: true);

            // Dato histórico: antes de esta versión la caja solo operaba en pesos dominicanos. Los documentos nuevos toman
            // la moneda del parámetro General.MonedaLocal.
            migrationBuilder.Sql("UPDATE Ventas SET Moneda = 'DOP', SimboloMoneda = N'RD$' WHERE Moneda = '';");
            migrationBuilder.Sql("UPDATE Devoluciones SET Moneda = 'DOP', SimboloMoneda = N'RD$' WHERE Moneda = '';");
            migrationBuilder.Sql("UPDATE CierresTurno SET Moneda = 'DOP' WHERE Moneda = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Monedas");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SimboloMoneda",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "SimboloMoneda",
                table: "Devoluciones");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "CierresTurno");

            migrationBuilder.AlterColumn<string>(
                name: "Moneda",
                table: "MovimientosCaja",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(3)",
                oldUnicode: false,
                oldMaxLength: 3,
                oldNullable: true);
        }
    }
}
