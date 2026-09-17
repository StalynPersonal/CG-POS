using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AccesoUsuarioClave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CredencialBarrasHash",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "CredencialBarrasHash",
                table: "Usuarios");

            migrationBuilder.RenameColumn(
                name: "PinHash",
                table: "Usuarios",
                newName: "ClaveHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ClaveHash",
                table: "Usuarios",
                newName: "PinHash");

            migrationBuilder.AddColumn<string>(
                name: "CredencialBarrasHash",
                table: "Usuarios",
                type: "char(64)",
                unicode: false,
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CredencialBarrasHash",
                table: "Usuarios",
                column: "CredencialBarrasHash",
                unique: true,
                filter: "[CredencialBarrasHash] IS NOT NULL");
        }
    }
}
