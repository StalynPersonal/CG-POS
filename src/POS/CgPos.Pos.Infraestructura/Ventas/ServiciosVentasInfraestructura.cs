using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Sincronizacion;
using CgPos.Pos.Infraestructura.Catalogo;
using CgPos.Pos.Infraestructura.Entregas;
using CgPos.Pos.Infraestructura.Fidelidad;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Ventas;

internal static class ConversionesVenta
{
    public static DatosTurno ADatos(this Turno turno) =>
        new(turno.Id, turno.Numero, turno.FechaOperacion, turno.CajaId, turno.UsuarioActualId, turno.UsuarioActualNombre, turno.FondoInicial, turno.Estado, turno.AbiertoEn);

    public static DatosVenta ADatos(this Venta venta, decimal montoIdentificacion, DocumentoElectronico? documento = null, DateOnly? venceSecuencia = null)
    {
        var totales = venta.CalcularTotales();

        var lineas = venta.Lineas
            .OrderBy(l => l.LineaAnuladaNumero ?? l.NumeroLinea)
            .ThenBy(l => l.EsReverso ? 1 : 0)
            .ThenBy(l => l.NumeroLinea)
            .Select(l => new DatosLineaVenta(
                l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo, l.UnidadMedidaCodigo,
                l.DecimalesCantidad, l.Cantidad, l.PrecioUnitario, l.ImporteConImpuesto, l.PorcentajeImpuesto, l.Lista, l.MotivoPrecio,
                l.LeidaDeBalanza, l.EsReverso, l.LineaAnuladaNumero, l.Anulada, l.Serial,
                l.PromocionCodigo, l.PromocionNombre, l.PromocionDescripcion, l.DescuentoPromocion, l.PromocionDesactivada,
                l.DescuentoManual, l.DescuentoManualTipo, l.DescuentoManualValor, l.MotivoDescuento, l.DescuentoAutorizadoPorNombre,
                l.DescuentoFactura, l.ImporteBruto, l.PermiteDescuentoManual, l.SerialPendiente, l.EsReverso ? 0m : venta.CantidadEnEntregas(l.NumeroLinea)))
            .ToList();

        var cliente = venta.ClienteNombre is { } nombre
            ? new DatosClienteVenta(venta.ClienteId, venta.ClienteTipoDocumento, venta.ClienteDocumento, nombre)
            : null;

        var fidelidad = venta.FidelidadMiembroId is { } miembroId
            ? new DatosFidelidadVenta(miembroId, venta.FidelidadCedula!, venta.FidelidadNombre!, venta.FidelidadNivel, venta.PuntosAcumulados, venta.PuntosCanjeados)
            : null;

        return new DatosVenta(
            venta.Id,
            venta.NumeroTransaccion,
            venta.Estado,
            venta.TurnoId,
            venta.UsuarioNombre,
            venta.IniciadaEn,
            lineas,
            new DatosTotalesVenta(
                totales.Subtotal,
                totales.Impuesto,
                totales.Total,
                totales.CantidadLineas,
                totales.CantidadArticulos,
                totales.Desglose.Select(d => new DatosDesgloseImpuesto(d.Porcentaje, d.IndicadorFacturacion, d.Base, d.Impuesto, d.Total)).ToList(),
                totales.Descuento,
                totales.Retencion),
            venta.TipoComprobante,
            cliente,
            venta.LimiteCompra,
            venta.LimiteCompra is { } limite && totales.Total > limite,
            venta.TipoComprobante == TipoComprobante.FacturaConsumo && venta.ClienteDocumento is null && totales.Total >= montoIdentificacion,
            montoIdentificacion,
            venta.Moneda,
            venta.SimboloMoneda,
            venta.DescuentoFacturaTipo is { } tipoDescuento
                ? new DatosDescuentoFactura(
                    tipoDescuento,
                    venta.DescuentoFacturaValor ?? 0m,
                    venta.DescuentoFacturaLineas?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList(),
                    venta.MotivoDescuentoFactura,
                    venta.DescuentoFacturaAutorizadoPorNombre,
                    venta.Lineas.Sum(l => l.DescuentoFactura))
                : null,
            venta.Pagos.Count == 0
                ? null
                : venta.Pagos.OrderBy(p => p.Numero)
                    .Select(p => new DatosPagoVenta(p.Numero, p.FormaPagoId, p.FormaPagoCodigo, p.FormaPagoNombre, p.Tipo, p.Moneda, p.MontoRecibido,
                        p.TasaCambio, p.MontoAplicado, p.Referencia, p.BancoNombre, p.TipoTarjetaNombre, p.UltimosDigitos, p.AprobacionManual))
                    .ToList(),
            venta.TotalCobrado,
            venta.Devuelta,
            venta.RedondeoEfectivo,
            venta.CobradaEn,
            documento is null
                ? null
                : new DatosComprobanteElectronico(documento.Encf, documento.TipoComprobante, documento.CodigoSeguridad, documento.FechaFirma, documento.UrlTimbre,
                    documento.Estado, venceSecuencia),
            fidelidad,
            venta.DestinosEntrega.Count == 0 ? null : venta.DestinosEntrega.OrderBy(d => d.Numero).Select(d => d.ADatos()).ToList(),
            venta.ListaBodaNumero is { } numeroLista ? new DatosListaBodaVenta(numeroLista, venta.ListaBodaEvento ?? string.Empty) : null);
    }

    public static ArticuloParaVenta AArticuloParaVenta(this DatosArticuloVenta datos) =>
        new(
            datos.ArticuloId, datos.Codigo, datos.CodigoLeido, datos.Descripcion, datos.Tipo, datos.DepartamentoId, datos.PermiteDescuentoManual,
            datos.UnidadMedidaCodigo, datos.PermiteDecimales, datos.DecimalesCantidad, datos.ImpuestoId, datos.PorcentajeImpuesto,
            datos.IndicadorFacturacion, datos.PrecioDetalle, datos.PrecioMayor, datos.CantidadMinimaMayor, datos.PrecioMinimo,
            datos.PesoLeido, datos.PrecioLeido, datos.EsServicio, datos.CategoriaId, datos.MarcaId);

    public static CodigoResultadoVenta ACodigoResultado(this CodigoErrorVenta codigo) => codigo switch
    {
        CodigoErrorVenta.SinPrecio => CodigoResultadoVenta.SinPrecio,
        CodigoErrorVenta.RequiereBalanza => CodigoResultadoVenta.RequiereBalanza,
        CodigoErrorVenta.CantidadInvalida => CodigoResultadoVenta.CantidadInvalida,
        CodigoErrorVenta.LineaNoEncontrada => CodigoResultadoVenta.LineaNoEncontrada,
        CodigoErrorVenta.MotivoRequerido => CodigoResultadoVenta.MotivoRequerido,
        CodigoErrorVenta.ComprobanteNoPermitido => CodigoResultadoVenta.ComprobanteNoPermitido,
        CodigoErrorVenta.DocumentoRequerido => CodigoResultadoVenta.DocumentoRequerido,
        CodigoErrorVenta.SinLineas => CodigoResultadoVenta.SinLineas,
        CodigoErrorVenta.RequiereSerial => CodigoResultadoVenta.RequiereSerial,
        CodigoErrorVenta.SerialDuplicado => CodigoResultadoVenta.SerialDuplicado,
        CodigoErrorVenta.ArticuloEnOferta => CodigoResultadoVenta.ArticuloEnOferta,
        CodigoErrorVenta.DescuentoNoPermitido => CodigoResultadoVenta.DescuentoNoPermitido,
        CodigoErrorVenta.DescuentoInvalido => CodigoResultadoVenta.DescuentoInvalido,
        CodigoErrorVenta.PagoInvalido => CodigoResultadoVenta.PagoInvalido,
        CodigoErrorVenta.PagoInsuficiente => CodigoResultadoVenta.PagoInsuficiente,
        CodigoErrorVenta.DevueltaNoPermitida => CodigoResultadoVenta.DevueltaNoPermitida,
        CodigoErrorVenta.EntregaInvalida => CodigoResultadoVenta.EntregaInvalida,
        _ => CodigoResultadoVenta.VentaNoEditable,
    };
}

internal sealed class ServicioTurnos(
    ContextoDatosPos contexto,
    GeneradorSecuencias secuencias,
    IParametros parametros,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioTurnos
{
    public async Task<DatosEstadoTurno> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        var fondoSugerido = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.FondoPredeterminado, sesion.CajaId, cancelacion);

        return new DatosEstadoTurno(
            turno?.ADatos(),
            fondoSugerido,
            turno is null && sesion.TienePermiso(CatalogoPermisos.AbrirTurno),
            turno is not null && turno.UsuarioActualId != sesion.UsuarioId);
    }

    public async Task<RespuestaTurno> AbrirAsync(SesionUsuario sesion, decimal? fondoInicial, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.AbrirTurno))
            return new RespuestaTurno(CodigoResultadoTurno.SinPermiso, "No tiene permiso para abrir turno.", null);

        var abierto = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (abierto is not null)
            return new RespuestaTurno(CodigoResultadoTurno.YaExisteTurnoAbierto, $"La caja ya tiene abierto el turno {abierto.Numero} de {abierto.UsuarioActualNombre}.", abierto.ADatos());

        var cajaOperativa = await (
                from caja in contexto.Cajas
                join sucursal in contexto.Sucursales on caja.SucursalId equals sucursal.Id
                where caja.Id == sesion.CajaId
                select caja.Habilitada && sucursal.Activa)
            .SingleOrDefaultAsync(cancelacion);
        if (!cajaOperativa)
            return new RespuestaTurno(CodigoResultadoTurno.CajaNoOperativa, "La caja está deshabilitada o su sucursal inactiva.", null);

        // El fondo es opcional (RF-4): sin fondo digitado ni sugerido configurado, el turno abre sin fondo.
        var fondo = fondoInicial ?? await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.FondoPredeterminado, sesion.CajaId, cancelacion) ?? 0m;
        if (fondo < 0)
            return new RespuestaTurno(CodigoResultadoTurno.FondoInvalido, "El fondo de caja no puede ser negativo.", null);

        var ahora = reloj.GetUtcNow();
        var numero = await secuencias.SiguienteAsync(sesion.CajaId, TiposSecuencia.Turno, cancelacion);
        var turno = Turno.Abrir(sesion.CajaId, sesion.SucursalId, numero, DateOnly.FromDateTime(reloj.GetLocalNow().DateTime),
            sesion.UsuarioId, sesion.Nombre, fondo, ahora);

        contexto.Turnos.Add(turno);
        auditoria.Registrar(new EntradaAuditoria("Caja.TurnoAbierto", "Turno", turno.Id.ToString(),
            Detalle: new { turno.Numero, Fondo = turno.FondoInicial, Caja = sesion.CajaCodigo },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // El índice único de turno abierto por caja ganó una carrera con otra apertura simultánea.
            contexto.ChangeTracker.Clear();
            return new RespuestaTurno(CodigoResultadoTurno.YaExisteTurnoAbierto, "La caja ya tiene un turno abierto.", null);
        }

        return new RespuestaTurno(CodigoResultadoTurno.Correcto, null, turno.ADatos());
    }
}

internal sealed class ServicioVentas(
    ContextoDatosPos contexto,
    IConsultaArticulos consultaArticulos,
    IConsultaDocumentos consultaDocumentos,
    IValidadorAutorizaciones autorizaciones,
    IParametros parametros,
    IBalanza balanza,
    ITerminalPago terminal,
    IImpresoraTicket impresora,
    IBandejaSalida bandejaSalida,
    IEmisorComprobantes emisorEcf,
    GeneradorSecuencias secuencias,
    IAuditoria auditoria,
    CgPos.Pos.Aplicacion.Sincronizacion.IClienteCentral central,
    TimeProvider reloj) : IServicioVentas, IServicioCobro
{
    // ---------- Cobro y periféricos (M08) ----------

    /// <summary>
    /// Cobra con el terminal. Si el terminal lee la tarjeta antes de cobrar (CS00 de CardNet), primero se le pide el BIN, se aplica el
    /// descuento del banco (RF-98) y se cobra ya con el total rebajado, para que el e-CF salga por lo que el cliente realmente pagó.
    /// </summary>
    /// <param name="pagaSaldo">La tarjeta cubre todo lo que falta: si el descuento baja el total, se le cobra menos.</param>
    public async Task<RespuestaOperacionTerminal> CobrarConTerminalAsync(SesionUsuario sesion, int ventaId, decimal monto, bool pagaSaldo = false,
        CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return new RespuestaOperacionTerminal(rechazo.Resultado, rechazo.Mensaje, null);
        if (monto <= 0)
            return new RespuestaOperacionTerminal(CodigoResultadoVenta.PagoInvalido, "El monto a cobrar con tarjeta debe ser mayor que cero.", null);

        string? aviso = null;
        DescuentoFacturaGuardado? anterior = null;
        if (terminal.ConsultaTarjeta)
        {
            var lectura = await terminal.ConsultarTarjetaAsync(cancelacion);
            if (lectura.SinConexion)
                return new RespuestaOperacionTerminal(CodigoResultadoVenta.TerminalSinConexion, lectura.Mensaje ?? "El terminal de pago no responde.", null);
            if (!lectura.Leida)
                return new RespuestaOperacionTerminal(CodigoResultadoVenta.TerminalRechazo, lectura.Mensaje ?? "No se leyó la tarjeta.", null);

            anterior = DescuentoFacturaGuardado.De(venta!);
            var totalAntes = venta!.CalcularTotales().Total;
            if (await AplicarDescuentoPorBinAsync(sesion, venta, lectura.Bin, cancelacion) is { } aplicado)
            {
                var rebaja = totalAntes - venta.CalcularTotales().Total;
                if (pagaSaldo)
                    monto = decimal.Round(Math.Max(0m, monto - rebaja), 2, MidpointRounding.AwayFromZero);
                aviso = $"{aplicado.Nombre}: {venta.SimboloMoneda}{rebaja:N2} de descuento por pagar con esa tarjeta.";
            }
            else
            {
                anterior = null;
            }

            if (monto <= 0)
                return new RespuestaOperacionTerminal(CodigoResultadoVenta.PagoInvalido,
                    "Con el descuento de la tarjeta ya no queda saldo por cobrar.", null, Datos(venta));
        }

        var totales = venta!.CalcularTotales();
        var impuesto = totales.Total > 0 ? decimal.Round(totales.Impuesto * monto / totales.Total, 2, MidpointRounding.AwayFromZero) : 0m;
        var resultado = await terminal.CobrarAsync(decimal.Round(monto, 2, MidpointRounding.AwayFromZero), impuesto, venta.NumeroTransaccion, cancelacion);
        var operacion = OperacionTerminal.Registrar(sesion.CajaId, venta.TurnoId, venta.Id, sesion.UsuarioId, TipoOperacionTerminal.Venta, monto,
            resultado.Aprobada, resultado.Aprobacion, resultado.UltimosDigitos, resultado.Marca, resultado.Mensaje, reloj.GetUtcNow(),
            referenciaTerminal: resultado.ReferenciaTerminal);
        contexto.OperacionesTerminal.Add(operacion);
        auditoria.Registrar(new EntradaAuditoria(resultado.Aprobada ? "Cobro.TarjetaAprobada" : "Cobro.TarjetaNoAprobada", TipoEntidadVenta, venta.NumeroTransaccion,
            Detalle: new { operacion.Id, operacion.Monto, operacion.Aprobacion, operacion.Marca, resultado.SinConexion, resultado.Mensaje },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));

        // La tarjeta no se aprobó: el descuento de ese banco no se queda puesto, el cliente puede pagar con otra.
        if (!resultado.Aprobada && anterior is not null)
        {
            anterior.Restaurar(venta, reloj.GetUtcNow());
            aviso = null;
        }

        await contexto.SaveChangesAsync(cancelacion);

        var datos = DatosOperacion(operacion, resultado.SinConexion);
        return resultado.Aprobada
            ? new RespuestaOperacionTerminal(CodigoResultadoVenta.Correcto, aviso, datos, aviso is null ? null : Datos(venta))
            : new RespuestaOperacionTerminal(resultado.SinConexion ? CodigoResultadoVenta.TerminalSinConexion : CodigoResultadoVenta.TerminalRechazo,
                resultado.Mensaje, datos, anterior is null ? null : Datos(venta));
    }

    /// <summary>El descuento de factura tal como estaba, para devolverlo si la tarjeta no aprueba.</summary>
    private sealed record DescuentoFacturaGuardado(TipoDescuento? Tipo, decimal? Valor, string? Lineas, string? Motivo, int? AutorizadoPorId,
        string? AutorizadoPorNombre)
    {
        public static DescuentoFacturaGuardado De(Venta venta) => new(venta.DescuentoFacturaTipo, venta.DescuentoFacturaValor, venta.DescuentoFacturaLineas,
            venta.MotivoDescuentoFactura, venta.DescuentoFacturaAutorizadoPorId, venta.DescuentoFacturaAutorizadoPorNombre);

        public void Restaurar(Venta venta, DateTimeOffset ahora)
        {
            venta.QuitarDescuentoFactura(ahora);
            if (Tipo is { } tipo && Valor is { } valor && Motivo is { Length: > 0 } motivo)
                venta.AplicarDescuentoFactura(tipo, valor, LineasSeleccionadas(), motivo, AutorizadoPorId, AutorizadoPorNombre, ahora);
        }

        private List<int>? LineasSeleccionadas() =>
            Lineas is { Length: > 0 } lineas
                ? lineas.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList()
                : null;
    }

    public async Task<RespuestaOperacionTerminal> AnularUltimaOperacionAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return new RespuestaOperacionTerminal(rechazo.Resultado, rechazo.Mensaje, null);

        var ultima = await contexto.OperacionesTerminal
            .Where(o => o.VentaId == ventaId && o.Tipo == TipoOperacionTerminal.Venta && o.Estado == EstadoOperacionTerminal.Aprobada && !o.UsadaEnCobro)
            .OrderByDescending(o => o.Fecha)
            .FirstOrDefaultAsync(cancelacion);
        if (ultima is null)
            return new RespuestaOperacionTerminal(CodigoResultadoVenta.OperacionTerminalInvalida, "No hay una tarjeta aprobada pendiente de aplicar para anular.", null);

        var resultado = await terminal.AnularAsync(ultima.Aprobacion ?? string.Empty, ultima.ReferenciaTerminal, ultima.Monto, cancelacion);
        if (!resultado.Aprobada)
            return new RespuestaOperacionTerminal(resultado.SinConexion ? CodigoResultadoVenta.TerminalSinConexion : CodigoResultadoVenta.TerminalRechazo,
                resultado.Mensaje ?? "El terminal no anuló la operación.", DatosOperacion(ultima, resultado.SinConexion));

        ultima.MarcarAnulada();
        var anulacion = OperacionTerminal.Registrar(sesion.CajaId, ultima.TurnoId, ultima.VentaId, sesion.UsuarioId, TipoOperacionTerminal.Anulacion, ultima.Monto,
            true, resultado.Aprobacion, ultima.UltimosDigitos, ultima.Marca, resultado.Mensaje, reloj.GetUtcNow(), ultima.Id, ultima.ReferenciaTerminal);
        contexto.OperacionesTerminal.Add(anulacion);
        auditoria.Registrar(new EntradaAuditoria("Cobro.TarjetaAnulada", TipoEntidadVenta, venta!.NumeroTransaccion,
            Detalle: new { Anulada = ultima.Id, ultima.Monto, ultima.Aprobacion, AprobacionAnulacion = resultado.Aprobacion },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaOperacionTerminal(CodigoResultadoVenta.Correcto, resultado.Mensaje, DatosOperacion(anulacion, false));
    }

    public async Task<RespuestaCobro> CobrarAsync(SesionUsuario sesion, int ventaId, IReadOnlyList<SolicitudPago> pagos, Guid? autorizacionId,
        CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return new RespuestaCobro(rechazo.Resultado, rechazo.Mensaje, null, null, rechazo.Venta, rechazo.PermisoRequerido);
        if (pagos is not { Count: > 0 })
            return new RespuestaCobro(CodigoResultadoVenta.PagoInvalido, "Agregue al menos una forma de pago.", null, null, Datos(venta!));

        var ahora = reloj.GetUtcNow();
        venta!.RecalcularPromociones(await PromocionesAsync(cancelacion), venta.SucursalId, reloj.GetLocalNow());

        // La aprobación manual de tarjeta por contingencia de la pasarela requiere permiso (RF-213).
        ResultadoPermiso? permiso = null;
        if (pagos.Any(p => p.AprobacionManual))
        {
            permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.AprobacionManualTarjeta, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
            if (!permiso.Permitido)
                return new RespuestaCobro(
                    permiso.AutorizacionRechazada ? CodigoResultadoVenta.AutorizacionInvalida : CodigoResultadoVenta.RequiereAutorizacion,
                    "La aprobación manual de tarjeta requiere autorización de un supervisor.", null, null, Datos(venta), CatalogoPermisos.AprobacionManualTarjeta);
        }

        var solicitados = await ArmarPagosAsync(venta, pagos, ahora, cancelacion);
        if (solicitados.Rechazo is { } pagoRechazado)
            return new RespuestaCobro(pagoRechazado.Codigo, pagoRechazado.Mensaje, null, null, Datos(venta));

        // El canje de puntos requiere permiso o clave de supervisor (RF-239); el saldo ya se validó.
        ResultadoPermiso? permisoCanje = null;
        if (solicitados.PuntosCanjeados > 0)
        {
            permisoCanje = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.CanjearPuntos, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
            if (!permisoCanje.Permitido)
            {
                await LiberarReservasAsync(solicitados.NotasExternas, cancelacion);
                return new RespuestaCobro(
                    permisoCanje.AutorizacionRechazada ? CodigoResultadoVenta.AutorizacionInvalida : CodigoResultadoVenta.RequiereAutorizacion,
                    "El canje de puntos requiere autorización de un supervisor.", null, null, Datos(venta), CatalogoPermisos.CanjearPuntos);
            }
        }

        var paso = await parametros.ObtenerDecimalAsync(ClavesParametros.PasoRedondeoEfectivo, sesion.CajaId, cancelacion);
        ResultadoCobro resultado;
        try
        {
            // El porcentaje vigente al cobrar es el que manda, por si lo cambiaron después de elegir el comprobante.
            venta.AplicarRetencionLey(
                await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.PorcentajeRetencionLey3223, sesion.CajaId, cancelacion) ?? 0m, ahora);
            resultado = venta.Cobrar(solicitados.Pagos, paso, _montoIdentificacion, sesion.UsuarioId, sesion.Nombre, ahora);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            contexto.ChangeTracker.Clear();
            await LiberarReservasAsync(solicitados.NotasExternas, cancelacion);
            var ventaActual = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta.Id, cancelacion);
            return new RespuestaCobro(excepcion.Codigo.ACodigoResultado(), excepcion.Message, null, null, Datos(ventaActual));
        }

        foreach (var operacion in solicitados.Operaciones)
            operacion.MarcarUsada();

        var movimientosPuntos = await PuntosDelCobroAsync(venta, resultado, solicitados.PuntosCanjeados, ahora, cancelacion);

        // El e-CF se emite y firma dentro de la misma transacción del cobro: si algo falla no se consume la secuencia.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        // Sin e-NCF disponible o sin poder firmar no se factura: el cobro se rechaza y no se consume nada.
        EmisionEcf? emision;
        try
        {
            emision = await emisorEcf.EmitirAsync(venta, cancelacion);
        }
        catch (EmisionEcfExcepcion excepcion)
        {
            await transaccion.RollbackAsync(cancelacion);
            contexto.ChangeTracker.Clear();
            await LiberarReservasAsync(solicitados.NotasExternas, cancelacion);
            var ventaActual = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta.Id, cancelacion);
            return new RespuestaCobro(excepcion.Codigo, excepcion.Message, null, null, Datos(ventaActual));
        }

        // Consumo de notas de crédito en la misma transacción del cobro (RF-38).
        var saldosNotas = new List<(Devolucion Nota, decimal Saldo)>();
        foreach (var (nota, monto) in solicitados.NotasCredito)
        {
            var saldo = nota.Consumir(venta.Id, venta.NumeroTransaccion, venta.CajaId, monto, DateOnly.FromDateTime(reloj.GetLocalNow().DateTime),
                solicitados.DiasVigenciaNotas, ahora);
            saldosNotas.Add((nota, saldo));
            bandejaSalida.Encolar("NotaCredito.Consumida", nota.Numero,
                new DocumentoConsumoNotaCredito(nota.Numero, nota.Encf, venta.NumeroTransaccion, monto, saldo, ahora));
        }

        // Notas de crédito de otras sucursales: el Central ya retuvo su saldo y aquí se le informa el consumo (RF-43).
        foreach (var externa in solicitados.NotasExternas)
            bandejaSalida.Encolar("NotaCredito.Consumida", externa.Numero,
                new DocumentoConsumoNotaCredito(externa.Numero, externa.Encf, venta.NumeroTransaccion, externa.Monto, 0m, ahora));

        // Pendientes de entrega y envío: un documento numerado por destino, en la bandeja de salida (RF-249, RN-14).
        var pendientes = new List<PendienteEntrega>();
        if (venta.DestinosEntrega.Count > 0)
        {
            // Único en toda la empresa, como el número de transacción: sucursal, caja y secuencia de la caja.
            var codigoSucursal = await contexto.Sucursales.Where(s => s.Id == venta.SucursalId).Select(s => s.Codigo).SingleAsync(cancelacion);
            var digitos = await NumeracionDocumentos.DigitosAsync(parametros, venta.CajaId, cancelacion);
            foreach (var destino in venta.DestinosEntrega.OrderBy(d => d.Numero))
            {
                var secuenciaPendiente = await secuencias.SiguienteAsync(venta.CajaId, TiposSecuencia.PendienteEntrega, cancelacion);
                var pendiente = PendienteEntrega.Crear(venta, destino, NumeroDocumento.Formatear(codigoSucursal, sesion.CajaCodigo, TipoDocumentoNumerado.PendienteEntrega, secuenciaPendiente, digitos), ahora);
                contexto.PendientesEntrega.Add(pendiente);
                pendientes.Add(pendiente);
                bandejaSalida.Encolar("Entregas.PendienteCreado", pendiente.Numero, await contexto.PendienteAsync(pendiente, cancelacion));
            }
        }

        // Puntos acumulados y canjeados: van al Central, que lleva el saldo oficial.
        foreach (var movimiento in movimientosPuntos)
        {
            contexto.MovimientosPuntos.Add(movimiento);
            bandejaSalida.Encolar("Fidelidad.MovimientoPuntos", DocumentosParaCentral.ReferenciaPuntos(movimiento), DocumentosParaCentral.MovimientoPuntos(movimiento));
        }

        // Documento, e-CF, mensaje para el Central y auditoría en la misma transacción (RF-270).
        var datosVenta = venta.ADatos(_montoIdentificacion, emision?.Documento, emision?.VenceSecuencia);
        bandejaSalida.Encolar("Venta.Cobrada", venta.NumeroTransaccion,
            await contexto.VentaCobradaAsync(venta, datosVenta, emision?.ParaCentral, ahora, cancelacion));
        auditoria.Registrar(new EntradaAuditoria("Ventas.Cobrada", TipoEntidadVenta, venta.NumeroTransaccion,
            Detalle: new
            {
                resultado.Total,
                resultado.TotalCobrado,
                resultado.Pagado,
                resultado.Devuelta,
                resultado.Redondeo,
                Pagos = venta.Pagos.Select(p => new { p.FormaPagoCodigo, p.MontoRecibido, p.MontoAplicado, p.Referencia, p.AprobacionManual }),
                venta.FidelidadCedula,
                venta.PuntosAcumulados,
                venta.PuntosCanjeados,
            },
            Motivo: (permiso ?? permisoCanje)?.Motivo,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: (permiso ?? permisoCanje) is { } autorizo ? Autorizador(autorizo) : null));

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }
        catch
        {
            // Si el cobro no quedó guardado, su XML no debe quedar en pendientes.
            if (emision is not null)
                emisorEcf.DescartarArchivo(emision);
            throw;
        }

        // Periféricos después de guardar: un fallo de impresora o gaveta nunca deshace el cobro.
        var encabezado = await EncabezadoTicketAsync(sesion, cancelacion);
        var impresion = await impresora.ImprimirAsync(GeneradorTicket.Generar(encabezado, datosVenta, esCopia: false), cancelacion);

        // Voucher con el saldo que queda de cada nota de crédito usada (RF-43).
        foreach (var (nota, saldo) in saldosNotas.Where(n => n.Saldo > 0))
            await impresora.ImprimirAsync(GeneradorTicket.GenerarSaldoNotaCredito(encabezado, nota.Encf ?? nota.Numero, nota.ClienteNombre, saldo, nota.Moneda,
                nota.VenceEn(solicitados.DiasVigenciaNotas),
                venta.NumeroTransaccion), cancelacion);

        // Voucher de cada pendiente con copia para el cliente y para el despacho (RF-55, RF-88).
        var politicaPendiente = pendientes.Count > 0 ? await parametros.ObtenerAsync(ClavesParametros.PoliticaPendiente, sesion.CajaId, cancelacion) : null;
        foreach (var pendiente in pendientes.Select(p => p.ADatos()))
        {
            await impresora.ImprimirAsync(GeneradorTicket.GenerarPendiente(encabezado, pendiente, copiaCliente: true, politicaPendiente), cancelacion);
            await impresora.ImprimirAsync(GeneradorTicket.GenerarPendiente(encabezado, pendiente, copiaCliente: false, politicaPendiente), cancelacion);
        }

        var gaveta = resultado.AbreGaveta ? await impresora.AbrirGavetaAsync(cancelacion) : null;
        var avisos = new[] { impresion.Correcto ? null : impresion.Mensaje, gaveta is { Correcto: false } ? gaveta.Mensaje : null }
            .Where(aviso => aviso is not null)
            .ToList();

        var (turno, _) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        var nueva = await IniciarVentaAsync(sesion, turno!, cancelacion);

        return new RespuestaCobro(
            CodigoResultadoVenta.Correcto,
            avisos.Count > 0 ? string.Join(" ", avisos) : null,
            new DatosCobro(venta.NumeroTransaccion, resultado.Total, resultado.TotalCobrado, resultado.Pagado, resultado.Devuelta, resultado.Redondeo,
                datosVenta.Pagos ?? [], impresion.Correcto, gaveta?.Correcto ?? false, pendientes.Select(p => p.Numero).ToList()),
            Datos(nueva),
            datosVenta);
    }

    public async Task<RespuestaImpresion> ReimprimirUltimoAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var ultima = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).Include(v => v.Pagos)
            .Where(v => v.CajaId == sesion.CajaId && v.Estado == EstadoVenta.Cobrada)
            .OrderByDescending(v => v.CobradaEn)
            .FirstOrDefaultAsync(cancelacion);
        if (ultima is null)
            return new RespuestaImpresion(false, "No hay ventas cobradas para reimprimir.");

        var documento = await contexto.DocumentosElectronicos.AsNoTracking().SingleOrDefaultAsync(d => d.VentaId == ultima.Id, cancelacion);
        _montoIdentificacion = await parametros.ObtenerDecimalAsync(ClavesParametros.MontoIdentificacionConsumo, sesion.CajaId, cancelacion);
        var impresion = await impresora.ImprimirAsync(
            GeneradorTicket.Generar(await EncabezadoTicketAsync(sesion, cancelacion), ultima.ADatos(_montoIdentificacion, documento), esCopia: true), cancelacion);
        auditoria.Registrar(new EntradaAuditoria("Ventas.Reimpresion", TipoEntidadVenta, ultima.NumeroTransaccion,
            Detalle: new { impresion.Correcto }, Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaImpresion(impresion.Correcto, impresion.Correcto ? $"Copia del ticket {ultima.NumeroTransaccion} enviada a la impresora." : impresion.Mensaje);
    }

    public async Task<RespuestaVenta> AbrirGavetaAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.AbrirGaveta, autorizacionId, "Caja", sesion.CajaCodigo.ToString("00"), cancelacion);
        if (!permiso.Permitido)
            return permiso.AutorizacionRechazada
                ? new RespuestaVenta(CodigoResultadoVenta.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", null, CatalogoPermisos.AbrirGaveta)
                : new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "Abrir la gaveta sin venta requiere autorización de un supervisor.", null, CatalogoPermisos.AbrirGaveta);

        auditoria.Registrar(new EntradaAuditoria("Caja.GavetaAbierta", "Caja", sesion.CajaCodigo.ToString("00"),
            Motivo: permiso.Motivo, Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        var gaveta = await impresora.AbrirGavetaAsync(cancelacion);
        return gaveta.Correcto
            ? new RespuestaVenta(CodigoResultadoVenta.Correcto, "Gaveta abierta.", null)
            : new RespuestaVenta(CodigoResultadoVenta.Correcto, gaveta.Mensaje, null);
    }

    /// <summary>Nota de crédito de otra sucursal: el Central la validó y retuvo su saldo mientras esta caja cobra (RF-43).</summary>
    /// <summary>Nota de crédito de otra sucursal con saldo reservado en el Central para la factura <paramref name="VentaNumero"/>.</summary>
    private sealed record NotaCreditoExternaUsada(string Numero, string Encf, decimal Monto, string VentaNumero);

    private sealed record PagosArmados(
        IReadOnlyList<PagoSolicitado> Pagos,
        IReadOnlyList<OperacionTerminal> Operaciones,
        IReadOnlyList<(Devolucion Nota, decimal Monto)> NotasCredito,
        IReadOnlyList<NotaCreditoExternaUsada> NotasExternas,
        (CodigoResultadoVenta Codigo, string Mensaje)? Rechazo,
        int PuntosCanjeados = 0,
        int DiasVigenciaNotas = 1)
    {
        public static PagosArmados Rechazado(CodigoResultadoVenta codigo, string mensaje) => new([], [], [], [], (codigo, mensaje));
    }

    /// <summary>
    /// Puntos de la venta cobrada de un miembro (RF-238, RF-239): acumula según las reglas vigentes y el factor de su nivel sobre lo que no
    /// pagó con puntos, y registra el canje. Sin reglas configuradas no acumula.
    /// </summary>
    private async Task<IReadOnlyList<MovimientoPuntos>> PuntosDelCobroAsync(Venta venta, ResultadoCobro resultado, int puntosCanjeados, DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        if (!venta.TieneFidelidad)
            return [];

        var miembro = await contexto.MiembrosFidelidad.AsNoTracking().SingleAsync(m => m.Id == venta.FidelidadMiembroId, cancelacion);
        var reglas = await contexto.ReglasAcumulacion.AsNoTracking().Where(r => r.Activa).ToListAsync(cancelacion);
        var factor = miembro.NivelId is { } nivelId
            ? await contexto.NivelesFidelidad.AsNoTracking().Where(n => n.Id == nivelId && n.Activo).Select(n => (decimal?)n.FactorAcumulacion).FirstOrDefaultAsync(cancelacion)
            : null;

        var pagadoConPuntos = venta.Pagos.Where(p => p.Tipo == TipoFormaPago.Puntos).Sum(p => p.MontoAplicado);
        var proporcion = resultado.TotalCobrado <= 0 ? 0m : 1m - pagadoConPuntos / resultado.TotalCobrado;
        var ahoraLocal = reloj.GetLocalNow();
        var acumulados = ReglasFidelidad.CalcularPuntos(
            venta.Lineas.Where(l => l.EstaActiva).Select(l => new LineaPuntuable(l.ArticuloId, l.DepartamentoId, l.PromocionId, l.ImporteConImpuesto, l.CategoriaId, l.MarcaId)),
            reglas, factor ?? 1m, proporcion, ahoraLocal);
        venta.RegistrarPuntos(acumulados, puntosCanjeados);

        var movimientos = new List<MovimientoPuntos>();
        if (acumulados > 0)
        {
            var meses = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.MesesVigenciaPuntos, venta.CajaId, cancelacion);
            DateOnly? vence = meses is { } vigencia ? DateOnly.FromDateTime(ahoraLocal.DateTime).AddMonths((int)vigencia) : null;
            movimientos.Add(MovimientoPuntos.Acumulacion(miembro, acumulados, venta.Id, venta.NumeroTransaccion, venta.CajaId, ahora, vence));
        }

        if (puntosCanjeados > 0)
            movimientos.Add(MovimientoPuntos.Canje(miembro, puntosCanjeados, venta.Id, venta.NumeroTransaccion, venta.CajaId, ahora));

        return movimientos;
    }

    /// <summary>Completa cada pago con los datos del maestro, la tasa del día y la aprobación registrada del terminal.</summary>
    /// <summary>Arma los pagos y, si alguno se rechaza, devuelve al Central el saldo que ya se hubiera reservado de notas de otras sucursales (RF-43).</summary>
    private async Task<PagosArmados> ArmarPagosAsync(Venta venta, IReadOnlyList<SolicitudPago> pagos, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var externas = new List<NotaCreditoExternaUsada>();
        var armados = await ArmarPagosAsync(venta, pagos, ahora, externas, cancelacion);
        if (armados.Rechazo is not null)
            await LiberarReservasAsync(externas, cancelacion);

        return armados;
    }

    private async Task<PagosArmados> ArmarPagosAsync(Venta venta, IReadOnlyList<SolicitudPago> pagos, DateTimeOffset ahora,
        List<NotaCreditoExternaUsada> externas, CancellationToken cancelacion)
    {
        var idsFormas = pagos.Select(p => p.FormaPagoId).Distinct().ToList();
        var formas = await contexto.FormasPago.AsNoTracking().Where(f => idsFormas.Contains(f.Id) && f.Activa).ToDictionaryAsync(f => f.Id, cancelacion);
        var idsBancos = pagos.Select(p => p.BancoId).OfType<int>().Distinct().ToList();
        var bancos = await contexto.Bancos.AsNoTracking().Where(b => idsBancos.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Nombre, cancelacion);
        var idsTipos = pagos.Select(p => p.TipoTarjetaId).OfType<int>().Distinct().ToList();
        var tipos = await contexto.TiposTarjeta.AsNoTracking().Where(t => idsTipos.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nombre, cancelacion);
        var tasas = await contexto.TasasCambio.AsNoTracking().ToListAsync(cancelacion);
        var idsOperaciones = pagos.Select(p => p.OperacionTerminalId).OfType<int>().Distinct().ToList();
        var operaciones = await contexto.OperacionesTerminal.Where(o => idsOperaciones.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancelacion);

        var solicitados = new List<PagoSolicitado>();
        var usadas = new List<OperacionTerminal>();
        var notas = new List<(Devolucion Nota, decimal Monto)>();
        var puntosCanje = 0;
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        int? diasVigenciaNotas = null;
        foreach (var pago in pagos)
        {
            if (!formas.TryGetValue(pago.FormaPagoId, out var forma))
                return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, "La forma de pago no existe o está inactiva.");

            var referencia = pago.Referencia;
            var ultimosDigitos = pago.UltimosDigitos;
            string? marca = null;
            int? operacionId = null;

            if (forma.Tipo == TipoFormaPago.Tarjeta && !pago.AprobacionManual)
            {
                if (pago.OperacionTerminalId is not { } id || !operaciones.TryGetValue(id, out var operacion) || !operacion.DisponibleParaCobro
                    || operacion.VentaId != venta.Id || operacion.Monto != decimal.Round(pago.MontoRecibido, 2, MidpointRounding.AwayFromZero)
                    || usadas.Contains(operacion))
                    return PagosArmados.Rechazado(CodigoResultadoVenta.OperacionTerminalInvalida,
                        "Pase la tarjeta por el terminal por el monto exacto, o registre la aprobación manual si la pasarela no responde.");

                referencia = operacion.Aprobacion;
                ultimosDigitos = operacion.UltimosDigitos;
                marca = operacion.Marca;
                operacionId = operacion.Id;
                usadas.Add(operacion);
            }

            // Nota de crédito por su e-NCF (RF-36): de esta caja o, si no la tiene, validada y reservada en el Central (RF-43).
            if (forma.Tipo == TipoFormaPago.NotaCredito)
            {
                var codigo = pago.Referencia?.Trim().ToUpperInvariant();
                var nota = string.IsNullOrEmpty(codigo)
                    ? null
                    : notas.Select(n => n.Nota).FirstOrDefault(n => n.Encf == codigo || n.Numero == codigo)
                        // También por su número: así una nota interna escaneada se rechaza con su motivo en vez de buscarse en el Central.
                        ?? await contexto.Devoluciones.Include(d => d.Consumos).FirstOrDefaultAsync(d => d.Encf == codigo || d.Numero == codigo, cancelacion);
                var monto = decimal.Round(pago.MontoRecibido, 2, MidpointRounding.AwayFromZero);

                if (nota is null)
                {
                    if (string.IsNullOrEmpty(codigo))
                        return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, "Escanee el código de la nota de crédito.");

                    var (externa, rechazoExterna) = await ReservarEnCentralAsync(venta.NumeroTransaccion, codigo, monto, externas, cancelacion);
                    if (rechazoExterna is not null)
                        return rechazoExterna;

                    externas.Add(externa!);
                    referencia = externa!.Encf;
                }
                else
                {
                    var disponible = nota.Saldo - notas.Where(n => n.Nota == nota).Sum(n => n.Monto);
                    diasVigenciaNotas ??= await parametros.ObtenerEnteroAsync(ClavesParametros.DiasVigenciaNotaCredito, venta.CajaId, cancelacion);
                    var problema = nota.EsInterna
                        ? $"La nota de crédito {codigo} es interna: solo ajusta la factura, no se usa como forma de pago."
                        : nota.EstadoSaldo(hoy, diasVigenciaNotas.Value) switch
                    {
                        EstadoNotaCredito.Consumida => $"La nota de crédito {codigo} ya fue consumida.",
                        EstadoNotaCredito.Vencida => $"La nota de crédito {codigo} venció el {nota.VenceEn(diasVigenciaNotas.Value):dd/MM/yyyy}.",
                        _ when monto > disponible => $"La nota de crédito {codigo} solo tiene {venta.SimboloMoneda}{disponible:N2} disponibles.",
                        _ => null,
                    };
                    if (problema is not null)
                        return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, problema);

                    notas.Add((nota, monto));
                    referencia = nota.Encf;
                }
            }

            // Puntos del miembro de la venta al valor configurado, con saldo, mínimo y tope sin conexión (RF-239, RF-243).
            if (forma.Tipo == TipoFormaPago.Puntos)
            {
                if (!venta.TieneFidelidad)
                    return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, "Para canjear puntos asigne primero la cédula del cliente en el programa de fidelidad.");

                var valorPunto = await parametros.ObtenerDecimalAsync(ClavesParametros.ValorPuntoFidelidad, venta.CajaId, cancelacion);
                var puntos = ReglasFidelidad.PuntosParaMonto(decimal.Round(pago.MontoRecibido, 2, MidpointRounding.AwayFromZero), valorPunto);
                var miembro = await contexto.MiembrosFidelidad.AsNoTracking().SingleAsync(m => m.Id == venta.FidelidadMiembroId, cancelacion);
                var disponibles = await contexto.SaldoPuntosAsync(miembro, hoy, cancelacion) - puntosCanje;
                var minimo = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.MinimoPuntosCanje, venta.CajaId, cancelacion);
                var maximo = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.MaximoPuntosCanjeSinConexion, venta.CajaId, cancelacion);

                if (puntos > disponibles)
                    return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido,
                        $"{miembro.Nombre} tiene {Math.Max(0, disponibles):N0} puntos disponibles; {venta.SimboloMoneda}{pago.MontoRecibido:N2} requieren {puntos:N0}.");
                if (minimo is { } puntosMinimos && puntos < puntosMinimos)
                    return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, $"El canje mínimo es de {puntosMinimos:N0} puntos.");
                if (maximo is { } puntosMaximos && puntosCanje + puntos > puntosMaximos)
                    return PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido,
                        $"Mientras la caja no confirme el saldo con el Central se canjean hasta {puntosMaximos:N0} puntos por transacción.");

                puntosCanje += puntos;
                referencia = $"{puntos} pts · {miembro.Cedula}";
            }

            var formaCobro = new FormaPagoParaCobro(forma.Id, forma.Codigo, forma.Nombre, forma.Tipo, forma.Moneda, forma.PermiteDevuelta,
                forma.RequiereReferencia, forma.RequiereBanco, forma.PermiteComprobanteFiscal, forma.AbreGaveta);

            solicitados.Add(new PagoSolicitado(
                formaCobro,
                pago.MontoRecibido,
                forma.Moneda == venta.Moneda ? null : TasaCambio.Vigente(tasas, forma.Moneda, ahora),
                referencia,
                pago.BancoId,
                pago.BancoId is { } bancoId && bancos.TryGetValue(bancoId, out var banco) ? banco : null,
                pago.TipoTarjetaId,
                pago.TipoTarjetaId is { } tipoId && tipos.TryGetValue(tipoId, out var tipo) ? tipo : marca,
                ultimosDigitos,
                pago.AprobacionManual,
                operacionId));
        }

        return new PagosArmados(solicitados, usadas, notas, externas, null, puntosCanje, diasVigenciaNotas ?? 1);
    }

    /// <summary>
    /// Nota de crédito que esta caja no tiene: el Central la valida y retiene su saldo mientras se cobra (RF-43). Sin comunicación no se acepta,
    /// porque el saldo de las notas de otras sucursales solo lo conoce el Central.
    /// </summary>
    private async Task<(NotaCreditoExternaUsada? Nota, PagosArmados? Rechazo)> ReservarEnCentralAsync(string ventaNumero, string codigo, decimal monto,
        IReadOnlyList<NotaCreditoExternaUsada> yaAplicadas, CancellationToken cancelacion)
    {
        var consulta = await central.ConsultarNotaCreditoAsync(codigo, cancelacion);
        if (consulta.Nota is not { } nota)
            return (null, PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, consulta.CentralRespondio
                ? $"La nota de crédito {codigo} no existe en esta caja ni en el Central."
                : $"La nota de crédito {codigo} no es de esta caja y el Central no responde: {consulta.Error}"));

        if (yaAplicadas.Any(aplicada => aplicada.Numero == nota.Numero))
            return (null, PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido, $"La nota de crédito {codigo} ya está aplicada en este cobro."));

        var reserva = await central.ReservarNotaCreditoAsync(nota.Numero, ventaNumero, monto, cancelacion);
        if (!reserva.Exitosa)
            return (null, PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido,
                reserva.Error ?? $"El Central no reservó el saldo de la nota de crédito {codigo}."));

        // El Central retiene lo que haya: si no alcanza, se devuelve y se pide el monto correcto.
        if (reserva.Monto < monto)
        {
            await central.LiberarReservaNotaCreditoAsync(nota.Numero, ventaNumero, cancelacion);
            return (null, PagosArmados.Rechazado(CodigoResultadoVenta.PagoInvalido,
                $"La nota de crédito {codigo} solo tiene {reserva.Monto:N2} disponibles en el Central."));
        }

        return (new NotaCreditoExternaUsada(nota.Numero, nota.Encf ?? nota.Numero, monto, ventaNumero), null);
    }

    /// <summary>Devuelve al Central el saldo retenido cuando el cobro no se completó.</summary>
    private async Task LiberarReservasAsync(IReadOnlyList<NotaCreditoExternaUsada> externas, CancellationToken cancelacion)
    {
        foreach (var externa in externas)
            await central.LiberarReservaNotaCreditoAsync(externa.Numero, externa.VentaNumero, cancelacion);
    }

    private Task<EncabezadoTicket> EncabezadoTicketAsync(SesionUsuario sesion, CancellationToken cancelacion) =>
        contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion);

    private static DatosOperacionTerminal DatosOperacion(OperacionTerminal operacion, bool sinConexion) =>
        new(operacion.Id, operacion.Estado == EstadoOperacionTerminal.Aprobada, sinConexion, operacion.Monto, operacion.Aprobacion, operacion.UltimosDigitos,
            operacion.Marca, operacion.Mensaje);

    // ---------- Venta ----------

    private const string TipoEntidadVenta = "Venta";

    /// <summary>Se lee de parámetros al validar el turno, que es el primer paso de toda operación.</summary>
    private decimal _montoIdentificacion;

    private IReadOnlyList<Promocion>? _promociones;

    public async Task<RespuestaVenta> ObtenerActualAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var venta = await VentaEnCursoAsync(sesion, turno!, cancelacion) ?? await IniciarVentaAsync(sesion, turno!, cancelacion);
        return Correcta(venta);
    }

    public async Task<RespuestaVenta> AgregarDesdeBalanzaAsync(SesionUsuario sesion, int ventaId, string codigo, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.RegistrarVenta))
            return new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "No tiene permiso para registrar ventas.", null, CatalogoPermisos.RegistrarVenta);

        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var codigoLimpio = codigo?.Trim() ?? string.Empty;
        var articulo = await consultaArticulos.BuscarPorCodigoAsync(codigoLimpio, cancelacion);
        if (articulo is null)
            return new RespuestaVenta(CodigoResultadoVenta.ArticuloNoEncontrado, $"No se encontró el artículo {codigoLimpio}.", Datos(venta!));
        if (articulo.Tipo != TipoArticulo.Pesado)
            return new RespuestaVenta(CodigoResultadoVenta.CantidadInvalida, $"{articulo.Descripcion} no se vende por peso.", Datos(venta!));

        var lectura = await balanza.LeerPesoAsync(cancelacion);
        if (lectura is not { Estable: true })
            return new RespuestaVenta(CodigoResultadoVenta.BalanzaSinLectura,
                lectura is null ? "La balanza no responde. Verifique que esté encendida y conectada." : "El peso no está estable. Espere a que la balanza se detenga.", Datos(venta!));

        if (ReglasBalanza.PesoNeto(lectura.Peso, articulo.Tara) is not { } neto)
            return new RespuestaVenta(CodigoResultadoVenta.BalanzaSinLectura,
                $"La balanza marca {lectura.Peso:0.000} {lectura.Unidad}: no queda peso del producto después de descontar el empaque ({articulo.Tara ?? 0:0.000}).", Datos(venta!));

        var pesado = articulo.AArticuloParaVenta() with { PesoLeido = neto, PrecioLeido = null };
        return await EjecutarAsync(venta!, () => venta!.AgregarArticulo(pesado, null, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> AgregarArticuloAsync(SesionUsuario sesion, int ventaId, string codigo, decimal? cantidad, string? serial = null,
        bool serialEnDespacho = false, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.RegistrarVenta))
            return new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "No tiene permiso para registrar ventas.", null, CatalogoPermisos.RegistrarVenta);

        if (!TryInterpretarEntrada(codigo, cantidad, out var codigoLimpio, out var cantidadFinal))
            return new RespuestaVenta(CodigoResultadoVenta.CantidadInvalida, "Formato no válido. Use código o cantidad*código (ej. 12*7891114119695).", null);

        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var articulo = await consultaArticulos.BuscarPorCodigoAsync(codigoLimpio, cancelacion);
        if (articulo is null)
            return new RespuestaVenta(CodigoResultadoVenta.ArticuloNoEncontrado, $"No se encontró el artículo {codigoLimpio}.", Datos(venta!));

        return await EjecutarAsync(venta!, () => venta!.AgregarArticulo(articulo.AArticuloParaVenta(), cantidadFinal, reloj.GetUtcNow(), serial, serialEnDespacho), cancelacion);
    }

    public async Task<RespuestaVenta> CambiarCantidadAsync(SesionUsuario sesion, int ventaId, int numeroLinea, decimal cantidad, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.CambiarCantidad(numeroLinea, cantidad, reloj.GetUtcNow()), cancelacion);
    }

    public Task<RespuestaVenta> EliminarLineaAsync(SesionUsuario sesion, int ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EliminarAsync(sesion, ventaId, autorizacionId, venta => venta.EliminarLinea(numeroLinea, reloj.GetUtcNow()), cancelacion);

    public Task<RespuestaVenta> EliminarPorCodigoAsync(SesionUsuario sesion, int ventaId, string codigo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EliminarAsync(sesion, ventaId, autorizacionId, venta => venta.EliminarPorCodigo(codigo, reloj.GetUtcNow()), cancelacion);

    public Task<RespuestaVenta> LimpiarAsync(SesionUsuario sesion, int ventaId, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        AnularYContinuarAsync(sesion, ventaId, CatalogoPermisos.LimpiarPantalla, "Pantalla limpiada", autorizacionId, "Ventas.PantallaLimpiada", cancelacion);

    public async Task<RespuestaVenta> AnularAsync(SesionUsuario sesion, int ventaId, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        // Sin autorización el motivo es obligatorio; con autorización se toma el motivo que dio el supervisor.
        if (string.IsNullOrWhiteSpace(motivo) && autorizacionId is null && sesion.TienePermiso(CatalogoPermisos.AnularVenta))
            return new RespuestaVenta(CodigoResultadoVenta.MotivoRequerido, "Indique el motivo de la anulación.", null);

        return await AnularYContinuarAsync(sesion, ventaId, CatalogoPermisos.AnularVenta, motivo, autorizacionId, "Ventas.Anulada", cancelacion);
    }

    public async Task<RespuestaVenta> AsignarClienteAsync(SesionUsuario sesion, int ventaId, string documento, string? nombre, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var consulta = await consultaDocumentos.ConsultarAsync(documento ?? string.Empty, cancelacion);

        if (!consulta.FormatoValido || consulta.Tipo is null)
            return new RespuestaVenta(CodigoResultadoVenta.DocumentoInvalido, "Digite un RNC (9 dígitos) o una cédula (11 dígitos).", Datos(venta!));

        // Hay cédulas antiguas que no cumplen el dígito verificador: se aceptan si el padrón o el maestro de clientes las conocen.
        if (!consulta.DigitoVerificadorValido && !consulta.EnPadron && consulta.Cliente is null)
            return new RespuestaVenta(CodigoResultadoVenta.DocumentoInvalido, $"El documento {consulta.Documento} no es válido (dígito verificador).", Datos(venta!));

        ClienteVenta cliente;
        if (consulta.Cliente is { } registrado)
        {
            cliente = new ClienteVenta(registrado.ClienteId, registrado.TipoDocumento, registrado.Documento, registrado.Nombre, registrado.TipoComprobante);
        }
        else
        {
            var nombreFinal = string.IsNullOrWhiteSpace(nombre) ? consulta.RazonSocial : nombre.Trim();
            if (string.IsNullOrWhiteSpace(nombreFinal))
                return new RespuestaVenta(CodigoResultadoVenta.NombreRequerido,
                    $"El documento {consulta.Documento} no está en el padrón DGII ni registrado. Indique el nombre del cliente.", Datos(venta!));

            // Un RNC del padrón factura a crédito fiscal por defecto; una cédula, a consumo.
            var comprobante = consulta.Tipo == TipoDocumentoIdentidad.Rnc ? TipoComprobante.FacturaCreditoFiscal : TipoComprobante.FacturaConsumo;
            cliente = new ClienteVenta(null, consulta.Tipo, consulta.Documento, nombreFinal, comprobante);
        }

        return await EjecutarAsync(venta!, () => venta!.AsignarCliente(cliente, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> QuitarClienteAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.QuitarCliente(reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> MarcarEntregaAsync(SesionUsuario sesion, int ventaId, SolicitudMarcarEntrega solicitud, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        string? almacenNombre = null;
        if (solicitud.Metodo == MetodoEntrega.RetiroAlmacen && solicitud.AlmacenId is { } almacenId)
        {
            almacenNombre = await contexto.Almacenes.AsNoTracking().Where(a => a.Id == almacenId && a.Activo).Select(a => a.Nombre).FirstOrDefaultAsync(cancelacion);
            if (almacenNombre is null)
                return new RespuestaVenta(CodigoResultadoVenta.EntregaInvalida, "El almacén seleccionado no existe o está inactivo.", Datos(venta!));
        }

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var ahora = reloj.GetUtcNow();
        var lineas = solicitud.Lineas ?? [];

        // Se valida sobre una copia sin seguimiento antes de pedir la clave del supervisor.
        try
        {
            var copia = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta!.Id, cancelacion);
            copia.MarcarEntrega(solicitud.Metodo, solicitud.AlmacenId, almacenNombre, solicitud.Envio, solicitud.FechaComprometida, solicitud.Comentario, lineas,
                null, null, hoy, ahora);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(venta!));
        }
        catch (ArgumentException excepcion)
        {
            return new RespuestaVenta(CodigoResultadoVenta.EntregaInvalida, excepcion.Message, Datos(venta!));
        }

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.MarcarPendiente, solicitud.AutorizacionId, TipoEntidadVenta, venta!.NumeroTransaccion,
            cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.MarcarPendiente, venta);

        return await EjecutarAsync(venta, () =>
        {
            var destino = venta.MarcarEntrega(solicitud.Metodo, solicitud.AlmacenId, almacenNombre, solicitud.Envio, solicitud.FechaComprometida, solicitud.Comentario,
                lineas, permiso.SupervisorId ?? sesion.UsuarioId, permiso.SupervisorNombre ?? sesion.Nombre, hoy, ahora);
            auditoria.Registrar(new EntradaAuditoria("Entregas.PendienteMarcado", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new
                {
                    destino.Numero,
                    destino.Metodo,
                    destino.AlmacenNombre,
                    destino.Direccion,
                    destino.FechaComprometida,
                    Lineas = destino.Lineas.Select(l => new { l.NumeroLinea, l.Cantidad }),
                },
                Motivo: permiso.Motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);
    }

    public async Task<RespuestaVenta> QuitarEntregaAsync(SesionUsuario sesion, int ventaId, int numeroDestino, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.QuitarEntrega(numeroDestino, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<IReadOnlyList<DatosAlmacen>> ListarAlmacenesAsync(SesionUsuario sesion, CancellationToken cancelacion = default) =>
        (await contexto.Almacenes.AsNoTracking().Where(a => a.Activo).OrderBy(a => a.Nombre).ToListAsync(cancelacion))
            .Select(a => new DatosAlmacen(a.Id, a.Codigo, a.Nombre, a.SucursalId, a.Direccion, a.SucursalId == sesion.SucursalId))
            .OrderByDescending(a => a.EsDeLaSucursal)
            .ToList();

    public async Task<RespuestaVenta> AsignarFidelidadAsync(SesionUsuario sesion, int ventaId, string cedula, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        string normalizada;
        try
        {
            normalizada = MiembroFidelidad.ValidarCedula(cedula);
        }
        catch (ArgumentException)
        {
            return new RespuestaVenta(CodigoResultadoVenta.DocumentoInvalido, "Digite la cédula del miembro del programa de fidelidad (11 dígitos).", Datos(venta!));
        }

        var miembro = await contexto.MiembrosFidelidad.AsNoTracking().SingleOrDefaultAsync(m => m.Cedula == normalizada && m.Activo, cancelacion);
        if (miembro is null)
            return new RespuestaVenta(CodigoResultadoVenta.NoInscritoFidelidad,
                $"La cédula {normalizada} no está inscrita en el programa de fidelidad. Puede inscribirla ahora.", Datos(venta!));

        var nivel = await contexto.NombreNivelAsync(miembro.NivelId, cancelacion);
        return await EjecutarAsync(venta!, () => venta!.AsignarFidelidad(new MiembroVenta(miembro.Id, miembro.Cedula, miembro.Nombre, nivel), reloj.GetUtcNow()),
            cancelacion);
    }

    public async Task<RespuestaVenta> QuitarFidelidadAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.QuitarFidelidad(reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> CambiarComprobanteAsync(SesionUsuario sesion, int ventaId, TipoComprobante tipo, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        if (venta!.TipoComprobante == tipo)
            return Correcta(venta);

        // Primero se valida que el cambio sea posible, para no pedir clave de supervisor en vano.
        try
        {
            ValidarComprobante(venta, tipo);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(venta));
        }

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.CambiarComprobante, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.CambiarComprobante, venta);

        var anterior = venta.TipoComprobante;
        var porcentajeRetencion = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.PorcentajeRetencionLey3223, sesion.CajaId, cancelacion) ?? 0m;
        return await EjecutarAsync(venta, () =>
        {
            venta.CambiarComprobante(tipo, reloj.GetUtcNow());

            // Régimen especial: la retención de la Ley 32-23 se descuenta de lo que paga el cliente (queda en la factura).
            venta.AplicarRetencionLey(porcentajeRetencion, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.ComprobanteCambiado", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Anterior = anterior, Nuevo = tipo, venta.ClienteDocumento },
                Motivo: permiso.Motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);
    }

    public async Task<RespuestaVenta> EstablecerLimiteCompraAsync(SesionUsuario sesion, int ventaId, decimal? limite, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.EstablecerLimiteCompra(limite, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaListaBoda> AsignarListaBodaAsync(SesionUsuario sesion, int ventaId, string? numero, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return new RespuestaListaBoda(rechazo.Resultado, rechazo.Mensaje, rechazo.Venta, null);

        // Quitar la lista no necesita Central.
        if (numero is not { Length: > 0 } buscado || buscado.Trim().Length == 0)
        {
            var quitada = await EjecutarAsync(venta!, () => venta!.AsignarListaBoda(null, null, reloj.GetUtcNow()), cancelacion);
            return new RespuestaListaBoda(quitada.Resultado, quitada.Mensaje, quitada.Venta, null);
        }

        var consulta = await central.ConsultarListaBodaAsync(buscado.Trim().ToUpperInvariant(), cancelacion);
        if (!consulta.CentralRespondio)
            return new RespuestaListaBoda(CodigoResultadoVenta.SinConexionCentral,
                $"Las listas de boda se consultan en el Central y no respondió: {consulta.Error}", Datos(venta!), null);
        if (consulta.Lista is not { } lista)
            return new RespuestaListaBoda(CodigoResultadoVenta.DocumentoInvalido, consulta.Error ?? "La lista no existe.", Datos(venta!), null);
        if (lista.Estado == CgPos.Dominio.ListasBoda.EstadoListaBoda.Cerrada)
            return new RespuestaListaBoda(CodigoResultadoVenta.DocumentoInvalido,
                $"La lista {lista.Numero} ({lista.Evento}) está cerrada.", Datos(venta!), lista);

        var respuesta = await EjecutarAsync(venta!, () =>
        {
            venta!.AsignarListaBoda(lista.Numero, lista.Evento, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.ListaBodaAsignada", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { lista.Numero, lista.Evento, lista.ClienteNombre },
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        }, cancelacion);

        return new RespuestaListaBoda(respuesta.Resultado,
            respuesta.Exitosa ? $"Lista {lista.Numero}: {lista.Evento}." : respuesta.Mensaje, respuesta.Venta, lista);
    }

    public async Task<RespuestaVenta> PonerEnEsperaAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var resultado = await EjecutarAsync(venta!, () =>
        {
            venta!.PonerEnEspera(reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.PuestaEnEspera", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Total = venta.CalcularTotales().Total },
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        }, cancelacion);

        return resultado.Exitosa ? await ContinuarConNuevaAsync(sesion, cancelacion) : resultado;
    }

    public async Task<IReadOnlyList<DatosVentaEnEspera>> ListarEnEsperaAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return [];

        var enEspera = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas)
            .Where(v => v.TurnoId == turno!.Id && v.UsuarioId == sesion.UsuarioId && v.Estado == EstadoVenta.EnEspera)
            .ToListAsync(cancelacion);

        return enEspera
            .OrderBy(v => v.PuestaEnEsperaEn)
            .Select(v =>
            {
                var totales = v.CalcularTotales();
                return new DatosVentaEnEspera(v.Id, v.NumeroTransaccion, v.ClienteNombre, totales.Total, totales.CantidadLineas, v.PuestaEnEsperaEn!.Value);
            })
            .ToList();
    }

    public async Task<RespuestaVenta> RetomarAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var enEspera = await contexto.Ventas.Include(v => v.Lineas).SingleOrDefaultAsync(v => v.Id == ventaId, cancelacion);
        if (enEspera is null || enEspera.TurnoId != turno!.Id || enEspera.UsuarioId != sesion.UsuarioId || enEspera.Estado != EstadoVenta.EnEspera)
            return new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, "La factura no está en espera en su turno.", null);

        var ahora = reloj.GetUtcNow();
        var actual = await VentaEnCursoAsync(sesion, turno, cancelacion);
        if (actual is not null)
        {
            if (actual.TieneLineasActivas)
                actual.PonerEnEspera(ahora);
            else if (actual.Lineas.Count == 0)
                contexto.Ventas.Remove(actual); // nunca tuvo artículos: no es un documento
            else
                actual.Anular("Sin artículos al retomar una factura en espera", sesion.UsuarioId, sesion.Nombre, ahora);
        }

        enEspera.Retomar(ahora);
        enEspera.RecalcularPromociones(await PromocionesAsync(cancelacion), enEspera.SucursalId, reloj.GetLocalNow());
        auditoria.Registrar(new EntradaAuditoria("Ventas.Retomada", TipoEntidadVenta, enEspera.NumeroTransaccion,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return Correcta(enEspera);
    }

    public async Task<RespuestaVenta> SuspenderAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.SuspenderVenta, autorizacionId, "Caja", sesion.CajaCodigo.ToString("00"), cancelacion);
        if (!permiso.Permitido)
        {
            return permiso.AutorizacionRechazada
                ? new RespuestaVenta(CodigoResultadoVenta.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", null, CatalogoPermisos.SuspenderVenta)
                : new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "Suspender operaciones requiere autorización de un supervisor.", null, CatalogoPermisos.SuspenderVenta);
        }

        auditoria.Registrar(new EntradaAuditoria("Caja.OperacionesSuspendidas", "Caja", sesion.CajaCodigo.ToString("00"),
            Motivo: permiso.Motivo,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaVenta(CodigoResultadoVenta.Correcto, null, null);
    }

    private async Task<RespuestaVenta> AnularYContinuarAsync(SesionUsuario sesion, int ventaId, string codigoPermiso, string? motivo, Guid? autorizacionId,
        string accionAuditoria, CancellationToken cancelacion)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        // Una venta vacía no se anula: no hay nada que limpiar y no se gasta un número.
        if (venta!.Lineas.Count == 0)
            return Correcta(venta);

        var permiso = await autorizaciones.VerificarAsync(sesion, codigoPermiso, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, codigoPermiso, venta);

        var motivoFinal = string.IsNullOrWhiteSpace(motivo) ? permiso.Motivo : motivo.Trim();
        var totales = venta.CalcularTotales();

        var resultado = await EjecutarAsync(venta, () =>
        {
            venta.Anular(motivoFinal ?? string.Empty, sesion.UsuarioId, sesion.Nombre, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria(accionAuditoria, TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Lineas = totales.CantidadLineas, totales.Total },
                Motivo: motivoFinal,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);

        return resultado.Exitosa ? await ContinuarConNuevaAsync(sesion, cancelacion) : resultado;
    }

    private async Task<RespuestaVenta> EliminarAsync(SesionUsuario sesion, int ventaId, Guid? autorizacionId, Func<Venta, LineaVenta> eliminar, CancellationToken cancelacion)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.EliminarLinea, autorizacionId, TipoEntidadVenta, venta!.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.EliminarLinea, venta);

        return await EjecutarAsync(venta, () =>
        {
            var reverso = eliminar(venta);
            var original = venta.Lineas.Single(l => l.NumeroLinea == reverso.LineaAnuladaNumero);
            auditoria.Registrar(new EntradaAuditoria("Ventas.LineaEliminada", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Linea = original.NumeroLinea, original.CodigoInterno, original.Descripcion, original.Cantidad, Importe = original.ImporteConImpuesto },
                Motivo: permiso.Motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);
    }

    private async Task<RespuestaVenta> EjecutarAsync(Venta venta, Action operacion, CancellationToken cancelacion)
    {
        var promociones = await PromocionesAsync(cancelacion);
        try
        {
            operacion();

            // Toda operación deja las ofertas al día: cantidades, líneas, días y horas pueden haber cambiado (RF-208).
            venta.RecalcularPromociones(promociones, venta.SucursalId, reloj.GetLocalNow());
            await contexto.SaveChangesAsync(cancelacion);
            return Correcta(venta);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            // Se descarta todo lo pendiente (incluida una autorización marcada como usada): no se consume si la operación falla.
            contexto.ChangeTracker.Clear();
            var ventaActual = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta.Id, cancelacion);
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(ventaActual));
        }
    }

    public async Task<RespuestaVenta> AplicarDescuentoLineaAsync(SesionUsuario sesion, int ventaId, int numeroLinea, TipoDescuento tipo, decimal valor,
        string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        if (await ValidarMotivoAsync(motivo, cancelacion) is { } motivoInvalido)
            return new RespuestaVenta(CodigoResultadoVenta.MotivoRequerido, motivoInvalido, Datos(venta!));

        // Se valida el descuento antes de pedir clave de supervisor.
        VistaPreviaDescuento vista;
        try
        {
            vista = venta!.PrevisualizarDescuentoLinea(numeroLinea, tipo, valor);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(venta!));
        }

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.DescuentoLinea, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.DescuentoLinea, venta);

        var linea = venta.Lineas.Single(l => l.NumeroLinea == numeroLinea && l.EstaActiva);
        if (await RechazoPorTopeAsync(sesion, permiso, CatalogoPermisos.DescuentoLinea, linea.ArticuloId, linea.DepartamentoId, vista, venta, cancelacion,
                linea.CategoriaId, linea.MarcaId) is { } excedido)
            return excedido;

        return await EjecutarAsync(venta, () =>
        {
            venta.AplicarDescuentoLinea(numeroLinea, tipo, valor, motivo!.Trim(), permiso.SupervisorId ?? sesion.UsuarioId, permiso.SupervisorNombre ?? sesion.Nombre,
                reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.DescuentoLinea", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Linea = numeroLinea, linea.CodigoInterno, Tipo = tipo, Valor = valor, vista.Monto, vista.Porcentaje },
                Motivo: motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);
    }

    public async Task<RespuestaVenta> QuitarDescuentoLineaAsync(SesionUsuario sesion, int ventaId, int numeroLinea, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.QuitarDescuentoLinea(numeroLinea, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> AplicarDescuentoFacturaAsync(SesionUsuario sesion, int ventaId, TipoDescuento tipo, decimal valor, IReadOnlyList<int>? lineas,
        string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        if (await ValidarMotivoAsync(motivo, cancelacion) is { } motivoInvalido)
            return new RespuestaVenta(CodigoResultadoVenta.MotivoRequerido, motivoInvalido, Datos(venta!));

        VistaPreviaDescuento vista;
        try
        {
            vista = venta!.PrevisualizarDescuentoFactura(tipo, valor, lineas);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(venta!));
        }

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.DescuentoFactura, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.DescuentoFactura, venta);

        if (await RechazoPorTopeAsync(sesion, permiso, CatalogoPermisos.DescuentoFactura, null, null, vista, venta, cancelacion) is { } excedido)
            return excedido;

        ResultadoDescuentoFactura? resultado = null;
        var respuesta = await EjecutarAsync(venta, () =>
        {
            resultado = venta.AplicarDescuentoFactura(tipo, valor, lineas, motivo!.Trim(), permiso.SupervisorId ?? sesion.UsuarioId,
                permiso.SupervisorNombre ?? sesion.Nombre, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.DescuentoFactura", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Tipo = tipo, Valor = valor, Lineas = lineas, resultado.Monto, resultado.Porcentaje, resultado.LineasExcluidas },
                Motivo: motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);

        if (!respuesta.Exitosa || resultado is not { LineasExcluidas.Count: > 0 } conExcluidas)
            return respuesta;

        return respuesta with
        {
            Mensaje = $"Las líneas {string.Join(", ", conExcluidas.LineasExcluidas)} no tomaron el descuento: están en oferta o su departamento no admite descuento manual.",
            LineasExcluidas = conExcluidas.LineasExcluidas,
        };
    }

    public async Task<RespuestaVenta> AplicarDescuentoTarjetaAsync(SesionUsuario sesion, int ventaId, string bin, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        if (DescuentoTarjeta.SoloDigitos(bin).Length < DescuentoTarjeta.LargoMinimoBin)
            return new RespuestaVenta(CodigoResultadoVenta.DocumentoInvalido,
                $"Digite al menos los primeros {DescuentoTarjeta.LargoMinimoBin} dígitos de la tarjeta.", Datos(venta!));

        var totalAntes = venta!.CalcularTotales().Total;
        var aplicado = await AplicarDescuentoPorBinAsync(sesion, venta, bin, cancelacion);
        if (aplicado is null)
            return new RespuestaVenta(CodigoResultadoVenta.Correcto, MotivoSinDescuento(venta), Datos(venta));

        await contexto.SaveChangesAsync(cancelacion);
        var rebaja = totalAntes - venta.CalcularTotales().Total;
        return Correcta(venta) with { Mensaje = $"{aplicado.Nombre}: {venta.SimboloMoneda}{rebaja:N2} de descuento por pagar con esa tarjeta." };
    }

    /// <summary>
    /// Aplica a la factura el descuento del banco que cubra ese BIN, sin guardar todavía. Devuelve el descuento aplicado o nulo si
    /// ninguno aplica; no toca un descuento manual ya puesto, que alguien autorizó y no se acumula con el del banco.
    /// </summary>
    private async Task<DescuentoTarjeta?> AplicarDescuentoPorBinAsync(SesionUsuario sesion, Venta venta, string? bin, CancellationToken cancelacion)
    {
        var digitos = DescuentoTarjeta.SoloDigitos(bin);
        if (digitos.Length < DescuentoTarjeta.LargoMinimoBin || EsDescuentoManual(venta))
            return null;

        // El descuento del banco se calcula sobre el total sin otro descuento de factura, para que pasar dos tarjetas no lo encadene.
        var total = venta.CalcularTotales().Total + venta.Lineas.Sum(l => l.DescuentoFactura);
        var ahoraLocal = reloj.GetLocalNow();
        var candidatos = await contexto.DescuentosTarjeta.AsNoTracking()
            .Where(d => d.Activo && d.VigenteDesde <= ahoraLocal && d.VigenteHasta >= ahoraLocal)
            .ToListAsync(cancelacion);

        // Si varios bancos cubren el mismo BIN, gana el que más le descuenta al cliente.
        var descuento = candidatos
            .Where(d => d.AplicaA(digitos, total, ahoraLocal))
            .OrderByDescending(d => d.Calcular(total))
            .FirstOrDefault();
        if (descuento is null || descuento.Calcular(total) is var monto && monto <= 0)
            return null;

        venta.AplicarDescuentoFactura(TipoDescuento.Monto, monto, null, $"{DescuentoTarjeta.PrefijoMotivo}{descuento.Nombre} ({descuento.Codigo})",
            sesion.UsuarioId, sesion.Nombre, reloj.GetUtcNow());
        auditoria.Registrar(new EntradaAuditoria("Ventas.DescuentoTarjeta", TipoEntidadVenta, venta.NumeroTransaccion,
            Detalle: new { descuento.Codigo, descuento.Nombre, Bin = digitos, Monto = monto },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        return descuento;
    }

    private static bool EsDescuentoManual(Venta venta) =>
        venta.MotivoDescuentoFactura is { Length: > 0 } motivo && !motivo.StartsWith(DescuentoTarjeta.PrefijoMotivo, StringComparison.Ordinal);

    private static string MotivoSinDescuento(Venta venta) =>
        EsDescuentoManual(venta)
            ? "La factura ya tiene un descuento aplicado: el del banco no se acumula."
            : "Esa tarjeta no tiene descuento vigente.";

    public async Task<RespuestaVenta> QuitarDescuentoFacturaAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.QuitarDescuentoFactura(reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> DesactivarPromocionAsync(SesionUsuario sesion, int ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var linea = venta!.Lineas.FirstOrDefault(l => l.NumeroLinea == numeroLinea && l.EstaActiva);
        if (linea is not { TienePromocionActiva: true })
            return new RespuestaVenta(CodigoResultadoVenta.DescuentoInvalido, $"La línea {numeroLinea} no tiene una oferta aplicada.", Datos(venta));

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.DesactivarPromocion, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.DesactivarPromocion, venta);

        var promocion = new { linea.PromocionCodigo, linea.PromocionNombre, linea.CodigoInterno, Descuento = linea.DescuentoPromocion };
        return await EjecutarAsync(venta, () =>
        {
            venta.DesactivarPromocion(numeroLinea, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Promociones.Desactivada", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: promocion,
                Motivo: permiso.Motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);
    }

    public async Task<IReadOnlyList<DatosMotivoDescuento>> ListarMotivosDescuentoAsync(CancellationToken cancelacion = default) =>
        await contexto.MotivosDescuento.AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new DatosMotivoDescuento(m.Codigo, m.Nombre))
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<DatosPromocionVigente>> ListarPromocionesVigentesAsync(SesionUsuario sesion, int articuloId, CancellationToken cancelacion = default)
    {
        var clasificacion = await contexto.Articulos.Where(a => a.Id == articuloId)
            .Select(a => new { a.DepartamentoId, a.CategoriaId, a.MarcaId })
            .SingleOrDefaultAsync(cancelacion);
        if (clasificacion is null)
            return [];

        var ahoraLocal = reloj.GetLocalNow();
        return (await PromocionesAsync(cancelacion))
            .Where(p => p.EstaVigente(sesion.SucursalId, ahoraLocal)
                && p.AplicaA(articuloId, clasificacion.DepartamentoId, clasificacion.CategoriaId, clasificacion.MarcaId))
            .OrderBy(p => p.VigenteHasta)
            .Select(p => new DatosPromocionVigente(p.Id, p.Codigo, p.Nombre, p.DescripcionCorta, p.Tipo, p.VigenteHasta))
            .ToList();
    }

    /// <summary>Ofertas activas en su rango de fechas; los días, horas y sucursal los filtra el dominio.</summary>
    private async Task<IReadOnlyList<Promocion>> PromocionesAsync(CancellationToken cancelacion)
    {
        if (_promociones is not null)
            return _promociones;

        var ahora = reloj.GetUtcNow();
        _promociones = await contexto.Promociones.AsNoTracking()
            .Where(p => p.Activa && p.VigenteDesde <= ahora && p.VigenteHasta >= ahora)
            .ToListAsync(cancelacion);
        return _promociones;
    }

    /// <summary>Si hay motivos configurados, el motivo debe ser uno de ellos (por nombre o código).</summary>
    private async Task<string?> ValidarMotivoAsync(string? motivo, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return "Seleccione el motivo del descuento.";

        var configurados = await contexto.MotivosDescuento.AsNoTracking().Where(m => m.Activo).Select(m => new { m.Codigo, m.Nombre }).ToListAsync(cancelacion);
        var buscado = motivo.Trim();
        return configurados.Count == 0 || configurados.Any(m => string.Equals(m.Nombre, buscado, StringComparison.OrdinalIgnoreCase)
                                                                || m.Codigo.ToString(System.Globalization.CultureInfo.InvariantCulture) == buscado)
            ? null
            : "Seleccione un motivo de la lista.";
    }

    /// <summary>Compara el descuento con el tope del nivel de quien lo autoriza (RN-10); el nivel es el del supervisor si hubo clave.</summary>
    private async Task<RespuestaVenta?> RechazoPorTopeAsync(SesionUsuario sesion, ResultadoPermiso permiso, string codigoPermiso, int? articuloId, int? departamentoId,
        VistaPreviaDescuento vista, Venta venta, CancellationToken cancelacion, int? categoriaId = null, int? marcaId = null)
    {
        var nivel = permiso.SupervisorId is { } supervisorId
            ? await (from usuario in contexto.Usuarios
                     join rol in contexto.Roles on usuario.RolId equals rol.Id
                     where usuario.Id == supervisorId
                     select rol.Nivel).SingleAsync(cancelacion)
            : sesion.Nivel;

        var topes = await contexto.TopesDescuento.AsNoTracking().ToListAsync(cancelacion);
        var evaluacion = ReglasTopeDescuento.Evaluar(topes, nivel, articuloId, departamentoId, vista.Porcentaje, vista.Monto, categoriaId, marcaId);
        if (evaluacion.Permitido)
            return null;

        // La autorización quedó marcada en memoria pero no se guarda: el supervisor puede reintentar con un monto menor.
        contexto.ChangeTracker.Clear();
        if (evaluacion.SinConfiguracion)
        {
            var sinTopes = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta.Id, cancelacion);
            return new RespuestaVenta(CodigoResultadoVenta.DescuentoNoPermitido,
                "No hay topes de descuento configurados (del artículo, su departamento ni generales): el descuento manual no se permite hasta configurarlos en el Central.",
                Datos(sinTopes));
        }

        var limite = evaluacion switch
        {
            { PorcentajeMaximo: { } porcentaje, MontoMaximo: { } monto } => $"{porcentaje:0.##}% y {venta.SimboloMoneda}{monto:N2}",
            { PorcentajeMaximo: { } porcentaje } => $"{porcentaje:0.##}%",
            { MontoMaximo: { } monto } => $"{venta.SimboloMoneda}{monto:N2}",
            _ => "sin descuento",
        };
        var ventaActual = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta.Id, cancelacion);
        return new RespuestaVenta(CodigoResultadoVenta.TopeDescuentoExcedido,
            $"El descuento ({vista.Porcentaje:0.##}%, {venta.SimboloMoneda}{vista.Monto:N2}) supera el tope del nivel {nivel} ({limite}). Requiere autorización de un nivel superior.",
            Datos(ventaActual), codigoPermiso);
    }

    private async Task<RespuestaVenta> ContinuarConNuevaAsync(SesionUsuario sesion, CancellationToken cancelacion)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        return rechazo ?? Correcta(await IniciarVentaAsync(sesion, turno!, cancelacion));
    }

    private Task<Venta?> VentaEnCursoAsync(SesionUsuario sesion, Turno turno, CancellationToken cancelacion) =>
        contexto.Ventas.Include(v => v.Lineas)
            .Where(v => v.TurnoId == turno.Id && v.UsuarioId == sesion.UsuarioId && v.Estado == EstadoVenta.EnCurso)
            .OrderByDescending(v => v.IniciadaEn)
            .FirstOrDefaultAsync(cancelacion);

    private async Task<Venta> IniciarVentaAsync(SesionUsuario sesion, Turno turno, CancellationToken cancelacion)
    {
        var codigoSucursal = await contexto.Sucursales.Where(s => s.Id == sesion.SucursalId).Select(s => s.Codigo).SingleAsync(cancelacion);
        var moneda = await contexto.MonedaLocalAsync(parametros, sesion.CajaId, cancelacion);
        var digitos = await NumeracionDocumentos.DigitosAsync(parametros, sesion.CajaId, cancelacion);
        var minimo = await NumeracionDocumentos.MinimoAsync(parametros, CgPos.Dominio.Organizacion.CatalogoParametros.ProximaFactura, sesion.CajaId, cancelacion);
        var secuencia = await secuencias.SiguienteAsync(sesion.CajaId, TiposSecuencia.Transaccion, cancelacion, minimo);

        var venta = Venta.Iniciar(sesion.SucursalId, codigoSucursal, sesion.CajaId, sesion.CajaCodigo, turno.Id, secuencia, digitos,
            sesion.UsuarioId, sesion.Nombre, moneda.Codigo, moneda.Simbolo, reloj.GetUtcNow());
        contexto.Ventas.Add(venta);
        await contexto.SaveChangesAsync(cancelacion);
        return venta;
    }

    private async Task<(Turno? Turno, RespuestaVenta? Rechazo)> TurnoDelUsuarioAsync(SesionUsuario sesion, CancellationToken cancelacion)
    {
        var turno = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);

        if (turno is null)
            return (null, new RespuestaVenta(CodigoResultadoVenta.TurnoNoAbierto, "Debe abrir turno antes de vender.", null));
        if (turno.UsuarioActualId != sesion.UsuarioId)
            return (null, new RespuestaVenta(CodigoResultadoVenta.TurnoDeOtroUsuario, $"La caja tiene abierto el turno {turno.Numero} de {turno.UsuarioActualNombre}.", null));

        _montoIdentificacion = await parametros.ObtenerDecimalAsync(ClavesParametros.MontoIdentificacionConsumo, sesion.CajaId, cancelacion);
        return (turno, null);
    }

    private async Task<(Venta? Venta, RespuestaVenta? Rechazo)> CargarVentaEditableAsync(SesionUsuario sesion, int ventaId, CancellationToken cancelacion)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return (null, rechazo);

        var venta = await contexto.Ventas.Include(v => v.Lineas).SingleOrDefaultAsync(v => v.Id == ventaId, cancelacion);
        if (venta is null || venta.CajaId != sesion.CajaId || venta.UsuarioId != sesion.UsuarioId || venta.TurnoId != turno!.Id)
            return (null, new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, "La venta no existe o no pertenece a su turno.", null));
        if (venta.Estado != EstadoVenta.EnCurso)
            return (null, new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, $"La venta {venta.NumeroTransaccion} ya no se puede modificar.", Datos(venta)));

        return (venta, null);
    }

    /// <summary>Aplica las mismas validaciones del dominio sobre una copia del estado, sin modificar la venta.</summary>
    private static void ValidarComprobante(Venta venta, TipoComprobante tipo)
    {
        if (!ReglasComprobante.EsDeVenta(tipo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.ComprobanteNoPermitido, $"El comprobante {ReglasComprobante.Nombre(tipo)} no se emite en una venta de caja.");
        if (!ReglasComprobante.ClienteCumple(tipo, venta.ClienteTipoDocumento, venta.ClienteDocumento))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DocumentoRequerido,
                $"El comprobante {ReglasComprobante.Nombre(tipo)} requiere asignar primero un cliente con {(tipo == TipoComprobante.Gubernamental ? "RNC" : "RNC o cédula")}.");
    }

    private DatosVenta Datos(Venta venta) => venta.ADatos(_montoIdentificacion);

    private RespuestaVenta Correcta(Venta venta) => new(CodigoResultadoVenta.Correcto, null, Datos(venta));

    private RespuestaVenta SinPermiso(ResultadoPermiso permiso, string codigoPermiso, Venta venta) =>
        permiso.AutorizacionRechazada
            ? new RespuestaVenta(CodigoResultadoVenta.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció. Solicítela de nuevo.", Datos(venta), codigoPermiso)
            : new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "Esta operación requiere autorización de un supervisor.", Datos(venta), codigoPermiso);

    private static UsuarioAuditoria? Autorizador(ResultadoPermiso permiso) =>
        permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null;

    /// <summary>Acepta "código" o "cantidad*código" (RF-14). La cantidad explícita tiene prioridad.</summary>
    internal static bool TryInterpretarEntrada(string? entrada, decimal? cantidad, out string codigo, out decimal? cantidadFinal)
    {
        codigo = entrada?.Trim() ?? string.Empty;
        cantidadFinal = cantidad;

        var posicion = codigo.IndexOf('*');
        if (posicion > 0 && cantidad is null)
        {
            if (!decimal.TryParse(codigo[..posicion], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var multiplicador))
                return false;

            cantidadFinal = multiplicador;
            codigo = codigo[(posicion + 1)..].Trim();
        }

        return codigo.Length > 0 && !codigo.Contains('*');
    }
}

internal sealed class ValidadorAutorizaciones(ContextoDatosPos contexto, TimeProvider reloj) : IValidadorAutorizaciones
{
    public async Task<ResultadoPermiso> VerificarAsync(SesionUsuario sesion, string permiso, Guid? autorizacionId, string tipoEntidad, string? entidadId,
        CancellationToken cancelacion = default)
    {
        // Sin autorización basta el permiso propio. Si se trae una autorización se usa aunque el usuario tenga el permiso:
        // así alguien de mayor nivel autoriza lo que excede el tope de descuento del usuario (RN-10).
        if (autorizacionId is not { } id)
            return sesion.TienePermiso(permiso) ? ResultadoPermiso.PermisoPropio : new ResultadoPermiso(false, false, false, null, null, null);

        var ahora = reloj.GetUtcNow();
        var autorizacion = await contexto.AutorizacionesOtorgadas.SingleOrDefaultAsync(a => a.Id == id, cancelacion);
        if (autorizacion is null || !autorizacion.PuedeUsarse(permiso, sesion.UsuarioId, sesion.CajaId, ahora))
            return sesion.TienePermiso(permiso) ? ResultadoPermiso.PermisoPropio : new ResultadoPermiso(false, false, true, null, null, null);

        autorizacion.MarcarUsada(ahora, tipoEntidad, entidadId);
        return new ResultadoPermiso(true, true, false, autorizacion.SupervisorId, autorizacion.SupervisorNombre, autorizacion.Motivo);
    }
}

/// <summary>
/// Indicador de conexión (RF-192): en línea si el Central respondió en los últimos ciclos de sincronización y no falló después; cuenta los
/// documentos de la bandeja de salida que aún no se confirman.
/// </summary>
internal sealed class ServicioEstadoSincronizacion(
    ContextoDatosPos contexto,
    CgPos.Pos.Aplicacion.Sincronizacion.IClienteCentral central,
    CgPos.Pos.Aplicacion.Sincronizacion.IEstadoConexionCentral conexion,
    Sincronizacion.OpcionesSincronizacion opciones,
    CgPos.Pos.Aplicacion.Sincronizacion.IServicioMantenimiento mantenimiento,
    CgPos.Pos.Aplicacion.Sincronizacion.EstadoMantenimiento estadoMantenimiento,
    TimeProvider reloj) : IEstadoSincronizacion
{
    public async Task<DatosEstadoSincronizacion> ObtenerAsync(CancellationToken cancelacion = default)
    {
        var pendientes = await contexto.BandejaSalida.CountAsync(m => m.Estado != EstadoMensajeSalida.Confirmado, cancelacion);
        var ultima = await contexto.BandejaSalida
            .Where(m => m.Estado == EstadoMensajeSalida.Confirmado)
            .MaxAsync(m => m.ConfirmadoEn, cancelacion);

        var intervalo = opciones.Intervalo;
        var enLinea = central.Configurado
            && conexion.UltimoContacto is { } contacto
            && contacto >= reloj.GetUtcNow() - intervalo * 3
            && (conexion.UltimoFallo is null || conexion.UltimoFallo < contacto);

        return new DatosEstadoSincronizacion(
            CentralConfigurado: central.Configurado,
            EnLinea: enLinea,
            DocumentosPendientes: pendientes,
            UltimaSincronizacion: ultima,
            UltimoError: enLinea ? null : conexion.UltimoError,
            Alertas: await mantenimiento.ObtenerAlertasAsync(cancelacion),
            UltimoRespaldo: estadoMantenimiento.UltimoRespaldoCorrecto);
    }
}
