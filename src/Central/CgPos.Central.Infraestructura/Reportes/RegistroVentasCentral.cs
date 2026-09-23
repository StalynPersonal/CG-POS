using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Organizacion;
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
internal sealed class RegistroVentasCentral(ContextoDatosCentral contexto, INumeracionCentral numeracion, TimeProvider reloj)
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
                linea.Impuesto, linea.ImporteConImpuesto, linea.Serial, linea.PromocionCodigo);

        await NumerarAsync(comprobante, DocumentosNumerados.Factura, cancelacion);
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

        await NumerarAsync(comprobante, DocumentosNumerados.NotaCredito, cancelacion);
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

    /// <summary>
    /// Guarda el cierre que informa una caja, identificado por la caja y el turno: un reenvío del mismo mensaje actualiza la
    /// fila en vez de duplicarla. Los ajustes se traen a propósito: un cierre ya corregido aquí no se pisa con lo que la
    /// caja mandó en su momento.
    /// </summary>
    public async Task RegistrarCierreAsync(DocumentoCierreTurno cierre, int sucursalId, int cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(cierre);
        var ahora = reloj.Ahora();
        var registrado = await contexto.CierresTurno.Include(c => c.FormasPago).Include(c => c.Ajustes)
            .SingleOrDefaultAsync(c => c.CajaId == cajaId && c.TurnoNumero == cierre.TurnoNumero, cancelacion);

        if (registrado is null)
        {
            registrado = CierreTurnoCentral.Registrar(cierre.TurnoNumero, cierre.Numero, sucursalId, cajaId, cierre.FechaOperacion,
                cierre.UsuarioNombre, cierre.Moneda, cierre.FondoInicial, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros,
                cierre.TotalEsperado, cierre.AbiertoEn, cierre.CerradoEn, ahora);
            contexto.CierresTurno.Add(registrado);
        }

        registrado.Actualizar(cierre.UsuarioNombre, cierre.FondoInicial, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros,
            cierre.TotalEsperado, ahora);
        registrado.ReemplazarFormasPago(cierre.FormasPago.Select(f => (f.Tipo, f.Nombre, f.Moneda, f.Transacciones, f.Esperado)));
    }

    /// <summary>
    /// Le pone al documento el número del Central. Si su secuencia falta o está apagada NO se rechaza el documento: la caja
    /// ya lo emitió y el dato fiscal no se puede perder. Se guarda sin número y queda para asignárselo cuando se configure.
    /// </summary>
    private async Task NumerarAsync(ComprobanteVentaCentral comprobante, string codigoDocumento, CancellationToken cancelacion)
    {
        try
        {
            comprobante.AsignarNumeroCentral(await numeracion.SiguienteAsync(codigoDocumento, cancelacion));
        }
        catch (SecuenciaCentralNoConfiguradaExcepcion)
        {
            // Queda sin número del Central; el documento entra igual.
        }
    }
}
