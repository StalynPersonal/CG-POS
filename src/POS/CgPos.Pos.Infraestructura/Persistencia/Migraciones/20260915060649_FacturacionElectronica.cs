using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class FacturacionElectronica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentosElectronicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    Encf = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: false),
                    FechaEmision = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    FechaFirma = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CodigoSeguridad = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MontoTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    HashXml = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RutaXml = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UrlTimbre = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    EstadoActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    MensajeEstado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosElectronicos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecuenciasEcf",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<long>(type: "bigint", nullable: false),
                    Hasta = table.Column<long>(type: "bigint", nullable: false),
                    Ultimo = table.Column<long>(type: "bigint", nullable: false),
                    VenceEn = table.Column<DateOnly>(type: "date", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuenciasEcf", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistorialEstadosEcf",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoElectronicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Mensaje = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistorialEstadosEcf", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistorialEstadosEcf_DocumentosElectronicos_DocumentoElectronicoId",
                        column: x => x.DocumentoElectronicoId,
                        principalTable: "DocumentosElectronicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosElectronicos_CajaId_Estado",
                table: "DocumentosElectronicos",
                columns: new[] { "CajaId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosElectronicos_Encf",
                table: "DocumentosElectronicos",
                column: "Encf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosElectronicos_VentaId",
                table: "DocumentosElectronicos",
                column: "VentaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistorialEstadosEcf_DocumentoElectronicoId",
                table: "HistorialEstadosEcf",
                column: "DocumentoElectronicoId");

            migrationBuilder.CreateIndex(
                name: "IX_SecuenciasEcf_CajaId_TipoComprobante_Activa_Desde",
                table: "SecuenciasEcf",
                columns: new[] { "CajaId", "TipoComprobante", "Activa", "Desde" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistorialEstadosEcf");

            migrationBuilder.DropTable(
                name: "SecuenciasEcf");

            migrationBuilder.DropTable(
                name: "DocumentosElectronicos");
        }
    }
}
