using System.Globalization;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Sincronizacion;
using CgPos.Pos.Infraestructura.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Tickets;
using CgPos.Pos.Infraestructura.Ventas;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Pos.Infraestructura.Turnos;

internal static class ConversionesCaja
{
    public static DatosMovimientoCaja ADatos(this MovimientoCaja movimiento) =>
        new(movimiento.Id, movimiento.Tipo, movimiento.Numero, movimiento.Monto, movimiento.Moneda, movimiento.Motivo, movimiento.UsuarioNombre,
            movimiento.UsuarioAnteriorNombre, movimiento.AutorizadoPorNombre, movimiento.Fecha);

    public static DatosCierre ADatos(this CierreTurno cierre, IEnumerable<MovimientoCaja> movimientos, LoteTarjetas? lote = null) =>
        new(cierre.Id, cierre.TurnoId, cierre.TurnoNumero, cierre.Numero, cierre.CajaId, cierre.SucursalId, cierre.FechaOperacion,
            cierre.FondoInicial, cierre.FondoEnCuadre, cierre.Moneda, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros, cierre.TotalEsperado,
            cierre.UsuarioNombre, cierre.AbiertoEn, cierre.CerradoEn,
            cierre.FormasPago.OrderBy(f => f.Orden)
                .Select(f => new DatosCierreFormaPago(f.FormaPagoId, f.Codigo, f.Nombre, f.Tipo, f.Moneda, f.Transacciones, f.Esperado))
                .ToList(),
            movimientos.OrderBy(m => m.Fecha).ThenBy(m => m.Numero).Select(m => m.ADatos()).ToList(),
            lote is null
                ? null
                : new DatosLoteTarjetas(lote.NumeroLote, lote.TransaccionesCaja, lote.MontoCaja, lote.TransaccionesTerminal, lote.MontoTerminal,
                    lote.Diferencia, lote.DetalleDelTerminal, lote.UsuarioNombre, lote.CerradoEn,
                    lote.Descuadres.Where(a => a.Origen == OrigenAprobacion.Caja).Select(a => a.Aprobacion).ToList(),
                    lote.Descuadres.Where(a => a.Origen == OrigenAprobacion.Terminal).Select(a => a.Aprobacion).ToList()));
}

internal sealed class ServicioCaja(
    ContextoDatosPos contexto,
    IValidadorAutorizaciones autorizaciones,
    IParametros parametros,
    IImpresoraTicket impresora,
    ITerminalPago terminal,
    IBandejaSalida bandejaSalida,
    IAuditoria auditoria,
    GeneradorSecuencias secuencias,
    TimeProvider reloj) : IServicioCaja
{
    private const string TipoEntidadTurno = "Turno";
    private const string TipoEntidadCierre = "CierreTurno";

    private sealed record CalculoTurno(
        bool FondoEnCuadre,
        int CantidadVentas,
        decimal TotalVentas,
        decimal TotalRetiros,
        decimal EfectivoEnGaveta,
        IReadOnlyList<EsperadoFormaPago> Esperados,
        IReadOnlyList<DatosDenominacion> Denominaciones,
        IReadOnlyList<MovimientoCaja> Movimientos,
        IReadOnlyList<string> Bloqueos,
        DatosMoneda MonedaLocal);

    public async Task<RespuestaCaja> ObtenerResumenAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos.AsNoTracking().FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (turno is null)
            return SinTurno();

        var calculo = await CalcularAsync(turno, cancelacion);
        // La cajera nunca ve los montos: el cuadre lo hace el supervisor con el papel en la mano.
        var mostrar = sesion.TienePermiso(CatalogoPermisos.PreCierre);
        return new RespuestaCaja(CodigoResultadoCaja.Correcto, null, Resumen: Resumen(turno, calculo, mostrar), Turno: turno.ADatos());
    }

    public async Task<RespuestaCaja> PreCierreAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos.AsNoTracking().FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (turno is null)
            return SinTurno();

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.PreCierre, autorizacionId, TipoEntidadTurno, turno.Id.ToString(), cancelacion);
        if (!permiso.Permitido)
            return Rechazo(permiso, CatalogoPermisos.PreCierre, "El pre-cierre se emite con clave de supervisor.");

        var calculo = await CalcularAsync(turno, cancelacion);
        var resumen = Resumen(turno, calculo, mostrarEsperado: true);
        var ahora = reloj.Ahora();

        auditoria.Registrar(new EntradaAuditoria("Caja.PreCierre", TipoEntidadTurno, turno.Id.ToString(),
            Detalle: new { turno.Numero, calculo.CantidadVentas, calculo.TotalVentas },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        var impresion = await impresora.ImprimirAsync(GeneradorTicket.GenerarPreCierre(await contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion), resumen, ahora),
            cancelacion);
        return new RespuestaCaja(CodigoResultadoCaja.Correcto, impresion.Correcto ? "Pre-cierre impreso." : impresion.Mensaje, Resumen: resumen, Turno: turno.ADatos());
    }

    /// <summary>
    /// Cierra el lote del terminal y lo cuadra con las tarjetas aprobadas del turno (RF-215). Si el modelo del terminal no detalla
    /// el lote, se informa lo de la caja para compararlo con el comprobante que imprime el terminal.
    /// </summary>
    public async Task<DatosConciliacionTarjetas> ConciliarTarjetasAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos.AsNoTracking().FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (turno is null)
            return new DatosConciliacionTarjetas(false, null, 0, 0m, 0, 0m, 0m, [], [], false, "No hay un turno abierto en esta caja.");

        var operaciones = await contexto.OperacionesTerminal.AsNoTracking()
            .Where(o => o.TurnoId == turno.Id && o.Estado == EstadoOperacionTerminal.Aprobada && o.Tipo == TipoOperacionTerminal.Venta)
            .ToListAsync(cancelacion);

        var enCaja = operaciones.Where(o => o.Aprobacion is { Length: > 0 }).Select(o => o.Aprobacion!).ToList();
        var montoCaja = operaciones.Sum(o => o.Monto);

        var lote = await terminal.CerrarLoteAsync(cancelacion);
        if (!lote.Correcto)
            return new DatosConciliacionTarjetas(false, null, operaciones.Count, montoCaja, 0, 0m, montoCaja, enCaja, [], false,
                lote.Mensaje ?? "No se pudo cerrar el lote del terminal.");

        var aprobacionesTerminal = lote.Aprobaciones ?? [];
        var detalla = lote.Transacciones > 0 || aprobacionesTerminal.Count > 0;
        var soloEnCaja = detalla ? enCaja.Except(aprobacionesTerminal, StringComparer.OrdinalIgnoreCase).ToList() : [];
        var soloEnTerminal = aprobacionesTerminal.Except(enCaja, StringComparer.OrdinalIgnoreCase).ToList();

        // El cuadre del lote se guarda y sube con el cierre: es lo que después se compara contra lo que deposita el banco.
        var anterior = await contexto.LotesTarjetas.FirstOrDefaultAsync(l => l.TurnoId == turno.Id, cancelacion);
        if (anterior is not null)
            contexto.LotesTarjetas.Remove(anterior);

        contexto.LotesTarjetas.Add(LoteTarjetas.Registrar(turno, lote.NumeroLote, operaciones.Count, montoCaja, lote.Transacciones, lote.Monto,
            detalla, soloEnCaja, soloEnTerminal, sesion.Nombre, reloj.Ahora()));
        auditoria.Registrar(new EntradaAuditoria("Caja.LoteTarjetasCerrado", TipoEntidadTurno, turno.Id.ToString(),
            Detalle: new { turno.Numero, lote.NumeroLote, TransaccionesCaja = operaciones.Count, MontoCaja = montoCaja, lote.Transacciones, lote.Monto },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return new DatosConciliacionTarjetas(
            true,
            lote.NumeroLote,
            operaciones.Count,
            montoCaja,
            lote.Transacciones,
            lote.Monto,
            detalla ? montoCaja - lote.Monto : 0m,
            soloEnCaja,
            soloEnTerminal,
            detalla,
            detalla ? lote.Mensaje : "El terminal cerró el lote sin detallarlo: compare con el comprobante que imprimió.");
    }

    public async Task<RespuestaCaja> RetirarEfectivoAsync(SesionUsuario sesion, decimal monto, string? motivo, Guid? autorizacionId,
        CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        monto = decimal.Round(monto, 2, MidpointRounding.AwayFromZero);
        if (monto <= 0)
            return new RespuestaCaja(CodigoResultadoCaja.MontoInvalido, "El monto del retiro debe ser mayor que cero.");

        // Antes de pedir la clave del supervisor: no se puede retirar más de lo que hay en la gaveta.
        var calculo = await CalcularAsync(turno!, cancelacion);
        if (monto > calculo.EfectivoEnGaveta)
            return new RespuestaCaja(CodigoResultadoCaja.EfectivoInsuficiente,
                $"El retiro ({calculo.MonedaLocal.Simbolo}{monto:N2}) supera el efectivo en la gaveta ({calculo.MonedaLocal.Simbolo}{calculo.EfectivoEnGaveta:N2}).");

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.RetiroEfectivo, autorizacionId, TipoEntidadTurno, turno!.Id.ToString(), cancelacion);
        if (!permiso.Permitido)
            return Rechazo(permiso, CatalogoPermisos.RetiroEfectivo, "El retiro de efectivo requiere autorización de un supervisor.");

        var numero = calculo.Movimientos.Count(m => m.Tipo == TipoMovimientoCaja.Retiro) + 1;
        var retiro = MovimientoCaja.Retiro(turno, numero, monto, calculo.MonedaLocal.Codigo, motivo, sesion.UsuarioId, sesion.Nombre, permiso.SupervisorId, permiso.SupervisorNombre,
            reloj.Ahora());
        contexto.MovimientosCaja.Add(retiro);

        var datos = retiro.ADatos();
        bandejaSalida.Encolar("Caja.RetiroEfectivo", $"{turno.Numero}-{datos.Tipo}-{datos.Numero}",
            new DocumentoMovimientoCaja(turno.Numero, DocumentosParaCentral.MovimientoTurno(datos)));
        auditoria.Registrar(new EntradaAuditoria("Caja.RetiroEfectivo", TipoEntidadTurno, turno.Id.ToString(),
            Detalle: new { turno.Numero, Retiro = numero, Monto = monto },
            Motivo: retiro.Motivo,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        // Después de guardar: la impresora o la gaveta nunca deshacen el retiro.
        var impresion = await impresora.ImprimirAsync(GeneradorTicket.GenerarRetiro(await contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion), datos, turno.Numero),
            cancelacion);
        var gaveta = await impresora.AbrirGavetaAsync(cancelacion);
        var avisos = string.Join(" ", new[] { impresion.Correcto ? null : impresion.Mensaje, gaveta.Correcto ? null : gaveta.Mensaje }.Where(a => a is not null));

        return new RespuestaCaja(CodigoResultadoCaja.Correcto, $"Retiro {numero} por {calculo.MonedaLocal.Simbolo}{monto:N2} registrado. {avisos}".Trim(), Movimiento: datos, Turno: turno.ADatos());
    }

    public async Task<RespuestaCaja> RelevarAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos.FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (turno is null)
            return SinTurno();
        if (turno.UsuarioActualId == sesion.UsuarioId)
            return new RespuestaCaja(CodigoResultadoCaja.TurnoDelMismoUsuario, "El turno ya está a su nombre.", Turno: turno.ADatos());
        if (!sesion.TienePermiso(CatalogoPermisos.RegistrarVenta))
            return new RespuestaCaja(CodigoResultadoCaja.SinPermiso, "Su usuario no puede operar la caja.");

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.RelevoCajero, autorizacionId, TipoEntidadTurno, turno.Id.ToString(), cancelacion);
        if (!permiso.Permitido)
            return Rechazo(permiso, CatalogoPermisos.RelevoCajero, $"El relevo de {turno.UsuarioActualNombre} requiere autorización de un supervisor.");

        var numero = await contexto.MovimientosCaja.CountAsync(m => m.TurnoId == turno.Id && m.Tipo == TipoMovimientoCaja.Relevo, cancelacion) + 1;
        var relevo = turno.Relevar(numero, sesion.UsuarioId, sesion.Nombre, permiso.SupervisorId, permiso.SupervisorNombre, reloj.Ahora());
        contexto.MovimientosCaja.Add(relevo);

        var datos = relevo.ADatos();
        bandejaSalida.Encolar("Caja.RelevoCajero", $"{turno.Numero}-{datos.Tipo}-{datos.Numero}",
            new DocumentoMovimientoCaja(turno.Numero, DocumentosParaCentral.MovimientoTurno(datos)));
        auditoria.Registrar(new EntradaAuditoria("Caja.RelevoCajero", TipoEntidadTurno, turno.Id.ToString(),
            Detalle: new { turno.Numero, Anterior = relevo.UsuarioAnteriorNombre, Nuevo = relevo.UsuarioNombre },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaCaja(CodigoResultadoCaja.Correcto, $"Relevo registrado: {sesion.Nombre} opera el turno {turno.Numero}.", Movimiento: datos,
            Turno: turno.ADatos());
    }

    /// <summary>
    /// Cierra el turno con lo que la caja sabe. La cajera no declara: entrega el dinero con el cuadre impreso y el
    /// supervisor lo cuenta después, en el módulo de cuadre del Central.
    /// </summary>
    public async Task<RespuestaCaja> CerrarAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var calculo = await CalcularAsync(turno!, cancelacion);
        if (calculo.Bloqueos.Count > 0)
            return new RespuestaCaja(CodigoResultadoCaja.CierreBloqueado, "No se puede cerrar el turno todavía.", Bloqueos: calculo.Bloqueos);

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.CerrarTurno, autorizacionId, TipoEntidadTurno, turno!.Id.ToString(), cancelacion);
        if (!permiso.Permitido)
            return Rechazo(permiso, CatalogoPermisos.CerrarTurno, "Cerrar el turno requiere autorización de un supervisor.");

        var ahora = reloj.Ahora();
        var numero = await contexto.CierresTurno.CountAsync(c => c.TurnoId == turno.Id, cancelacion) + 1;
        CierreTurno cierre;
        try
        {
            cierre = CierreTurno.Registrar(turno, numero, calculo.FondoEnCuadre, calculo.CantidadVentas, calculo.TotalVentas, calculo.TotalRetiros,
                calculo.Esperados, calculo.MonedaLocal.Codigo, sesion.UsuarioId, sesion.Nombre, ahora);
        }
        catch (ReglaCierreExcepcion excepcion)
        {
            contexto.ChangeTracker.Clear();
            return new RespuestaCaja(CodigoResultadoCaja.DeclaracionInvalida, excepcion.Message);
        }

        contexto.CierresTurno.Add(cierre);

        // Si alguien suspendió y no volvió, el rato parado se cierra aquí: si no, el reporte diría que el almuerzo duró
        // catorce horas. Queda marcado para que se distinga de una reanudación normal.
        foreach (var suspension in await contexto.SuspensionesCaja.Where(s => s.TurnoId == turno.Id && s.ReanudadaEn == null).ToListAsync(cancelacion))
        {
            suspension.CerrarPorCierreDeTurno(ahora);
            AvisoSuspensiones.Encolar(bandejaSalida, suspension, turno.FechaOperacion);
        }

        // Las transacciones en curso sin artículos activos no son documentos: salen de la mesa de trabajo al cerrar. Si
        // tuvieron líneas, lo que pasó queda en la auditoría.
        var enCurso = await contexto.VentasEnProceso.Where(v => v.TurnoId == turno.Id).ToListAsync(cancelacion);
        foreach (var venta in enCurso)
        {
            contexto.VentasEnProceso.Remove(venta);
            if (venta.Lineas.Count > 0)
                auditoria.Registrar(new EntradaAuditoria("Ventas.Descartada", "Venta", venta.Identificacion,
                    Motivo: "Sin artículos al cerrar el turno",
                    Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        }

        // El lote del terminal, si el cajero lo cerró: viaja con el cierre para que el Central lo cuadre contra el banco.
        var lote = await contexto.LotesTarjetas.AsNoTracking().FirstOrDefaultAsync(l => l.TurnoId == turno.Id, cancelacion);
        var datos = cierre.ADatos(calculo.Movimientos, lote);
        bandejaSalida.Encolar("Caja.TurnoCerrado", turno.Numero.ToString(CultureInfo.InvariantCulture), DocumentosParaCentral.CierreTurno(datos));
        auditoria.Registrar(new EntradaAuditoria("Caja.TurnoCerrado", TipoEntidadTurno, turno.Id.ToString(),
            Detalle: new { turno.Numero, Cierre = cierre.Numero, cierre.TotalEsperado },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        // Con el turno cerrado, las ventas en curso y en espera quedaron vacías: sus Id vuelven a 1 para el turno siguiente.
        await secuencias.ReiniciarIdsDeTrabajoAsync(cancelacion);

        var impresion = await impresora.ImprimirAsync(GeneradorTicket.GenerarCierre(await contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion), datos, esCopia: false),
            cancelacion);
        var mensaje = $"Turno {turno.Numero} cerrado. Entregue el efectivo y el comprobante del lote al supervisor con el cuadre impreso."
                      + (impresion.Correcto ? string.Empty : $" {impresion.Mensaje}");
        return new RespuestaCaja(CodigoResultadoCaja.Correcto, mensaje, Cierre: datos, Turno: turno.ADatos());
    }

    public async Task<IReadOnlyList<DatosCierre>> ListarCierresAsync(SesionUsuario sesion, int maximo = 20, CancellationToken cancelacion = default)
    {
        var cierres = await contexto.CierresTurno.AsNoTracking().Include(c => c.FormasPago)
            .Where(c => c.CajaId == sesion.CajaId)
            .OrderByDescending(c => c.CerradoEn)
            .Take(Math.Clamp(maximo, 1, 100))
            .ToListAsync(cancelacion);

        var turnos = cierres.Select(c => c.TurnoId).Distinct().ToList();
        var movimientos = await contexto.MovimientosCaja.AsNoTracking().Where(m => turnos.Contains(m.TurnoId)).ToListAsync(cancelacion);

        return cierres.Select(c => c.ADatos(movimientos.Where(m => m.TurnoId == c.TurnoId))).ToList();
    }

    public async Task<RespuestaCaja> ReimprimirCierreAsync(SesionUsuario sesion, int cierreId, CancellationToken cancelacion = default)
    {
        var cierre = await contexto.CierresTurno.AsNoTracking().Include(c => c.FormasPago)
            .SingleOrDefaultAsync(c => c.Id == cierreId && c.CajaId == sesion.CajaId, cancelacion);
        if (cierre is null)
            return new RespuestaCaja(CodigoResultadoCaja.CierreNoEncontrado, "El cierre no existe en esta caja.");

        var movimientos = await contexto.MovimientosCaja.AsNoTracking().Where(m => m.TurnoId == cierre.TurnoId).ToListAsync(cancelacion);
        var datos = cierre.ADatos(movimientos);
        var impresion = await impresora.ImprimirAsync(GeneradorTicket.GenerarCierre(await contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion), datos, esCopia: true),
            cancelacion);

        auditoria.Registrar(new EntradaAuditoria("Caja.CierreReimpreso", TipoEntidadCierre, cierre.Id.ToString(),
            Detalle: new { cierre.TurnoNumero, impresion.Correcto }, Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return impresion.Correcto
            ? new RespuestaCaja(CodigoResultadoCaja.Correcto, "Cierre reimpreso.", Cierre: datos)
            : new RespuestaCaja(CodigoResultadoCaja.Correcto, impresion.Mensaje, Cierre: datos);
    }

    private async Task<CalculoTurno> CalcularAsync(Turno turno, CancellationToken cancelacion)
    {
        var fondoEnCuadre = await parametros.ObtenerBooleanoAsync(ClavesParametros.FondoEnCuadre, turno.CajaId, cancelacion);
        var monedaLocal = await contexto.MonedaLocalAsync(parametros, turno.CajaId, cancelacion);

        var cobradas = await contexto.Ventas.AsNoTracking()
            .Where(v => v.TurnoId == turno.Id && v.Estado == EstadoVenta.Cobrada)
            .ToListAsync(cancelacion);
        var pagos = cobradas.SelectMany(v => v.Pagos).ToList();

        var formas = await contexto.FormasPago.AsNoTracking().OrderBy(f => f.Orden).ToListAsync(cancelacion);
        var movimientos = await contexto.MovimientosCaja.AsNoTracking().Where(m => m.TurnoId == turno.Id).OrderBy(m => m.Fecha).ToListAsync(cancelacion);
        var denominaciones = await contexto.Denominaciones.AsNoTracking().Where(d => d.Activa)
            .OrderBy(d => d.Moneda).ThenByDescending(d => d.Valor)
            .Select(d => new DatosDenominacion(d.Id, d.Moneda, d.Valor, d.Tipo))
            .ToListAsync(cancelacion);

        // Retiros y reembolsos al cliente: todo lo que salió de la gaveta baja lo esperado (RF-123, RF-261).
        var retiros = movimientos.Where(m => m.Tipo is TipoMovimientoCaja.Retiro or TipoMovimientoCaja.Reembolso).Sum(m => m.Monto);
        var esperados = ReglasCuadre.CalcularEsperados(
            formas.Where(f => f.Activa || pagos.Any(p => p.FormaPagoId == f.Id)).Select(f => new FormaPagoCuadre(f.Id, f.Codigo, f.Nombre, f.Tipo, f.Moneda, f.Orden)),
            pagos.Select(p => new PagoCuadre(p.FormaPagoId, p.MontoRecibido, p.MontoAplicado)),
            cobradas.Sum(v => v.Devuelta), retiros, turno.FondoInicial, fondoEnCuadre, monedaLocal.Codigo);

        // Validaciones de cierre (RF-265, RN-22).
        var bloqueos = new List<string>();
        // Las que no se cobraron están en las tablas de trabajo: la guardada se nombra por la referencia del cajero y la
        // que está en curso por su borrador, porque ninguna tiene número de factura.
        var enEspera = await contexto.VentasGuardadas.AsNoTracking().Where(v => v.TurnoId == turno.Id)
            .OrderBy(v => v.PuestaEnEsperaEn).Select(v => v.Referencia).ToListAsync(cancelacion);
        // Con un turno de un día anterior ya no se pueden cobrar: la única salida es limpiarlas.
        var diaAnterior = await ReglasTurno.BloqueoDiaAnteriorAsync(turno, parametros, reloj, cancelacion) is not null;
        if (enEspera.Count > 0)
            bloqueos.Add(diaAnterior
                ? $"Hay {enEspera.Count} factura(s) en espera: {string.Join(", ", enEspera)}. El turno es de un día anterior y ya no se pueden cobrar: "
                  + "retómelas y límpielas para poder hacer el cierre."
                : $"Hay {enEspera.Count} factura(s) en espera: {string.Join(", ", enEspera)}. Retómelas y cóbrelas o anúlelas.");
        var enCurso = await contexto.VentasEnProceso.AsNoTracking().Where(v => v.TurnoId == turno.Id).ToListAsync(cancelacion);
        foreach (var venta in enCurso.Where(v => v.TieneLineasActivas))
            bloqueos.Add(diaAnterior
                ? $"La transacción {venta.Identificacion} de {venta.UsuarioNombre} está en curso con artículos y el turno es de un día anterior: "
                  + "límpiela para poder hacer el cierre."
                : $"La transacción {venta.Identificacion} de {venta.UsuarioNombre} está en curso con artículos: cóbrela o anúlela.");

        var idsCobradas = cobradas.Select(v => v.Id).ToList();
        var conEcf = idsCobradas.Count == 0
            ? []
            : await contexto.DocumentosElectronicos.AsNoTracking().Where(d => idsCobradas.Contains(d.VentaId) && d.TipoOrigen == OrigenComprobante.Venta).Select(d => d.VentaId).ToListAsync(cancelacion);
        var sinEcf = cobradas.Where(v => !conEcf.Contains(v.Id)).Select(v => v.NumeroTransaccion).ToList();
        if (sinEcf.Count > 0)
            bloqueos.Add($"Hay {sinEcf.Count} venta(s) sin e-CF firmado: {string.Join(", ", sinEcf.Take(5))}.");

        return new CalculoTurno(fondoEnCuadre, cobradas.Count, cobradas.Sum(v => v.TotalCobrado ?? 0m), retiros,
            ReglasCuadre.EfectivoLocalEnGaveta(esperados, turno.FondoInicial, fondoEnCuadre, monedaLocal.Codigo), esperados, denominaciones, movimientos, bloqueos,
            monedaLocal);
    }

    private static DatosResumenTurno Resumen(Turno turno, CalculoTurno calculo, bool mostrarEsperado) =>
        new(turno.ADatos(), mostrarEsperado, calculo.FondoEnCuadre,
            mostrarEsperado ? calculo.CantidadVentas : null,
            mostrarEsperado ? calculo.TotalVentas : null,
            calculo.TotalRetiros,
            calculo.Esperados
                .Select(e => new DatosFormaPagoTurno(e.FormaPagoId, e.Codigo, e.Nombre, e.Tipo, e.Moneda, e.Orden,
                    mostrarEsperado ? e.Esperado : null, mostrarEsperado ? e.Transacciones : null))
                .ToList(),
            calculo.Denominaciones,
            calculo.Movimientos.Select(m => m.ADatos()).ToList(),
            calculo.Bloqueos,
            calculo.MonedaLocal.Codigo);

    private async Task<(Turno? Turno, RespuestaCaja? Rechazo)> TurnoDelUsuarioAsync(SesionUsuario sesion, CancellationToken cancelacion)
    {
        var turno = await contexto.Turnos.FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (turno is null)
            return (null, SinTurno());
        if (turno.UsuarioActualId != sesion.UsuarioId)
            return (null, new RespuestaCaja(CodigoResultadoCaja.TurnoDeOtroUsuario,
                $"El turno {turno.Numero} está a nombre de {turno.UsuarioActualNombre}. Haga el relevo primero.", Turno: turno.ADatos()));

        return (turno, null);
    }

    private static RespuestaCaja SinTurno() => new(CodigoResultadoCaja.TurnoNoAbierto, "La caja no tiene turno abierto.");

    private static RespuestaCaja Rechazo(ResultadoPermiso permiso, string codigoPermiso, string mensaje) =>
        permiso.AutorizacionRechazada
            ? new RespuestaCaja(CodigoResultadoCaja.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", codigoPermiso)
            : new RespuestaCaja(CodigoResultadoCaja.RequiereAutorizacion, mensaje, codigoPermiso);

    private static UsuarioAuditoria? Autorizador(ResultadoPermiso permiso) =>
        permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null;
}
