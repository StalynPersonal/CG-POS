using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// El Central lleva el último acceso de cada usuario de caja: la caja lo avisa al entrar el usuario y aquí se ve el de
/// todas las terminales juntas, sin tener que ir equipo por equipo.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiIngresosUsuarioPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_ingreso_avisado_por_la_caja_queda_como_ultimo_acceso_y_no_retrocede()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var ingreso = new DateTimeOffset(2026, 9, 19, 13, 30, 0, TimeSpan.FromHours(-4));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje("C001", ingreso)));

        var usuario = Assert.Single(await ListarAsync(cliente, admin), u => u.Codigo == "C001");
        Assert.Equal(ingreso, usuario.UltimoIngresoEn);
        Assert.Equal(CentralEnPruebas.CajaUno, usuario.UltimoIngresoCajaId);

        // Un aviso más viejo que llega tarde (la caja estuvo sin red) no retrocede la fecha.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje("C001", ingreso.AddHours(-3))));
        Assert.Equal(ingreso, Assert.Single(await ListarAsync(cliente, admin), u => u.Codigo == "C001").UltimoIngresoEn);

        // Un usuario que no existe en el Central no da el mensaje por malo: se guarda y no toca a nadie.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje("NOEXISTE", ingreso)));

        // El acceso vive en su propia tabla: por eso un ingreso no cambia la versión del usuario y no lo reparte otra vez a
        // todas las cajas. Si esto se guardara en la fila del usuario, cada ingreso daría trabajo de sincronización a todas.
        Task<long> VersionAsync() => central.UsarContextoAsync(contexto =>
            contexto.UsuariosCaja.AsNoTracking().Where(u => u.Codigo == "C001")
                .Select(u => Microsoft.EntityFrameworkCore.EF.Property<long>(u, "Version")).SingleAsync());

        var antes = await VersionAsync();
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje("C001", ingreso.AddHours(1))));
        Assert.Equal(antes, await VersionAsync());
    }

    private static MensajeSincronizacion Mensaje(string usuario, DateTimeOffset ingresoEn)
    {
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var contenido = JsonSerializer.Serialize(new DocumentoIngresoUsuario(usuario, ingresoEn), OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.IngresoUsuario, usuario, contenido, HashSincronizacion.Calcular(contenido),
            sucursal, caja, DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<IReadOnlyList<DatosUsuarioCaja>> ListarAsync(HttpClient cliente, string token)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/usuarios-caja/usuarios", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<DatosUsuarioCaja>>(OpcionesJson.Predeterminadas))!;
    }
}
