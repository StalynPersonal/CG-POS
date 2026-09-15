using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class RecepcionSincronizacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConflictosSincronizacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MensajeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoMensaje = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DetectadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Ocurrencias = table.Column<int>(type: "int", nullable: false),
                    UltimaOcurrenciaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ResueltoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ResueltoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Resolucion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConflictosSincronizacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConflictosSincronizacion_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosRecibidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoMensaje = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AgregadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashContenido = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CreadoEnCaja = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    RecibidoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Reenvios = table.Column<int>(type: "int", nullable: false),
                    UltimoReenvioEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosRecibidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosRecibidos_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentosRecibidos_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EstadosSincronizacionCaja",
                columns: table => new
                {
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UltimaRecepcionEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MensajesRecibidos = table.Column<long>(type: "bigint", nullable: false),
                    Duplicados = table.Column<long>(type: "bigint", nullable: false),
                    Rechazados = table.Column<long>(type: "bigint", nullable: false),
                    UltimoRechazoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadosSincronizacionCaja", x => x.CajaId);
                    table.ForeignKey(
                        name: "FK_EstadosSincronizacionCaja_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComprobantesRecibidos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgregadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Encf = table.Column<string>(type: "char(13)", unicode: false, fixedLength: true, maxLength: 13, nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    XmlFirmado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashXml = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    FechaFirma = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    RecibidoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    EstadoDgii = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EstadoDgiiEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    MensajeDgii = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprobantesRecibidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprobantesRecibidos_DocumentosRecibidos_DocumentoId",
                        column: x => x.DocumentoId,
                        principalTable: "DocumentosRecibidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprobantesRecibidos_DocumentoId",
                table: "ComprobantesRecibidos",
                column: "DocumentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprobantesRecibidos_Encf",
                table: "ComprobantesRecibidos",
                column: "Encf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComprobantesRecibidos_EstadoDgii_RecibidoEn",
                table: "ComprobantesRecibidos",
                columns: new[] { "EstadoDgii", "RecibidoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictosSincronizacion_CajaId_ResueltoEn",
                table: "ConflictosSincronizacion",
                columns: new[] { "CajaId", "ResueltoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_ConflictosSincronizacion_MensajeId_CajaId_Tipo",
                table: "ConflictosSincronizacion",
                columns: new[] { "MensajeId", "CajaId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosRecibidos_AgregadoId",
                table: "DocumentosRecibidos",
                column: "AgregadoId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosRecibidos_CajaId_RecibidoEn",
                table: "DocumentosRecibidos",
                columns: new[] { "CajaId", "RecibidoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosRecibidos_SucursalId",
                table: "DocumentosRecibidos",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosRecibidos_TipoMensaje_RecibidoEn",
                table: "DocumentosRecibidos",
                columns: new[] { "TipoMensaje", "RecibidoEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComprobantesRecibidos");

            migrationBuilder.DropTable(
                name: "ConflictosSincronizacion");

            migrationBuilder.DropTable(
                name: "EstadosSincronizacionCaja");

            migrationBuilder.DropTable(
                name: "DocumentosRecibidos");
        }
    }
}
