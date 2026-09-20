using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Reportes;

internal sealed class ServicioCierresCaja(ContextoDatosCentral contexto, IAuditoriaCentral auditoria, TimeProvider reloj) : IServicioCierresCaja
{
    private const string TipoEntidad = "CierreTurno";

    public async Task<IReadOnlyList<DatosCierreCaja>> ListarAsync(int? sucursalId, int? cajaId, DateOnly desde, DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.CierresTurno.AsNoTracking().Include(c => c.FormasPago).Include(c => c.Ajustes).AsSplitQuery()
            .Where(c => c.FechaOperacion >= desde && c.FechaOperacion <= hasta);
        if (sucursalId is { } sucursal)
            consulta = consulta.Where(c => c.SucursalId == sucursal);
        if (cajaId is { } caja)
            consulta = consulta.Where(c => c.CajaId == caja);

        var cierres = await consulta.OrderByDescending(c => c.FechaOperacion).ThenBy(c => c.CajaId).ThenBy(c => c.TurnoNumero).ToListAsync(cancelacion);
        if (cierres.Count == 0)
            return [];

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);
        var consolidados = await ConsolidadosAsync(cierres, cancelacion);

        return cierres.Select(c => Datos(c, sucursales, cajas, consolidados.Contains((c.SucursalId, c.FechaOperacion)))).ToList();
    }

    public async Task<ResultadoAdministracion> AjustarAsync(int cierreId, SolicitudAjusteCierre solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(actor);

        if (string.IsNullOrWhiteSpace(solicitud.Motivo))
            return ResultadoAdministracion.Error("Indique el motivo de la corrección.");

        var cierre = await contexto.CierresTurno.Include(c => c.FormasPago).Include(c => c.Ajustes)
            .SingleOrDefaultAsync(c => c.Id == cierreId, cancelacion);
        if (cierre is null)
            return ResultadoAdministracion.Inexistente("El cierre no existe.");

        // Un día ya consolidado en la sucursal no se toca: sus totales y su depósito ya se dieron por buenos.
        if (await contexto.CierresSucursal.AsNoTracking()
                .AnyAsync(s => s.SucursalId == cierre.SucursalId && s.FechaOperacion == cierre.FechaOperacion, cancelacion))
            return ResultadoAdministracion.Error(
                $"El día {cierre.FechaOperacion:dd/MM/yyyy} de esa sucursal ya tiene su cierre consolidado: ese cuadre no se puede corregir.");

        AjusteCierreTurno ajuste;
        try
        {
            ajuste = cierre.Ajustar(solicitud.FormaPagoId, solicitud.Declarado, solicitud.Motivo, actor.Nombre, reloj.Ahora());
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message.Split(" (Parameter")[0]);
        }

        var caja = await contexto.Cajas.AsNoTracking().Where(c => c.Id == cierre.CajaId).Select(c => c.Codigo).SingleAsync(cancelacion);
        auditoria.Registrar(new EntradaAuditoria("Reportes.CierreAjustado", TipoEntidad, cierre.Id.ToString(),
            Detalle: new
            {
                Caja = caja,
                cierre.TurnoNumero,
                Forma = ajuste.FormaPagoNombre,
                ajuste.DeclaradoAnterior,
                ajuste.DeclaradoNuevo,
                DiferenciaDelCierre = cierre.Diferencia,
            },
            Motivo: ajuste.Motivo, Usuario: actor));

        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(cierre.Id);
    }

    /// <summary>Los pares sucursal y día que ya tienen su cierre consolidado: esos cierres no se pueden corregir.</summary>
    private async Task<HashSet<(int SucursalId, DateOnly Fecha)>> ConsolidadosAsync(IReadOnlyList<CierreTurnoCentral> cierres, CancellationToken cancelacion)
    {
        var sucursales = cierres.Select(c => c.SucursalId).Distinct().ToList();
        var fechas = cierres.Select(c => c.FechaOperacion).Distinct().ToList();

        return (await contexto.CierresSucursal.AsNoTracking()
                .Where(s => sucursales.Contains(s.SucursalId) && fechas.Contains(s.FechaOperacion))
                .Select(s => new { s.SucursalId, s.FechaOperacion })
                .ToListAsync(cancelacion))
            .Select(s => (s.SucursalId, s.FechaOperacion))
            .ToHashSet();
    }

    private static DatosCierreCaja Datos(CierreTurnoCentral cierre, IReadOnlyDictionary<int, string> sucursales, IReadOnlyDictionary<int, string> cajas,
        bool enCierreSucursal) =>
        new(cierre.Id,
            sucursales.GetValueOrDefault(cierre.SucursalId) ?? string.Empty,
            cajas.GetValueOrDefault(cierre.CajaId) ?? string.Empty,
            cierre.TurnoNumero,
            cierre.Numero,
            cierre.FechaOperacion,
            cierre.UsuarioNombre,
            cierre.Ciego,
            cierre.CantidadVentas,
            cierre.TotalVentas,
            cierre.TotalEsperado,
            cierre.TotalDeclarado,
            cierre.Diferencia,
            cierre.CerradoEn,
            enCierreSucursal,
            cierre.FormasPago.OrderBy(f => f.Moneda, StringComparer.Ordinal).ThenBy(f => f.Nombre, StringComparer.Ordinal)
                .Select(f => new DatosFormaPagoCierreCaja(f.Id, f.Tipo, f.Nombre, f.Moneda, f.Transacciones, f.Esperado, f.Declarado, f.Diferencia))
                .ToList(),
            cierre.Ajustes.OrderBy(a => a.AjustadoEn)
                .Select(a => new DatosAjusteCierre(a.FormaPagoNombre, a.Moneda, a.DeclaradoAnterior, a.DeclaradoNuevo, a.Motivo, a.AjustadoPorNombre, a.AjustadoEn))
                .ToList());
}
