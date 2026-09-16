using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ResumenConsumoDgii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsResumenConsumo",
                table: "ComprobantesRecibidos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EsResumenConsumo",
                table: "ComprobantesRecibidos");
        }
    }
}
