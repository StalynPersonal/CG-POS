using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Pagos;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// Módulo de cuadre: el supervisor de la tienda entra con su usuario de caja, ve los cierres que le entregaron y declara
/// lo que contó. La caja cierra sin declarar nada, así que sin este paso el cierre queda pendiente.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiCuadrePruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_supervisor_entra_con_su_usuario_de_caja_y_cuadra_el_cierre_que_le_entregaron()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        // La caja entrega el turno: informa lo esperado y nada más.
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(200, 900));
        var turno = Random.Shared.NextInt64(1_000, 999_999);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarCierreAsync(cliente, tokenCaja, dia, turno, esperado: 3000m, tarjeta: 1500m));

        // Con una clave incorrecta no se entra.
        Assert.Null((await IngresarAsync(cliente, "S001", "esta-no-es")).TokenAcceso);

        var sesion = await IngresarAsync(cliente, "S001", "Supervisor.2026");
        Assert.True(sesion.Exitosa, sesion.Mensaje);
        var token = sesion.TokenAcceso!;
        Assert.Contains("Cuadre.Declarar", sesion.Sesion!.Permisos);
        var sucursal = Assert.Single(sesion.Sesion.Sucursales).Id;

        // Ve su cierre esperando el conteo.
        var pendiente = Assert.Single(await PendientesAsync(cliente, token, sucursal), c => c.TurnoNumero == turno);
        Assert.True(pendiente.PendienteDeCuadre);
        Assert.Equal(4500m, pendiente.TotalEsperado);

        var efectivo = pendiente.FormasPago.Single(f => f.Tipo == TipoFormaPago.Efectivo);
        var tarjeta = pendiente.FormasPago.Single(f => f.Tipo == TipoFormaPago.Tarjeta);
        var denominaciones = await DenominacionesAsync(cliente, token);
        var billete = denominaciones.First(d => d.Moneda == "DOP" && d.Valor == 1000m);

        // El efectivo declarado tiene que ser el que sale del conteo.
        var conteoMalo = new SolicitudCuadreCierre(
            [new SolicitudDeclaracionCuadre(efectivo.Id, 3000m), new SolicitudDeclaracionCuadre(tarjeta.Id, 1500m)],
            [new SolicitudConteoCuadre(billete.Id, 2)]);
        Assert.Contains("no coincide con el conteo", (await CuadrarAsync(cliente, token, pendiente.Id, conteoMalo)).Cuerpo!.Mensaje);

        // Faltan 1,000 pesos: se contaron dos billetes de mil y el esperado eran tres.
        var cuadre = new SolicitudCuadreCierre(
            [new SolicitudDeclaracionCuadre(efectivo.Id, 2000m), new SolicitudDeclaracionCuadre(tarjeta.Id, 1500m)],
            [new SolicitudConteoCuadre(billete.Id, 2)]);
        Assert.True((await CuadrarAsync(cliente, token, pendiente.Id, cuadre)).Cuerpo!.Exitosa);

        Assert.DoesNotContain(await PendientesAsync(cliente, token, sucursal), c => c.TurnoNumero == turno);

        var cuadrado = Assert.Single(await CierresAsync(cliente, token, sucursal, dia), c => c.TurnoNumero == turno);
        Assert.False(cuadrado.PendienteDeCuadre);
        Assert.Equal((3500m, -1000m), (cuadrado.TotalDeclarado, cuadrado.Diferencia));
        Assert.Equal("Supervisor Desarrollo", cuadrado.CuadradoPor);

        // Cuadrar dos veces no se puede: para arreglar un cuadre está la corrección.
        Assert.Contains("ya lo cuadró", (await CuadrarAsync(cliente, token, pendiente.Id, cuadre)).Cuerpo!.Mensaje);

        Assert.True(await central.UsarContextoAsync(contexto => contexto.Auditoria.AsNoTracking()
            .AnyAsync(a => a.Accion == "Cuadre.CierreCuadrado" && a.EntidadId == cuadrado.Id.ToString())));
    }

    [SkippableFact]
    public async Task El_cajero_sin_permiso_de_cuadre_no_entra_al_modulo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();

        var rechazado = await IngresarAsync(cliente, "C001", "Cajero.2026");

        Assert.False(rechazado.Exitosa);
        Assert.Contains("no tiene acceso", rechazado.Mensaje);
    }

    private static async Task<RespuestaSesionCuadre> IngresarAsync(HttpClient cliente, string usuario, string clave)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/cuadre/ingreso", null,
            new SolicitudIngresoCuadre(usuario, clave)));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCuadre>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<IReadOnlyList<DatosCierreCaja>> PendientesAsync(HttpClient cliente, string token, int sucursalId) =>
        await ObtenerAsync<List<DatosCierreCaja>>(cliente, token, $"/api/cuadre/pendientes?sucursalId={sucursalId}");

    private static async Task<IReadOnlyList<DatosCierreCaja>> CierresAsync(HttpClient cliente, string token, int sucursalId, DateOnly dia) =>
        await ObtenerAsync<List<DatosCierreCaja>>(cliente, token, $"/api/cuadre/cierres?sucursalId={sucursalId}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}");

    private static async Task<IReadOnlyList<CgPos.Contratos.Catalogo.DatosDenominacion>> DenominacionesAsync(HttpClient cliente, string token) =>
        await ObtenerAsync<List<CgPos.Contratos.Catalogo.DatosDenominacion>>(cliente, token, "/api/cuadre/denominaciones");

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CuadrarAsync(HttpClient cliente, string token, int cierreId,
        SolicitudCuadreCierre solicitud)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cuadre/{cierreId}", token, solicitud));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }

    private static async Task<EstadoRecepcion?> EnviarCierreAsync(HttpClient cliente, string token, DateOnly dia, long turno, decimal esperado, decimal tarjeta)
    {
        var cierre = new DocumentoCierreTurno(turno, 1, dia, 0m, false, "DOP", 8, esperado + tarjeta, 0m, esperado + tarjeta, "Cajero Desarrollo",
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(-4)),
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-4)),
            [
                new DocumentoCierreFormaPago("EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 8, esperado),
                new DocumentoCierreFormaPago("TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 2, tarjeta),
            ],
            []);

        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var contenido = JsonSerializer.Serialize(cierre, OpcionesJson.Predeterminadas);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.TurnoCerrado,
            turno.ToString(System.Globalization.CultureInfo.InvariantCulture), contenido, HashSincronizacion.Calcular(contenido), sucursal, caja,
            DateTimeOffset.UtcNow);

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }
}
