using System.Globalization;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Reportes;

internal sealed class ServicioReportesCentral(ContextoDatosCentral contexto, IServicioMonitorCentral monitor) : IServicioReportesCentral
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-DO");

    public async Task<IReadOnlyList<DatosVentasReporte>> VentasAsync(FiltroReporte filtro, CancellationToken cancelacion = default)
    {
        var codigos = await CodigosAsync(cancelacion);
        var filas = await Comprobantes(filtro)
            .GroupBy(c => new { c.FechaOperacion, c.SucursalId, c.CajaId })
            .Select(g => new
            {
                g.Key.FechaOperacion,
                g.Key.SucursalId,
                g.Key.CajaId,
                Facturas = g.Count(c => c.Tipo == TipoComprobanteVenta.Factura),
                NotasCredito = g.Count(c => c.Tipo == TipoComprobanteVenta.NotaCredito),
                Subtotal = g.Sum(c => c.Subtotal),
                Descuento = g.Sum(c => c.Descuento),
                Impuesto = g.Sum(c => c.Impuesto),
                Total = g.Sum(c => c.Total),
            })
            .ToListAsync(cancelacion);

        return filas
            .OrderBy(f => f.FechaOperacion)
            .ThenBy(f => codigos.Sucursales.GetValueOrDefault(f.SucursalId))
            .ThenBy(f => codigos.Cajas.GetValueOrDefault(f.CajaId))
            .Select(f => new DatosVentasReporte(f.FechaOperacion, codigos.Sucursales.GetValueOrDefault(f.SucursalId) ?? string.Empty,
                codigos.Cajas.GetValueOrDefault(f.CajaId) ?? string.Empty, f.Facturas, f.NotasCredito, f.Subtotal, f.Descuento, f.Impuesto, f.Total))
            .ToList();
    }

    public async Task<IReadOnlyList<DatosItbisReporte>> ItbisAsync(FiltroReporte filtro, CancellationToken cancelacion = default)
    {
        var comprobantes = Comprobantes(filtro).Select(c => c.Id);
        var filas = await contexto.ImpuestosVenta.AsNoTracking()
            .Where(i => comprobantes.Contains(i.ComprobanteId))
            .GroupBy(i => i.Porcentaje)
            .Select(g => new DatosItbisReporte(g.Key, g.Sum(i => i.Base), g.Sum(i => i.Impuesto), g.Select(i => i.ComprobanteId).Distinct().Count()))
            .ToListAsync(cancelacion);

        return filas.OrderByDescending(f => f.Porcentaje).ToList();
    }

    public async Task<IReadOnlyList<DatosFormato607>> Formato607Async(FiltroReporte filtro, CancellationToken cancelacion = default)
    {
        // Solo lo que tiene e-NCF: el 607 reporta comprobantes fiscales.
        var comprobantes = await Comprobantes(filtro).Where(c => c.Encf != null).OrderBy(c => c.Fecha).ToListAsync(cancelacion);

        return comprobantes.Select(c => new DatosFormato607(
            string.IsNullOrWhiteSpace(c.ClienteDocumento) ? null : DocumentoIdentidad.Normalizar(c.ClienteDocumento),
            c.ClienteTipoDocumento switch
            {
                TipoDocumentoIdentidad.Rnc => 1,
                TipoDocumentoIdentidad.Cedula => 2,
                _ => 3,
            },
            c.TipoComprobanteFiscal,
            c.Encf!,
            c.EncfModificado,
            c.FechaOperacion,
            // El 607 se declara en positivo: la nota de crédito va como su propio registro.
            Math.Abs(c.Subtotal - c.Descuento),
            Math.Abs(c.Impuesto),
            Math.Abs(c.ImpuestoRetenido),
            c.Tipo == TipoComprobanteVenta.NotaCredito ? "Nota de crédito" : "Bienes")).ToList();
    }

    public async Task<IReadOnlyList<DatosCuadreReporte>> CuadresAsync(FiltroReporte filtro, CancellationToken cancelacion = default)
    {
        var codigos = await CodigosAsync(cancelacion);
        var consulta = contexto.CierresTurno.AsNoTracking()
            .Where(c => c.FechaOperacion >= filtro.Desde && c.FechaOperacion <= filtro.Hasta);
        if (filtro.SucursalId is { } sucursal)
            consulta = consulta.Where(c => c.SucursalId == sucursal);
        if (filtro.CajaId is { } caja)
            consulta = consulta.Where(c => c.CajaId == caja);

        var cierres = await consulta.OrderBy(c => c.FechaOperacion).ThenBy(c => c.CerradoEn).ToListAsync(cancelacion);

        return cierres.Select(c => new DatosCuadreReporte(c.FechaOperacion, codigos.Sucursales.GetValueOrDefault(c.SucursalId) ?? string.Empty,
            codigos.Cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.TurnoNumero, c.UsuarioNombre, c.CantidadVentas, c.TotalVentas, c.TotalEsperado,
            c.TotalDeclarado, c.Diferencia, c.Ciego)).ToList();
    }

    public async Task<IReadOnlyList<DatosEcfReporte>> EcfAsync(FiltroReporte filtro, CancellationToken cancelacion = default)
    {
        var codigos = await CodigosAsync(cancelacion);
        // El día es el de aquí, no el del reloj del servidor ni el de UTC.
        var desde = new DateTimeOffset(filtro.Desde.ToDateTime(TimeOnly.MinValue), RelojNegocio.Desfase);
        var hasta = new DateTimeOffset(filtro.Hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), RelojNegocio.Desfase);

        var consulta = contexto.ComprobantesRecibidos.AsNoTracking()
            .Where(c => c.FechaFirma >= desde && c.FechaFirma < hasta);
        if (filtro.SucursalId is { } sucursal)
            consulta = consulta.Where(c => c.SucursalId == sucursal);
        if (filtro.CajaId is { } caja)
            consulta = consulta.Where(c => c.CajaId == caja);

        var comprobantes = await consulta.OrderBy(c => c.Encf).ToListAsync(cancelacion);

        return comprobantes.Select(c => new DatosEcfReporte(c.Encf, c.TipoComprobante, codigos.Sucursales.GetValueOrDefault(c.SucursalId) ?? string.Empty,
            codigos.Cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.RecibidoEn, c.EstadoDgii, c.TrackId, c.MensajeDgii)).ToList();
    }

    public async Task<TablaReporte> TablaAsync(TipoReporteCentral tipo, FiltroReporte filtro, CancellationToken cancelacion = default)
    {
        var subtitulo = $"Del {filtro.Desde:dd/MM/yyyy} al {filtro.Hasta:dd/MM/yyyy}";
        return tipo switch
        {
            TipoReporteCentral.Ventas => TablaVentas(await VentasAsync(filtro, cancelacion), subtitulo),
            TipoReporteCentral.Itbis => TablaItbis(await ItbisAsync(filtro, cancelacion), subtitulo),
            TipoReporteCentral.Formato607 => Tabla607(await Formato607Async(filtro, cancelacion), subtitulo),
            TipoReporteCentral.Cuadres => TablaCuadres(await CuadresAsync(filtro, cancelacion), subtitulo),
            TipoReporteCentral.Ecf => TablaEcf(await EcfAsync(filtro, cancelacion), subtitulo),
            TipoReporteCentral.Sincronizacion => await TablaSincronizacionAsync(cancelacion),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Reporte no válido."),
        };
    }

    private static TablaReporte TablaVentas(IReadOnlyList<DatosVentasReporte> datos, string subtitulo) =>
        new("Ventas", subtitulo,
            ["Fecha", "Sucursal", "Caja", "Facturas", "Notas de crédito", "Subtotal", "Descuento", "ITBIS", "Total"],
            [false, false, false, true, true, true, true, true, true],
            datos.Select(d => (IReadOnlyList<string>)
            [
                d.Fecha.ToString("dd/MM/yyyy"), d.SucursalCodigo, d.CajaCodigo, Entero(d.Facturas), Entero(d.NotasCredito),
                Monto(d.Subtotal), Monto(d.Descuento), Monto(d.Impuesto), Monto(d.Total),
            ]).ToList(),
            [
                "Total", string.Empty, string.Empty, Entero(datos.Sum(d => d.Facturas)), Entero(datos.Sum(d => d.NotasCredito)),
                Monto(datos.Sum(d => d.Subtotal)), Monto(datos.Sum(d => d.Descuento)), Monto(datos.Sum(d => d.Impuesto)), Monto(datos.Sum(d => d.Total)),
            ]);

    private static TablaReporte TablaItbis(IReadOnlyList<DatosItbisReporte> datos, string subtitulo) =>
        new("ITBIS por tasa", subtitulo,
            ["Tasa", "Comprobantes", "Base", "ITBIS"],
            [false, true, true, true],
            datos.Select(d => (IReadOnlyList<string>)
                [$"{d.Porcentaje.ToString("N2", Cultura)} %", Entero(d.Comprobantes), Monto(d.Base), Monto(d.Impuesto)]).ToList(),
            ["Total", Entero(datos.Sum(d => d.Comprobantes)), Monto(datos.Sum(d => d.Base)), Monto(datos.Sum(d => d.Impuesto))]);

    private static TablaReporte Tabla607(IReadOnlyList<DatosFormato607> datos, string subtitulo) =>
        new("Formato 607", subtitulo,
            ["RNC o cédula", "Tipo id.", "Tipo", "e-NCF", "e-NCF modificado", "Fecha", "Monto facturado", "ITBIS facturado", "ITBIS retenido"],
            [false, true, false, false, false, false, true, true, true],
            datos.Select(d => (IReadOnlyList<string>)
            [
                d.Rnc ?? string.Empty, d.TipoIdentificacion.ToString(Cultura), d.TipoComprobante.ToString(), d.Encf, d.EncfModificado ?? string.Empty,
                d.Fecha.ToString("dd/MM/yyyy"), Monto(d.MontoFacturado), Monto(d.ItbisFacturado), Monto(d.ItbisRetenido),
            ]).ToList(),
            [
                "Total", string.Empty, string.Empty, Entero(datos.Count), string.Empty, string.Empty,
                Monto(datos.Sum(d => d.MontoFacturado)), Monto(datos.Sum(d => d.ItbisFacturado)), Monto(datos.Sum(d => d.ItbisRetenido)),
            ]);

    private static TablaReporte TablaCuadres(IReadOnlyList<DatosCuadreReporte> datos, string subtitulo) =>
        new("Cuadres de caja", subtitulo,
            ["Fecha", "Sucursal", "Caja", "Turno", "Cajero", "Ventas", "Total ventas", "Esperado", "Declarado", "Diferencia"],
            [false, false, false, true, false, true, true, true, true, true],
            datos.Select(d => (IReadOnlyList<string>)
            [
                d.Fecha.ToString("dd/MM/yyyy"), d.SucursalCodigo, d.CajaCodigo, d.TurnoNumero.ToString(Cultura), d.UsuarioNombre,
                Entero(d.CantidadVentas), Monto(d.TotalVentas), Monto(d.Esperado), Monto(d.Declarado), Monto(d.Diferencia),
            ]).ToList(),
            [
                "Total", string.Empty, string.Empty, string.Empty, string.Empty, Entero(datos.Sum(d => d.CantidadVentas)),
                Monto(datos.Sum(d => d.TotalVentas)), Monto(datos.Sum(d => d.Esperado)), Monto(datos.Sum(d => d.Declarado)),
                Monto(datos.Sum(d => d.Diferencia)),
            ]);

    private static TablaReporte TablaEcf(IReadOnlyList<DatosEcfReporte> datos, string subtitulo) =>
        new("e-CF y su estado en la DGII", subtitulo,
            ["e-NCF", "Tipo", "Sucursal", "Caja", "Recibido", "Estado DGII", "TrackId", "Mensaje"],
            [false, false, false, false, false, false, false, false],
            datos.Select(d => (IReadOnlyList<string>)
            [
                d.Encf, d.TipoComprobante.ToString(), d.SucursalCodigo, d.CajaCodigo, d.RecibidoEn.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                d.Estado.ToString(), d.TrackId ?? string.Empty, d.Mensaje ?? string.Empty,
            ]).ToList(),
            ["Comprobantes", Entero(datos.Count), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty]);

    private async Task<TablaReporte> TablaSincronizacionAsync(CancellationToken cancelacion)
    {
        var estado = await monitor.ObtenerAsync(cancelacion);
        return new TablaReporte("Sincronización de las cajas", $"Al {DateTimeOffset.Now:dd/MM/yyyy HH:mm}",
            ["Sucursal", "Caja", "Habilitada", "Último mensaje", "Última descarga", "Documentos", "Rechazos", "Conflictos abiertos", "Alerta"],
            [false, false, false, false, false, true, true, true, false],
            estado.Cajas.Select(c => (IReadOnlyList<string>)
            [
                c.SucursalCodigo, c.CajaCodigo, c.Habilitada ? "Sí" : "No",
                c.UltimaRecepcionEn?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Nunca",
                c.UltimaDescargaEn?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Nunca",
                Entero((int)c.MensajesRecibidos), Entero((int)c.Rechazados), Entero(c.ConflictosAbiertos), string.Join("; ", c.Alertas),
            ]).ToList());
    }

    private IQueryable<ComprobanteVentaCentral> Comprobantes(FiltroReporte filtro)
    {
        var consulta = contexto.VentasCentral.AsNoTracking()
            .Where(c => c.FechaOperacion >= filtro.Desde && c.FechaOperacion <= filtro.Hasta);
        if (filtro.SucursalId is { } sucursal)
            consulta = consulta.Where(c => c.SucursalId == sucursal);
        if (filtro.CajaId is { } caja)
            consulta = consulta.Where(c => c.CajaId == caja);

        return consulta;
    }

    private async Task<(Dictionary<int, string> Sucursales, Dictionary<int, string> Cajas)> CodigosAsync(CancellationToken cancelacion) =>
        (await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion),
            await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion));

    private static string Monto(decimal valor) => valor.ToString("N2", Cultura);

    private static string Entero(int valor) => valor.ToString("N0", Cultura);
}
