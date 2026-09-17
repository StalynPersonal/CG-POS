using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CgPos.Central.Infraestructura.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DepartamentoCategoriaMarca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Familia pasa a llamarse Departamento. Los maestros publicados se guardan como JSON: se renombran el tipo y las propiedades.
            // El cambio de fila actualiza su versión, así que las cajas vuelven a bajar esos maestros con el formato nuevo.
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Tipo = 'Departamento' WHERE Tipo = 'Familia';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"familiaId\":', '\"departamentoId\":') WHERE Contenido LIKE '%\"familiaId\":%';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"familias\":', '\"departamentos\":') WHERE Contenido LIKE '%\"familias\":%';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"tipo\":\"Familia\"', '\"tipo\":\"Departamento\"') WHERE Tipo = 'ReglaAcumulacion';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"tipo\":\"Departamento\"', '\"tipo\":\"Familia\"') WHERE Tipo = 'ReglaAcumulacion';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"departamentos\":', '\"familias\":') WHERE Contenido LIKE '%\"departamentos\":%';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Contenido = REPLACE(Contenido, '\"departamentoId\":', '\"familiaId\":') WHERE Contenido LIKE '%\"departamentoId\":%';");
            migrationBuilder.Sql("UPDATE MaestrosCentral SET Tipo = 'Familia' WHERE Tipo = 'Departamento';");
        }
    }
}
