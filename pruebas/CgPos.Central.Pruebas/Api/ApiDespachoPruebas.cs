using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// El despacho se opera en el Central: la caja crea el pendiente al cobrar y lo informa una sola vez, y de ahí en adelante el
/// Central lo prepara, lo entrega y lo anula. Así se atiende a un cliente que llama o que llega a otra tienda.
/// </summary>
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
        var aTiempo = Pendiente(CentralEnPruebas.CajaDos, MetodoEntrega.RetiroSucursal, EstadoPendiente.Preparado,
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

        var detalle = await ObtenerAsync<DetallePendienteCentral>(cliente, admin, $"/api/manager/despacho/pendientes/{resumen.Id}");
        Assert.Equal(atrasado.Numero, detalle.Pendiente.Numero);
        Assert.Equal("Cincel", Assert.Single(detalle.Pendiente.Lineas).Descripcion);
    }

    [SkippableFact]
    public async Task El_central_prepara_entrega_por_partes_con_constancia_y_la_caja_no_pisa_lo_despachado()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var creado = Pendiente(CentralEnPruebas.CajaUno, MetodoEntrega.RetiroSucursal, EstadoPendiente.Pendiente, DateOnly.FromDateTime(DateTime.Today), 3m, 0m);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteCreado, creado, CentralEnPruebas.CajaUno)));
        var id = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos).Id;

        // No se puede saltar pasos: de pendiente no se pasa directamente a despachado, y menos en un retiro.
        var salto = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/estado",
            new SolicitudEstadoPendiente(EstadoPendiente.Despachado));
        Assert.False(salto.Cuerpo!.Exitosa);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/estado",
            new SolicitudEstadoPendiente(EstadoPendiente.Preparado))).Cuerpo!.Exitosa);

        // Sin quien recibe no se entrega: la constancia la firma alguien (RF-254).
        var sinRecibe = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/entregas",
            new SolicitudEntregaPendiente([new CantidadEntregada(1, 1m)], null, null));
        Assert.Contains("quien recibe", sinRecibe.Cuerpo!.Mensaje);

        // Tampoco más de lo que queda.
        var deMas = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/entregas",
            new SolicitudEntregaPendiente([new CantidadEntregada(1, 5m)], "Juan Pérez", "00113918205"));
        Assert.Contains("quedan 3", deMas.Cuerpo!.Mensaje);

        // Entrega parcial: el pendiente queda Parcial con lo que falta.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/entregas",
            new SolicitudEntregaPendiente([new CantidadEntregada(1, 1m)], "Juan Pérez", "00113918205"))).Cuerpo!.Exitosa);

        var parcial = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos);
        Assert.Equal((EstadoPendiente.Parcial, 3m, 1m), (parcial.Estado, parcial.Unidades, parcial.UnidadesEntregadas));

        // La constancia sale en carta, con el pendiente y quien recibió.
        using (var pdf = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/manager/despacho/pendientes/{id}/entregas/1/pdf", admin)))
        {
            pdf.EnsureSuccessStatusCode();
            var texto = Encoding.Latin1.GetString(await pdf.Content.ReadAsByteArrayAsync());
            Assert.StartsWith("%PDF-1.4", texto, StringComparison.Ordinal);
            Assert.Contains("612 792", texto, StringComparison.Ordinal);
            Assert.Contains(creado.Numero, texto, StringComparison.Ordinal);
            Assert.Contains("Juan P", texto, StringComparison.Ordinal);
        }

        // Con entregas ya no se anula: la mercancía salió.
        var anular = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/anular",
            new SolicitudAnularPendiente("El cliente se arrepintió"));
        Assert.Contains("no se puede anular", anular.Cuerpo!.Mensaje);

        // Se entrega el resto y queda cerrado.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/entregas",
            new SolicitudEntregaPendiente([new CantidadEntregada(1, 2m)], "Juan Pérez", "00113918205"))).Cuerpo!.Exitosa);

        var cerrado = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos);
        Assert.Equal((EstadoPendiente.Entregado, 3m, false), (cerrado.Estado, cerrado.UnidadesEntregadas, cerrado.Atrasado));

        // Un reenvío del mensaje de la caja no devuelve el pendiente a "Pendiente": la caja ya no manda sobre él.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteCreado, creado, CentralEnPruebas.CajaUno)));
        var despues = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos);
        Assert.Equal(EstadoPendiente.Entregado, despues.Estado);

        // Lo entregado ya no cuenta como abierto.
        var abiertos = await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?soloAbiertos=true&buscar={creado.Numero}");
        Assert.Empty(abiertos.Elementos);
        Assert.True((await ObtenerAsync<ResumenDespachoCentral>(cliente, admin, "/api/manager/despacho/resumen")).EntregadosHoy >= 1);
    }

    [SkippableFact]
    public async Task El_pendiente_que_sube_la_caja_recibe_el_numero_del_Central_y_conserva_el_suyo()
    {
        // 1. Si no hay SQL Server disponible, la prueba se salta en vez de fallar.
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);

        // 2. Un cliente HTTP contra el Central levantado en memoria, y las credenciales que hacen falta:
        //    la caja se autentica como dispositivo; el administrador, como usuario del Manager.
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        // 3. Lo que va a pasar: la caja crea un pendiente al cobrar y lo informa al Central.
        var creado = Pendiente(CentralEnPruebas.CajaUno, MetodoEntrega.RetiroSucursal, EstadoPendiente.Pendiente,
            DateOnly.FromDateTime(DateTime.Today), 2m, 0m);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteCreado, creado, CentralEnPruebas.CajaUno)));

        // 4. Lo que se comprueba: el Central lo guardó con SU número, y el de la caja sigue ahí.
        var detalle = await ObtenerAsync<DetallePendienteCentral>(cliente, admin,
            $"/api/manager/despacho/pendientes/{Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos).Id}");

        Assert.Equal(creado.Numero, detalle.Pendiente.Numero);
        Assert.StartsWith("DES", detalle.Resumen.NumeroCentral, StringComparison.Ordinal);

        // 5. Y el número no se repite si el mensaje llega dos veces: el pendiente ya existe y no se vuelve a numerar.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteCreado, creado, CentralEnPruebas.CajaUno)));
        var despues = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos);
        Assert.Equal(detalle.Resumen.NumeroCentral, despues.NumeroCentral);
    }

    [SkippableFact]
    public async Task Un_pendiente_sin_entregas_se_anula_con_motivo_y_libera_la_mercancia_para_devolverla()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var creado = Pendiente(CentralEnPruebas.CajaUno, MetodoEntrega.Envio, EstadoPendiente.Pendiente, DateOnly.FromDateTime(DateTime.Today), 2m, 0m);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Mensaje(TiposMensaje.PendienteCreado, creado, CentralEnPruebas.CajaUno)));
        var id = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos).Id;

        // Sin motivo no se anula.
        var sinMotivo = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/anular", new SolicitudAnularPendiente("  "));
        Assert.Contains("motivo", sinMotivo.Cuerpo!.Mensaje);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/despacho/pendientes/{id}/anular",
            new SolicitudAnularPendiente("El cliente canceló el envío"))).Cuerpo!.Exitosa);

        var anulado = Assert.Single((await ObtenerAsync<PaginaPendientesCentral>(cliente, admin, $"/api/manager/despacho/pendientes?buscar={creado.Numero}")).Elementos);
        Assert.Equal(EstadoPendiente.Anulado, anulado.Estado);

        var detalle = await ObtenerAsync<DetallePendienteCentral>(cliente, admin, $"/api/manager/despacho/pendientes/{id}");
        Assert.Equal("El cliente canceló el envío", detalle.Pendiente.MotivoAnulacion);
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

    [SkippableFact]
    public async Task Operar_el_despacho_no_da_permiso_para_anular_un_pendiente()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SOLODES{Guid.NewGuid().ToString("N")[..5].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Solo.Despacho#2026", false, CatalogoPermisosCentral.OperarDespacho);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Solo.Despacho#2026")).Cuerpo!.TokenAcceso!;

        // Ve los pendientes...
        using (var lista = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/despacho/pendientes", token)))
            lista.EnsureSuccessStatusCode();

        // ...pero anular libera mercancía ya facturada y lleva su propio permiso.
        using var anular = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/manager/despacho/pendientes/1/anular", token,
            new SolicitudAnularPendiente("Lo que sea")));
        Assert.Equal(HttpStatusCode.Forbidden, anular.StatusCode);
    }

    private static DocumentoPendienteEntrega Pendiente(int cajaId, MetodoEntrega metodo, EstadoPendiente estado, DateOnly? comprometida, decimal cantidad,
        decimal entregada)
    {
        var creado = DateTimeOffset.UtcNow.AddHours(-2);
        return new DocumentoPendienteEntrega(CentralEnPruebas.NumeroDocumento(cajaId, CgPos.Dominio.Comun.TipoDocumentoNumerado.PendienteEntrega),
            CentralEnPruebas.NumeroDocumento(cajaId, CgPos.Dominio.Comun.TipoDocumentoNumerado.Factura),
            metodo, estado, metodo == MetodoEntrega.RetiroSucursal ? "01" : null, metodo == MetodoEntrega.RetiroSucursal ? "Sucursal Kennedy" : null,
            metodo == MetodoEntrega.Envio ? "Calle Principal 10" : null, "Los Prados", "Santo Domingo", "Casa azul", "8095551234", "Transporte Veloz", 350m,
            comprometida, "Llamar antes", "00113918205", "Cliente de Despacho", "Cajero Desarrollo", null, creado, creado, "Cajero Desarrollo", null,
            [new DatosLineaPendiente(1, "CINCEL", "Cincel", "UND", 0, false, cantidad, entregada, null)],
            []);
    }

    private static MensajeSincronizacion Mensaje(string tipo, DocumentoPendienteEntrega pendiente, int cajaId)
    {
        var contenido = JsonSerializer.Serialize(pendiente, OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), tipo, pendiente.Numero, contenido, HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(cajaId).Sucursal, CentralEnPruebas.CodigosCaja(cajaId).Caja,
            DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo,
        string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
