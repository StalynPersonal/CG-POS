using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ArticulosServicio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsServicio",
                table: "LineasVenta",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsServicio",
                table: "LineasDevolucion",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsServicio",
                table: "Articulos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsServicio",
                table: "LineasVenta");

            migrationBuilder.DropColumn(
                name: "EsServicio",
                table: "LineasDevolucion");

            migrationBuilder.DropColumn(
                name: "EsServicio",
                table: "Articulos");
        }
    }
}
