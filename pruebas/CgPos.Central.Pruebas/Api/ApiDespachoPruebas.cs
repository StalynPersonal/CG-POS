using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiDespachoPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_central_ve_los_pendientes_de_todas_las_sucursales_y_marca_los_atrasados()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var atrasado = Pendiente(CentralEnPruebas.CajaUno, MetodoEntrega.Envio, EstadoPendiente.Pendiente,
            DateOnly.FromDateTime(DateTime.Today).AddDays(-3), 4m, 0m);
        var aTiempo = Pendiente(CentralEnPruebas.CajaDos, MetodoEntrega.RetiroAlmacen, EstadoPendiente.Preparado,
            DateOnly.FromDateTime(DateTime.Today).AddDays(5), 2m, 0m);

        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenUno, Mensaje(TiposMensaje.PendienteCreado, atrasado, CentralEnPruebas.CajaUno)));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenDos, Mensaje(TiposMensaje.PendienteCreado, aTiempo, CentralEnPruebas.CajaDos)));

        // Los atrasados salen primero y quedan marcados.
        var pagina = await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, "/api/manager/despacho/pendientes?soloAtrasados=true");
        var resumen = Assert.Single(pagina.Elementos, p => p.Numero == atrasado.Numero);
        Assert.True(resumen.Atrasado);
        Assert.Equal((MetodoEntrega.Envio, EstadoPendiente.Pendiente, 4m), (resumen.Metodo, resumen.Estado, resumen.Unidades));
        Assert.DoesNotContain(pagina.Elementos, p => p.Numero == aTiempo.Numero);

        // La búsqueda encuentra por cliente, teléfono o factura.
        var porCliente = await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={aTiempo.VentaNumero}");
        Assert.Equal(aTiempo.Numero, Assert.Single(porCliente.Elementos).Numero);

        var detalle = await ObtenerAsync<DetallePendienteCentral>(cliente, admin, $"/api/manager/despacho/pendientes/{atrasado.Id}");
        Assert.Equal(atrasado.Numero, detalle.Pendiente.Numero);
        Assert.Equal("Cincel", Assert.Single(detalle.Pendiente.Lineas).Descripcion);
    }

    [SkippableFact]
    public async Task La_actualizacion_de_la_caja_manda_y_un_mensaje_viejo_no_pisa_el_estado_mas_nuevo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var creado = Pendiente(CentralEnPruebas.CajaUno, MetodoEntrega.RetiroAlmacen, EstadoPendiente.Pendiente, DateOnly.FromDateTime(DateTime.Today), 3m, 0m);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteCreado, creado, CentralEnPruebas.CajaUno)));

        var entregado = creado with
        {
            Estado = EstadoPendiente.Entregado,
            // Entregado ahora: cuenta como entregado hoy a cualquier hora que corra la prueba.
            ActualizadoEn = DateTimeOffset.UtcNow,
            Lineas = [creado.Lineas[0] with { CantidadEntregada = 3m }],
        };
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteActualizado, entregado, CentralEnPruebas.CajaUno)));

        // Un reenvío tardío del documento viejo no devuelve el pendiente a "Pendiente".
        var viejo = creado with { ActualizadoEn = creado.ActualizadoEn.AddMinutes(-5) };
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteActualizado, viejo, CentralEnPruebas.CajaUno)));

        var pagina = await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}");
        var resumen = Assert.Single(pagina.Elementos);
        Assert.Equal((EstadoPendiente.Entregado, 3m, false), (resumen.Estado, resumen.UnidadesEntregadas, resumen.Atrasado));

        // Lo entregado ya no cuenta como abierto.
        var abiertos = await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?soloAbiertos=true&buscar={creado.Numero}");
        Assert.Empty(abiertos.Elementos);
        Assert.True((await ObtenerAsync<ResumenDespachoCentral>(cliente, admin, "/api/manager/despacho/resumen")).EntregadosHoy >= 1);
    }

    [SkippableFact]
    public async Task Sin_permiso_de_despacho_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINDES{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Despacho#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Despacho#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/despacho/pendientes", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static DatosPendienteEntrega Pendiente(Guid cajaId, MetodoEntrega metodo, EstadoPendiente estado, DateOnly? comprometida, decimal cantidad,
        decimal entregada)
    {
        var sufijo = Random.Shared.Next(100_000, 999_999);
        var creado = DateTimeOffset.UtcNow.AddHours(-2);
        return new DatosPendienteEntrega(Guid.CreateVersion7(), $"PE-{sufijo}", Guid.CreateVersion7(), $"01-01-{sufijo}", CentralEnPruebas.Sucursal, cajaId,
            metodo, estado, metodo == MetodoEntrega.RetiroAlmacen ? Guid.CreateVersion7() : null, metodo == MetodoEntrega.RetiroAlmacen ? "Almacén Central" : null,
            metodo == MetodoEntrega.Envio ? "Calle Principal 10" : null, "Los Prados", "Santo Domingo", "Casa azul", "8095551234", "Transporte Veloz", 350m,
            comprometida, "Llamar antes", "00113918205", "Cliente de Despacho", "Cajero Desarrollo", null, creado, creado, "Cajero Desarrollo", null,
            [new DatosLineaPendiente(1, "CINCEL", "Cincel", "UND", 0, false, cantidad, entregada, null)],
            []);
    }

    private static MensajeSincronizacion Mensaje(string tipo, DatosPendienteEntrega pendiente, Guid cajaId)
    {
        var contenido = JsonSerializer.Serialize(pendiente, OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), tipo, pendiente.Id, contenido, HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(cajaId).Sucursal, CentralEnPruebas.CodigosCaja(cajaId).Caja,
            DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
