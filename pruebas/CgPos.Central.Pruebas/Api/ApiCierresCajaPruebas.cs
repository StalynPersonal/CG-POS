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
/// Corrección de un cuadre desde el Central. En la caja, cerrar el turno es definitivo: este es el único camino para
/// arreglar un cierre que salió mal, y tiene que dejar rastro de lo que informó la terminal.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiCierresCajaPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Corregir_el_cuadre_recalcula_la_diferencia_y_guarda_lo_que_informo_la_caja()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        // La caja entrega el turno con lo esperado; todavía nadie ha contado el dinero.
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(200, 900));
        var cierre = Cierre(dia, esperado: 5000m);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token,
            Mensaje(TiposMensaje.TurnoCerrado, cierre.TurnoNumero.ToString(System.Globalization.CultureInfo.InvariantCulture), cierre)));

        var pendiente = Assert.Single(await ListarAsync(cliente, admin, dia), c => c.TurnoNumero == cierre.TurnoNumero);
        Assert.True(pendiente.PendienteDeCuadre);
        Assert.Contains(await PendientesAsync(cliente, admin), c => c.Id == pendiente.Id);

        // El supervisor cuenta y declara: faltan 50 pesos en efectivo.
        var formaEfectivo = Assert.Single(pendiente.FormasPago);
        Assert.True((await CuadrarAsync(cliente, admin, pendiente.Id,
            new SolicitudCuadreCierre([new SolicitudDeclaracionCuadre(formaEfectivo.Id, 4950m)], []))).Cuerpo!.Exitosa);

        var registrado = Assert.Single(await ListarAsync(cliente, admin, dia), c => c.TurnoNumero == cierre.TurnoNumero);
        Assert.Equal((-50m, false), (registrado.Diferencia, registrado.EnCierreSucursal));
        Assert.False(registrado.PendienteDeCuadre);
        Assert.False(string.IsNullOrWhiteSpace(registrado.CuadradoPor));
        Assert.Empty(registrado.Ajustes);
        Assert.DoesNotContain(await PendientesAsync(cliente, admin), c => c.Id == pendiente.Id);

        // Sin motivo no se corrige nada.
        var efectivo = Assert.Single(registrado.FormasPago);
        var sinMotivo = await AjustarAsync(cliente, admin, registrado.Id, new SolicitudAjusteCierre(efectivo.Id, 5000m, "   "));
        Assert.Equal("Indique el motivo de la corrección.", sinMotivo.Cuerpo!.Mensaje);

        // Con motivo: aparecieron los 50 pesos, así que el cierre queda cuadrado.
        const string Motivo = "El billete de 50 estaba pegado al de 100; se contó de nuevo con el supervisor.";
        Assert.True((await AjustarAsync(cliente, admin, registrado.Id, new SolicitudAjusteCierre(efectivo.Id, 5000m, Motivo))).Cuerpo!.Exitosa);

        var corregido = Assert.Single(await ListarAsync(cliente, admin, dia), c => c.TurnoNumero == cierre.TurnoNumero);
        Assert.Equal((5000m, 0m), (corregido.TotalDeclarado, corregido.Diferencia));
        Assert.Equal((5000m, 0m), (Assert.Single(corregido.FormasPago).Declarado, Assert.Single(corregido.FormasPago).Diferencia));

        // Queda lo que informó la caja, el motivo y quién corrigió.
        var ajuste = Assert.Single(corregido.Ajustes);
        Assert.Equal((4950m, 5000m, Motivo), (ajuste.DeclaradoAnterior, ajuste.DeclaradoNuevo, ajuste.Motivo));
        Assert.False(string.IsNullOrWhiteSpace(ajuste.AjustadoPorNombre));

        Assert.True(await central.UsarContextoAsync(contexto => contexto.Auditoria.AsNoTracking()
            .AnyAsync(a => a.Accion == "Reportes.CierreAjustado" && a.EntidadId == corregido.Id.ToString())));

        // En el reporte de cuadres se ven las dos cifras: lo que declaró la caja y lo que vale después de la corrección.
        using (var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/reportes/Cuadres?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}", admin)))
        {
            respuesta.EnsureSuccessStatusCode();
            var tabla = (await respuesta.Content.ReadFromJsonAsync<TablaReporte>(OpcionesJson.Predeterminadas))!;
            var fila = Assert.Single(tabla.Filas, f => f[3] == cierre.TurnoNumero.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(("4,950.00", "5,000.00", "0.00", "Sí"), (fila[8], fila[9], fila[10], fila[11]));
        }

        // El mismo cierre reenviado por la caja no borra la corrección: traería otra vez las cifras viejas.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token,
            Mensaje(TiposMensaje.TurnoCerrado, cierre.TurnoNumero.ToString(System.Globalization.CultureInfo.InvariantCulture), cierre)));
        Assert.Equal(5000m, Assert.Single(await ListarAsync(cliente, admin, dia), c => c.TurnoNumero == cierre.TurnoNumero).TotalDeclarado);

        // Y un día ya consolidado en la sucursal deja de poderse corregir.
        Assert.True((await CerrarSucursalAsync(cliente, admin, dia)).Cuerpo!.Exitosa);
        var consolidado = Assert.Single(await ListarAsync(cliente, admin, dia), c => c.TurnoNumero == cierre.TurnoNumero);
        Assert.True(consolidado.EnCierreSucursal);

        var bloqueado = await AjustarAsync(cliente, admin, registrado.Id, new SolicitudAjusteCierre(efectivo.Id, 4900m, "Otra corrección"));
        Assert.Contains("ya tiene su cierre consolidado", bloqueado.Cuerpo!.Mensaje);
    }

    private static DocumentoCierreTurno Cierre(DateOnly dia, decimal esperado) =>
        new(Random.Shared.NextInt64(1_000, 999_999), 1, dia, 1000m, false, "DOP",
            10, esperado, 0m, esperado, "Cajero Desarrollo",
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(-4)),
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-4)),
            [new DocumentoCierreFormaPago("EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 10, esperado)],
            []);

    private static MensajeSincronizacion Mensaje(string tipo, string referencia, object documento)
    {
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var contenido = JsonSerializer.Serialize(documento, OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), tipo, referencia, contenido, HashSincronizacion.Calcular(contenido), sucursal, caja,
            DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<IReadOnlyList<DatosCierreCaja>> ListarAsync(HttpClient cliente, string token, DateOnly dia)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/cierres-caja?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<DatosCierreCaja>>(OpcionesJson.Predeterminadas))!;
    }

    /// <summary>Los cierres de la sucursal de prueba que están esperando al supervisor.</summary>
    private static async Task<IReadOnlyList<DatosCierreCaja>> PendientesAsync(HttpClient cliente, string token)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/cierres-caja/pendientes?sucursalId={CentralEnPruebas.Sucursal}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<DatosCierreCaja>>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CuadrarAsync(HttpClient cliente, string token, int cierreId,
        SolicitudCuadreCierre solicitud)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/manager/cierres-caja/{cierreId}/cuadre", token, solicitud));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> AjustarAsync(HttpClient cliente, string token, int cierreId,
        SolicitudAjusteCierre solicitud)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/manager/cierres-caja/{cierreId}/ajustes", token, solicitud));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CerrarSucursalAsync(HttpClient cliente, string token, DateOnly dia)
    {
        var solicitud = new SolicitudCierreSucursal(CentralEnPruebas.Sucursal, dia, [], "Cierre de prueba");
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/manager/cierres-sucursal", token, solicitud));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }
}
