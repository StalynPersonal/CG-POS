using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class NumeroDocumentoUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VentasCentral_Tipo_Numero",
                table: "VentasCentral",
                columns: new[] { "Tipo", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VentasCentral_Tipo_Numero",
                table: "VentasCentral");
        }
    }
}
