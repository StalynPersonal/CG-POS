﻿using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiReportesPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Las_ventas_cobradas_alimentan_los_reportes_de_ventas_itbis_y_607()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var dia = new DateOnly(2026, 3, 10);

        var (venta, encf) = Venta(dia, 1000m, 180m);
        var mensaje = Mensaje(TiposMensaje.VentaCobrada, venta.Venta.Id, venta);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, mensaje));

        // Reenviar el mismo mensaje no duplica la venta en los reportes.
        Assert.Equal(EstadoRecepcion.Duplicado, await EnviarAsync(cliente, token, mensaje));

        var ventas = await TablaAsync(cliente, admin, TipoReporteCentral.Ventas, dia, dia);
        var fila = Assert.Single(ventas.Filas, f => f[0] == "10/03/2026");
        Assert.Equal("1", fila[3]);
        Assert.Contains("1,180.00", fila[8]);

        var itbis = await TablaAsync(cliente, admin, TipoReporteCentral.Itbis, dia, dia);
        Assert.Contains(itbis.Filas, f => f[0].StartsWith("18") && f[3] == "180.00");

        var formato = await TablaAsync(cliente, admin, TipoReporteCentral.Formato607, dia, dia);
        var registro = Assert.Single(formato.Filas, f => f[3] == encf);
        Assert.Equal(("401007551", "1", "1,000.00", "180.00"), (registro[0], registro[1], registro[6], registro[7]));

        // El archivo del 607 sale con el encabezado y una línea por comprobante.
        var archivo = await DescargarAsync(cliente, admin, "/api/manager/reportes/formato607/archivo", dia, dia);
        var texto = Encoding.UTF8.GetString(archivo.Contenido);
        Assert.StartsWith("607|", texto);
        Assert.Contains($"|1|{encf}||20260310|1000.00|180.00|0.00", texto);
    }

    [SkippableFact]
    public async Task Los_cierres_de_turno_alimentan_el_reporte_de_cuadres_y_se_exporta_a_excel_y_pdf()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var dia = new DateOnly(2026, 4, 15);

        var cierre = Cierre(dia, esperado: 5000m, declarado: 4950m);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.TurnoCerrado, cierre.Id, cierre)));

        var cuadres = await TablaAsync(cliente, admin, TipoReporteCentral.Cuadres, dia, dia);
        var fila = Assert.Single(cuadres.Filas, f => f[3] == "7");
        Assert.Equal(("5,000.00", "4,950.00", "-50.00"), (fila[7], fila[8], fila[9]));

        // Un cierre reabierto y cerrado otra vez actualiza la fila, no crea otra.
        var corregido = cierre with { TotalDeclarado = 5000m, Diferencia = 0m, ReabiertoPorNombre = "Supervisor", ReabiertoEn = DateTimeOffset.UtcNow,
            MotivoReapertura = "Faltó contar un sobre" };
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.TurnoCerrado, cierre.Id, corregido)));

        var despues = await TablaAsync(cliente, admin, TipoReporteCentral.Cuadres, dia, dia);
        Assert.Equal("0.00", Assert.Single(despues.Filas, f => f[3] == "7")[9]);

        // Excel es un .xlsx legible y el PDF empieza por su cabecera.
        var excel = await DescargarAsync(cliente, admin, "/api/manager/reportes/Cuadres/excel", dia, dia);
        Assert.EndsWith(".xlsx", excel.Nombre);
        using (var paquete = new ZipArchive(new MemoryStream(excel.Contenido), ZipArchiveMode.Read))
        {
            Assert.NotNull(paquete.GetEntry("xl/workbook.xml"));
            using var hoja = new StreamReader(paquete.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            var contenido = await hoja.ReadToEndAsync();
            Assert.Contains("Cuadres de caja", contenido);
            Assert.Contains("5,000.00", contenido);
        }

        var pdf = await DescargarAsync(cliente, admin, "/api/manager/reportes/Cuadres/pdf", dia, dia);
        Assert.StartsWith("%PDF-1.4", Encoding.ASCII.GetString(pdf.Contenido[..8]));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(pdf.Contenido));
    }

    [SkippableFact]
    public async Task Sin_permiso_de_reportes_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINREP{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Reportes#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Reportes#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            "/api/manager/reportes/Ventas?desde=2026-01-01&hasta=2026-01-31", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static (DocumentoVentaCobrada Documento, string Encf) Venta(DateOnly dia, decimal baseImponible, decimal impuesto)
    {
        var encf = $"E32{Random.Shared.NextInt64(1, 9_999_999_999):D10}";
        var cobrada = new DateTimeOffset(dia.ToDateTime(new TimeOnly(11, 30)), TimeSpan.FromHours(-4));
        var total = baseImponible + impuesto;
        var venta = new DatosVenta(Guid.CreateVersion7(), $"01-01-{Random.Shared.Next(100_000, 999_999)}", EstadoVenta.Cobrada, Guid.CreateVersion7(),
            "Cajero Desarrollo", cobrada.AddMinutes(-5),
            [],
            new DatosTotalesVenta(baseImponible, impuesto, total, 1, 1m, [new DatosDesgloseImpuesto(18m, 1, baseImponible, impuesto, total)]),
            TipoComprobante.FacturaCreditoFiscal,
            new DatosClienteVenta(null, TipoDocumentoIdentidad.Rnc, "401007551", "Cliente de Reportes"),
            null, false, false, 250_000m, "DOP", "RD$", null,
            [new DatosPagoVenta(1, Guid.CreateVersion7(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", total, null, total, null, null, null, null, false)],
            total, 0m, 0m, cobrada,
            new DatosComprobanteElectronico(encf, TipoComprobante.FacturaCreditoFiscal, "ABC123", cobrada, "https://ecf.dgii.gov.do/consulta",
                EstadoDocumentoElectronico.PendienteSincronizar));

        return (new DocumentoVentaCobrada(venta, CentralEnPruebas.Sucursal, CentralEnPruebas.CajaUno, venta.TurnoId, Guid.CreateVersion7(), cobrada), encf);
    }

    private static DatosCierre Cierre(DateOnly dia, decimal esperado, decimal declarado) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), 7, 1, CentralEnPruebas.CajaUno, CentralEnPruebas.Sucursal, dia, true, 1000m, false, "DOP",
            12, 5000m, 0m, esperado, declarado, declarado - esperado, "Cajero Desarrollo",
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(-4)),
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-4)), EstadoCierre.Vigente, null, null, null,
            [new DatosCierreFormaPago(Guid.CreateVersion7(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 12, esperado, declarado, declarado - esperado)],
            [], []);

    private static MensajeSincronizacion Mensaje(string tipo, Guid agregadoId, object documento, Guid? id = null)
    {
        var contenido = JsonSerializer.Serialize(documento, OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(id ?? Guid.CreateVersion7(), tipo, agregadoId, contenido, HashSincronizacion.Calcular(contenido),
            CentralEnPruebas.CajaUno, DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<TablaReporte> TablaAsync(HttpClient cliente, string token, TipoReporteCentral tipo, DateOnly desde, DateOnly hasta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/reportes/{tipo}?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<TablaReporte>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(string Nombre, byte[] Contenido)> DescargarAsync(HttpClient cliente, string token, string ruta, DateOnly desde, DateOnly hasta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"{ruta}?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}", token));
        respuesta.EnsureSuccessStatusCode();
        var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName ?? string.Empty;
        return (nombre.Trim('"'), await respuesta.Content.ReadAsByteArrayAsync());
    }
}
