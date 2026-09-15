using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PendientesEntrega : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SerialPendiente",
                table: "LineasVenta",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Almacenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Almacenes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestinosEntregaVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlmacenNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Transportista = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoEnvio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FechaComprometida = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AutorizadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinosEntregaVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DestinosEntregaVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PendientesEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VentaNumero = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metodo = table.Column<int>(type: "int", nullable: false),
                    AlmacenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlmacenNombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Sector = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Referencia = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Transportista = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CostoEnvio = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FechaComprometida = table.Column<DateOnly>(type: "date", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ClienteDocumento = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    ClienteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VendidoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ActualizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendientesEntrega", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LineasDestinoEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinoEntregaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroLinea = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasDestinoEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasDestinoEntrega_DestinosEntregaVenta_DestinoEntregaId",
                        column: x => x.DestinoEntregaId,
                        principalTable: "DestinosEntregaVenta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntregasPendiente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PendienteEntregaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    RecibeNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecibeCedula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregasPendiente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregasPendiente_PendientesEntrega_PendienteEntregaId",
                        column: x => x.PendienteEntregaId,
                        principalTable: "PendientesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LineasPendienteEntrega",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PendienteEntregaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroLineaVenta = table.Column<int>(type: "int", nullable: false),
                    ArticuloId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodigoInterno = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnidadMedidaCodigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DecimalesCantidad = table.Column<int>(type: "int", nullable: false),
                    Serializado = table.Column<bool>(type: "bit", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CantidadEntregada = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasPendienteEntrega", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasPendienteEntrega_PendientesEntrega_PendienteEntregaId",
                        column: x => x.PendienteEntregaId,
                        principalTable: "PendientesEntrega",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LineasEntregaPendiente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntregaPendienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroLineaVenta = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Serial = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineasEntregaPendiente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LineasEntregaPendiente_EntregasPendiente_EntregaPendienteId",
                        column: x => x.EntregaPendienteId,
                        principalTable: "EntregasPendiente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Almacenes_Codigo",
                table: "Almacenes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DestinosEntregaVenta_VentaId_Numero",
                table: "DestinosEntregaVenta",
                columns: new[] { "VentaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntregasPendiente_PendienteEntregaId",
                table: "EntregasPendiente",
                column: "PendienteEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasDestinoEntrega_DestinoEntregaId",
                table: "LineasDestinoEntrega",
                column: "DestinoEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasEntregaPendiente_EntregaPendienteId",
                table: "LineasEntregaPendiente",
                column: "EntregaPendienteId");

            migrationBuilder.CreateIndex(
                name: "IX_LineasPendienteEntrega_PendienteEntregaId",
                table: "LineasPendienteEntrega",
                column: "PendienteEntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_Estado_CreadoEn",
                table: "PendientesEntrega",
                columns: new[] { "Estado", "CreadoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_Numero",
                table: "PendientesEntrega",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendientesEntrega_VentaId",
                table: "PendientesEntrega",
                column: "VentaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Almacenes");

            migrationBuilder.DropTable(
                name: "LineasDestinoEntrega");

            migrationBuilder.DropTable(
                name: "LineasEntregaPendiente");

            migrationBuilder.DropTable(
                name: "LineasPendienteEntrega");

            migrationBuilder.DropTable(
                name: "DestinosEntregaVenta");

            migrationBuilder.DropTable(
                name: "EntregasPendiente");

            migrationBuilder.DropTable(
                name: "PendientesEntrega");

            migrationBuilder.DropColumn(
                name: "SerialPendiente",
                table: "LineasVenta");
        }
    }
}
