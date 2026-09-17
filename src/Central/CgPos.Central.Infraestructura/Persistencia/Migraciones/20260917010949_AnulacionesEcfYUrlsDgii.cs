using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AnulacionesEcfYUrlsDgii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnulacionesEcf",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SecuenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoComprobante = table.Column<int>(type: "int", nullable: false),
                    Desde = table.Column<long>(type: "bigint", nullable: false),
                    Hasta = table.Column<long>(type: "bigint", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SolicitadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    RespuestaDgii = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    XmlFirmado = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnulacionesEcf", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnulacionesEcf_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnulacionesEcf_CajaId_TipoComprobante",
                table: "AnulacionesEcf",
                columns: new[] { "CajaId", "TipoComprobante" });

            // Las direcciones base de la DGII se reemplazan por una dirección completa por servicio (Central.Dgii.Url*), que se configuran aparte.
            migrationBuilder.Sql("DELETE FROM Parametros WHERE Clave IN ('Central.Dgii.UrlBase', 'Central.Dgii.UrlBaseConsumo');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnulacionesEcf");
        }
    }
}
