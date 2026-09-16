using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// Arma el modelo de lectura de los reportes con lo que informan las cajas. No guarda: quien lo llama decide la transacción,
/// para que el documento recibido y su reflejo en los reportes queden juntos.
/// </summary>
internal sealed class RegistroVentasCentral(ContextoDatosCentral contexto, TimeProvider reloj)
{
    public async Task RegistrarVentaAsync(DocumentoVentaCobrada documento, Guid sucursalId, Guid cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var venta = documento.Venta;
        if (await contexto.VentasCentral.AnyAsync(c => c.Id == venta.Id, cancelacion))
            return;

        var cobrada = venta.CobradaEn ?? documento.CobradaEn;
        var comprobante = ComprobanteVentaCentral.Registrar(venta.Id, TipoComprobanteVenta.Factura, venta.NumeroTransaccion, sucursalId, cajaId,
            documento.TurnoId, venta.UsuarioNombre, cobrada, DateOnly.FromDateTime(cobrada.LocalDateTime), venta.TipoComprobante, venta.Comprobante?.Encf, null,
            venta.Cliente?.TipoDocumento, venta.Cliente?.Documento, venta.Cliente?.Nombre, venta.Moneda, venta.Totales.Subtotal, venta.Totales.Descuento,
            venta.Totales.Impuesto, 0m, venta.TotalCobrado ?? venta.Totales.Total, venta.Totales.CantidadLineas, reloj.GetUtcNow());

        foreach (var impuesto in venta.Totales.Desglose)
            comprobante.AgregarImpuesto(impuesto.Porcentaje, impuesto.Base, impuesto.Impuesto);

        foreach (var pago in venta.Pagos ?? [])
            comprobante.AgregarPago(pago.Tipo, pago.FormaPagoNombre, pago.Moneda, pago.MontoAplicado);

        contexto.VentasCentral.Add(comprobante);
    }

    public async Task RegistrarNotaCreditoAsync(DocumentoNotaCreditoEmitida documento, Guid sucursalId, Guid cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var nota = documento.NotaCredito;
        if (await contexto.VentasCentral.AnyAsync(c => c.Id == nota.Id, cancelacion))
            return;

        var comprobante = ComprobanteVentaCentral.Registrar(nota.Id, TipoComprobanteVenta.NotaCredito, nota.Numero, sucursalId, cajaId, documento.TurnoId,
            nota.UsuarioNombre, nota.CreadaEn, DateOnly.FromDateTime(nota.CreadaEn.LocalDateTime), CgPos.Dominio.Fiscal.TipoComprobante.NotaCredito,
            nota.Comprobante?.Encf, nota.EncfOrigen, nota.ClienteTipoDocumento, nota.ClienteDocumento, nota.ClienteNombre, nota.Moneda, nota.Subtotal, 0m,
            nota.Impuesto, nota.ImpuestoRetenido, nota.Total, nota.Lineas.Count, reloj.GetUtcNow());

        contexto.VentasCentral.Add(comprobante);
    }

    /// <summary>Un cierre reabierto y vuelto a cerrar llega otra vez: se actualiza con lo último que informó la caja.</summary>
    public async Task RegistrarCierreAsync(DatosCierre cierre, Guid sucursalId, Guid cajaId, CancellationToken cancelacion)
    {
        ArgumentNullException.ThrowIfNull(cierre);
        var ahora = reloj.GetUtcNow();
        var registrado = await contexto.CierresTurno.Include(c => c.FormasPago).SingleOrDefaultAsync(c => c.Id == cierre.Id, cancelacion);

        if (registrado is null)
        {
            registrado = CierreTurnoCentral.Registrar(cierre.Id, cierre.TurnoId, cierre.TurnoNumero, cierre.Numero, sucursalId, cajaId, cierre.FechaOperacion,
                cierre.UsuarioNombre, cierre.Moneda, cierre.Ciego, cierre.FondoInicial, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros,
                cierre.TotalEsperado, cierre.TotalDeclarado, cierre.Diferencia, cierre.AbiertoEn, cierre.CerradoEn, ahora);
            contexto.CierresTurno.Add(registrado);
        }

        registrado.Actualizar(cierre.UsuarioNombre, cierre.Ciego, cierre.FondoInicial, cierre.CantidadVentas, cierre.TotalVentas, cierre.TotalRetiros,
            cierre.TotalEsperado, cierre.TotalDeclarado, cierre.Diferencia, cierre.ReabiertoPorNombre, cierre.ReabiertoEn, cierre.MotivoReapertura, ahora);
        registrado.ReemplazarFormasPago(cierre.FormasPago.Select(f => (f.Tipo, f.Nombre, f.Moneda, f.Transacciones, f.Esperado, f.Declarado, f.Diferencia)));
    }
}
