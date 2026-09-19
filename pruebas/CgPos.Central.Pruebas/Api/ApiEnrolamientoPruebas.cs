using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// Enrolamiento de cajas: una caja recién instalada pide entrar, alguien la acepta en el Central y solo entonces recibe su
/// credencial, atada al equipo que la pidió. Nadie escribe un secreto a mano.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiEnrolamientoPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task La_caja_pide_entrar_y_recibe_su_credencial_cuando_la_aceptan()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        await LiberarYLimpiarAsync(CentralEnPruebas.CajaUno);

        var equipo = Huella();
        var token = Huella();

        // Mientras nadie la acepte, la caja queda esperando y no recibe nada.
        var (estado, primera) = await SolicitarAsync(cliente, sucursal, caja, equipo, token);
        Assert.Equal(HttpStatusCode.Accepted, estado);
        Assert.Equal(EstadoEnrolamientoCaja.Pendiente, primera!.Estado);
        Assert.Null(primera.Secreto);

        // Insistir no la aprueba sola: sigue pendiente y no se acumulan solicitudes repetidas.
        Assert.Equal(EstadoEnrolamientoCaja.Pendiente, (await SolicitarAsync(cliente, sucursal, caja, equipo, token)).Cuerpo!.Estado);

        var solicitud = Assert.Single(await ListarAsync(cliente, administrador), s => s.HuellaEquipo == equipo);
        Assert.Equal((sucursal, caja, "Pendiente", true), (solicitud.SucursalCodigo, solicitud.CajaCodigo, solicitud.Estado, solicitud.CajaExiste));

        using (var aprobada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{solicitud.Id}/aprobar", administrador)))
            Assert.Equal(HttpStatusCode.NoContent, aprobada.StatusCode);

        // Aceptada: ahora sí viaja la credencial, y con ella la caja obtiene su token.
        var (entregada, credencial) = await SolicitarAsync(cliente, sucursal, caja, equipo, token);
        Assert.Equal(HttpStatusCode.OK, entregada);
        Assert.Equal(EstadoEnrolamientoCaja.Entregada, credencial!.Estado);
        Assert.NotNull(credencial.Secreto);

        var (estadoToken, conToken) = await PedirTokenAsync(cliente, sucursal, caja, credencial.Secreto!, equipo);
        Assert.Equal(HttpStatusCode.OK, estadoToken);
        Assert.True(conToken!.Exitoso, conToken.Mensaje);

        // La credencial no se entrega dos veces: un segundo pedido ya no trae nada.
        var (repetida, sinCredencial) = await SolicitarAsync(cliente, sucursal, caja, equipo, token);
        Assert.Equal(HttpStatusCode.Forbidden, repetida);
        Assert.Equal(EstadoEnrolamientoCaja.NoDisponible, sinCredencial!.Estado);
        Assert.Null(sinCredencial.Secreto);

        // El Central guarda la huella del equipo, no el secreto en claro.
        var guardada = await central.UsarContextoAsync(contexto => contexto.CredencialesDispositivo
            .Where(c => c.CajaId == CentralEnPruebas.CajaUno && c.RevocadaEn == null)
            .Select(c => new { c.HuellaEquipo, c.SecretoHash })
            .SingleAsync());
        Assert.Equal(equipo, guardada.HuellaEquipo);
        Assert.NotEqual(credencial.Secreto, guardada.SecretoHash);
    }

    [SkippableFact]
    public async Task La_credencial_de_una_caja_no_sirve_en_otro_equipo_hasta_que_lo_liberan()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaDos);
        await LiberarYLimpiarAsync(CentralEnPruebas.CajaDos);

        var equipoOriginal = Huella();
        var secreto = await EnrolarAsync(cliente, administrador, sucursal, caja, equipoOriginal);

        // El mismo secreto desde otro equipo es una credencial copiada: se rechaza y se dice qué hacer.
        var (rechazo, mensaje) = await PedirTokenAsync(cliente, sucursal, caja, secreto, Huella());
        Assert.Equal(HttpStatusCode.Unauthorized, rechazo);
        Assert.Contains("otro equipo", mensaje!.Mensaje, StringComparison.OrdinalIgnoreCase);

        // El equipo de siempre sigue trabajando sin enterarse.
        Assert.True((await PedirTokenAsync(cliente, sucursal, caja, secreto, equipoOriginal)).Cuerpo!.Exitoso);

        // Una solicitud de otro equipo no se puede aceptar mientras la caja esté atada: primero hay que liberarla.
        var equipoNuevo = Huella();
        await SolicitarAsync(cliente, sucursal, caja, equipoNuevo, Huella());
        var pendiente = Assert.Single(await ListarAsync(cliente, administrador), s => s.HuellaEquipo == equipoNuevo);
        Assert.True(pendiente.CajaConEquipoFijado);

        using (var conflicto = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{pendiente.Id}/aprobar", administrador)))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
            Assert.Contains("libere el equipo", await conflicto.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }

        var liberar = $"/api/cajas/{CentralEnPruebas.CajaDos}/credencial/liberar-equipo";
        using (var sinMotivo = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, liberar, administrador, new SolicitudMotivo(" "))))
            Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        using (var liberada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, liberar, administrador, new SolicitudMotivo("Equipo dañado"))))
            Assert.Equal(HttpStatusCode.NoContent, liberada.StatusCode);

        // Liberada, la caja se instala en el equipo nuevo pidiendo entrar otra vez.
        var secretoNuevo = await EnrolarAsync(cliente, administrador, sucursal, caja, equipoNuevo);
        Assert.True((await PedirTokenAsync(cliente, sucursal, caja, secretoNuevo, equipoNuevo)).Cuerpo!.Exitoso);
        Assert.NotEqual(secreto, secretoNuevo);
    }

    [SkippableFact]
    public async Task Una_solicitud_rechazada_no_entrega_credencial_y_solo_el_dueno_de_la_solicitud_la_recoge()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        await LiberarYLimpiarAsync(CentralEnPruebas.CajaUno);

        var equipo = Huella();
        await SolicitarAsync(cliente, sucursal, caja, equipo, Huella());
        var solicitud = Assert.Single(await ListarAsync(cliente, administrador), s => s.HuellaEquipo == equipo);

        using (var sinMotivo = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{solicitud.Id}/rechazar",
            administrador, new SolicitudMotivo(" "))))
            Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        using (var rechazada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{solicitud.Id}/rechazar",
            administrador, new SolicitudMotivo("No es un equipo de la empresa"))))
            Assert.Equal(HttpStatusCode.NoContent, rechazada.StatusCode);

        // Rechazada y sin credencial: la caja que insiste vuelve a la cola, no se cuela.
        var (estado, respuesta) = await SolicitarAsync(cliente, sucursal, caja, equipo, Huella());
        Assert.Equal(HttpStatusCode.Accepted, estado);
        Assert.Equal(EstadoEnrolamientoCaja.Pendiente, respuesta!.Estado);
        Assert.Null(respuesta.Secreto);

        // Aceptada la nueva, la credencial solo la recoge quien trae el token de esa solicitud.
        var reabierta = Assert.Single(await ListarAsync(cliente, administrador), s => s.HuellaEquipo == equipo);
        using (var aprobada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{reabierta.Id}/aprobar", administrador)))
            Assert.Equal(HttpStatusCode.NoContent, aprobada.StatusCode);

        var (impostor, negada) = await SolicitarAsync(cliente, sucursal, caja, equipo, Huella());
        Assert.Equal(HttpStatusCode.Forbidden, impostor);
        Assert.Equal(EstadoEnrolamientoCaja.NoDisponible, negada!.Estado);
        Assert.Null(negada.Secreto);
    }

    [SkippableFact]
    public async Task Una_caja_que_no_existe_en_el_Central_queda_pendiente_y_se_dice_que_hay_que_crearla()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var administrador = await TokenAdministradorAsync(cliente);

        var equipo = Huella();
        var (estado, respuesta) = await SolicitarAsync(cliente, "01", "99", equipo, Huella());
        Assert.Equal(HttpStatusCode.Accepted, estado);
        Assert.Contains("no existe en el Central", respuesta!.Mensaje);

        var solicitud = Assert.Single(await ListarAsync(cliente, administrador), s => s.HuellaEquipo == equipo);
        Assert.False(solicitud.CajaExiste);

        using var noSeAcepta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{solicitud.Id}/aprobar", administrador));
        Assert.Equal(HttpStatusCode.Conflict, noSeAcepta.StatusCode);

        // Una solicitud sin huella o sin token no se registra.
        using var incompleta = await cliente.PostAsJsonAsync("/api/dispositivos/solicitud",
            new SolicitudEnrolamientoCaja("01", "01", string.Empty, "PC", string.Empty), OpcionesJson.Predeterminadas);
        Assert.Equal(HttpStatusCode.BadRequest, incompleta.StatusCode);
    }

    private static string Huella() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static async Task<string> TokenAdministradorAsync(HttpClient cliente) =>
        (await CentralEnPruebas.IngresarAsync(cliente, "ADMIN", CentralEnPruebas.ContrasenaAdministrador)).Cuerpo!.TokenAcceso!;

    /// <summary>Deja la caja como recién creada: sin equipo atado y sin solicitudes de corridas anteriores.</summary>
    private Task<int> LiberarYLimpiarAsync(int cajaId) =>
        central.UsarContextoAsync(async contexto =>
        {
            foreach (var credencial in await contexto.CredencialesDispositivo.Where(c => c.CajaId == cajaId).ToListAsync())
                credencial.LiberarEquipo();

            contexto.SolicitudesEnrolamiento.RemoveRange(await contexto.SolicitudesEnrolamiento.Where(s => s.CajaId == cajaId).ToListAsync());
            return await contexto.SaveChangesAsync();
        });

    private static async Task<(HttpStatusCode Estado, RespuestaEnrolamientoCaja? Cuerpo)> SolicitarAsync(HttpClient cliente, string sucursal, string caja,
        string huella, string token)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/dispositivos/solicitud",
            new SolicitudEnrolamientoCaja(sucursal, caja, huella, "PC-PRUEBA", token), OpcionesJson.Predeterminadas);
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaEnrolamientoCaja>(OpcionesJson.Predeterminadas));
    }

    private static async Task<IReadOnlyList<DatosSolicitudEnrolamiento>> ListarAsync(HttpClient cliente, string administrador)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/enrolamiento", administrador));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<DatosSolicitudEnrolamiento>>(OpcionesJson.Predeterminadas))!;
    }

    /// <summary>Pide entrar, lo acepta el administrador y devuelve el secreto que recibió la caja.</summary>
    private static async Task<string> EnrolarAsync(HttpClient cliente, string administrador, string sucursal, string caja, string huella)
    {
        var token = Huella();
        await SolicitarAsync(cliente, sucursal, caja, huella, token);
        var solicitud = Assert.Single(await ListarAsync(cliente, administrador), s => s.HuellaEquipo == huella);

        using (var aprobada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/enrolamiento/{solicitud.Id}/aprobar", administrador)))
            Assert.Equal(HttpStatusCode.NoContent, aprobada.StatusCode);

        var (_, credencial) = await SolicitarAsync(cliente, sucursal, caja, huella, token);
        return credencial!.Secreto!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaTokenDispositivo? Cuerpo)> PedirTokenAsync(HttpClient cliente, string sucursal, string caja,
        string secreto, string huella)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/dispositivos/token",
            new SolicitudTokenDispositivo(sucursal, caja, secreto, huella), OpcionesJson.Predeterminadas);
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaTokenDispositivo>(OpcionesJson.Predeterminadas));
    }
}
