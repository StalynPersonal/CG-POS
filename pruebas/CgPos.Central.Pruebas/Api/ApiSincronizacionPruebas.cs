using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using CgPos.Pos.Infraestructura.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiSincronizacionPruebas(CentralEnPruebas central)
{
    private const string Ruta = "/api/sincronizacion/mensajes";

    [SkippableFact]
    public async Task Venta_con_ecf_se_guarda_una_vez_y_el_reenvio_se_confirma_sin_duplicar()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var encf = NuevoEncf();
        var mensaje = MensajeVenta(CentralEnPruebas.CajaUno, encf);

        var primero = await EnviarAsync(cliente, token, mensaje);
        Assert.Equal((HttpStatusCode.OK, EstadoRecepcion.Recibido), (primero.Estado, primero.Cuerpo!.Estado));

        var reenvio = await EnviarAsync(cliente, token, mensaje);
        Assert.Equal((HttpStatusCode.OK, EstadoRecepcion.Duplicado), (reenvio.Estado, reenvio.Cuerpo!.Estado));

        var (documento, comprobante, estado) = await central.UsarContextoAsync(async contexto => (
            await contexto.DocumentosRecibidos.SingleAsync(d => d.Id == mensaje.Id),
            await contexto.ComprobantesRecibidos.SingleAsync(c => c.Encf == encf),
            await contexto.EstadosSincronizacionCaja.SingleAsync(e => e.CajaId == CentralEnPruebas.CajaUno)));

        Assert.Equal(1, documento.Reenvios);
        Assert.Equal(CentralEnPruebas.Sucursal, documento.SucursalId);
        Assert.Equal(mensaje.AgregadoId, comprobante.AgregadoId);
        Assert.Equal(EstadoEnvioDgii.Pendiente, comprobante.EstadoDgii);
        Assert.StartsWith("<ECF>", comprobante.XmlFirmado);
        Assert.True(estado.MensajesRecibidos >= 1 && estado.Duplicados >= 1);
    }

    [SkippableFact]
    public async Task Otro_contenido_con_el_mismo_id_hash_invalido_o_xml_alterado_se_rechazan_y_quedan_como_conflicto()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var original = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf());
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, token, original)).Cuerpo!.Estado);

        var otroContenido = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf(), id: original.Id, agregadoId: original.AgregadoId);
        var distinto = await EnviarAsync(cliente, token, otroContenido);
        Assert.Equal((HttpStatusCode.UnprocessableEntity, EstadoRecepcion.Rechazado), (distinto.Estado, distinto.Cuerpo!.Estado));
        Assert.Contains("otro contenido", distinto.Cuerpo.Error);

        var hashInvalido = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf()) with { HashContenido = new string('A', 64) };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await EnviarAsync(cliente, token, hashInvalido)).Estado);

        var xmlAlterado = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf(), alterarXml: true);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await EnviarAsync(cliente, token, xmlAlterado)).Estado);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await EnviarAsync(cliente, token, xmlAlterado)).Estado);

        var conflictos = await central.UsarContextoAsync(contexto => contexto.ConflictosSincronizacion
            .Where(c => c.MensajeId == original.Id || c.MensajeId == hashInvalido.Id || c.MensajeId == xmlAlterado.Id)
            .ToListAsync());

        Assert.Contains(conflictos, c => c.MensajeId == original.Id && c.Tipo == TipoConflictoSincronizacion.ContenidoDistinto);
        Assert.Contains(conflictos, c => c.MensajeId == hashInvalido.Id && c.Tipo == TipoConflictoSincronizacion.HashInvalido);
        var alterado = Assert.Single(conflictos, c => c.MensajeId == xmlAlterado.Id);
        Assert.Equal((TipoConflictoSincronizacion.XmlAlterado, 2, true), (alterado.Tipo, alterado.Ocurrencias, alterado.Abierto));

        // Nada rechazado se guardó.
        Assert.False(await central.UsarContextoAsync(contexto => contexto.DocumentosRecibidos.AnyAsync(d => d.Id == xmlAlterado.Id || d.Id == hashInvalido.Id)));
    }

    [SkippableFact]
    public async Task Mensaje_con_la_caja_de_otro_token_se_rechaza_como_conflicto()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCajaDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var mensajeCajaUno = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf());

        var respuesta = await EnviarAsync(cliente, tokenCajaDos, mensajeCajaUno);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.Estado);
        var conflicto = await central.UsarContextoAsync(contexto => contexto.ConflictosSincronizacion.SingleAsync(c => c.MensajeId == mensajeCajaUno.Id));
        Assert.Equal((TipoConflictoSincronizacion.CajaNoCoincide, CentralEnPruebas.CajaDos), (conflicto.Tipo, conflicto.CajaId));
    }

    [SkippableFact]
    public async Task Encf_repetido_guarda_la_transaccion_y_registra_el_conflicto_sin_duplicar_el_comprobante()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var encf = NuevoEncf();
        var primera = MensajeVenta(CentralEnPruebas.CajaUno, encf);
        var segunda = MensajeVenta(CentralEnPruebas.CajaUno, encf);

        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, token, primera)).Cuerpo!.Estado);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, token, segunda)).Cuerpo!.Estado);

        var (documentos, comprobantes, conflicto) = await central.UsarContextoAsync(async contexto => (
            await contexto.DocumentosRecibidos.CountAsync(d => d.Id == primera.Id || d.Id == segunda.Id),
            await contexto.ComprobantesRecibidos.Where(c => c.Encf == encf).Select(c => c.DocumentoId).ToListAsync(),
            await contexto.ConflictosSincronizacion.SingleAsync(c => c.MensajeId == segunda.Id)));

        Assert.Equal(2, documentos);
        Assert.Equal([primera.Id], comprobantes);
        Assert.Equal(TipoConflictoSincronizacion.EncfDuplicado, conflicto.Tipo);
    }

    [SkippableFact]
    public async Task La_recepcion_exige_token_de_caja_y_la_clave_de_idempotencia_del_mensaje()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var mensaje = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf());

        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(cliente, null, mensaje)).Estado);
        Assert.Equal(HttpStatusCode.Forbidden, (await EnviarAsync(cliente, await CentralEnPruebas.TokenAdministradorAsync(cliente), mensaje)).Estado);

        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        using var solicitud = CentralEnPruebas.Solicitud(HttpMethod.Post, Ruta, token, mensaje);
        solicitud.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString());
        using var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, respuesta.StatusCode);
    }

    [SkippableFact]
    public async Task El_cliente_de_la_caja_se_autentica_y_sincroniza_contra_el_central_real()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var http = central.CrearCliente();
        var secreto = await CentralEnPruebas.EmitirCredencialAsync(http, CentralEnPruebas.CajaUno);
        var caja = new ClienteCentralHttp(http, CentralEnPruebas.CajaUno, secreto, TimeProvider.System);
        var mensaje = MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf());

        var enviado = await caja.EnviarAsync(mensaje);
        Assert.True(enviado.Confirmado, enviado.Error);
        Assert.True((await caja.EnviarAsync(mensaje)).Confirmado);

        var conOtroSecreto = await new ClienteCentralHttp(http, CentralEnPruebas.CajaUno, secreto + "x", TimeProvider.System).EnviarAsync(MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf()));
        Assert.Equal((false, true), (conOtroSecreto.Confirmado, conOtroSecreto.CentralRespondio));
        Assert.Contains("no autenticó", conOtroSecreto.Error);

        // Credencial revocada: el token guardado deja de valer, la caja pide otro y el Central no la autentica.
        using (var revocar = await http.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cajas/{CentralEnPruebas.CajaUno}/credencial/revocar",
                   await CentralEnPruebas.TokenAdministradorAsync(http), new CgPos.Contratos.Central.SolicitudRevocacionCredencial("Prueba"))))
            revocar.EnsureSuccessStatusCode();

        var revocada = await caja.EnviarAsync(MensajeVenta(CentralEnPruebas.CajaUno, NuevoEncf()));
        Assert.Equal((false, true), (revocada.Confirmado, revocada.CentralRespondio));
    }

    private static string NuevoEncf() => $"E32{Random.Shared.NextInt64(1, 9_999_999_999):D10}";

    private static MensajeSincronizacion MensajeVenta(Guid cajaId, string encf, Guid? id = null, Guid? agregadoId = null, bool alterarXml = false)
    {
        var xml = $"<ECF><Encabezado><eNCF>{encf}</eNCF></Encabezado><Signature>firma</Signature></ECF>";
        var ecf = new DocumentoElectronicoParaCentral(encf, (TipoComprobante)32, alterarXml ? xml.Replace("firma", "otra", StringComparison.Ordinal) : xml,
            HashSincronizacion.Calcular(xml), DateTimeOffset.UtcNow);
        var contenido = JsonSerializer.Serialize(new { venta = new { numeroTransaccion = "01-01-00000001", total = 850.00m }, cajaId, ecf }, OpcionesJson.Predeterminadas);

        return new MensajeSincronizacion(id ?? Guid.CreateVersion7(), TiposMensaje.VentaCobrada, agregadoId ?? Guid.CreateVersion7(), contenido,
            HashSincronizacion.Calcular(contenido), cajaId, DateTimeOffset.UtcNow);
    }

    private static async Task<(HttpStatusCode Estado, RespuestaRecepcionCentral? Cuerpo)> EnviarAsync(HttpClient cliente, string? token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, Ruta, token, mensaje));
        var cuerpo = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, cuerpo);
    }
}
