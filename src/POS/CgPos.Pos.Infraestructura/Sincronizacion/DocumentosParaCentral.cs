using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Infraestructura.Entregas;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Arma los documentos que la caja envía al Central: los Id locales se cambian por números de documento y códigos de maestros,
/// que son lo único que caja y Central comparten.
/// </summary>
internal static class DocumentosParaCentral
{
    public static async Task<DocumentoVentaCobrada> VentaCobradaAsync(this ContextoDatosPos contexto, Venta venta, DatosVenta datos,
        DocumentoElectronicoParaCentral? ecf, DateTimeOffset cobradaEn, CancellationToken cancelacion)
    {
        var turnoNumero = await contexto.Turnos.AsNoTracking().Where(t => t.Id == venta.TurnoId).Select(t => t.Numero).SingleAsync(cancelacion);
        var usuarioCodigo = await contexto.Usuarios.AsNoTracking().Where(u => u.Id == venta.UsuarioId).Select(u => u.Codigo).SingleAsync(cancelacion);
        var clienteCodigo = datos.Cliente?.ClienteId is { } clienteId
            ? await contexto.Clientes.AsNoTracking().Where(c => c.Id == clienteId).Select(c => c.Codigo).SingleOrDefaultAsync(cancelacion)
            : null;
        var almacenes = await CodigosAlmacenesAsync(contexto, (datos.DestinosEntrega ?? []).Select(d => d.AlmacenId), cancelacion);

        return new DocumentoVentaCobrada(
            datos.NumeroTransaccion,
            turnoNumero,
            usuarioCodigo,
            datos.UsuarioNombre,
            datos.IniciadaEn,
            datos.CobradaEn ?? cobradaEn,
            datos.TipoComprobante,
            datos.Cliente is { } cliente ? new DocumentoClienteVenta(clienteCodigo, cliente.TipoDocumento, cliente.Documento, cliente.Nombre) : null,
            datos.Moneda,
            datos.Lineas.Select(l => new DocumentoLineaVenta(l.NumeroLinea, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo, l.UnidadMedidaCodigo,
                l.DecimalesCantidad, l.Cantidad, l.PrecioUnitario, l.Importe, l.PorcentajeImpuesto, l.Lista, l.MotivoPrecio, l.LeidaDeBalanza, l.EsReverso,
                l.LineaAnuladaNumero, l.Anulada, l.Serial, l.PromocionCodigo, l.DescuentoPromocion, l.DescuentoManual, l.DescuentoManualTipo,
                l.DescuentoManualValor, l.MotivoDescuento, l.DescuentoAutorizadoPorNombre, l.DescuentoFactura, l.ImporteBruto, l.CantidadEnEntrega)).ToList(),
            datos.Totales,
            datos.DescuentoFactura,
            (datos.Pagos ?? []).Select(p => new DocumentoPagoVenta(p.Numero, p.FormaPagoCodigo, p.FormaPagoNombre, p.Tipo, p.Moneda, p.MontoRecibido,
                p.TasaCambio, p.MontoAplicado, p.Referencia, p.BancoNombre, p.TipoTarjetaNombre, p.UltimosDigitos, p.AprobacionManual)).ToList(),
            datos.TotalCobrado ?? datos.Totales.Total,
            datos.Devuelta,
            datos.RedondeoEfectivo,
            datos.Comprobante,
            datos.Fidelidad is { } fidelidad
                ? new DocumentoFidelidadVenta(fidelidad.Cedula, fidelidad.Nombre, fidelidad.PuntosAcumulados, fidelidad.PuntosCanjeados)
                : null,
            (datos.DestinosEntrega ?? []).Select(d => new DocumentoDestinoEntrega(d.Numero, d.Metodo, d.AlmacenId is { } id ? almacenes.GetValueOrDefault(id) : null,
                d.AlmacenNombre, d.Direccion, d.Sector, d.Ciudad, d.Referencia, d.Telefono, d.Transportista, d.CostoEnvio, d.FechaComprometida, d.Comentario,
                d.AutorizadoPorNombre, d.Lineas)).ToList(),
            ecf,
            datos.ListaBoda?.Numero,
            datos.CotizacionNumero);
    }

    public static async Task<DocumentoPendienteEntrega> PendienteAsync(this ContextoDatosPos contexto, PendienteEntrega pendiente, CancellationToken cancelacion)
    {
        var datos = pendiente.ADatos();
        var almacenCodigo = (await CodigosAlmacenesAsync(contexto, [datos.AlmacenId], cancelacion)).Values.SingleOrDefault();
        return new DocumentoPendienteEntrega(datos.Numero, datos.VentaNumero, datos.Metodo, datos.Estado, almacenCodigo, datos.AlmacenNombre, datos.Direccion,
            datos.Sector, datos.Ciudad, datos.Referencia, datos.Telefono, datos.Transportista, datos.CostoEnvio, datos.FechaComprometida, datos.Comentario,
            datos.ClienteDocumento, datos.ClienteNombre, datos.VendidoPorNombre, datos.AutorizadoPorNombre, datos.CreadoEn, datos.ActualizadoEn,
            datos.ActualizadoPorNombre, datos.MotivoAnulacion, datos.Lineas, datos.Entregas);
    }

    public static DocumentoNotaCreditoEmitida NotaCreditoEmitida(DatosNotaCredito nota, long? turnoNumero, DocumentoElectronicoParaCentral? ecf) =>
        new(nota.Numero, nota.VentaOrigenNumero, nota.EncfOrigen, nota.VentaOrigenCobradaEn, turnoNumero, nota.ClienteTipoDocumento, nota.ClienteDocumento,
            nota.ClienteNombre, nota.MotivoCodigo, nota.MotivoNombre, nota.Observacion, nota.UsuarioNombre, nota.AutorizadoPorNombre, nota.RetieneImpuesto,
            nota.EsTotal, nota.Subtotal, nota.Impuesto, nota.ImpuestoRetenido, nota.Total, nota.Moneda, nota.FechaEmision, nota.CreadaEn, nota.Lineas,
            nota.Comprobante, nota.PuntosReversados, nota.Reembolso, nota.ReembolsoReferencia, nota.ReembolsoDetalle, ecf, nota.EsInterna);

    public static DocumentoMovimientoTurno MovimientoTurno(DatosMovimientoCaja movimiento) =>
        new(movimiento.Tipo, movimiento.Numero, movimiento.Monto, movimiento.Moneda, movimiento.Motivo, movimiento.UsuarioNombre,
            movimiento.UsuarioAnteriorNombre, movimiento.AutorizadoPorNombre, movimiento.Fecha);

    public static DocumentoCierreTurno CierreTurno(DatosCierre cierre) =>
        new(cierre.TurnoNumero, cierre.Numero, cierre.FechaOperacion, cierre.Ciego, cierre.FondoInicial, cierre.FondoEnCuadre, cierre.Moneda,
            cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros, cierre.TotalEsperado, cierre.TotalDeclarado, cierre.Diferencia, cierre.UsuarioNombre,
            cierre.AbiertoEn, cierre.CerradoEn,
            cierre.FormasPago.Select(f => new DocumentoCierreFormaPago(f.Codigo, f.Nombre, f.Tipo, f.Moneda, f.Transacciones, f.Esperado, f.Declarado,
                f.Diferencia)).ToList(),
            cierre.Denominaciones,
            cierre.Movimientos.Select(MovimientoTurno).ToList());

    public static DocumentoMovimientoPuntos MovimientoPuntos(MovimientoPuntos movimiento) =>
        new(movimiento.Cedula, movimiento.Tipo, movimiento.Puntos, movimiento.Documento, movimiento.Fecha, movimiento.VenceEn);

    /// <summary>Referencia del mensaje de un movimiento de puntos: el documento que lo originó y el tipo.</summary>
    public static string ReferenciaPuntos(MovimientoPuntos movimiento) => $"{movimiento.Documento}-{movimiento.Tipo}";

    private static async Task<Dictionary<int, string>> CodigosAlmacenesAsync(ContextoDatosPos contexto, IEnumerable<int?> ids, CancellationToken cancelacion)
    {
        var buscar = ids.OfType<int>().Distinct().ToList();
        return buscar.Count == 0
            ? []
            : await contexto.Almacenes.AsNoTracking().Where(a => buscar.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Codigo, cancelacion);
    }
}
