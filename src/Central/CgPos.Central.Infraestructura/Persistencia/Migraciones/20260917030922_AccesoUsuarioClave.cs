using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <summary>
    /// Los usuarios de caja entran solo con usuario y clave: el hash del PIN pasa a ser el de la clave, el carné se retira
    /// y el parámetro de intentos cambia de nombre.
    /// </summary>
    public partial class AccesoUsuarioClave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"pinHash\":', '\"claveHash\":') WHERE Tipo = 'UsuarioCaja';");
            migrationBuilder.Sql("UPDATE Parametros SET Clave = 'Seguridad.IntentosMaximosClave' WHERE Clave = 'Seguridad.IntentosMaximosPin';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Parametros SET Clave = 'Seguridad.IntentosMaximosPin' WHERE Clave = 'Seguridad.IntentosMaximosClave';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"claveHash\":', '\"pinHash\":') WHERE Tipo = 'UsuarioCaja';");
        }
    }
}
