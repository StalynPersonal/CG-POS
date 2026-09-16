using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiNotasCreditoPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Una_nota_de_credito_de_otra_sucursal_se_consulta_reserva_y_consume_una_sola_vez()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var (mensaje, notaId, encf) = MensajeEmision(CentralEnPruebas.CajaUno, 1000m, DateOnly.FromDateTime(DateTime.Today).AddMonths(6));
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenUno, mensaje)).Estado);

        // Otra caja, de otra sucursal, ve el saldo y reserva parte.
        var nota = await ObtenerAsync<DatosNotaCreditoCentral>(cliente, tokenDos, $"/api/notas-credito/{encf}");
        Assert.Equal((notaId, 1000m, 1000m, EstadoNotaCreditoCentral.Vigente), (nota.Id, nota.Total, nota.Disponible, nota.Estado));

        var reserva = await ReservarAsync(cliente, tokenDos, notaId, 400m);
        Assert.True(reserva.Exitosa, reserva.Mensaje);
        Assert.Equal(400m, reserva.Monto);
        Assert.Equal(600m, (await ObtenerAsync<DatosNotaCreditoCentral>(cliente, tokenDos, $"/api/notas-credito/{encf}")).Disponible);

        // Pedir más de lo disponible reserva lo que queda y lo avisa.
        var parcial = await ReservarAsync(cliente, tokenDos, notaId, 1000m);
        Assert.Equal((true, 600m), (parcial.Exitosa, parcial.Monto));
        Assert.Contains("Solo hay", parcial.Mensaje);

        // Liberar devuelve el saldo retenido.
        using (var liberada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Delete, $"/api/notas-credito/reservas/{parcial.ReservaId}", tokenDos)))
            Assert.Equal(HttpStatusCode.NoContent, liberada.StatusCode);
        Assert.Equal(600m, (await ObtenerAsync<DatosNotaCreditoCentral>(cliente, tokenDos, $"/api/notas-credito/{encf}")).Disponible);

        // El consumo descuenta el saldo y cierra la reserva; repetirlo no lo descuenta dos veces.
        var ventaId = Guid.CreateVersion7();
        var consumo = MensajeConsumo(CentralEnPruebas.CajaDos, notaId, encf, ventaId, 400m);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenDos, consumo)).Estado);
        Assert.Equal(EstadoRecepcion.Duplicado, (await EnviarAsync(cliente, tokenDos, consumo)).Estado);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenDos, MensajeConsumo(CentralEnPruebas.CajaDos, notaId, encf, ventaId, 400m))).Estado);

        var despues = await ObtenerAsync<DatosNotaCreditoCentral>(cliente, tokenDos, $"/api/notas-credito/{encf}");
        Assert.Equal((400m, 600m), (despues.Consumido, despues.Disponible));
        Assert.False(despues.Sobregirada);
    }

    [SkippableFact]
    public async Task Un_consumo_que_llega_antes_que_la_emision_se_descuenta_al_registrarla()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var (emision, notaId, encf) = MensajeEmision(CentralEnPruebas.CajaUno, 500m, DateOnly.FromDateTime(DateTime.Today).AddMonths(3));

        var consumo = MensajeConsumo(CentralEnPruebas.CajaDos, notaId, encf, Guid.CreateVersion7(), 200m);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenDos, consumo)).Estado);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenUno, emision)).Estado);

        var nota = await ObtenerAsync<DatosNotaCreditoCentral>(cliente, tokenDos, $"/api/notas-credito/{encf}");
        Assert.Equal((500m, 200m, 300m), (nota.Total, nota.Consumido, nota.Disponible));
    }

    [SkippableFact]
    public async Task Una_nota_vencida_no_se_reserva_y_el_central_puede_habilitarla_dentro_del_maximo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var (mensaje, notaId, encf) = MensajeEmision(CentralEnPruebas.CajaUno, 750m, DateOnly.FromDateTime(DateTime.Today).AddMonths(-2));
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenUno, mensaje)).Estado);

        var vencida = await ReservarAsync(cliente, tokenUno, notaId, 100m);
        Assert.False(vencida.Exitosa);
        Assert.Contains("venció", vencida.Mensaje);

        // En desarrollo el máximo son 12 meses desde la emisión.
        var excesiva = await ProrrogarAsync(cliente, admin, notaId, DateOnly.FromDateTime(DateTime.Today).AddYears(3), "Cliente reclamó tarde");
        Assert.Equal(HttpStatusCode.BadRequest, excesiva.Estado);
        Assert.Contains("no puede pasar", excesiva.Cuerpo!.Mensaje);

        var nuevaFecha = DateOnly.FromDateTime(DateTime.Today).AddMonths(1);
        Assert.True((await ProrrogarAsync(cliente, admin, notaId, nuevaFecha, "Autorizado por gerencia")).Cuerpo!.Exitosa);

        var habilitada = await ObtenerAsync<DatosNotaCreditoCentral>(cliente, tokenUno, $"/api/notas-credito/{encf}");
        Assert.Equal((EstadoNotaCreditoCentral.Vigente, nuevaFecha, "Autorizado por gerencia"), (habilitada.Estado, habilitada.VenceEn, habilitada.MotivoProrroga));
        Assert.True((await ReservarAsync(cliente, tokenUno, notaId, 100m)).Exitosa);

        var pagina = await ObtenerAsync<PaginaNotasCreditoCentral>(cliente, admin, $"/api/manager/notas-credito?buscar={encf}");
        Assert.Equal(notaId, Assert.Single(pagina.Elementos).Id);
        var movimientos = await ObtenerAsync<List<DatosMovimientoNotaCredito>>(cliente, admin, $"/api/manager/notas-credito/{notaId}/movimientos");
        Assert.Contains(movimientos, m => m.Tipo == "Reserva");
        Assert.Contains(movimientos, m => m.Tipo == "Prórroga");
    }

    [SkippableFact]
    public async Task Sin_permiso_de_notas_de_credito_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINNC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Notas#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Notas#2026")).Cuerpo!.TokenAcceso!;

        using var manager = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/notas-credito", token));
        using var caja = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/notas-credito/E340000000001", token));

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (manager.StatusCode, caja.StatusCode));
    }

    private static (MensajeSincronizacion Mensaje, Guid NotaId, string Encf) MensajeEmision(Guid cajaId, decimal total, DateOnly venceEn)
    {
        var notaId = Guid.CreateVersion7();
        var encf = $"E34{Random.Shared.NextInt64(1, 9_999_999_999):D10}";
        var xml = $"<ECF><Encabezado><eNCF>{encf}</eNCF></Encabezado><Signature>firma</Signature></ECF>";
        var comprobante = new DatosComprobanteElectronico(encf, TipoComprobante.NotaCredito, "ABC123", DateTimeOffset.UtcNow, "https://ecf.dgii.gov.do/consulta",
            EstadoDocumentoElectronico.PendienteSincronizar);
        var nota = new DatosNotaCredito(notaId, $"NC-{encf[3..]}", Guid.CreateVersion7(), "01-01-00000001", "E320000000001", DateTimeOffset.UtcNow.AddDays(-1),
            TipoDocumentoIdentidad.Cedula, "00113918205", "Cliente de prueba", "DEV", "Devolucion", null, "Cajero Prueba", null, false, true,
            total / 1.18m, total - (total / 1.18m), 0m, total, total, "DOP", venceEn, EstadoNotaCredito.Vigente, DateTimeOffset.UtcNow, [], comprobante);
        var ecf = new DocumentoElectronicoParaCentral(encf, TipoComprobante.NotaCredito, xml, HashSincronizacion.Calcular(xml), DateTimeOffset.UtcNow);
        var contenido = JsonSerializer.Serialize(new DocumentoNotaCreditoEmitida(nota, CentralEnPruebas.Sucursal, cajaId, null, ecf), OpcionesJson.Predeterminadas);

        return (new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.NotaCreditoEmitida, notaId, contenido, HashSincronizacion.Calcular(contenido), cajaId,
            DateTimeOffset.UtcNow), notaId, encf);
    }

    private static MensajeSincronizacion MensajeConsumo(Guid cajaId, Guid notaId, string encf, Guid ventaId, decimal monto)
    {
        var contenido = JsonSerializer.Serialize(
            new DocumentoConsumoNotaCredito(notaId, encf, ventaId, "02-01-00000005", cajaId, monto, 0m, DateTimeOffset.UtcNow), OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.NotaCreditoConsumida, notaId, contenido, HashSincronizacion.Calcular(contenido), cajaId,
            DateTimeOffset.UtcNow);
    }

    private static async Task<RespuestaRecepcionCentral> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<RespuestaReservaNotaCredito> ReservarAsync(HttpClient cliente, string token, Guid notaId, decimal monto)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/notas-credito/{notaId}/reservas", token,
            new SolicitudReservaNotaCredito(monto)));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaReservaNotaCredito>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> ProrrogarAsync(HttpClient cliente, string token, Guid notaId, DateOnly venceEn,
        string motivo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/manager/notas-credito/{notaId}/prorrogar", token,
            new SolicitudProrrogaNotaCredito(venceEn, motivo)));
        var cuerpo = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, cuerpo);
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
