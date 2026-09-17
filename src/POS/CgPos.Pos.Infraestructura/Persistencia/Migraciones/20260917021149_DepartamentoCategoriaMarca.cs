using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DepartamentoCategoriaMarca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Familia pasa a llamarse Departamento: se renombran tabla, columnas, índices y llaves para conservar los datos.
            migrationBuilder.DropForeignKey(name: "FK_Articulos_Familias_FamiliaId", table: "Articulos");
            migrationBuilder.DropIndex(name: "IX_TopesDescuento_Nivel_FamiliaId_ArticuloId", table: "TopesDescuento");

            migrationBuilder.RenameTable(name: "Familias", newName: "Departamentos");
            migrationBuilder.RenameIndex(name: "IX_Familias_Codigo", table: "Departamentos", newName: "IX_Departamentos_Codigo");
            migrationBuilder.Sql("EXEC sp_rename N'PK_Familias', N'PK_Departamentos', N'OBJECT';");

            migrationBuilder.RenameColumn(name: "FamiliaId", table: "Articulos", newName: "DepartamentoId");
            migrationBuilder.RenameIndex(name: "IX_Articulos_FamiliaId", table: "Articulos", newName: "IX_Articulos_DepartamentoId");
            migrationBuilder.RenameColumn(name: "FamiliaId", table: "LineasVenta", newName: "DepartamentoId");
            migrationBuilder.RenameColumn(name: "FamiliaId", table: "TopesDescuento", newName: "DepartamentoId");
            migrationBuilder.RenameColumn(name: "Familias", table: "Promociones", newName: "Departamentos");

            // Categoría (dentro de un departamento) y marca.
            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DepartamentoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categorias_Departamentos_DepartamentoId",
                        column: x => x.DepartamentoId,
                        principalTable: "Departamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(name: "CategoriaId", table: "Articulos", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "MarcaId", table: "Articulos", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "CategoriaId", table: "LineasVenta", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "MarcaId", table: "LineasVenta", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "CategoriaId", table: "TopesDescuento", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "MarcaId", table: "TopesDescuento", type: "uniqueidentifier", nullable: true);
            migrationBuilder.AddColumn<string>(name: "Categorias", table: "Promociones", type: "varchar(max)", unicode: false, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Marcas", table: "Promociones", type: "varchar(max)", unicode: false, nullable: false, defaultValue: "");

            migrationBuilder.CreateIndex(name: "IX_TopesDescuento_Nivel_DepartamentoId_ArticuloId", table: "TopesDescuento",
                columns: new[] { "Nivel", "DepartamentoId", "ArticuloId" });
            migrationBuilder.CreateIndex(name: "IX_Articulos_CategoriaId", table: "Articulos", column: "CategoriaId");
            migrationBuilder.CreateIndex(name: "IX_Articulos_MarcaId", table: "Articulos", column: "MarcaId");
            migrationBuilder.CreateIndex(name: "IX_Categorias_Codigo", table: "Categorias", column: "Codigo", unique: true);
            migrationBuilder.CreateIndex(name: "IX_Categorias_DepartamentoId", table: "Categorias", column: "DepartamentoId");
            migrationBuilder.CreateIndex(name: "IX_Marcas_Codigo", table: "Marcas", column: "Codigo", unique: true);

            migrationBuilder.AddForeignKey(name: "FK_Articulos_Departamentos_DepartamentoId", table: "Articulos", column: "DepartamentoId",
                principalTable: "Departamentos", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Articulos_Categorias_CategoriaId", table: "Articulos", column: "CategoriaId",
                principalTable: "Categorias", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(name: "FK_Articulos_Marcas_MarcaId", table: "Articulos", column: "MarcaId",
                principalTable: "Marcas", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Articulos_Categorias_CategoriaId", table: "Articulos");
            migrationBuilder.DropForeignKey(name: "FK_Articulos_Marcas_MarcaId", table: "Articulos");
            migrationBuilder.DropForeignKey(name: "FK_Articulos_Departamentos_DepartamentoId", table: "Articulos");
            migrationBuilder.DropIndex(name: "IX_TopesDescuento_Nivel_DepartamentoId_ArticuloId", table: "TopesDescuento");
            migrationBuilder.DropIndex(name: "IX_Articulos_CategoriaId", table: "Articulos");
            migrationBuilder.DropIndex(name: "IX_Articulos_MarcaId", table: "Articulos");

            migrationBuilder.DropTable(name: "Categorias");
            migrationBuilder.DropTable(name: "Marcas");

            migrationBuilder.DropColumn(name: "CategoriaId", table: "Articulos");
            migrationBuilder.DropColumn(name: "MarcaId", table: "Articulos");
            migrationBuilder.DropColumn(name: "CategoriaId", table: "LineasVenta");
            migrationBuilder.DropColumn(name: "MarcaId", table: "LineasVenta");
            migrationBuilder.DropColumn(name: "CategoriaId", table: "TopesDescuento");
            migrationBuilder.DropColumn(name: "MarcaId", table: "TopesDescuento");
            migrationBuilder.DropColumn(name: "Categorias", table: "Promociones");
            migrationBuilder.DropColumn(name: "Marcas", table: "Promociones");

            migrationBuilder.RenameColumn(name: "Departamentos", table: "Promociones", newName: "Familias");
            migrationBuilder.RenameColumn(name: "DepartamentoId", table: "TopesDescuento", newName: "FamiliaId");
            migrationBuilder.RenameColumn(name: "DepartamentoId", table: "LineasVenta", newName: "FamiliaId");
            migrationBuilder.RenameIndex(name: "IX_Articulos_DepartamentoId", table: "Articulos", newName: "IX_Articulos_FamiliaId");
            migrationBuilder.RenameColumn(name: "DepartamentoId", table: "Articulos", newName: "FamiliaId");

            migrationBuilder.Sql("EXEC sp_rename N'PK_Departamentos', N'PK_Familias', N'OBJECT';");
            migrationBuilder.RenameIndex(name: "IX_Departamentos_Codigo", table: "Departamentos", newName: "IX_Familias_Codigo");
            migrationBuilder.RenameTable(name: "Departamentos", newName: "Familias");

            migrationBuilder.CreateIndex(name: "IX_TopesDescuento_Nivel_FamiliaId_ArticuloId", table: "TopesDescuento",
                columns: new[] { "Nivel", "FamiliaId", "ArticuloId" });
            migrationBuilder.AddForeignKey(name: "FK_Articulos_Familias_FamiliaId", table: "Articulos", column: "FamiliaId",
                principalTable: "Familias", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }
}
