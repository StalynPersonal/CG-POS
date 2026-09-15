using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DescuentosYPromociones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DescuentoFacturaAutorizadoPorId",
                table: "Ventas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescuentoFacturaAutorizadoPorNombre",
                table: "Ventas",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescuentoFacturaLineas",
                table: "Ventas",
                type: "varchar(2000)",
                unicode: false,
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DescuentoFacturaTipo",
                table: "Ventas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoFacturaValor",
                table: "Ventas",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDescuentoFactura",
                table: "Ventas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DescuentoAutorizadoPorId",
                table: "LineasVenta",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescuentoAutorizadoPorNombre",
                table: "LineasVenta",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoFactura",
                table: "LineasVenta",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoManual",
                table: "LineasVenta",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DescuentoManualTipo",
                table: "LineasVenta",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoManualValor",
                table: "LineasVenta",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DescuentoPromocion",
                table: "LineasVenta",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MotivoDescuento",
                table: "LineasVenta",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromocionCodigo",
                table: "LineasVenta",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PromocionDesactivada",
                table: "LineasVenta",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PromocionDescripcion",
                table: "LineasVenta",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromocionId",
                table: "LineasVenta",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromocionNombre",
                table: "LineasVenta",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MotivosDescuento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotivosDescuento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promociones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CantidadLleva = table.Column<int>(type: "int", nullable: true),
                    CantidadPaga = table.Column<int>(type: "int", nullable: true),
                    CantidadMinima = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    LimitePorCliente = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VigenteDesde = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    VigenteHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Dias = table.Column<int>(type: "int", nullable: false),
                    HoraDesde = table.Column<TimeOnly>(type: "time", nullable: true),
                    HoraHasta = table.Column<TimeOnly>(type: "time", nullable: true),
                    SoloFidelidad = table.Column<bool>(type: "bit", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    Articulos = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Familias = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    Sucursales = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promociones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TopesDescuento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    FamiliaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PorcentajeMaximo = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    MontoMaximo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopesDescuento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineasVenta_PromocionId",
                table: "LineasVenta",
                column: "PromocionId");

            migrationBuilder.CreateIndex(
                name: "IX_MotivosDescuento_Codigo",
                table: "MotivosDescuento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promociones_Activa_VigenteHasta",
                table: "Promociones",
                columns: new[] { "Activa", "VigenteHasta" });

            migrationBuilder.CreateIndex(
                name: "IX_Promociones_Codigo",
                table: "Promociones",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopesDescuento_Nivel_FamiliaId_ArticuloId",
                table: "TopesDescuento",
                columns: new[] { "Nivel", "FamiliaId", "ArticuloId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MotivosDescuento");

            migrationBuilder.DropTable(
                name: "Promociones");

            migrationBuilder.DropTable(
                name: "TopesDescuento");

            migrationBuilder.DropIndex(
                name: "IX_LineasVenta_PromocionId",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoFacturaAutorizadoPorId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoFacturaAutorizadoPorNombre",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoFacturaLineas",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoFacturaTipo",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoFacturaValor",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "MotivoDescuentoFactura",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "DescuentoAutorizadoPorId",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoAutorizadoPorNombre",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoFactura",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoManual",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoManualTipo",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoManualValor",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "DescuentoPromocion",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "MotivoDescuento",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "PromocionCodigo",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "PromocionDesactivada",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "PromocionDescripcion",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "PromocionId",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "PromocionNombre",
                table: "LineasVenta");
        }
    }
}
