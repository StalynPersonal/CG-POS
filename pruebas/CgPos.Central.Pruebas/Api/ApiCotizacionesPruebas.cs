using System.Net;
using System.Net.Http.Json;
using System.Text;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Cotizaciones;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// Cotizaciones: se arman en el Central con los precios del día congelados y las cajas las buscan por su número para
/// facturarlas. El presupuesto que se le entrega al cliente tiene que decir lo mismo que después cobra la caja.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiCotizacionesPruebas(CentralEnPruebas central)
{
    private const string Cincel = "43138";
    private const string Cemento = "CEM-425";

    [SkippableFact]
    public async Task Una_cotizacion_congela_el_precio_del_dia_y_la_caja_la_encuentra_por_su_numero()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var caja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        // El precio del maestro puede cambiar entre pruebas: se lee ahora para saber qué debería congelarse.
        var precioDelDia = await PrecioAsync(cliente, admin, Cincel);
        var esperado = decimal.Round(3 * precioDelDia, 2, MidpointRounding.AwayFromZero) + (10 * 450m) - 200m;
        var nombre = $"Constructora {Guid.NewGuid():N}"[..28];

        // Sin precio indicado toma el del maestro; con precio indicado manda el pactado.
        var solicitud = new SolicitudCotizacion(nombre, "131234567", "809-555-0100", null, null, null, "Entrega en obra",
            [new SolicitudLineaCotizacion(Cincel, 3, null), new SolicitudLineaCotizacion(Cemento, 10, 450m, 200m)]);

        var creada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones", solicitud);
        Assert.True(creada.Cuerpo!.Exitosa, creada.Cuerpo.Mensaje);

        var cotizacion = Assert.Single(await ListarAsync(cliente, admin, nombre));
        Assert.StartsWith("COT", cotizacion.Numero, StringComparison.Ordinal);
        Assert.Equal((EstadoCotizacion.Abierta, false), (cotizacion.Estado, cotizacion.Vencida));

        Assert.Equal(esperado, cotizacion.Subtotal); // los precios van sin ITBIS; el ITBIS se suma aparte
        Assert.Equal(cotizacion.Subtotal + cotizacion.Impuesto, cotizacion.Total);
        Assert.Equal(200m, cotizacion.Descuento);

        var delMaestro = Assert.Single(cotizacion.Lineas, l => l.ArticuloCodigo == Cincel);
        Assert.Equal((precioDelDia, 18m), (delMaestro.PrecioUnitario, delMaestro.PorcentajeImpuesto));
        Assert.False(string.IsNullOrWhiteSpace(delMaestro.Descripcion));

        // Vence con los días configurados en el Central, contados desde hoy.
        Assert.True(cotizacion.VenceEn > DateOnly.FromDateTime(DateTime.Today));

        // La caja la busca por su número, con su token de dispositivo, y recibe los precios congelados.
        using (var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/cotizaciones/{cotizacion.Numero}", caja)))
        {
            respuesta.EnsureSuccessStatusCode();
            var paraCaja = (await respuesta.Content.ReadFromJsonAsync<DatosCotizacionParaCaja>(OpcionesJson.Predeterminadas))!;
            Assert.Equal((cotizacion.Numero, cotizacion.Total, false), (paraCaja.Numero, paraCaja.Total, paraCaja.Vencida));
            Assert.Equal(450m, Assert.Single(paraCaja.Lineas, l => l.ArticuloCodigo == Cemento).PrecioUnitario);
        }

        // Una que no existe es 404, no un error: la caja lo distingue de no haber podido preguntar.
        using (var inexistente = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/cotizaciones/COT999999", caja)))
            Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        // El PDF que se le entrega al cliente sale en carta y con su número.
        using (var pdf = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/manager/cotizaciones/{cotizacion.Id}/pdf", admin)))
        {
            pdf.EnsureSuccessStatusCode();
            var contenido = await pdf.Content.ReadAsByteArrayAsync();
            var texto = Encoding.Latin1.GetString(contenido);
            Assert.StartsWith("%PDF-1.4", texto, StringComparison.Ordinal);
            Assert.Contains("612 792", texto, StringComparison.Ordinal);
            Assert.Contains(cotizacion.Numero, texto, StringComparison.Ordinal);
        }
    }

    [SkippableFact]
    public async Task Una_cotizacion_anulada_deja_de_cambiarse_y_un_articulo_inexistente_se_rechaza()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var inventado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones",
            new SolicitudCotizacion("Cliente de prueba", null, null, null, null, null, null, [new SolicitudLineaCotizacion("NO-EXISTE", 1, null)]));
        Assert.Contains("NO-EXISTE", inventado.Cuerpo!.Mensaje);

        var cliente_ = $"Ferretería {Guid.NewGuid():N}"[..25];
        var creada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones",
            new SolicitudCotizacion(cliente_, null, null, null, null, null, null, [new SolicitudLineaCotizacion(Cincel, 2, null)]));
        var id = creada.Cuerpo!.Id!.Value;

        // Sin motivo no se anula.
        var sinMotivo = await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/cotizaciones/{id}/anular", new SolicitudAnularCotizacion("  "));
        Assert.Equal("Indique el motivo de la anulación.", sinMotivo.Cuerpo!.Mensaje);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/manager/cotizaciones/{id}/anular",
            new SolicitudAnularCotizacion("El cliente cambió de proveedor"))).Cuerpo!.Exitosa);

        var anulada = Assert.Single(await ListarAsync(cliente, admin, cliente_));
        Assert.Equal(EstadoCotizacion.Anulada, anulada.Estado);
        Assert.Equal("El cliente cambió de proveedor", anulada.MotivoAnulacion);

        // Y ya no se le cambian las líneas.
        var actualizar = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/manager/cotizaciones/{id}",
            new SolicitudCotizacion(cliente_, null, null, null, null, null, null, [new SolicitudLineaCotizacion(Cincel, 5, null)]));
        Assert.False(actualizar.Cuerpo!.Exitosa);
        Assert.Contains("anulada", actualizar.Cuerpo.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Sin_su_secuencia_configurada_el_documento_no_se_crea_y_se_dice_cual_falta()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var solicitud = new SolicitudCotizacion($"Cliente {Guid.NewGuid():N}"[..24], null, null, null, null, null, null,
            [new SolicitudLineaCotizacion(Cincel, 1, null)]);

        // Desactivada, la cotización no se emite: el contador se conserva pero el documento no se puede crear.
        await CambiarSecuenciaAsync(central, activa: false);
        var apagada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones", solicitud);
        Assert.False(apagada.Cuerpo!.Exitosa);
        Assert.Contains("desactivada", apagada.Cuerpo.Mensaje, StringComparison.OrdinalIgnoreCase);

        // Y sin la fila siquiera, se dice qué prefijo falta en vez de inventar uno.
        await QuitarSecuenciaAsync(central);
        var sinFila = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones", solicitud);
        Assert.False(sinFila.Cuerpo!.Exitosa);
        Assert.Contains("Cotizacion", sinFila.Cuerpo.Mensaje, StringComparison.Ordinal);

        // Restituida desde la pantalla de secuencias, la numeración sigue donde iba.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/secuencias",
            new SolicitudSecuenciaCentral("Cotizacion", "COT", "Cotización", 500, 6, true))).Cuerpo!.Exitosa);

        var creada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones", solicitud);
        Assert.True(creada.Cuerpo!.Exitosa, creada.Cuerpo.Mensaje);
        Assert.Equal("COT000501", Assert.Single(await ListarAsync(cliente, admin, solicitud.ClienteNombre)).Numero);

        // El contador no se deja retroceder: repetiría números ya entregados.
        var atras = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/secuencias",
            new SolicitudSecuenciaCentral("Cotizacion", "COT", "Cotización", 10, 6, true));
        Assert.False(atras.Cuerpo!.Exitosa);
        Assert.Contains("repetiría", atras.Cuerpo.Mensaje, StringComparison.OrdinalIgnoreCase);

        // El prefijo se cambia y la emisión sigue funcionando: el sistema busca la secuencia por su código,
        // no por el prefijo. El contador sigue donde iba y lo ya emitido conserva el suyo.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/secuencias",
            new SolicitudSecuenciaCentral("Cotizacion", "PRE", "Cotización", 501, 6, true))).Cuerpo!.Exitosa);

        var conNuevoPrefijo = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/manager/cotizaciones", solicitud);
        Assert.True(conNuevoPrefijo.Cuerpo!.Exitosa, conNuevoPrefijo.Cuerpo.Mensaje);
        Assert.Contains(await ListarAsync(cliente, admin, solicitud.ClienteNombre), c => c.Numero == "PRE000502");

        // Y se deja como estaba, que otras pruebas de la colección cotizan con COT.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/secuencias",
            new SolicitudSecuenciaCentral("Cotizacion", "COT", "Cotización", 502, 6, true))).Cuerpo!.Exitosa);
    }

    private static Task CambiarSecuenciaAsync(CentralEnPruebas central, bool activa) =>
        central.UsarContextoAsync(async contexto =>
        {
            await contexto.Database.ExecuteSqlAsync($"UPDATE SecuenciasCentral SET Activa = {activa} WHERE Codigo = 'Cotizacion'");
            return true;
        });

    private static Task QuitarSecuenciaAsync(CentralEnPruebas central) =>
        central.UsarContextoAsync(async contexto =>
        {
            await contexto.Database.ExecuteSqlAsync($"DELETE FROM SecuenciasCentral WHERE Codigo = 'Cotizacion'");
            return true;
        });

    /// <summary>El precio publicado del artículo en este momento: otras pruebas de la colección pueden haberlo cambiado.</summary>
    private static async Task<decimal> PrecioAsync(HttpClient cliente, string token, string codigo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/cotizaciones/articulos?buscar={codigo}", token));
        respuesta.EnsureSuccessStatusCode();
        var pagina = (await respuesta.Content.ReadFromJsonAsync<PaginaMaestros<CgPos.Contratos.Catalogo.ArticuloCarga>>(OpcionesJson.Predeterminadas))!;
        return pagina.Elementos.Single(e => e.Dato.Codigo == codigo).Dato.PrecioDetalle;
    }

    private static async Task<IReadOnlyList<DatosCotizacion>> ListarAsync(HttpClient cliente, string token, string buscar)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/cotizaciones?buscar={Uri.EscapeDataString(buscar)}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<IReadOnlyList<DatosCotizacion>>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo,
        string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }
}
