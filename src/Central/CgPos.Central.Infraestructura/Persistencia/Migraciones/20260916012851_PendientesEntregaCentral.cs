using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PendientesEntregaCentral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendientesEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaNumero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    AlmacenNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ClienteDocumento = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FechaComprometida = table.Column<DateOnly>(type: "date", nullable: true),
                    Unidades = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnidadesEntregadas = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    RecibidoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TextoBusqueda = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendientesEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendientesEntrega_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendientesEntrega_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_CajaId",
                table: "PendientesEntrega",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_Estado_FechaComprometida",
                table: "PendientesEntrega",
                columns: new[] { "Estado", "FechaComprometida" });

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_Numero",
                table: "PendientesEntrega",
                column: "Numero");

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_SucursalId",
                table: "PendientesEntrega",
                column: "SucursalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendientesEntrega");
        }
    }
}
