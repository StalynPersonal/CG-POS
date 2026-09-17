using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ArchivosArranqueAplicados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivosArranqueAplicados",
                columns: table => new
                {
                    Clave = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Huella = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    AplicadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivosArranqueAplicados", x => x.Clave);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivosArranqueAplicados");
        }
    }
}
