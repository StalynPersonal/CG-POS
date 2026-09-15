using CgPos.Contratos.CargaInicial;

namespace CgPos.Pos.Aplicacion.CargaInicial;

/// <summary>
/// Aplica un <see cref="PaqueteCargaInicial"/> a la base local de forma idempotente:
/// crea lo que no existe, actualiza lo existente y guarda todo en una sola transacción.
/// </summary>
public interface ICargaInicial
{
    /// <exception cref="CargaInicialInvalidaExcepcion">El paquete tiene errores; no se guarda nada.</exception>
    Task<ResultadoCargaInicial> AplicarAsync(PaqueteCargaInicial paquete, CancellationToken cancelacion = default);

    /// <exception cref="FileNotFoundException">No existe el archivo.</exception>
    /// <exception cref="CargaInicialInvalidaExcepcion">JSON inválido o paquete con errores; no se guarda nada.</exception>
    Task<ResultadoCargaInicial> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default);
}

public sealed record ResultadoCargaInicial(
    int Sucursales,
    int Cajas,
    int Roles,
    int Usuarios,
    int Parametros,
    int Creados,
    int Actualizados,
    int PermisosCatalogo);

public sealed class CargaInicialInvalidaExcepcion(IReadOnlyList<string> errores)
    : Exception("La carga inicial no es válida:" + Environment.NewLine + string.Join(Environment.NewLine, errores.Select(e => "- " + e)))
{
    public IReadOnlyList<string> Errores { get; } = errores;
}
