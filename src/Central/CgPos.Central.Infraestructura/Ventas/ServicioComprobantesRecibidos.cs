using CgPos.Central.Aplicacion.Ventas;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Ventas;

internal sealed class ServicioComprobantesRecibidos(ContextoDatosCentral contexto) : IServicioComprobantesRecibidos
{
    public async Task<PaginaComprobantesRecibidos> ListarAsync(FiltroComprobantesRecibidos filtro, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var consulta = Filtrar(filtro);

        var total = await consulta.CountAsync(cancelacion);
        var suma = await consulta.SumAsync(c => (decimal?)c.Total, cancelacion) ?? 0m;

        var tamano = Math.Clamp(filtro.Tamano, 1, IServicioComprobantesRecibidos.TamanoMaximoPagina);
        var comprobantes = await consulta
            .OrderByDescending(c => c.Fecha)
            .ThenByDescending(c => c.Id)
            .Skip(Math.Max(0, filtro.Pagina) * tamano)
            .Take(tamano)
            .ToListAsync(cancelacion);

        return new PaginaComprobantesRecibidos(await DatosAsync(comprobantes, cancelacion), total, suma);
    }

    public async Task<DatosComprobanteRecibidoDetalle?> ObtenerAsync(int comprobanteId, CancellationToken cancelacion = default)
    {
        var comprobante = await contexto.VentasCentral.AsNoTracking()
            .Include(c => c.Lineas)
            .Include(c => c.Impuestos)
            .Include(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, cancelacion);
        if (comprobante is null)
            return null;

        var datos = (await DatosAsync([comprobante], cancelacion))[0];
        var ecf = comprobante.Encf is { Length: > 0 } encf
            ? await contexto.ComprobantesRecibidos.AsNoTracking()
                .Where(c => c.Encf == encf)
                .Select(c => new { c.MensajeDgii, Tiene = true })
                .FirstOrDefaultAsync(cancelacion)
            : null;

        return new DatosComprobanteRecibidoDetalle(
            datos,
            comprobante.EncfModificado,
            comprobante.ImpuestoRetenido,
            comprobante.Lineas
                .OrderBy(l => l.NumeroLinea)
                .Select(l => new DatosLineaComprobanteRecibido(l.NumeroLinea, l.Codigo, l.Descripcion, l.UnidadMedida, l.Cantidad, l.PrecioUnitario,
                    l.Descuento, l.Impuesto, l.Importe, l.Serial, l.PromocionCodigo))
                .ToList(),
            comprobante.Impuestos
                .OrderByDescending(i => i.Porcentaje)
                .Select(i => new DatosImpuestoComprobanteRecibido(i.Porcentaje, i.Base, i.Impuesto))
                .ToList(),
            comprobante.Pagos
                .Select(p => new DatosPagoComprobanteRecibido(p.Tipo, p.FormaPagoNombre, p.Moneda, p.Monto))
                .ToList(),
            ecf?.Tiene ?? false,
            ecf?.MensajeDgii);
    }

    private IQueryable<ComprobanteVentaCentral> Filtrar(FiltroComprobantesRecibidos filtro)
    {
        var consulta = contexto.VentasCentral.AsNoTracking()
            .Where(c => c.FechaOperacion >= filtro.Desde && c.FechaOperacion <= filtro.Hasta);

        if (filtro.SucursalId is { } sucursalId)
            consulta = consulta.Where(c => c.SucursalId == sucursalId);
        if (filtro.CajaId is { } cajaId)
            consulta = consulta.Where(c => c.CajaId == cajaId);
        if (filtro.Tipo is { } tipo)
            consulta = consulta.Where(c => c.Tipo == tipo);
        if (filtro.Buscar is { Length: > 0 })
        {
            var texto = filtro.Buscar.Trim();
            consulta = consulta.Where(c => c.Numero.Contains(texto) || (c.Encf != null && c.Encf.Contains(texto))
                || (c.ClienteDocumento != null && c.ClienteDocumento.Contains(texto))
                || (c.ClienteNombre != null && c.ClienteNombre.Contains(texto)));
        }

        return consulta;
    }

    private async Task<IReadOnlyList<DatosComprobanteRecibido>> DatosAsync(IReadOnlyList<ComprobanteVentaCentral> comprobantes, CancellationToken cancelacion)
    {
        if (comprobantes.Count == 0)
            return [];

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nombre, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        // El estado en la DGII sale del e-CF recibido, que se guarda aparte con su XML firmado.
        var encf = comprobantes.Select(c => c.Encf).OfType<string>().Distinct().ToList();
        var estados = encf.Count == 0
            ? []
            : await contexto.ComprobantesRecibidos.AsNoTracking()
                .Where(c => encf.Contains(c.Encf))
                .ToDictionaryAsync(c => c.Encf, c => c.EstadoDgii, cancelacion);

        return comprobantes.Select(c => new DatosComprobanteRecibido(
            c.Id,
            c.Tipo,
            c.Numero,
            c.Encf,
            c.TipoComprobanteFiscal,
            sucursales.GetValueOrDefault(c.SucursalId) ?? string.Empty,
            cajas.GetValueOrDefault(c.CajaId) ?? string.Empty,
            c.TurnoNumero,
            c.UsuarioNombre,
            c.Fecha,
            c.FechaOperacion,
            c.ClienteDocumento,
            c.ClienteNombre,
            c.Moneda,
            c.Subtotal,
            c.Descuento,
            c.Impuesto,
            c.Total,
            c.CantidadLineas,
            c.Encf is { } suyo && estados.TryGetValue(suyo, out var estado) ? estado : null,
            c.RegistradoEn)).ToList();
    }
}
