using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class NotasCreditoCentrales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumosNotaCredito",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaCreditoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaNumero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    RegistradoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumosNotaCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumosNotaCredito_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotasCredito",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Encf = table.Column<string>(type: "char(13)", unicode: false, fixedLength: true, maxLength: 13, nullable: true),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteDocumento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClienteNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Moneda = table.Column<string>(type: "char(3)", unicode: false, fixedLength: true, maxLength: 3, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Consumido = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VenceEn = table.Column<DateOnly>(type: "date", nullable: false),
                    EmitidaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    RegistradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ProrrogadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ProrrogadaPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MotivoProrroga = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotasCredito_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReservasNotaCredito",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotaCreditoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VenceEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    CerradaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    Cierre = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasNotaCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasNotaCredito_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasNotaCredito_NotasCredito_NotaCreditoId",
                        column: x => x.NotaCreditoId,
                        principalTable: "NotasCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosNotaCredito_CajaId",
                table: "ConsumosNotaCredito",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumosNotaCredito_NotaCreditoId_VentaId",
                table: "ConsumosNotaCredito",
                columns: new[] { "NotaCreditoId", "VentaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_CajaId",
                table: "NotasCredito",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_ClienteDocumento",
                table: "NotasCredito",
                column: "ClienteDocumento");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_Encf",
                table: "NotasCredito",
                column: "Encf",
                unique: true,
                filter: "[Encf] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_Numero",
                table: "NotasCredito",
                column: "Numero");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_SucursalId",
                table: "NotasCredito",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCredito_VenceEn",
                table: "NotasCredito",
                column: "VenceEn");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasNotaCredito_CajaId",
                table: "ReservasNotaCredito",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasNotaCredito_NotaCreditoId_CerradaEn_VenceEn",
                table: "ReservasNotaCredito",
                columns: new[] { "NotaCreditoId", "CerradaEn", "VenceEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumosNotaCredito");

            migrationBuilder.DropTable(
                name: "ReservasNotaCredito");

            migrationBuilder.DropTable(
                name: "NotasCredito");
        }
    }
}
