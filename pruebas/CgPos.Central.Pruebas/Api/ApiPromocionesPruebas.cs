using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiPromocionesPruebas(CentralEnPruebas central)
{
    private static readonly DateTimeOffset Inicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateTimeOffset Fin = new(2030, 12, 31, 23, 59, 59, TimeSpan.FromHours(-4));

    [SkippableFact]
    public async Task Una_promocion_valida_su_alcance_y_muestra_cuantas_cajas_la_recibieron()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var (departamento, articulo) = await CrearArticuloAsync(cliente, admin);
        var sufijo = articulo.Codigo[1..];

        var promocion = new PromocionCarga(Guid.CreateVersion7(), $"P{sufijo}", "Diez por ciento", TipoPromocion.Porcentaje, 10m, Inicio, Fin,
            Articulos: [articulo.Id], Sucursales: [CentralEnPruebas.Sucursal]);
        var guardada = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/promociones/{promocion.Id}", promocion);
        Assert.True(guardada.Cuerpo!.Exitosa, guardada.Cuerpo.Mensaje);

        Assert.Contains("artículo(s) inexistente(s)", (await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/promociones/{promocion.Id}",
            promocion with { Articulos = [Guid.CreateVersion7()] })).Cuerpo!.Mensaje);
        Assert.Contains("sucursal(es) inexistente(s)", (await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/promociones/{promocion.Id}",
            promocion with { Sucursales = [Guid.CreateVersion7()] })).Cuerpo!.Mensaje);
        Assert.Contains("No se puede cambiar el código de la promoción", (await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/promociones/{promocion.Id}",
            promocion with { Codigo = $"Q{sufijo}" })).Cuerpo!.Mensaje);

        var listada = Assert.Single(await ObtenerAsync<List<DatosPromocionCentral>>(cliente, admin, "/api/promociones"), p => p.Promocion.Id == promocion.Id);
        Assert.Equal("-10%", listada.Oferta);
        Assert.True(listada.CajasDestino >= 1);

        // La caja confirma lo aplicado en la descarga siguiente: con dos descargas la promoción cuenta como recibida.
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var hasta = (await ObtenerAsync<PaqueteBajadaMaestros>(cliente, tokenCaja, "/api/sincronizacion/maestros?desde=0")).Hasta;
        await ObtenerAsync<PaqueteBajadaMaestros>(cliente, tokenCaja, $"/api/sincronizacion/maestros?desde={hasta}");

        var distribuida = Assert.Single(await ObtenerAsync<List<DatosPromocionCentral>>(cliente, admin, "/api/promociones"), p => p.Promocion.Id == promocion.Id);
        Assert.InRange(distribuida.CajasConPromocion, 1, distribuida.CajasDestino);
        Assert.Equal(departamento.Id, articulo.DepartamentoId);
    }

    [SkippableFact]
    public async Task La_importacion_valida_todo_el_archivo_y_solo_publica_sin_errores()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var (_, articulo) = await CrearArticuloAsync(cliente, admin);
        var sufijo = articulo.Codigo[1..];

        var existente = new PromocionCarga(Guid.CreateVersion7(), $"E{sufijo}", "Existente", TipoPromocion.Porcentaje, 5m, Inicio, Fin, Articulos: [articulo.Id]);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/promociones/{existente.Id}", existente)).Cuerpo!.Exitosa);

        const string encabezado = "codigo;nombre;tipo;valor;desde;hasta;articulos;departamentos;sucursales;lleva;paga;cantidad_minima;limite_cliente;dias;hora_desde;hora_hasta;solo_fidelidad;activa";
        var valida = $"I{sufijo};2x1 importado;lleva_paga;;2026-01-01;2030-12-31;{articulo.Codigo};;;2;1;;;lun|mié|vie;08:00;12:00;no;si";
        var actualiza = $"E{sufijo};Existente 15%;porcentaje;15;01/01/2026;31/12/2030;{articulo.Codigo};;;;;;;todos;;;no;si";
        var tipoMalo = $"M{sufijo};Mala;regalo;5;2026-01-01;2030-12-31;{articulo.Codigo};;;;;;;;;;;";
        var sinArticulo = $"N{sufijo};Sin artículo;porcentaje;5;2026-01-01;2030-12-31;NOEXISTE{sufijo};;;;;;;;;;;";

        var validacion = await ImportarAsync(cliente, admin, string.Join("\n", encabezado, valida, tipoMalo, sinArticulo), soloValidar: false);
        Assert.False(validacion.Publicada);
        Assert.Equal([3, 4], validacion.Errores.Select(e => e.Linea));
        Assert.Contains("Tipo de promoción desconocido", validacion.Errores[0].Mensaje);
        Assert.Contains($"No existe el artículo 'NOEXISTE{sufijo}'", validacion.Errores[1].Mensaje);
        Assert.DoesNotContain(await ObtenerAsync<List<DatosPromocionCentral>>(cliente, admin, "/api/promociones"), p => p.Promocion.Codigo == $"I{sufijo}");

        var soloValidar = await ImportarAsync(cliente, admin, string.Join("\r\n", encabezado, valida, actualiza), soloValidar: true);
        Assert.Equal((2, 1, 1, false, 0), (soloValidar.Leidas, soloValidar.Nuevas, soloValidar.Actualizadas, soloValidar.Publicada, soloValidar.Errores.Count));

        var publicada = await ImportarAsync(cliente, admin, string.Join("\r\n", encabezado, valida, actualiza), soloValidar: false);
        Assert.True(publicada.Publicada, string.Join(" ", publicada.Errores.Select(e => e.Mensaje)));

        var promociones = await ObtenerAsync<List<DatosPromocionCentral>>(cliente, admin, "/api/promociones");
        var importada = Assert.Single(promociones, p => p.Promocion.Codigo == $"I{sufijo}").Promocion;
        Assert.Equal((2, 1, DiasSemana.Lunes | DiasSemana.Miercoles | DiasSemana.Viernes, new TimeOnly(8, 0)),
            (importada.CantidadLleva!.Value, importada.CantidadPaga!.Value, importada.Dias, importada.HoraDesde!.Value));
        Assert.Equal(new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.FromHours(-4)), importada.VigenteHasta);
        var actualizada = Assert.Single(promociones, p => p.Promocion.Id == existente.Id).Promocion;
        Assert.Equal(15m, actualizada.Valor);
    }

    [SkippableFact]
    public async Task La_simulacion_elige_la_oferta_como_la_caja()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var (departamento, articulo) = await CrearArticuloAsync(cliente, admin);
        var sufijo = articulo.Codigo[1..];

        var diez = new PromocionCarga(Guid.CreateVersion7(), $"A{sufijo}", "Diez por ciento", TipoPromocion.Porcentaje, 10m, Inicio, Fin, Articulos: [articulo.Id]);
        var fidelidad = new PromocionCarga(Guid.CreateVersion7(), $"B{sufijo}", "Especial fidelidad", TipoPromocion.PrecioEspecial, 85m, Inicio, Fin,
            Departamentos: [departamento.Id], SoloFidelidad: true);
        var inactiva = new PromocionCarga(Guid.CreateVersion7(), $"C{sufijo}", "Mitad inactiva", TipoPromocion.Porcentaje, 50m, Inicio, Fin, Articulos: [articulo.Id], Activa: false);
        foreach (var promocion in new[] { diez, fidelidad, inactiva })
            Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/promociones/{promocion.Id}", promocion)).Cuerpo!.Exitosa);

        var momento = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(-4));
        var dos = await SimularAsync(cliente, admin, new SolicitudSimulacionPromociones(articulo.Id, 2, CentralEnPruebas.Sucursal, momento, ConFidelidad: false));
        Assert.Equal((diez.Id, 180m, 200m), (dos.GanadoraId!.Value, dos.Total, dos.BrutoDetalle));
        Assert.Equal(3, dos.Candidatas.Count);
        Assert.Contains("fidelidad", Assert.Single(dos.Candidatas, c => c.Id == fidelidad.Id).Motivo);
        Assert.Contains("inactiva", Assert.Single(dos.Candidatas, c => c.Id == inactiva.Id).Motivo);

        var conFidelidad = await SimularAsync(cliente, admin, new SolicitudSimulacionPromociones(articulo.Id, 2, CentralEnPruebas.Sucursal, momento, ConFidelidad: true));
        Assert.Equal((fidelidad.Id, 170m), (conFidelidad.GanadoraId!.Value, conFidelidad.Total));

        // Desde 10 unidades el mayor (900) iguala al detalle con 10% (900): la caja cobra por mayor y no aplica la oferta.
        var mayor = await SimularAsync(cliente, admin, new SolicitudSimulacionPromociones(articulo.Id, 10, CentralEnPruebas.Sucursal, momento, ConFidelidad: false));
        Assert.Equal(((Guid?)null, 900m, 900m), (mayor.GanadoraId, mayor.Total, mayor.BrutoMayor!.Value));

        using var inexistente = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/promociones/simular", admin,
            new SolicitudSimulacionPromociones(Guid.CreateVersion7(), 1, CentralEnPruebas.Sucursal, momento, false)));
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [SkippableFact]
    public async Task Sin_permiso_de_promociones_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINPRO{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Promociones#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Promociones#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/promociones", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>Departamento propio para que ninguna promoción de otras pruebas o de desarrollo alcance al artículo.</summary>
    private static async Task<(DepartamentoCarga Departamento, ArticuloCarga Articulo)> CrearArticuloAsync(HttpClient cliente, string admin)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var departamento = new DepartamentoCarga(Guid.CreateVersion7(), $"F{sufijo}", $"Departamento promociones {sufijo}");
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/maestros/departamentos/{departamento.Id}", departamento)).Cuerpo!.Exitosa);
        var categoria = new CategoriaCarga(Guid.CreateVersion7(), $"C{sufijo}", $"Categoría promociones {sufijo}", departamento.Id);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/maestros/categorias/{categoria.Id}", categoria)).Cuerpo!.Exitosa);

        var unidad = (await ObtenerAsync<List<DatosMaestroCentral<UnidadMedidaCarga>>>(cliente, admin, "/api/maestros/unidades-medida")).First().Dato;
        var impuesto = (await ObtenerAsync<List<DatosMaestroCentral<ImpuestoCarga>>>(cliente, admin, "/api/maestros/impuestos")).First(i => i.Dato.Activo).Dato;
        var articulo = new ArticuloCarga(Guid.CreateVersion7(), $"S{sufijo}", $"Simulado {sufijo}", departamento.Id, unidad.Id, impuesto.Id, 100m,
            PrecioMayor: 90m, CantidadMinimaMayor: 10m, CategoriaId: categoria.Id);
        var creado = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/maestros/articulos/{articulo.Id}", articulo);
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);
        return (departamento, articulo);
    }

    private static async Task<ResultadoImportacionPromociones> ImportarAsync(HttpClient cliente, string token, string contenido, bool soloValidar)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/promociones/importar", token,
            new SolicitudImportacionPromociones(contenido, -240, soloValidar)));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ResultadoImportacionPromociones>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<ResultadoSimulacionPromociones> SimularAsync(HttpClient cliente, string token, SolicitudSimulacionPromociones solicitud)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/promociones/simular", token, solicitud));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<ResultadoSimulacionPromociones>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
