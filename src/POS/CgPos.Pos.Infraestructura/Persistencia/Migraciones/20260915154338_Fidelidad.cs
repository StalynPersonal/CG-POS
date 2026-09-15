using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Fidelidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FidelidadCedula",
                table: "Ventas",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FidelidadMiembroId",
                table: "Ventas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FidelidadNivel",
                table: "Ventas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FidelidadNombre",
                table: "Ventas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PuntosAcumulados",
                table: "Ventas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntosCanjeados",
                table: "Ventas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PuntosReversados",
                table: "Devoluciones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MiembrosFidelidad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cedula = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NivelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SaldoSincronizado = table.Column<int>(type: "int", nullable: false),
                    SaldoSincronizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    PuntosPorVencer = table.Column<int>(type: "int", nullable: false),
                    ProximoVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    InscritoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    InscritoEnCaja = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiembrosFidelidad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NivelesFidelidad",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    FactorAcumulacion = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelesFidelidad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReglasAcumulacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DiaSemana = table.Column<int>(type: "int", nullable: true),
                    MontoBase = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Puntos = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    VigenteHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglasAcumulacion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosPuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MiembroId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cedula = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Puntos = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DevolucionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Documento = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VenceEn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosPuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosPuntos_MiembrosFidelidad_MiembroId",
                        column: x => x.MiembroId,
                        principalTable: "MiembrosFidelidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MiembrosFidelidad_Cedula",
                table: "MiembrosFidelidad",
                column: "Cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_MiembroId_Fecha",
                table: "MovimientosPuntos",
                columns: new[] { "MiembroId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_VentaId",
                table: "MovimientosPuntos",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_NivelesFidelidad_Codigo",
                table: "NivelesFidelidad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReglasAcumulacion_Codigo",
                table: "ReglasAcumulacion",
                column: "Codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosPuntos");

            migrationBuilder.DropTable(
                name: "NivelesFidelidad");

            migrationBuilder.DropTable(
                name: "ReglasAcumulacion");

            migrationBuilder.DropTable(
                name: "MiembrosFidelidad");

            migrationBuilder.DropColumn(
                name: "FidelidadCedula",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FidelidadMiembroId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FidelidadNivel",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "FidelidadNombre",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "PuntosAcumulados",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "PuntosCanjeados",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "PuntosReversados",
                table: "Devoluciones");
        }
    }
}
