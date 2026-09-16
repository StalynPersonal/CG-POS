using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ReportesCentral : Migration
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
                    TurnoNumero = table.Column<long>(type: "bigint", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaOperacion = table.Column<DateOnly>(type: "date", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Ciego = table.Column<bool>(type: "bit", nullable: false),
                    FondoInicial = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CantidadVentas = table.Column<int>(type: "int", nullable: false),
                    TotalVentas = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalRetiros = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalEsperado = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalDeclarado = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AbiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CerradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ReabiertoPorNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReabiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MotivoReapertura = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresTurno", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresTurno_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CierresTurno_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VentasCentral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    FechaOperacion = table.Column<DateOnly>(type: "date", nullable: false),
                    TipoComprobanteFiscal = table.Column<int>(type: "int", nullable: false),
                    Encf = table.Column<string>(type: "char(13)", unicode: false, fixedLength: true, maxLength: 13, nullable: true),
                    EncfModificado = table.Column<string>(type: "char(13)", unicode: false, fixedLength: true, maxLength: 13, nullable: true),
                    ClienteTipoDocumento = table.Column<int>(type: "int", nullable: true),
                    ClienteDocumento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Descuento = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Impuesto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ImpuestoRetenido = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CantidadLineas = table.Column<int>(type: "int", nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentasCentral", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VentasCentral_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VentasCentral_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CierresFormaPago",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CierreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Transacciones = table.Column<int>(type: "int", nullable: false),
                    Esperado = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Declarado = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Diferencia = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresFormaPago", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CierresFormaPago_CierresTurno_CierreId",
                        column: x => x.CierreId,
                        principalTable: "CierresTurno",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImpuestosVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Base = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Impuesto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpuestosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImpuestosVenta_VentasCentral_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "VentasCentral",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagosVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComprobanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    FormaPagoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosVenta_VentasCentral_ComprobanteId",
                        column: x => x.ComprobanteId,
                        principalTable: "VentasCentral",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CierresFormaPago_CierreId",
                table: "CierresFormaPago",
                column: "CierreId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_CajaId",
                table: "CierresTurno",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_FechaOperacion_SucursalId_CajaId",
                table: "CierresTurno",
                columns: new[] { "FechaOperacion", "SucursalId", "CajaId" });

            migrationBuilder.CreateIndex(
                name: "IX_CierresTurno_SucursalId",
                table: "CierresTurno",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpuestosVenta_ComprobanteId",
                table: "ImpuestosVenta",
                column: "ComprobanteId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosVenta_ComprobanteId",
                table: "PagosVenta",
                column: "ComprobanteId");

            migrationBuilder.CreateIndex(
                name: "IX_VentasCentral_CajaId",
                table: "VentasCentral",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_VentasCentral_Encf",
                table: "VentasCentral",
                column: "Encf");

            migrationBuilder.CreateIndex(
                name: "IX_VentasCentral_FechaOperacion_SucursalId_CajaId",
                table: "VentasCentral",
                columns: new[] { "FechaOperacion", "SucursalId", "CajaId" });

            migrationBuilder.CreateIndex(
                name: "IX_VentasCentral_SucursalId",
                table: "VentasCentral",
                column: "SucursalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CierresFormaPago");

            migrationBuilder.DropTable(
                name: "ImpuestosVenta");

            migrationBuilder.DropTable(
                name: "PagosVenta");

            migrationBuilder.DropTable(
                name: "CierresTurno");

            migrationBuilder.DropTable(
                name: "VentasCentral");
        }
    }
}
