using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Reportes;

/// <summary>
/// Ingreso al módulo de cuadre. Entran los supervisores y gerentes de tienda con el mismo usuario y clave que usan en la
/// caja: no se les crea un usuario del Central ni ven el resto del Central, solo el cuadre de su sucursal.
/// </summary>
public interface IServicioSesionesCuadre
{
    Task<ResultadoSesionCuadre> IngresarAsync(string codigo, string clave, CancellationToken cancelacion = default);
}

/// <param name="Sesion">La sesión cuando el ingreso es correcto; nula si se rechazó.</param>
public sealed record ResultadoSesionCuadre(DatosSesionCuadre? Sesion, string? Mensaje = null)
{
    public bool Exitoso => Sesion is not null;

    public static ResultadoSesionCuadre Rechazado(string mensaje) => new(null, mensaje);
}
