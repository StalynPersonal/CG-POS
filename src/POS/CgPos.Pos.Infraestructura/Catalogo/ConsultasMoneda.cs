using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Catalogo;

internal static class ConsultasMoneda
{
    /// <summary>
    /// Moneda local de la caja: el parámetro General.MonedaLocal debe indicar una moneda activa del maestro.
    /// Sin ella la caja no puede vender ni cuadrar.
    /// </summary>
    /// <exception cref="ParametroNoConfiguradoExcepcion">Falta el parámetro o la moneda no existe o está inactiva.</exception>
    public static async Task<DatosMoneda> MonedaLocalAsync(this ContextoDatosPos contexto, IParametros parametros, Guid cajaId, CancellationToken cancelacion)
    {
        var codigo = (await parametros.ObtenerRequeridoAsync(ClavesParametros.MonedaLocal, cajaId, cancelacion)).Trim().ToUpperInvariant();

        return await contexto.Monedas.AsNoTracking()
                .Where(m => m.Codigo == codigo && m.Activa)
                .Select(m => new DatosMoneda(m.Codigo, m.Nombre, m.Simbolo))
                .SingleOrDefaultAsync(cancelacion)
            ?? throw new ParametroNoConfiguradoExcepcion(ClavesParametros.MonedaLocal,
                $"indica la moneda «{codigo}», que no existe o está inactiva en el maestro de monedas");
    }
}
