using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CierresDeTurno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresTurno",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnoNumero = table.Column<long>(type: "bigint", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    FechaOperacion = table.Column<DateOnly>(type: "date", nullable: false),
                    AbiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Ciego = table.Column<bool>(type: "bit", nullable: false),
                    FondoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FondoEnCuadre = table.Column<bool>(type: "bit", nullable: false),
                    CantidadVentas = table.Column<int>(type: "int", nullable: false),
                    TotalVentas = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRetiros = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalEsperado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDeclarado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CerradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    ReabiertoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReabiertoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReabiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MotivoReapertura = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurno_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioAnteriorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AutorizadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CierresTurnoDenominaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CierreTurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DenominacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Importe = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurnoDenominaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurnoDenominaciones_CierresTurno_CierreTurnoId",
                        column: x => x.CierreTurnoId,
                        principalTable: "CierresTurno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CierresTurnoFormasPago",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CierreTurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormaPagoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Transacciones = table.Column<int>(type: "int", nullable: false),
                    Esperado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Declarado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurnoFormasPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurnoFormasPago_CierresTurno_CierreTurnoId",
                        column: x => x.CierreTurnoId,
                        principalTable: "CierresTurno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_CajaId_CerradoEn",
                table: "CierresTurno",
                columns: new[] { "CajaId", "CerradoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_TurnoId_Numero",
                table: "CierresTurno",
                columns: new[] { "TurnoId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurnoDenominaciones_CierreTurnoId",
                table: "CierresTurnoDenominaciones",
                column: "CierreTurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurnoFormasPago_CierreTurnoId",
                table: "CierresTurnoFormasPago",
                column: "CierreTurnoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_TurnoId_Tipo_Numero",
                table: "MovimientosCaja",
                columns: new[] { "TurnoId", "Tipo", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresTurnoDenominaciones");

            migrationBuilder.DropTable(
                name: "CierresTurnoFormasPago");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "CierresTurno");
        }
    }
}
