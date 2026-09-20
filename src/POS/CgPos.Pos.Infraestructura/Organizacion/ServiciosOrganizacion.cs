using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Seguridad;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Infraestructura.Organizacion;

/// <summary>
/// Qué caja es este equipo, según lo que se escribió en su pantalla de configuración. Nulo mientras no se haya configurado.
/// </summary>
internal sealed class ContextoCajaConfigurado(Aplicacion.Sincronizacion.IConfiguracionCaja configuracion, IServiceScopeFactory ambitos) : IContextoCaja
{
    private int? _cajaId;

    public string? SucursalCodigo => Configuracion?.SucursalCodigo;

    public string? CajaCodigo => Configuracion?.CajaCodigo;

    /// <summary>Se busca por los códigos y se recuerda una vez encontrada: la caja no cambia de Id en su base.</summary>
    public int? CajaId
    {
        get
        {
            if (_cajaId is not null || SucursalCodigo is not { } sucursal || CajaCodigo is not { } caja)
                return _cajaId;

            using var ambito = ambitos.CreateScope();
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            _cajaId = contexto.Cajas.AsNoTracking()
                .Where(c => c.Codigo == caja && contexto.Sucursales.Any(s => s.Id == c.SucursalId && s.Codigo == sucursal))
                .Select(c => (int?)c.Id)
                .FirstOrDefault();
            return _cajaId;
        }
    }

    /// <summary>
    /// La configuración se lee aquí de forma síncrona porque estas propiedades las consulta medio sistema; el servicio la
    /// recuerda en memoria, así que no toca la base en cada llamada.
    /// </summary>
    private Aplicacion.Sincronizacion.DatosConfiguracionCaja? Configuracion =>
        configuracion.ObtenerAsync().ConfigureAwait(false).GetAwaiter().GetResult();
}

internal sealed class ServicioParametros(ContextoDatosPos contexto) : IParametros
{
    public async Task<string?> ObtenerAsync(string clave, int? cajaId = null, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        var sucursalId = cajaId is null
            ? null
            : await contexto.Cajas.Where(c => c.Id == cajaId).Select(c => (int?)c.SucursalId).FirstOrDefaultAsync(cancelacion);

        var candidatos = await contexto.Parametros
            .AsNoTracking()
            .Where(p => p.Clave == clave
                && ((p.CajaId == null && p.SucursalId == null)
                    || (cajaId != null && p.CajaId == cajaId)
                    || (sucursalId != null && p.SucursalId == sucursalId)))
            .ToListAsync(cancelacion);

        var elegido = candidatos.FirstOrDefault(p => p.CajaId is not null)
            ?? candidatos.FirstOrDefault(p => p.SucursalId is not null)
            ?? candidatos.FirstOrDefault();

        return elegido?.Valor;
    }
}

internal sealed class ServicioEstadoCaja(ContextoDatosPos contexto, IContextoCaja contextoCaja, IParametros parametros) : IEstadoCaja
{
    public async Task<DatosEstadoCaja> ObtenerAsync(CancellationToken cancelacion = default)
    {
        if (contextoCaja.CajaId is not { } cajaId)
            return new DatosEstadoCaja(false, false, Problema: "Esta caja todavía no llegó del Central. Revise su configuración y que el Central la acepte.");

        var datos = await (
                from caja in contexto.Cajas
                join sucursal in contexto.Sucursales on caja.SucursalId equals sucursal.Id
                join empresa in contexto.Empresas on sucursal.EmpresaId equals empresa.Id
                where caja.Id == cajaId
                select new
                {
                    caja.Codigo,
                    caja.Nombre,
                    caja.Habilitada,
                    Sucursal = sucursal.Nombre,
                    SucursalActiva = sucursal.Activa,
                    Empresa = empresa.NombreComercial ?? empresa.RazonSocial,
                })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancelacion);

        if (datos is null)
            return new DatosEstadoCaja(true, false, cajaId, Problema: "La caja configurada no existe en la base local. Aplique la carga inicial.");

        var problema = !datos.Habilitada
            ? "Esta caja está deshabilitada desde el Central."
            : !datos.SucursalActiva
                ? "La sucursal de esta caja está inactiva."
                : null;

        // Sin moneda local configurada se puede ingresar; vender o cuadrar informa qué falta configurar.
        DatosMoneda? moneda = null;
        try
        {
            moneda = await contexto.MonedaLocalAsync(parametros, cajaId, cancelacion);
        }
        catch (ParametroNoConfiguradoExcepcion)
        {
        }

        return new DatosEstadoCaja(true, problema is null, cajaId, datos.Codigo, datos.Nombre, datos.Sucursal, datos.Empresa, problema, moneda);
    }
}
