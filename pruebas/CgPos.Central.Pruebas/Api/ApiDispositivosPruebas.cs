using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiDispositivosPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Caja_con_credencial_obtiene_su_token_y_cada_token_solo_sirve_para_su_tipo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);
        var credencial = await EmitirAsync(cliente, administrador, CentralEnPruebas.CajaUno);

        Assert.Equal("01", credencial.CajaCodigo);

        var (estado, token) = await PedirTokenAsync(cliente, CentralEnPruebas.CajaUno, credencial.Secreto);
        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.True(token!.Exitoso, token.Mensaje);

        using (var identidad = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/dispositivos/actual", token.Token)))
        {
            var dispositivo = await identidad.Content.ReadFromJsonAsync<DatosDispositivo>(OpcionesJson.Predeterminadas);
            Assert.Equal(CentralEnPruebas.CajaUno, dispositivo!.CajaId);
            Assert.Equal("01", dispositivo.SucursalCodigo);
        }

        using (var cajaComoUsuario = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/sesion/actual", token.Token)))
            Assert.Equal(HttpStatusCode.Forbidden, cajaComoUsuario.StatusCode);

        using var usuarioComoCaja = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/dispositivos/actual", administrador));
        Assert.Equal(HttpStatusCode.Forbidden, usuarioComoCaja.StatusCode);

        // El Central guarda solo el hash del secreto.
        var guardado = await central.UsarContextoAsync(contexto =>
            contexto.CredencialesDispositivo.Where(c => c.CajaId == CentralEnPruebas.CajaUno && c.RevocadaEn == null).Select(c => c.SecretoHash).SingleAsync());
        Assert.NotEqual(credencial.Secreto, guardado);
    }

    [SkippableFact]
    public async Task Secreto_incorrecto_credencial_reemplazada_o_revocada_no_autentican()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);
        var primera = await EmitirAsync(cliente, administrador, CentralEnPruebas.CajaUno);

        var (incorrecto, rechazo) = await PedirTokenAsync(cliente, CentralEnPruebas.CajaUno, primera.Secreto + "x");
        Assert.Equal(HttpStatusCode.Unauthorized, incorrecto);
        Assert.Equal("Credencial de caja no válida.", rechazo!.Mensaje);

        var (_, tokenPrimera) = await PedirTokenAsync(cliente, CentralEnPruebas.CajaUno, primera.Secreto);

        // Emitir otra reemplaza la anterior: su secreto y sus tokens dejan de valer.
        var segunda = await EmitirAsync(cliente, administrador, CentralEnPruebas.CajaUno);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PedirTokenAsync(cliente, CentralEnPruebas.CajaUno, primera.Secreto)).Estado);
        using (var tokenViejo = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/dispositivos/actual", tokenPrimera!.Token)))
            Assert.Equal(HttpStatusCode.Unauthorized, tokenViejo.StatusCode);

        var (_, tokenSegunda) = await PedirTokenAsync(cliente, CentralEnPruebas.CajaUno, segunda.Secreto);
        Assert.True(tokenSegunda!.Exitoso);

        var ruta = $"/api/cajas/{CentralEnPruebas.CajaUno}/credencial/revocar";
        using (var sinMotivo = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, ruta, administrador, new SolicitudRevocacionCredencial(" "))))
            Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        using (var revocada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, ruta, administrador, new SolicitudRevocacionCredencial("Equipo robado"))))
            Assert.Equal(HttpStatusCode.NoContent, revocada.StatusCode);

        using (var otraVez = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, ruta, administrador, new SolicitudRevocacionCredencial("Equipo robado"))))
            Assert.Equal(HttpStatusCode.NotFound, otraVez.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PedirTokenAsync(cliente, CentralEnPruebas.CajaUno, segunda.Secreto)).Estado);
        using var tokenRevocado = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/dispositivos/actual", tokenSegunda.Token));
        Assert.Equal(HttpStatusCode.Unauthorized, tokenRevocado.StatusCode);

        using var cajaInexistente = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cajas/{Guid.CreateVersion7()}/credencial", administrador));
        Assert.Equal(HttpStatusCode.NotFound, cajaInexistente.StatusCode);
    }

    [SkippableFact]
    public async Task Caja_deshabilitada_no_obtiene_token_y_el_que_tenia_deja_de_valer()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);
        var credencial = await EmitirAsync(cliente, administrador, CentralEnPruebas.CajaDos);
        var (_, token) = await PedirTokenAsync(cliente, CentralEnPruebas.CajaDos, credencial.Secreto);

        await HabilitarCajaDosAsync(false);
        try
        {
            using (var conToken = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/dispositivos/actual", token!.Token)))
                Assert.Equal(HttpStatusCode.Unauthorized, conToken.StatusCode);

            var (estado, rechazo) = await PedirTokenAsync(cliente, CentralEnPruebas.CajaDos, credencial.Secreto);
            Assert.Equal(HttpStatusCode.Unauthorized, estado);
            Assert.Equal("La caja está deshabilitada en el Central.", rechazo!.Mensaje);
        }
        finally
        {
            await HabilitarCajaDosAsync(true);
        }

        Assert.True((await PedirTokenAsync(cliente, CentralEnPruebas.CajaDos, credencial.Secreto)).Cuerpo!.Exitoso);
    }

    private Task<int> HabilitarCajaDosAsync(bool habilitada) =>
        central.UsarContextoAsync(async contexto =>
        {
            var caja = await contexto.Cajas.SingleAsync(c => c.Id == CentralEnPruebas.CajaDos);
            if (habilitada) caja.Habilitar(); else caja.Deshabilitar();
            return await contexto.SaveChangesAsync();
        });

    private static async Task<string> TokenAdministradorAsync(HttpClient cliente) =>
        (await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador)).Cuerpo!.TokenAcceso!;

    private static async Task<DatosCredencialDispositivo> EmitirAsync(HttpClient cliente, string tokenAdministrador, Guid cajaId)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cajas/{cajaId}/credencial", tokenAdministrador));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<DatosCredencialDispositivo>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaTokenDispositivo? Cuerpo)> PedirTokenAsync(HttpClient cliente, Guid cajaId, string secreto)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/dispositivos/token", new SolicitudTokenDispositivo(cajaId, secreto), OpcionesJson.Predeterminadas);
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaTokenDispositivo>(OpcionesJson.Predeterminadas));
    }
}
