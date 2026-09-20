using System.Text.Json;
using CgPos.Central.Aplicacion.Ventas;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Ventas;

internal sealed class ServicioFacturasParaCaja(ContextoDatosCentral contexto) : IServicioFacturasParaCaja
{
    public async Task<DatosFacturaParaCaja?> BuscarAsync(string numeroOEncf, int cajaId, CancellationToken cancelacion = default)
    {
        var buscado = (numeroOEncf ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return null;

        var factura = await contexto.VentasCentral.AsNoTracking().Include(v => v.Lineas)
            .FirstOrDefaultAsync(v => v.Tipo == TipoComprobanteVenta.Factura && (v.Numero == buscado || v.Encf == buscado), cancelacion);
        if (factura is null)
            return null;

        var devuelto = await DevueltoPorLineaAsync(factura.Numero, cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        return new DatosFacturaParaCaja(
            factura.Numero,
            factura.Encf,
            sucursales.GetValueOrDefault(factura.SucursalId) ?? string.Empty,
            cajas.GetValueOrDefault(factura.CajaId) ?? string.Empty,
            factura.CajaId == cajaId,
            factura.FechaOperacion,
            factura.Fecha,
            factura.ClienteDocumento,
            factura.ClienteNombre,
            factura.Moneda,
            factura.Total,
            factura.Lineas.OrderBy(l => l.NumeroLinea)
                .Select(l => new DatosLineaFacturaParaCaja(l.NumeroLinea, l.Codigo, l.Descripcion, l.UnidadMedida, l.Cantidad, l.PrecioUnitario,
                    l.Descuento, l.Impuesto, l.Importe, l.Serial, devuelto.GetValueOrDefault(l.NumeroLinea)))
                .ToList());
    }

    public async Task<IReadOnlyList<ResumenFacturaParaCaja>> ListarAsync(string? buscar, DateOnly desde, DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var texto = (buscar ?? string.Empty).Trim();
        if (texto.Length == 0)
            return [];

        var consulta = contexto.VentasCentral.AsNoTracking()
            .Where(v => v.Tipo == TipoComprobanteVenta.Factura && v.FechaOperacion >= desde && v.FechaOperacion <= hasta)
            .Where(v => v.Numero.Contains(texto)
                || (v.Encf != null && v.Encf.Contains(texto))
                || (v.ClienteDocumento != null && v.ClienteDocumento.Contains(texto))
                || (v.ClienteNombre != null && v.ClienteNombre.Contains(texto)));

        var facturas = await consulta.OrderByDescending(v => v.Fecha).Take(IServicioFacturasParaCaja.MaximoResultados).ToListAsync(cancelacion);
        if (facturas.Count == 0)
            return [];

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        return facturas.Select(v => new ResumenFacturaParaCaja(v.Numero, v.Encf, sucursales.GetValueOrDefault(v.SucursalId) ?? string.Empty,
            cajas.GetValueOrDefault(v.CajaId) ?? string.Empty, v.FechaOperacion, v.ClienteDocumento, v.ClienteNombre, v.Total)).ToList();
    }

    /// <summary>
    /// Cuánto se devolvió de cada línea de esa factura, en cualquier caja de la empresa. Sale de las notas de crédito que
    /// mandaron las cajas: el Central guarda su documento completo, y es lo único que conserva a qué línea de la factura
    /// corresponde cada devolución.
    /// </summary>
    private async Task<Dictionary<int, decimal>> DevueltoPorLineaAsync(string facturaNumero, CancellationToken cancelacion)
    {
        var documentos = await contexto.DocumentosRecibidos.AsNoTracking()
            .Where(d => d.TipoMensaje == TiposMensaje.NotaCreditoEmitida && d.Contenido.Contains(facturaNumero))
            .Select(d => d.Contenido)
            .ToListAsync(cancelacion);

        var devuelto = new Dictionary<int, decimal>();
        foreach (var contenido in documentos)
        {
            DocumentoNotaCreditoEmitida? nota;
            try
            {
                nota = JsonSerializer.Deserialize<DocumentoNotaCreditoEmitida>(contenido, OpcionesJson.Predeterminadas);
            }
            catch (JsonException)
            {
                continue;
            }

            // El filtro de la consulta busca el número en todo el texto: aquí se comprueba que sea de verdad su factura.
            if (nota is null || !string.Equals(nota.VentaOrigenNumero, facturaNumero, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var linea in nota.Lineas)
                devuelto[linea.NumeroLineaOrigen] = devuelto.GetValueOrDefault(linea.NumeroLineaOrigen) + linea.Cantidad;
        }

        return devuelto;
    }
}
