using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Pos.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Auditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OcurridoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    Accion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TipoEntidad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntidadId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AutorizadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutorizadoPorNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BandejaSalida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoMensaje = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AgregadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Contenido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HashContenido = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: false),
                    ProximoIntentoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    EnviadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    ConfirmadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BandejaSalida", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rnc = table.Column<string>(type: "char(9)", unicode: false, fixedLength: true, maxLength: 9, nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permisos",
                columns: table => new
                {
                    Codigo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permisos", x => x.Codigo);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sucursales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sucursales_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolesPermisos",
                columns: table => new
                {
                    RolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermisoCodigo = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesPermisos", x => new { x.RolId, x.PermisoCodigo });
                    table.ForeignKey(
                        name: "FK_RolesPermisos_Permisos_PermisoCodigo",
                        column: x => x.PermisoCodigo,
                        principalTable: "Permisos",
                        principalColumn: "Codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolesPermisos_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    PinHash = table.Column<string>(type: "varchar(256)", unicode: false, maxLength: 256, nullable: true),
                    CredencialBarrasHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: true),
                    IntentosFallidos = table.Column<int>(type: "int", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true),
                    UltimoIngresoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Roles_RolId",
                        column: x => x.RolId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cajas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Habilitada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cajas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cajas_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parametros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    SucursalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametros", x => x.Id);
                    table.CheckConstraint("CK_Parametros_UnSoloAmbito", "[SucursalId] IS NULL OR [CajaId] IS NULL");
                    table.ForeignKey(
                        name: "FK_Parametros_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parametros_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuariosCajas",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CajaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosCajas", x => new { x.UsuarioId, x.CajaId });
                    table.ForeignKey(
                        name: "FK_UsuariosCajas_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuariosCajas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Accion",
                table: "Auditoria",
                column: "Accion");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_OcurridoEn",
                table: "Auditoria",
                column: "OcurridoEn");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_TipoEntidad_EntidadId",
                table: "Auditoria",
                columns: new[] { "TipoEntidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_BandejaSalida_AgregadoId",
                table: "BandejaSalida",
                column: "AgregadoId");

            migrationBuilder.CreateIndex(
                name: "IX_BandejaSalida_Estado_ProximoIntentoEn",
                table: "BandejaSalida",
                columns: new[] { "Estado", "ProximoIntentoEn" });

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_SucursalId_Codigo",
                table: "Cajas",
                columns: new[] { "SucursalId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Rnc",
                table: "Empresas",
                column: "Rnc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parametros_CajaId",
                table: "Parametros",
                column: "CajaId");

            migrationBuilder.CreateIndex(
                name: "IX_Parametros_Clave_SucursalId_CajaId",
                table: "Parametros",
                columns: new[] { "Clave", "SucursalId", "CajaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parametros_SucursalId",
                table: "Parametros",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Codigo",
                table: "Roles",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolesPermisos_PermisoCodigo",
                table: "RolesPermisos",
                column: "PermisoCodigo");

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_EmpresaId_Codigo",
                table: "Sucursales",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Codigo",
                table: "Usuarios",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CredencialBarrasHash",
                table: "Usuarios",
                column: "CredencialBarrasHash",
                unique: true,
                filter: "[CredencialBarrasHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_RolId",
                table: "Usuarios",
                column: "RolId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuariosCajas_CajaId",
                table: "UsuariosCajas",
                column: "CajaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria");

            migrationBuilder.DropTable(
                name: "BandejaSalida");

            migrationBuilder.DropTable(
                name: "Parametros");

            migrationBuilder.DropTable(
                name: "RolesPermisos");

            migrationBuilder.DropTable(
                name: "UsuariosCajas");

            migrationBuilder.DropTable(
                name: "Permisos");

            migrationBuilder.DropTable(
                name: "Cajas");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Sucursales");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Empresas");
        }
    }
}
