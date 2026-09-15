using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class TextoBusquedaMaestros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TextoBusqueda",
                table: "MaestrosCentral",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TextoBusqueda",
                table: "MaestrosCentral");
        }
    }
}
