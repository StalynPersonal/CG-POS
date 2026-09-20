using System.Globalization;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Reportes;

internal sealed class ServicioCierresSucursal(ContextoDatosCentral contexto, IAuditoriaCentral auditoria, TimeProvider reloj) : IServicioCierresSucursal
{
    public async Task<DatosPreparacionCierreSucursal?> PrepararAsync(int sucursalId, DateOnly fechaOperacion, CancellationToken cancelacion = default)
    {
        var sucursal = await contexto.Sucursales.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sucursalId, cancelacion);
        if (sucursal is null)
            return null;

        var cierres = await CierresDelDiaAsync(sucursalId, fechaOperacion, cancelacion);
        var cajas = await CodigosCajasAsync(cancelacion);
        var formas = CierreSucursal.AgruparFormasPago(cierres);
        var consolidado = await contexto.CierresSucursal.AsNoTracking()
            .Where(c => c.SucursalId == sucursalId && c.FechaOperacion == fechaOperacion)
            .Select(c => (int?)c.Id)
            .SingleOrDefaultAsync(cancelacion);

        return new DatosPreparacionCierreSucursal(sucursal.Id, sucursal.Codigo, sucursal.Nombre, fechaOperacion,
            cierres.OrderBy(c => cajas.GetValueOrDefault(c.CajaId), StringComparer.Ordinal).ThenBy(c => c.TurnoNumero)
                .Select(c => new DatosCierreTurnoSucursal(cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.TurnoNumero, c.UsuarioNombre, c.CantidadVentas,
                    c.TotalVentas, c.TotalEsperado, c.TotalDeclarado, c.Diferencia, c.CerradoEn, c.DeclaradoPorLaCaja, c.Ajustado))
                .ToList(),
            formas.Select(f => new DatosFormaPagoCierreSucursal(f.Tipo, f.Nombre, f.Moneda, f.Transacciones, f.Esperado, f.Declarado, f.Diferencia)).ToList(),
            Efectivo(CierreSucursal.ADepositar(formas.Select(f => (f.Tipo, f.Moneda, f.Declarado))), []),
            await PendientesAsync(sucursalId, fechaOperacion, cierres, cajas, cancelacion),
            consolidado);
    }

    public async Task<ResultadoAdministracion> ConsolidarAsync(SolicitudCierreSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(actor);

        var preparacion = await PrepararAsync(solicitud.SucursalId, solicitud.FechaOperacion, cancelacion);
        if (preparacion is null)
            return ResultadoAdministracion.Inexistente("La sucursal no existe.");
        if (preparacion.CierreSucursalId is not null)
            return ResultadoAdministracion.Error($"La sucursal {preparacion.SucursalCodigo} ya tiene su cierre del {solicitud.FechaOperacion:dd/MM/yyyy}.");
        if (preparacion.Pendientes.Count > 0)
            return ResultadoAdministracion.Error($"No se puede cerrar la sucursal todavía: {string.Join(" ", preparacion.Pendientes)}");

        var codigos = (solicitud.Depositos ?? []).Select(d => d.BancoCodigo?.Trim().ToUpperInvariant()).OfType<string>().Distinct().ToList();
        var bancos = await contexto.Bancos.AsNoTracking().Where(b => codigos.Contains(b.Codigo)).ToDictionaryAsync(b => b.Codigo, b => b.Nombre, cancelacion);
        if (codigos.FirstOrDefault(c => !bancos.ContainsKey(c)) is { } desconocido)
            return ResultadoAdministracion.Error($"El banco '{desconocido}' no existe en el maestro de bancos.");

        CierreSucursal cierre;
        try
        {
            var cierres = await CierresDelDiaAsync(solicitud.SucursalId, solicitud.FechaOperacion, cancelacion);
            cierre = CierreSucursal.Consolidar(solicitud.SucursalId, solicitud.FechaOperacion, cierres,
                (solicitud.Depositos ?? []).Select(d => new DepositoSolicitado(d.Moneda, d.BancoCodigo, bancos.GetValueOrDefault(d.BancoCodigo?.Trim().ToUpperInvariant() ?? string.Empty) ?? string.Empty,
                    d.NumeroBoleta, d.Monto, d.FechaDeposito)),
                solicitud.Observacion, actor.Nombre, reloj.Ahora());
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }

        contexto.CierresSucursal.Add(cierre);
        auditoria.Registrar(new EntradaAuditoria("Reportes.CierreSucursal", "CierreSucursal", $"{preparacion.SucursalCodigo}-{solicitud.FechaOperacion:yyyyMMdd}",
            Detalle: new
            {
                Sucursal = preparacion.SucursalCodigo,
                cierre.FechaOperacion,
                cierre.CantidadCierres,
                cierre.TotalDeclarado,
                cierre.Diferencia,
                ADepositar = cierre.EfectivoADepositar,
                DiferenciaDeposito = cierre.DiferenciaDeposito,
            },
            Motivo: cierre.Observacion, Usuario: actor));

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // Dos usuarios cerraron la misma sucursal y día a la vez: el índice único deja pasar solo uno.
            return ResultadoAdministracion.Error($"La sucursal {preparacion.SucursalCodigo} ya tiene su cierre del {solicitud.FechaOperacion:dd/MM/yyyy}.");
        }

        return ResultadoAdministracion.Correcto(cierre.Id);
    }

    public async Task<IReadOnlyList<DatosCierreSucursal>> ListarAsync(int? sucursalId, DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default)
    {
        var consulta = Consulta().Where(c => c.FechaOperacion >= desde && c.FechaOperacion <= hasta);
        if (sucursalId is { } sucursal)
            consulta = consulta.Where(c => c.SucursalId == sucursal);

        var cierres = await consulta.OrderByDescending(c => c.FechaOperacion).ThenBy(c => c.SucursalId).ToListAsync(cancelacion);
        return await DatosAsync(cierres, cancelacion);
    }

    public async Task<DatosCierreSucursal?> ObtenerAsync(int cierreSucursalId, CancellationToken cancelacion = default)
    {
        var cierre = await Consulta().SingleOrDefaultAsync(c => c.Id == cierreSucursalId, cancelacion);
        return cierre is null ? null : (await DatosAsync([cierre], cancelacion))[0];
    }

    private IQueryable<CierreSucursal> Consulta() =>
        contexto.CierresSucursal.AsNoTracking().Include(c => c.FormasPago).Include(c => c.Depositos).AsSplitQuery();

    /// <summary>Con sus ajustes, para poder mostrar junto a cada cierre lo que declaró la caja y lo que se corrigió aquí.</summary>
    private Task<List<CierreTurnoCentral>> CierresDelDiaAsync(int sucursalId, DateOnly fechaOperacion, CancellationToken cancelacion) =>
        contexto.CierresTurno.AsNoTracking().Include(c => c.FormasPago).Include(c => c.Ajustes).AsSplitQuery()
            .Where(c => c.SucursalId == sucursalId && c.FechaOperacion == fechaOperacion)
            .ToListAsync(cancelacion);

    /// <summary>
    /// Lo que impide consolidar: turnos que cobraron ventas ese día y no han informado su cierre, y cierres que se reabrieron en la caja y no se
    /// han vuelto a cerrar. Consolidar sin ellos dejaría efectivo fuera del depósito.
    /// </summary>
    private async Task<IReadOnlyList<string>> PendientesAsync(int sucursalId, DateOnly fechaOperacion, IReadOnlyList<CierreTurnoCentral> cierres,
        IReadOnlyDictionary<int, string> cajas, CancellationToken cancelacion)
    {
        var pendientes = new List<string>();

        var turnosConVentas = await contexto.VentasCentral.AsNoTracking()
            .Where(v => v.SucursalId == sucursalId && v.FechaOperacion == fechaOperacion && v.TurnoNumero != null)
            .Select(v => new { v.CajaId, TurnoNumero = v.TurnoNumero!.Value })
            .Distinct()
            .ToListAsync(cancelacion);
        var cajasConVentas = turnosConVentas.Select(t => t.CajaId).Distinct().ToList();
        var cerrados = (await contexto.CierresTurno.AsNoTracking()
                .Where(c => cajasConVentas.Contains(c.CajaId))
                .Select(c => new { c.CajaId, c.TurnoNumero })
                .ToListAsync(cancelacion))
            .Select(c => (c.CajaId, c.TurnoNumero))
            .ToHashSet();
        foreach (var turno in turnosConVentas.Where(t => !cerrados.Contains((t.CajaId, t.TurnoNumero))).OrderBy(t => t.CajaId).ThenBy(t => t.TurnoNumero))
            pendientes.Add($"La caja {cajas.GetValueOrDefault(turno.CajaId)} tiene el turno {turno.TurnoNumero} con ventas y sin cierre recibido.");

        return pendientes;
    }

    private async Task<IReadOnlyList<DatosCierreSucursal>> DatosAsync(IReadOnlyList<CierreSucursal> cierres, CancellationToken cancelacion)
    {
        if (cierres.Count == 0)
            return [];

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => (s.Codigo, s.Nombre), cancelacion);
        var ids = cierres.Select(c => c.SucursalId).Distinct().ToList();
        var fechas = cierres.Select(c => c.FechaOperacion).Distinct().ToList();
        var registros = await contexto.CierresTurno.AsNoTracking()
            .Where(c => ids.Contains(c.SucursalId) && fechas.Contains(c.FechaOperacion))
            .Select(c => new { c.SucursalId, c.FechaOperacion, c.RegistradoEn })
            .ToListAsync(cancelacion);

        return cierres.Select(cierre =>
        {
            var (codigo, nombre) = sucursales.GetValueOrDefault(cierre.SucursalId);
            var posteriores = registros.Count(r => r.SucursalId == cierre.SucursalId && r.FechaOperacion == cierre.FechaOperacion && r.RegistradoEn > cierre.CerradoEn);
            return new DatosCierreSucursal(cierre.Id, cierre.SucursalId, codigo, nombre ?? string.Empty,
                cierre.FechaOperacion, cierre.CantidadCierres, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalEsperado, cierre.TotalDeclarado,
                cierre.Diferencia,
                cierre.FormasPago.OrderBy(f => f.Tipo).ThenBy(f => f.Nombre, StringComparer.Ordinal)
                    .Select(f => new DatosFormaPagoCierreSucursal(f.Tipo, f.Nombre, f.Moneda, f.Transacciones, f.Esperado, f.Declarado, f.Diferencia)).ToList(),
                Efectivo(cierre.EfectivoADepositar, cierre.Depositos.Select(d => (d.Moneda, d.Monto))),
                cierre.Depositos.OrderBy(d => d.Moneda, StringComparer.Ordinal).ThenBy(d => d.FechaDeposito)
                    .Select(d => new DatosDepositoCierreSucursal(d.Moneda, d.BancoCodigo, d.BancoNombre, d.NumeroBoleta, d.Monto, d.FechaDeposito)).ToList(),
                cierre.Observacion, cierre.CerradoPor, cierre.CerradoEn, posteriores);
        }).ToList();
    }

    private static IReadOnlyList<DatosEfectivoCierreSucursal> Efectivo(IReadOnlyDictionary<string, decimal> aDepositar, IEnumerable<(string Moneda, decimal Monto)> depositos)
    {
        var lista = depositos.ToList();
        return CierreSucursal.DiferenciaPorMoneda(aDepositar, lista)
            .OrderBy(d => d.Key, StringComparer.Ordinal)
            .Select(d => new DatosEfectivoCierreSucursal(d.Key, aDepositar.GetValueOrDefault(d.Key),
                lista.Where(x => string.Equals(x.Moneda, d.Key, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Monto), d.Value))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<int, string>> CodigosCajasAsync(CancellationToken cancelacion) =>
        await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);
}
