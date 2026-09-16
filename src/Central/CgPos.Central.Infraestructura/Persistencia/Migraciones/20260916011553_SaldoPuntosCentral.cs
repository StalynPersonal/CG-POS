using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class SaldoPuntosCentral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MovimientosPuntos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MiembroId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cedula = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Origen = table.Column<int>(type: "int", nullable: false),
                    Puntos = table.Column<int>(type: "int", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DevolucionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Documento = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VenceEn = table.Column<DateOnly>(type: "date", nullable: true),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosPuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosPuntos_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosPuntos_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaldosPuntos",
                columns: table => new
                {
                    MiembroId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cedula = table.Column<string>(type: "char(11)", unicode: false, fixedLength: true, maxLength: 11, nullable: false),
                    Puntos = table.Column<int>(type: "int", nullable: false),
                    PuntosPorVencer = table.Column<int>(type: "int", nullable: false),
                    ProximoVencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Vencidos = table.Column<int>(type: "int", nullable: false),
                    CalculadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaldosPuntos", x => x.MiembroId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_CajaId",
                table: "MovimientosPuntos",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_Cedula",
                table: "MovimientosPuntos",
                column: "Cedula");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_MiembroId_Fecha",
                table: "MovimientosPuntos",
                columns: new[] { "MiembroId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosPuntos_SucursalId",
                table: "MovimientosPuntos",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_SaldosPuntos_ProximoVencimiento",
                table: "SaldosPuntos",
                column: "ProximoVencimiento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimientosPuntos");

            migrationBuilder.DropTable(
                name: "SaldosPuntos");
        }
    }
}
