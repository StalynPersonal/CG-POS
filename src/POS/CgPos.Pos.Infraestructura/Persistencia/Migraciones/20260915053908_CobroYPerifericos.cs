using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CobroYPerifericos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CobradaEn",
                table: "Ventas",
                type: "datetimeoffset(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CobradaPorId",
                table: "Ventas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CobradaPorNombre",
                table: "Ventas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Devuelta",
                table: "Ventas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RedondeoEfectivo",
                table: "Ventas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCobrado",
                table: "Ventas",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperacionesTerminal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Aprobacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    UltimosDigitos = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    Marca = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Mensaje = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    OperacionAnuladaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsadaEnCobro = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacionesTerminal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PagosVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    FormaPagoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FormaPagoCodigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FormaPagoNombre = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    MontoRecibido = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TasaCambio = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    MontoAplicado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    BancoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BancoNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TipoTarjetaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TipoTarjetaNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UltimosDigitos = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: true),
                    AprobacionManual = table.Column<bool>(type: "bit", nullable: false),
                    ParaConciliar = table.Column<bool>(type: "bit", nullable: false),
                    OperacionTerminalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PermiteDevuelta = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagosVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TasasCambio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Moneda = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false),
                    Tasa = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TasasCambio", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TurnoId_Estado",
                table: "Ventas",
                columns: new[] { "TurnoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesTerminal_CajaId_TurnoId_Fecha",
                table: "OperacionesTerminal",
                columns: new[] { "CajaId", "TurnoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacionesTerminal_VentaId",
                table: "OperacionesTerminal",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosVenta_OperacionTerminalId",
                table: "PagosVenta",
                column: "OperacionTerminalId");

            migrationBuilder.CreateIndex(
                name: "IX_PagosVenta_VentaId_Numero",
                table: "PagosVenta",
                columns: new[] { "VentaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TasasCambio_Moneda_VigenteDesde",
                table: "TasasCambio",
                columns: new[] { "Moneda", "VigenteDesde" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperacionesTerminal");

            migrationBuilder.DropTable(
                name: "PagosVenta");

            migrationBuilder.DropTable(
                name: "TasasCambio");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_TurnoId_Estado",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CobradaEn",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CobradaPorId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CobradaPorNombre",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "Devuelta",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "RedondeoEfectivo",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "TotalCobrado",
                table: "Ventas");
        }
    }
}
