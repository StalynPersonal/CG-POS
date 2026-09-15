using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.CargaInicial;

/// <summary>Aplica un <see cref="PaqueteCargaCentral"/> de forma idempotente, todo en una sola transacción.</summary>
public interface ICargaInicialCentral
{
    /// <exception cref="CargaCentralInvalidaExcepcion">El paquete tiene errores; no se guarda nada.</exception>
    Task<ResultadoCargaCentral> AplicarAsync(PaqueteCargaCentral paquete, CancellationToken cancelacion = default);

    /// <exception cref="FileNotFoundException">No existe el archivo.</exception>
    /// <exception cref="CargaCentralInvalidaExcepcion">JSON inválido o paquete con errores; no se guarda nada.</exception>
    Task<ResultadoCargaCentral> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default);
}

public sealed record ResultadoCargaCentral(int Sucursales, int Cajas, int Parametros, int Roles, int Usuarios, int Creados, int Actualizados);

public sealed class CargaCentralInvalidaExcepcion(IReadOnlyList<string> errores)
    : Exception("La carga inicial del Central no es válida:" + Environment.NewLine + string.Join(Environment.NewLine, errores.Select(e => "- " + e)))
{
    public IReadOnlyList<string> Errores { get; } = errores;
}
