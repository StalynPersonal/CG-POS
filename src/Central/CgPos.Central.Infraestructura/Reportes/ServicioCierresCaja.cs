using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Reportes;
using CgPos.Dominio.Turnos;
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

    public async Task<IReadOnlyList<DatosDenominacion>> ListarDenominacionesAsync(CancellationToken cancelacion = default) =>
        await contexto.Denominaciones.AsNoTracking()
            .Where(d => d.Activa)
            .OrderBy(d => d.Moneda).ThenByDescending(d => d.Valor)
            .Select(d => new DatosDenominacion(d.Id, d.Moneda, d.Valor, d.Tipo))
            .ToListAsync(cancelacion);

    /// <summary>Lo que el supervisor tiene por cuadrar en su sucursal: lo más viejo primero, que es lo que urge.</summary>
    public async Task<IReadOnlyList<DatosCierreCaja>> ListarPendientesDeCuadreAsync(int sucursalId, CancellationToken cancelacion = default)
    {
        var cierres = await contexto.CierresTurno.AsNoTracking().Include(c => c.FormasPago).Include(c => c.Ajustes).AsSplitQuery()
            .Where(c => c.SucursalId == sucursalId && c.CuadradoEn == null)
            .OrderBy(c => c.FechaOperacion).ThenBy(c => c.CerradoEn)
            .ToListAsync(cancelacion);
        if (cierres.Count == 0)
            return [];

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);
        var consolidados = await ConsolidadosAsync(cierres, cancelacion);

        return cierres.Select(c => Datos(c, sucursales, cajas, consolidados.Contains((c.SucursalId, c.FechaOperacion)))).ToList();
    }

    /// <summary>
    /// El supervisor declara lo que contó. El cierre queda cuadrado con su faltante o sobrante, que es de la cajera del
    /// turno; quién lo cuadró queda en el propio cierre y en la auditoría.
    /// </summary>
    public async Task<ResultadoAdministracion> CuadrarAsync(int cierreId, SolicitudCuadreCierre solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(actor);

        var cierre = await contexto.CierresTurno.Include(c => c.FormasPago).Include(c => c.Denominaciones).Include(c => c.Ajustes)
            .SingleOrDefaultAsync(c => c.Id == cierreId, cancelacion);
        if (cierre is null)
            return ResultadoAdministracion.Inexistente("El cierre no existe.");

        var denominaciones = await contexto.Denominaciones.AsNoTracking().Where(d => d.Activa).ToListAsync(cancelacion);
        var conteo = new List<ConteoDenominacion>();
        foreach (var item in solicitud.Conteo)
        {
            if (denominaciones.SingleOrDefault(d => d.Id == item.DenominacionId) is not { } denominacion)
                return ResultadoAdministracion.Error("Una denominación del conteo no existe o está inactiva.");
            conteo.Add(new ConteoDenominacion(denominacion.Id, denominacion.Moneda, denominacion.Valor, denominacion.Tipo, item.Cantidad));
        }

        try
        {
            cierre.Cuadrar(solicitud.Declarados.Select(d => new DeclaradoFormaPago(d.FormaPagoId, d.Monto)).ToList(), conteo, actor.Nombre, reloj.Ahora());
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message.Split(" (Parameter")[0]);
        }

        var caja = await contexto.Cajas.AsNoTracking().Where(c => c.Id == cierre.CajaId).Select(c => c.Codigo).SingleAsync(cancelacion);
        auditoria.Registrar(new EntradaAuditoria("Cuadre.CierreCuadrado", TipoEntidad, cierre.Id.ToString(),
            Detalle: new
            {
                Caja = caja,
                cierre.TurnoNumero,
                Cajera = cierre.UsuarioNombre,
                cierre.TotalEsperado,
                cierre.TotalDeclarado,
                cierre.Diferencia,
            },
            Usuario: actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(cierre.Id);
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

    /// <summary>
    /// El día de la sucursal sumado por forma de pago. Los cierres que todavía nadie cuadró entran con su esperado pero sin
    /// declarado: se cuentan aparte, porque mientras falte una caja el resumen no está completo.
    /// </summary>
    public async Task<DatosResumenCuadre> ResumenDelDiaAsync(int sucursalId, DateOnly dia, CancellationToken cancelacion = default)
    {
        var cierres = await contexto.CierresTurno.AsNoTracking().Include(c => c.FormasPago).AsSplitQuery()
            .Where(c => c.SucursalId == sucursalId && c.FechaOperacion == dia)
            .ToListAsync(cancelacion);

        var formas = cierres.SelectMany(c => c.FormasPago)
            .GroupBy(f => (f.Nombre, f.Tipo, f.Moneda))
            .OrderBy(g => g.Key.Moneda, StringComparer.Ordinal).ThenBy(g => g.Key.Nombre, StringComparer.Ordinal)
            .Select(g => new DatosResumenFormaPagoCuadre(g.Key.Nombre, g.Key.Tipo, g.Key.Moneda, g.Sum(f => f.Transacciones),
                g.Sum(f => f.Esperado), g.Sum(f => f.Declarado), g.Sum(f => f.Declarado - f.Esperado)))
            .ToList();

        return new DatosResumenCuadre(dia, cierres.Count, cierres.Count(c => c.PendienteDeCuadre),
            cierres.Sum(c => c.TotalVentas), cierres.Sum(c => c.TotalEsperado), cierres.Sum(c => c.TotalDeclarado),
            cierres.Where(c => !c.PendienteDeCuadre).Sum(c => c.Diferencia), formas);
    }

    /// <summary>Lo que le faltó y le sobró a cada cajera; solo cuentan los cierres ya cuadrados, que son los que tienen conteo.</summary>
    public async Task<IReadOnlyList<DatosDiferenciaCajero>> DiferenciasPorCajeroAsync(int sucursalId, DateOnly desde, DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var cierres = await contexto.CierresTurno.AsNoTracking()
            .Where(c => c.SucursalId == sucursalId && c.FechaOperacion >= desde && c.FechaOperacion <= hasta && c.CuadradoEn != null)
            .Select(c => new { c.UsuarioNombre, c.Diferencia })
            .ToListAsync(cancelacion);

        return cierres.GroupBy(c => c.UsuarioNombre)
            .Select(g => new DatosDiferenciaCajero(g.Key, g.Count(), g.Count(c => c.Diferencia != 0m),
                g.Where(c => c.Diferencia < 0m).Sum(c => -c.Diferencia),
                g.Where(c => c.Diferencia > 0m).Sum(c => c.Diferencia),
                g.Sum(c => c.Diferencia)))
            .OrderBy(d => d.Diferencia)
            .ToList();
    }

    public async Task<IReadOnlyList<DatosMovimientoTurno>> MovimientosAsync(int sucursalId, DateOnly desde, DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var cierres = await contexto.CierresTurno.AsNoTracking().Include(c => c.Movimientos).AsSplitQuery()
            .Where(c => c.SucursalId == sucursalId && c.FechaOperacion >= desde && c.FechaOperacion <= hasta)
            .ToListAsync(cancelacion);
        if (cierres.Count == 0)
            return [];

        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        return cierres
            .SelectMany(c => c.Movimientos.Select(m => new DatosMovimientoTurno(c.FechaOperacion, cajas.GetValueOrDefault(c.CajaId) ?? string.Empty,
                c.TurnoNumero, m.Tipo, m.Numero, m.Monto, m.Moneda, m.Motivo, m.UsuarioNombre, m.UsuarioAnteriorNombre, m.AutorizadoPorNombre, m.Fecha)))
            .OrderByDescending(m => m.Fecha)
            .ToList();
    }

    public async Task<ArchivoReporte?> CuadreEnPdfAsync(int cierreId, int? sucursalId, CancellationToken cancelacion = default)
    {
        var cierre = await contexto.CierresTurno.AsNoTracking()
            .Include(c => c.FormasPago).Include(c => c.Denominaciones).Include(c => c.Movimientos).Include(c => c.Ajustes).AsSplitQuery()
            .SingleOrDefaultAsync(c => c.Id == cierreId && (sucursalId == null || c.SucursalId == sucursalId), cancelacion);
        if (cierre is null)
            return null;

        var sucursal = await contexto.Sucursales.AsNoTracking().Where(s => s.Id == cierre.SucursalId).Select(s => $"{s.Codigo} {s.Nombre}").SingleAsync(cancelacion);
        var caja = await contexto.Cajas.AsNoTracking().Where(c => c.Id == cierre.CajaId).Select(c => c.Codigo).SingleAsync(cancelacion);
        var empresa = await contexto.Empresas.AsNoTracking().SingleAsync(cancelacion);

        var pdf = GeneradorPdfCuadre.Crear(cierre, sucursal, caja,
            new EmpresaEnDocumento(empresa.NombreComercial ?? empresa.RazonSocial, empresa.Rnc, empresa.Direccion, empresa.Telefono));

        return new ArchivoReporte($"Cuadre-{caja}-{cierre.TurnoNumero}.pdf", "application/pdf", pdf);
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
            cierre.PendienteDeCuadre,
            cierre.CuadradoPor,
            cierre.CuadradoEn,
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
