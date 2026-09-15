using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Seguridad;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Organizacion;

internal sealed class ContextoCajaConfigurado(IConfiguration configuracion) : IContextoCaja
{
    public Guid? CajaId => Guid.TryParse(configuracion["Caja:Id"], out var id) && id != Guid.Empty ? id : null;
}

internal sealed class ServicioParametros(ContextoDatosPos contexto) : IParametros
{
    public async Task<string?> ObtenerAsync(string clave, Guid? cajaId = null, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);

        var sucursalId = cajaId is null
            ? null
            : await contexto.Cajas.Where(c => c.Id == cajaId).Select(c => (Guid?)c.SucursalId).FirstOrDefaultAsync(cancelacion);

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
            return new DatosEstadoCaja(false, false, Problema: "La caja no está configurada en este equipo (Caja:Id).");

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

/// <summary>
/// Lector de huella simulado hasta integrar el dispositivo real: identifica siempre al usuario
/// configurado en <c>Perifericos:HuellaSimulada:CodigoUsuario</c> (vacío = no reconoce ninguna huella).
/// </summary>
internal sealed class LectorHuellaSimulado(IConfiguration configuracion, ContextoDatosPos contexto) : ILectorHuella
{
    public async Task<Guid?> IdentificarUsuarioAsync(CancellationToken cancelacion = default)
    {
        var codigo = configuracion["Perifericos:HuellaSimulada:CodigoUsuario"]?.Trim();
        if (string.IsNullOrEmpty(codigo))
            return null;

        return await contexto.Usuarios.Where(u => u.Codigo == codigo).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancelacion);
    }
}
