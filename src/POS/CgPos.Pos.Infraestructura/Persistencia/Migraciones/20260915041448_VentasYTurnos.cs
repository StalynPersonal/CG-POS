using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class VentasYTurnos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutorizacionesOtorgadas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permiso = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SolicitanteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupervisorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConcedidaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    UsadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UsadaEnTipoEntidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UsadaEnEntidadId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutorizacionesOtorgadas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecuenciasCaja",
                columns: table => new
                {
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Ultimo = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuenciasCaja", x => new { x.CajaId, x.Tipo });
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<long>(type: "bigint", nullable: false),
                    FechaOperacion = table.Column<DateOnly>(type: "date", nullable: false),
                    UsuarioAperturaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioAperturaNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    UsuarioActualId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioActualNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FondoInicial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    AbiertoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CerradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turnos_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroTransaccion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Secuencia = table.Column<long>(type: "bigint", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    IniciadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    AnuladaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AnuladaPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AnuladaPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LineasVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoLeido = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TipoArticulo = table.Column<int>(type: "int", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermiteDescuentoManual = table.Column<bool>(type: "bit", nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PermiteDecimales = table.Column<bool>(type: "bit", nullable: false),
                    DecimalesCantidad = table.Column<int>(type: "int", nullable: false),
                    ImpuestoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PorcentajeImpuesto = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IndicadorFacturacion = table.Column<int>(type: "int", nullable: false),
                    PrecioDetalle = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioMayor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CantidadMinimaMayor = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PrecioMinimo = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Cantidad = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Lista = table.Column<int>(type: "int", nullable: false),
                    MotivoPrecio = table.Column<int>(type: "int", nullable: false),
                    ImporteEtiqueta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LeidaDeBalanza = table.Column<bool>(type: "bit", nullable: false),
                    EsReverso = table.Column<bool>(type: "bit", nullable: false),
                    LineaAnuladaNumero = table.Column<int>(type: "int", nullable: true),
                    Anulada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutorizacionesOtorgadas_SolicitanteId_ConcedidaEn",
                table: "AutorizacionesOtorgadas",
                columns: new[] { "SolicitanteId", "ConcedidaEn" });

            migrationBuilder.CreateIndex(
                name: "IX_LineasVenta_VentaId_NumeroLinea",
                table: "LineasVenta",
                columns: new[] { "VentaId", "NumeroLinea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_CajaId_Abierto",
                table: "Turnos",
                column: "CajaId",
                unique: true,
                filter: "[Estado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_CajaId_Numero",
                table: "Turnos",
                columns: new[] { "CajaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_CajaId_Secuencia",
                table: "Ventas",
                columns: new[] { "CajaId", "Secuencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_NumeroTransaccion",
                table: "Ventas",
                column: "NumeroTransaccion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TurnoId_UsuarioId_Estado",
                table: "Ventas",
                columns: new[] { "TurnoId", "UsuarioId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutorizacionesOtorgadas");

            migrationBuilder.DropTable(
                name: "LineasVenta");

            migrationBuilder.DropTable(
                name: "SecuenciasCaja");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "Turnos");
        }
    }
}
