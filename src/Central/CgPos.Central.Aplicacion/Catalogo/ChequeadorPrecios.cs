using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Catalogo;

/// <summary>
/// Chequeador de precios (RF-95): la página de consulta que se pone en la tienda para que el cliente vea el precio de un artículo.
/// Consulta siempre al Central, así que muestra el precio vigente y la oferta del momento sin depender de una caja.
/// </summary>
public interface IServicioChequeadorPrecios
{
    /// <summary>Está habilitado en el Central; si no, la página no consulta nada.</summary>
    Task<bool> HabilitadoAsync(CancellationToken cancelacion = default);

    /// <summary>Sucursales entre las que elige el chequeador, sin exigir los permisos de organización.</summary>
    Task<IReadOnlyList<DatosSucursalChequeador>> ListarSucursalesAsync(CancellationToken cancelacion = default);

    /// <param name="codigo">Código interno, de barras, de proveedor o referencia del artículo.</param>
    /// <param name="sucursalId">Sucursal donde está el chequeador, para las ofertas que son de una sucursal.</param>
    Task<DatosPrecioChequeador?> ConsultarAsync(string codigo, int? sucursalId, CancellationToken cancelacion = default);
}
