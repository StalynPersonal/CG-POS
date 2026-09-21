using System.Text.Json;
using CgPos.Central.Aplicacion.Ventas;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Ventas;

internal sealed class ServicioFacturasParaCaja(ContextoDatosCentral contexto, IParametrosCentral parametros, TimeProvider reloj)
    : IServicioFacturasParaCaja
{
    public async Task<DatosFacturaParaCaja?> BuscarAsync(string numeroOEncf, int cajaId, CancellationToken cancelacion = default)
    {
        var buscado = (numeroOEncf ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return null;

        var factura = await contexto.VentasCentral.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Tipo == TipoComprobanteVenta.Factura && (v.Numero == buscado || v.Encf == buscado), cancelacion);
        if (factura is null)
            return null;

        // Las líneas salen del documento que mandó la caja y no de la tabla de reportes: es lo único que conserva el tipo de
        // artículo, los decimales de la cantidad y la tasa de cada línea, y sin eso la caja no puede armar la nota de crédito.
        var documento = await DocumentoDeLaVentaAsync(factura.Numero, cancelacion);
        if (documento is null)
            return null;

        var devuelto = await DevueltoPorLineaAsync(factura.Numero, cancelacion);

        // Lo que otra caja tiene retenido mientras emite su nota cuenta como devuelto: si no, las dos verían lo mismo disponible.
        foreach (var (linea, cantidad) in await RetenidoPorOtrasCajasAsync(factura.Numero, cajaId, cancelacion))
            devuelto[linea] = devuelto.GetValueOrDefault(linea) + cantidad;

        // La mercancía que todavía no se ha entregado tampoco se devuelve (RF-233): el cliente no la tiene. El Central es quien
        // despacha, así que es el único que sabe qué se entregó ya y qué sigue pendiente.
        foreach (var (linea, cantidad) in await PorEntregarAsync(factura.Numero, cancelacion))
            devuelto[linea] = devuelto.GetValueOrDefault(linea) + cantidad;

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        return new DatosFacturaParaCaja(
            factura.Numero,
            factura.Encf,
            sucursales.GetValueOrDefault(factura.SucursalId) ?? string.Empty,
            cajas.GetValueOrDefault(factura.CajaId) ?? string.Empty,
            factura.CajaId == cajaId,
            documento.TipoComprobante,
            factura.FechaOperacion,
            documento.CobradaEn,
            factura.ClienteDocumento,
            factura.ClienteNombre,
            factura.Moneda,
            factura.Total,
            documento.Lineas.Where(l => !l.Anulada && !l.EsReverso).OrderBy(l => l.NumeroLinea)
                .Select(l => new DatosLineaFacturaParaCaja(l.NumeroLinea, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo,
                    l.UnidadMedidaCodigo, l.DecimalesCantidad, l.PorcentajeImpuesto, l.Cantidad, l.PrecioUnitario,
                    l.DescuentoPromocion + l.DescuentoManual + l.DescuentoFactura, decimal.Round(l.Importe - (l.Importe / (1m + l.PorcentajeImpuesto / 100m)), 2,
                        MidpointRounding.AwayFromZero), l.Importe, l.Serial, devuelto.GetValueOrDefault(l.NumeroLinea)))
                .ToList());
    }

    /// <summary>El documento <c>Venta.Cobrada</c> tal como lo mandó la caja. Si llegó dos veces, cualquiera sirve: son idénticos.</summary>
    private async Task<DocumentoVentaCobrada?> DocumentoDeLaVentaAsync(string numero, CancellationToken cancelacion)
    {
        var contenidos = await contexto.DocumentosRecibidos.AsNoTracking()
            .Where(d => d.TipoMensaje == TiposMensaje.VentaCobrada && d.Referencia == numero)
            .Select(d => d.Contenido)
            .ToListAsync(cancelacion);

        foreach (var contenido in contenidos)
        {
            try
            {
                if (JsonSerializer.Deserialize<DocumentoVentaCobrada>(contenido, OpcionesJson.Predeterminadas) is { } documento
                    && string.Equals(documento.Numero, numero, StringComparison.OrdinalIgnoreCase))
                    return documento;
            }
            catch (JsonException)
            {
            }
        }

        return null;
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

    public async Task<RespuestaReservaFactura> ReservarAsync(string facturaNumero, int cajaId, IReadOnlyDictionary<int, decimal> lineas,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        var numero = (facturaNumero ?? string.Empty).Trim().ToUpperInvariant();
        var pedidas = lineas.Where(l => l.Value > 0m).ToDictionary(l => l.Key, l => l.Value);
        if (numero.Length == 0 || pedidas.Count == 0)
            return new RespuestaReservaFactura(false, "Indique la factura y las líneas a reservar.");

        var factura = await contexto.VentasCentral.AsNoTracking().Include(v => v.Lineas)
            .FirstOrDefaultAsync(v => v.Tipo == TipoComprobanteVenta.Factura && v.Numero == numero, cancelacion);
        if (factura is null)
            return new RespuestaReservaFactura(false, $"La factura {numero} no existe en el Central.");

        var minutos = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DevolucionesMinutosReserva, cancelacion);
        var ahora = reloj.Ahora();

        // Las reservas de la factura se bloquean mientras se calcula el disponible: dos cajas no retienen la misma mercancía.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        var reservas = await contexto.ReservasFactura
            .FromSql($"SELECT * FROM ReservasFactura WITH (UPDLOCK, ROWLOCK) WHERE FacturaNumero = {numero} AND CerradaEn IS NULL")
            .Include(r => r.Lineas)
            .ToListAsync(cancelacion);

        foreach (var vencida in reservas.Where(r => !r.EstaVigente(ahora)))
            vencida.Cerrar("Vencida", ahora);

        // Un reintento de la misma caja reemplaza lo que ya tenía retenido de esa factura.
        foreach (var anterior in reservas.Where(r => r.CajaId == cajaId && r.CerradaEn is null))
            anterior.Cerrar("Reemplazada", ahora);

        var devuelto = await DevueltoPorLineaAsync(numero, cancelacion);
        var retenido = reservas.Where(r => r.CerradaEn is null && r.EstaVigente(ahora))
            .SelectMany(r => r.Lineas)
            .GroupBy(l => l.NumeroLinea)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(l => l.Cantidad));

        foreach (var (numeroLinea, cantidad) in pedidas.OrderBy(p => p.Key))
        {
            var vendida = factura.Lineas.FirstOrDefault(l => l.NumeroLinea == numeroLinea)?.Cantidad ?? 0m;
            var libre = vendida - devuelto.GetValueOrDefault(numeroLinea) - retenido.GetValueOrDefault(numeroLinea);
            if (cantidad > libre)
                return new RespuestaReservaFactura(false, libre <= 0m
                    ? $"La línea {numeroLinea} de la factura {numero} ya no tiene nada por devolver: otra caja la está devolviendo ahora mismo."
                    : $"De la línea {numeroLinea} de la factura {numero} solo quedan {libre:0.###} por devolver.");
        }

        var reserva = ReservaFacturaCentral.Crear(numero, cajaId, pedidas, ahora, TimeSpan.FromMinutes(minutos));
        contexto.ReservasFactura.Add(reserva);
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return new RespuestaReservaFactura(true, null, reserva.VenceEn);
    }

    public async Task<bool> LiberarReservaAsync(string facturaNumero, int cajaId, CancellationToken cancelacion = default)
    {
        var numero = (facturaNumero ?? string.Empty).Trim().ToUpperInvariant();
        if (numero.Length == 0)
            return false;

        var abiertas = await contexto.ReservasFactura
            .Where(r => r.FacturaNumero == numero && r.CajaId == cajaId && r.CerradaEn == null)
            .ToListAsync(cancelacion);
        if (abiertas.Count == 0)
            return false;

        var ahora = reloj.Ahora();
        foreach (var reserva in abiertas)
            reserva.Cerrar("Liberada", ahora);

        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    /// <summary>
    /// Cantidad por línea de la factura que sigue pendiente de entregar, en pendientes no anulados (RF-233). Mientras esté en el
    /// pendiente no se puede devolver: primero se anula, que libera la mercancía.
    /// </summary>
    private async Task<Dictionary<int, decimal>> PorEntregarAsync(string facturaNumero, CancellationToken cancelacion)
    {
        var pendientes = await contexto.PendientesEntrega.AsNoTracking().Include(p => p.Lineas)
            .Where(p => p.VentaNumero == facturaNumero && p.Estado != EstadoPendiente.Anulado)
            .ToListAsync(cancelacion);

        return pendientes.SelectMany(p => p.Lineas)
            .GroupBy(l => l.NumeroLineaVenta)
            .Select(grupo => (Linea: grupo.Key, Pendiente: grupo.Sum(l => l.Cantidad - l.CantidadEntregada)))
            .Where(x => x.Pendiente > 0)
            .ToDictionary(x => x.Linea, x => x.Pendiente);
    }

    /// <summary>Lo que otras cajas tienen retenido de esa factura ahora mismo, por línea.</summary>
    private async Task<Dictionary<int, decimal>> RetenidoPorOtrasCajasAsync(string facturaNumero, int cajaId, CancellationToken cancelacion)
    {
        var ahora = reloj.Ahora();
        var reservas = await contexto.ReservasFactura.AsNoTracking().Include(r => r.Lineas)
            .Where(r => r.FacturaNumero == facturaNumero && r.CajaId != cajaId && r.CerradaEn == null && r.VenceEn > ahora)
            .ToListAsync(cancelacion);

        return reservas.SelectMany(r => r.Lineas)
            .GroupBy(l => l.NumeroLinea)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(l => l.Cantidad));
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
