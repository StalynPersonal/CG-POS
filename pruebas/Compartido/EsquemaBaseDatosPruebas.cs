using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace CgPos.Pruebas.Compartido;

/// <summary>
/// Crea la base de una prueba con el mismo script que se usa en producción (<c>scripts/base-datos</c>). Así el script
/// queda verificado en cada corrida: si el modelo cambia y el script no se regenera, las pruebas fallan.
/// </summary>
public static class EsquemaBaseDatosPruebas
{
    /// <param name="cadenaConexion">Conexión a la base temporal que se va a crear (su catálogo aún no existe).</param>
    /// <param name="script">Ruta del structura_base_datos.sql que corresponde a esa base.</param>
    /// <param name="sinAdministrador">
    /// Quita el administrador que siembra el script, para que la prueba use los usuarios de su propia carga inicial.
    /// El administrador del script se prueba aparte.
    /// </param>
    public static async Task CrearAsync(string cadenaConexion, string script, bool sinAdministrador = false,
        CancellationToken cancelacion = default)
    {
        var destino = new SqlConnectionStringBuilder(cadenaConexion);
        var baseDatos = destino.InitialCatalog;
        var sql = await File.ReadAllTextAsync(script, cancelacion);

        // El script trae el nombre definitivo de la base; aquí se apunta a la temporal de la prueba.
        sql = Regex.Replace(sql, @"CgPos(Central|Caja)\b", baseDatos);

        // Los lotes van separados por GO, como en SQL Server Management Studio.
        var lotes = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline)
            .Select(lote => lote.Trim())
            .Where(lote => lote.Length > 0)
            .ToList();

        var servidor = new SqlConnectionStringBuilder(cadenaConexion) { InitialCatalog = "master" }.ConnectionString;
        await using var conexion = new SqlConnection(servidor);
        await conexion.OpenAsync(cancelacion);

        foreach (var lote in lotes)
        {
            await using var comando = new SqlCommand(lote, conexion) { CommandTimeout = 120 };
            await comando.ExecuteNonQueryAsync(cancelacion);
        }

        if (!sinAdministrador)
            return;

        await using var limpieza = new SqlCommand(
            $"USE [{baseDatos}]; DELETE FROM [RolesCentralPermisos]; DELETE FROM [UsuariosCentral]; DELETE FROM [RolesCentral];", conexion);
        await limpieza.ExecuteNonQueryAsync(cancelacion);
    }

    /// <summary>Ruta del script de la base del Central.</summary>
    public static string ScriptCentral(string raizRepositorio) =>
        Path.Combine(raizRepositorio, "scripts", "base-datos", "central", "structura_base_datos.sql");

    /// <summary>Ruta del script de la base de una caja.</summary>
    public static string ScriptCaja(string raizRepositorio) =>
        Path.Combine(raizRepositorio, "scripts", "base-datos", "pos", "structura_base_datos.sql");
}
