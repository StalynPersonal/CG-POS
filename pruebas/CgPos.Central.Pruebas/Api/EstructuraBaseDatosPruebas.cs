using CgPos.Central.Pruebas.Soporte;
using CgPos.Dominio.Seguridad;
using CgPos.Pruebas.Compartido;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// El script con el que se crea la base del Central (<c>scripts/base-datos/central/structura_base_datos.sql</c>) deja el
/// sistema listo para entrar por primera vez: un administrador con todos los permisos y la contraseña que documenta el script.
/// </summary>
public class EstructuraBaseDatosPruebas
{
    private const string Codigo = "ADMIN";
    private const string Contrasena = "Admin.CGPOS#2026";

    [SkippableFact]
    public async Task El_script_deja_al_administrador_con_todos_los_permisos_y_su_contrasena_valida()
    {
        var servidor = new ConfigurationBuilder()
            .AddUserSecrets<EstructuraBaseDatosPruebas>(optional: true)
            .AddEnvironmentVariables("CGPOS_")
            .Build()
            .GetConnectionString("ServidorPruebas");
        Skip.If(string.IsNullOrWhiteSpace(servidor), "No hay servidor SQL de pruebas configurado (ConnectionStrings:ServidorPruebas).");

        var cadena = new SqlConnectionStringBuilder(servidor) { InitialCatalog = $"CgPosEstructura_{Guid.NewGuid():N}" }.ConnectionString;
        await EsquemaBaseDatosPruebas.CrearAsync(cadena, EsquemaBaseDatosPruebas.ScriptCentral(RutasPrueba.RaizRepositorio()));

        try
        {
            await using var conexion = new SqlConnection(cadena);
            await conexion.OpenAsync();

            // El usuario administrador queda activo, obligado a cambiar su contraseña y con el rol que tiene todos los permisos.
            var (activo, debeCambiar, hash, permisos) = await LeerAdministradorAsync(conexion);
            Assert.True(activo);
            Assert.True(debeCambiar, "El administrador debe cambiar su contraseña en el primer ingreso.");
            Assert.Equal(CatalogoPermisosCentral.Todos.Count, permisos);

            // La contraseña del script la valida el mismo verificador del Central.
            Assert.True(Verificar(Contrasena, hash), "La contraseña documentada en el script no coincide con el hash sembrado.");
            Assert.False(Verificar("otra cosa", hash));

            // Los Id sembrados a mano no chocan con los que reparte la secuencia.
            await using var secuencia = new SqlCommand(
                "SELECT CAST(current_value AS int) FROM sys.sequences WHERE name = 'EntityFrameworkHiLoSequence'", conexion);
            Assert.True((int)(await secuencia.ExecuteScalarAsync())! > 100);
        }
        finally
        {
            await BorrarAsync(servidor!, new SqlConnectionStringBuilder(cadena).InitialCatalog);
        }
    }

    private static async Task<(bool Activo, bool DebeCambiar, string Hash, int Permisos)> LeerAdministradorAsync(SqlConnection conexion)
    {
        await using var comando = new SqlCommand(
            """
            SELECT u.Activo, u.DebeCambiarContrasena, u.ContrasenaHash,
                   (SELECT COUNT(*) FROM RolesCentralPermisos p WHERE p.RolId = u.RolId)
            FROM UsuariosCentral u
            WHERE u.Codigo = @codigo
            """, conexion);
        comando.Parameters.AddWithValue("@codigo", Codigo);

        await using var lector = await comando.ExecuteReaderAsync();
        Assert.True(await lector.ReadAsync(), $"El script no sembró el usuario {Codigo}.");
        return (lector.GetBoolean(0), lector.GetBoolean(1), lector.GetString(2), lector.GetInt32(3));
    }

    /// <summary>Verificación con el mismo formato del Central: PBKDF2-SHA256$iteraciones$sal$hash.</summary>
    private static bool Verificar(string contrasena, string hash)
    {
        if (hash.Split('$') is not ["PBKDF2-SHA256", var iteraciones, var sal, var esperado])
            return false;

        var calculado = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            contrasena, Convert.FromBase64String(sal), int.Parse(iteraciones),
            System.Security.Cryptography.HashAlgorithmName.SHA256, Convert.FromBase64String(esperado).Length);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(calculado, Convert.FromBase64String(esperado));
    }

    private static async Task BorrarAsync(string servidor, string baseDatos)
    {
        var maestro = new SqlConnectionStringBuilder(servidor) { InitialCatalog = "master" }.ConnectionString;
        await using var conexion = new SqlConnection(maestro);
        await conexion.OpenAsync();
        await using var comando = new SqlCommand(
            $"ALTER DATABASE [{baseDatos}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{baseDatos}];", conexion);
        await comando.ExecuteNonQueryAsync();
    }
}
