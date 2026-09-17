using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using CgPos.Dominio.Ventas;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiMonitorPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_monitor_alerta_los_rechazos_de_la_dgii_y_permite_el_reenvio_dirigido()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var (rechazadoId, encf) = await RegistrarComprobanteAsync(c => c.RegistrarResultado(EstadoEnvioDgii.Rechazado, "Firma inválida", DateTimeOffset.UtcNow, "TRK-R"));
        var (aceptadoId, _) = await RegistrarComprobanteAsync(c => c.RegistrarResultado(EstadoEnvioDgii.Aceptado, null, DateTimeOffset.UtcNow, "TRK-A"));

        var monitor = await ObtenerAsync<DatosMonitorCentral>(cliente, admin, "/api/monitor");
        var caja = Assert.Single(monitor.Cajas, c => c.CajaId == CentralEnPruebas.CajaUno);
        Assert.True(caja.ComprobantesRechazados >= 1);
        Assert.Contains(caja.Alertas, a => a.Contains("rechazados por la DGII"));
        Assert.True(monitor.ComprobantesPorEstado.Single(e => e.Estado == EstadoEnvioDgii.Rechazado).Cantidad >= 1);

        var pagina = await ObtenerAsync<PaginaComprobantesDgii>(cliente, admin, $"/api/monitor/comprobantes?estado=Rechazado&buscar={encf[3..]}");
        var encontrado = Assert.Single(pagina.Elementos);
        Assert.Equal((rechazadoId, "Firma inválida", "01"), (encontrado.Id, encontrado.MensajeDgii, encontrado.CajaCodigo));
        Assert.False(string.IsNullOrEmpty(encontrado.CajaNombre));
        Assert.False(string.IsNullOrEmpty(encontrado.SucursalNombre));

        // Filtrado por sucursal: la suya lo trae, otra sucursal no.
        Assert.Single((await ObtenerAsync<PaginaComprobantesDgii>(cliente, admin,
            $"/api/monitor/comprobantes?buscar={encf[3..]}&sucursalId={CentralEnPruebas.Sucursal}")).Elementos);
        Assert.Empty((await ObtenerAsync<PaginaComprobantesDgii>(cliente, admin,
            $"/api/monitor/comprobantes?buscar={encf[3..]}&sucursalId={Guid.CreateVersion7()}")).Elementos);

        using (var xml = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/monitor/comprobantes/{rechazadoId}/xml", admin)))
            Assert.Contains("<RNCEmisor>", await xml.Content.ReadAsStringAsync());

        Assert.True((await EnviarAsync(cliente, admin, $"/api/monitor/comprobantes/{rechazadoId}/reenviar")).Cuerpo!.Exitosa);
        var reenviado = await central.UsarContextoAsync(c => c.ComprobantesRecibidos.AsNoTracking().SingleAsync(x => x.Id == rechazadoId));
        Assert.Equal((EstadoEnvioDgii.Pendiente, (string?)null), (reenviado.EstadoDgii, reenviado.TrackId));

        Assert.Contains("ya fue aceptado", (await EnviarAsync(cliente, admin, $"/api/monitor/comprobantes/{aceptadoId}/reenviar")).Cuerpo!.Mensaje);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(cliente, admin, $"/api/monitor/comprobantes/{Guid.CreateVersion7()}/reenviar")).Estado);
    }

    [SkippableFact]
    public async Task Un_conflicto_se_resuelve_con_su_resolucion_y_pasa_a_resueltos()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var mensajeId = Guid.CreateVersion7();
        var conflictoId = await central.UsarContextoAsync(async contexto =>
        {
            var conflicto = ConflictoSincronizacion.Registrar(CentralEnPruebas.CajaUno, CentralEnPruebas.Sucursal, mensajeId, "Venta.Cobrada",
                TipoConflictoSincronizacion.EncfDuplicado, "El e-NCF ya llegó en otro documento.", DateTimeOffset.UtcNow);
            contexto.ConflictosSincronizacion.Add(conflicto);
            await contexto.SaveChangesAsync();
            return conflicto.Id;
        });

        Assert.Contains(await ObtenerAsync<List<DatosConflictoSincronizacion>>(cliente, admin, "/api/monitor/conflictos?abiertos=true"), c => c.Id == conflictoId);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await EnviarAsync(cliente, admin, $"/api/monitor/conflictos/{conflictoId}/resolver", new SolicitudResolverConflicto(" "))).Estado);

        Assert.True((await EnviarAsync(cliente, admin, $"/api/monitor/conflictos/{conflictoId}/resolver",
            new SolicitudResolverConflicto("Se anuló la venta repetida en la caja."))).Cuerpo!.Exitosa);

        Assert.DoesNotContain(await ObtenerAsync<List<DatosConflictoSincronizacion>>(cliente, admin, "/api/monitor/conflictos?abiertos=true"), c => c.Id == conflictoId);
        var resuelto = Assert.Single(await ObtenerAsync<List<DatosConflictoSincronizacion>>(cliente, admin, "/api/monitor/conflictos?abiertos=false"), c => c.Id == conflictoId);
        Assert.Equal("Se anuló la venta repetida en la caja.", resuelto.Resolucion);
        Assert.False(string.IsNullOrWhiteSpace(resuelto.ResueltoPor));
        Assert.Contains("ya está resuelto",
            (await EnviarAsync(cliente, admin, $"/api/monitor/conflictos/{conflictoId}/resolver", new SolicitudResolverConflicto("Otra vez"))).Cuerpo!.Mensaje);
    }

    [SkippableFact]
    public async Task Una_caja_que_acaba_de_comunicarse_no_tiene_alerta_de_comunicacion_y_sin_umbral_se_responde_422()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        using (var bajada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sincronizacion/maestros?desde=0", tokenCaja)))
            bajada.EnsureSuccessStatusCode();

        var caja = Assert.Single((await ObtenerAsync<DatosMonitorCentral>(cliente, admin, "/api/monitor")).Cajas, c => c.CajaId == CentralEnPruebas.CajaDos);
        Assert.NotNull(caja.UltimaDescargaEn);
        Assert.DoesNotContain(caja.Alertas, a => a.Contains("comunic"));

        var anterior = await central.CambiarParametroAsync(ClavesParametrosCentral.MonitorMinutosSinComunicacion, null);
        try
        {
            using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/monitor", admin));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.MonitorMinutosSinComunicacion, anterior);
        }
    }

    [SkippableFact]
    public async Task El_monitor_cuenta_las_ventas_que_las_cajas_cobraron_en_contingencia()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var antes = Assert.Single((await ObtenerAsync<DatosMonitorCentral>(cliente, admin, "/api/monitor")).Cajas, c => c.CajaId == CentralEnPruebas.CajaUno)
            .VentasEnContingencia;

        // Una venta cobrada sin poder firmar su e-CF llega al Central sin comprobante.
        var contenido = JsonSerializer.Serialize(VentaSinEcf(), OpcionesJson.Predeterminadas);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.VentaCobrada, Guid.CreateVersion7(), contenido,
            HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno).Sucursal, CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno).Caja, DateTimeOffset.UtcNow);
        using (var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", tokenCaja, mensaje)))
            respuesta.EnsureSuccessStatusCode();

        var monitor = await ObtenerAsync<DatosMonitorCentral>(cliente, admin, "/api/monitor");
        var caja = Assert.Single(monitor.Cajas, c => c.CajaId == CentralEnPruebas.CajaUno);
        Assert.Equal(antes + 1, caja.VentasEnContingencia);
        Assert.True(monitor.VentasEnContingencia >= caja.VentasEnContingencia);
        Assert.Contains(caja.Alertas, a => a.Contains("contingencia"));
    }

    /// <summary>Venta cobrada que la caja no pudo firmar: viaja sin e-CF y el Central la cuenta como contingencia.</summary>
    private static DocumentoVentaCobrada VentaSinEcf()
    {
        var cobrada = DateTimeOffset.UtcNow;
        var venta = new DatosVenta(Guid.CreateVersion7(), $"0101{Guid.NewGuid().ToString("N")[..12]}", EstadoVenta.Cobrada, Guid.CreateVersion7(),
            "Cajero Desarrollo", cobrada.AddMinutes(-3), [],
            new DatosTotalesVenta(1000m, 180m, 1180m, 1, 1m, [new DatosDesgloseImpuesto(18m, 1, 1000m, 180m, 1180m)]),
            TipoComprobante.FacturaConsumo, null, null, false, false, 250_000m, "DOP", "RD$", null, null, 1180m, 0m, 0m, cobrada);

        return new DocumentoVentaCobrada(venta, CentralEnPruebas.Sucursal, CentralEnPruebas.CajaUno, venta.TurnoId, Guid.CreateVersion7(), cobrada);
    }

    [SkippableFact]
    public async Task Sin_permiso_de_monitoreo_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINMON{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Monitor#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Monitor#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/monitor", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private async Task<(Guid Id, string Encf)> RegistrarComprobanteAsync(Action<ComprobanteRecibido> preparar)
    {
        var encf = $"E31{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        var id = await central.UsarContextoAsync(async contexto =>
        {
            var ahora = DateTimeOffset.UtcNow;
            var documento = DocumentoRecibido.Recibir(Guid.CreateVersion7(), CentralEnPruebas.CajaUno, CentralEnPruebas.Sucursal, "Venta.Cobrada", Guid.CreateVersion7(),
                "{}", new string('A', DocumentoRecibido.LargoHash), ahora, ahora);
            var comprobante = ComprobanteRecibido.Registrar(documento, encf, TipoComprobante.FacturaCreditoFiscal,
                "<ECF><Encabezado><Emisor><RNCEmisor>131246796</RNCEmisor></Emisor></Encabezado></ECF>", new string('B', DocumentoRecibido.LargoHash), ahora, ahora);
            preparar(comprobante);
            contexto.DocumentosRecibidos.Add(documento);
            contexto.ComprobantesRecibidos.Add(comprobante);
            await contexto.SaveChangesAsync();
            return comprobante.Id;
        });
        return (id, encf);
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, string ruta, object? cuerpo = null)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
