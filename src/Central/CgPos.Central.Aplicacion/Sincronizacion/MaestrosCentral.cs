using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;

namespace CgPos.Central.Aplicacion.Sincronizacion;

/// <param name="Publicados">Registros nuevos o que cambiaron (bajan a las cajas).</param>
/// <param name="SinCambios">Registros iguales a lo ya publicado (no reciben versión nueva).</param>
public sealed record ResultadoPublicacion(int Publicados, int SinCambios);

public sealed class PublicacionInvalidaExcepcion(IReadOnlyList<string> errores)
    : Exception("La publicación de maestros no es válida:" + Environment.NewLine + string.Join(Environment.NewLine, errores.Select(e => "- " + e)))
{
    public IReadOnlyList<string> Errores { get; } = errores;
}

/// <summary>Publica maestros para las cajas (RN-24: el Central es la autoridad). Todo o nada: con un error no se guarda nada.</summary>
public interface IPublicadorMaestros
{
    /// <summary>Valida cada registro con las reglas del dominio, las referencias y los códigos únicos contra lo ya publicado.</summary>
    /// <exception cref="PublicacionInvalidaExcepcion">Algún registro no es válido.</exception>
    /// <param name="corregirDocumentoCliente">Admite el cambio de documento de un cliente ya publicado (solo desde la corrección auditada).</param>
    Task<ResultadoPublicacion> PublicarAsync(PaqueteMaestros paquete, string usuario, CancellationToken cancelacion = default, bool corregirDocumentoCliente = false);

    /// <summary>
    /// Roles, usuarios y parámetros de las cajas en el formato de la carga inicial de la caja. Un PIN o carné en claro se publica solo como hash,
    /// con el mismo formato que verifica la caja; un PIN que no cambió conserva su hash.
    /// </summary>
    /// <exception cref="PublicacionInvalidaExcepcion">Algún registro no es válido.</exception>
    Task<ResultadoPublicacion> PublicarSeguridadCajasAsync(IReadOnlyList<RolCarga> roles, IReadOnlyList<UsuarioCarga> usuarios, IReadOnlyList<ParametroCarga> parametros,
        string usuario, CancellationToken cancelacion = default);
}

public interface IServicioBajadaMaestros
{
    /// <summary>
    /// Lo que cambió desde la versión que la caja ya tiene (RF-273), limitado a lo que le corresponde: sus parámetros (general, de su sucursal y de ella)
    /// y sus rangos de e-CF. Desde 0 es el aprovisionamiento completo de una caja nueva (RF-281).
    /// </summary>
    Task<PaqueteBajadaMaestros> ObtenerAsync(CajaRemitente caja, long desde, CancellationToken cancelacion = default);
}
