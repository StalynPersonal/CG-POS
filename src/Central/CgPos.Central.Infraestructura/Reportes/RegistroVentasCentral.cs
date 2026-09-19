using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// Arma el modelo de lectura de los reportes con lo que informan las cajas. No guarda: quien lo llama decide la transacción,
/// para que el documento recibido y su reflejo en los reportes queden juntos.
/// </summary>
internal sealed class RegistroVentasCentral(ContextoDatosCentral contexto, TimeProvider reloj)
{
    /// <summary>La factura se identifica por su número: si ya se registró (un reenvío), no se repite.</summary>
    public async Task RegistrarVentaAsync(DocumentoVentaCobrada venta, int sucursalId, int cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(venta);
        if (await YaRegistradoAsync(TipoComprobanteVenta.Factura, venta.Numero, cancelacion))
            return;

        var cobrada = venta.CobradaEn;
        var comprobante = ComprobanteVentaCentral.Registrar(TipoComprobanteVenta.Factura, venta.Numero.Trim(), sucursalId, cajaId,
            venta.TurnoNumero, venta.UsuarioNombre, cobrada, DateOnly.FromDateTime(cobrada.LocalDateTime), venta.TipoComprobante, venta.Comprobante?.Encf, null,
            venta.Cliente?.TipoDocumento, venta.Cliente?.Documento, venta.Cliente?.Nombre, venta.Moneda, venta.Totales.Subtotal, venta.Totales.Descuento,
            // La retención de la Ley 32-23 se informa como retenido: el cliente pagó el total menos esa retención.
            venta.Totales.Impuesto, venta.Totales.Retencion, venta.TotalCobrado, venta.Totales.CantidadLineas, reloj.Ahora());

        foreach (var impuesto in venta.Totales.Desglose)
            comprobante.AgregarImpuesto(impuesto.Porcentaje, impuesto.Base, impuesto.Impuesto);

        foreach (var pago in venta.Pagos)
            comprobante.AgregarPago(pago.Tipo, pago.FormaPagoNombre, pago.Moneda, pago.MontoAplicado);

        // El detalle se guarda para poder abrir la factura en el Central sin depender del XML (M16).
        foreach (var linea in venta.Lineas.Where(l => !l.Anulada))
            comprobante.AgregarLinea(linea.NumeroLinea, linea.CodigoInterno, linea.Descripcion, linea.UnidadMedidaCodigo, linea.Cantidad,
                linea.PrecioUnitario, linea.DescuentoPromocion + linea.DescuentoManual + linea.DescuentoFactura,
                linea.Importe - decimal.Round(linea.Importe / (1 + (linea.PorcentajeImpuesto / 100m)), 2, MidpointRounding.AwayFromZero),
                linea.Importe, linea.Serial, linea.PromocionCodigo);

        contexto.VentasCentral.Add(comprobante);
    }

    public async Task RegistrarNotaCreditoAsync(DocumentoNotaCreditoEmitida nota, int sucursalId, int cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(nota);
        if (await YaRegistradoAsync(TipoComprobanteVenta.NotaCredito, nota.Numero, cancelacion))
            return;

        var comprobante = ComprobanteVentaCentral.Registrar(TipoComprobanteVenta.NotaCredito, nota.Numero.Trim(), sucursalId, cajaId, nota.TurnoNumero,
            nota.UsuarioNombre, nota.CreadaEn, DateOnly.FromDateTime(nota.CreadaEn.LocalDateTime), CgPos.Dominio.Fiscal.TipoComprobante.NotaCredito,
            nota.Comprobante?.Encf, nota.EncfOrigen, nota.ClienteTipoDocumento, nota.ClienteDocumento, nota.ClienteNombre, nota.Moneda, nota.Subtotal, 0m,
            nota.Impuesto, nota.ImpuestoRetenido, nota.Total, nota.Lineas.Count, reloj.Ahora());

        foreach (var linea in nota.Lineas)
            comprobante.AgregarLinea(linea.NumeroLineaOrigen, linea.CodigoInterno, linea.Descripcion, linea.UnidadMedidaCodigo, linea.Cantidad,
                linea.PrecioUnitario, 0m, linea.Impuesto, linea.Importe, linea.Serial, null);

        contexto.VentasCentral.Add(comprobante);
    }

    /// <summary>
    /// El número de factura o de nota de crédito identifica el documento en toda la empresa: lleva la sucursal, la caja y el tipo, así que solo se
    /// repite si la caja reenvía el mismo documento.
    /// </summary>
    private async Task<bool> YaRegistradoAsync(TipoComprobanteVenta tipo, string numero, CancellationToken cancelacion)
    {
        var buscado = numero.Trim();
        return contexto.VentasCentral.Local.Any(c => c.Tipo == tipo && c.Numero == buscado)
               || await contexto.VentasCentral.AsNoTracking().AnyAsync(c => c.Tipo == tipo && c.Numero == buscado, cancelacion);
    }

    /// <summary>Un cierre reabierto y vuelto a cerrar llega otra vez: se actualiza con lo último que informó la caja. Se identifica por la caja y el turno.</summary>
    public async Task RegistrarCierreAsync(DocumentoCierreTurno cierre, int sucursalId, int cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(cierre);
        var ahora = reloj.Ahora();
        var registrado = await contexto.CierresTurno.Include(c => c.FormasPago)
            .SingleOrDefaultAsync(c => c.CajaId == cajaId && c.TurnoNumero == cierre.TurnoNumero, cancelacion);

        if (registrado is null)
        {
            registrado = CierreTurnoCentral.Registrar(cierre.TurnoNumero, cierre.Numero, sucursalId, cajaId, cierre.FechaOperacion,
                cierre.UsuarioNombre, cierre.Moneda, cierre.Ciego, cierre.FondoInicial, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros,
                cierre.TotalEsperado, cierre.TotalDeclarado, cierre.Diferencia, cierre.AbiertoEn, cierre.CerradoEn, ahora);
            contexto.CierresTurno.Add(registrado);
        }

        registrado.Actualizar(cierre.UsuarioNombre, cierre.Ciego, cierre.FondoInicial, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros,
            cierre.TotalEsperado, cierre.TotalDeclarado, cierre.Diferencia, cierre.ReabiertoPorNombre, cierre.ReabiertoEn, cierre.MotivoReapertura, ahora);
        registrado.ReemplazarFormasPago(cierre.FormasPago.Select(f => (f.Tipo, f.Nombre, f.Moneda, f.Transacciones, f.Esperado, f.Declarado, f.Diferencia)));
    }
}
