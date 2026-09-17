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

        var promocion = new PromocionCarga($"P{sufijo}", "Diez por ciento", TipoPromocion.Porcentaje, 10m, Inicio, Fin,
            Articulos: [articulo.Codigo], Sucursales: [1]);
        var guardada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/promociones", promocion);
        Assert.True(guardada.Cuerpo!.Exitosa, guardada.Cuerpo.Mensaje);

        Assert.Contains("No existe el artículo", (await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/promociones",
            promocion with { Articulos = [$"NOEXISTE{sufijo}"] })).Cuerpo!.Mensaje);
        Assert.Contains("No existe la sucursal", (await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/promociones",
            promocion with { Sucursales = [99] })).Cuerpo!.Mensaje);
        Assert.Contains("Ya existe", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/promociones", promocion)).Cuerpo!.Mensaje);

        var listada = Assert.Single(await ObtenerAsync<List<DatosPromocionCentral>>(cliente, admin, "/api/promociones"), p => p.Promocion.Codigo == promocion.Codigo);
        Assert.Equal("-10%", listada.Oferta);
        Assert.True(listada.CajasDestino >= 1);

        // La caja confirma lo aplicado en la descarga siguiente: con dos descargas la promoción cuenta como recibida.
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var hasta = (await ObtenerAsync<PaqueteBajadaMaestros>(cliente, tokenCaja, "/api/sincronizacion/maestros?desde=0")).Hasta;
        await ObtenerAsync<PaqueteBajadaMaestros>(cliente, tokenCaja, $"/api/sincronizacion/maestros?desde={hasta}");

        var distribuida = Assert.Single(await ObtenerAsync<List<DatosPromocionCentral>>(cliente, admin, "/api/promociones"), p => p.Promocion.Codigo == promocion.Codigo);
        Assert.InRange(distribuida.CajasConPromocion, 1, distribuida.CajasDestino);
        Assert.Equal(departamento.Codigo, articulo.DepartamentoCodigo);
    }

    [SkippableFact]
    public async Task La_importacion_valida_todo_el_archivo_y_solo_publica_sin_errores()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var (_, articulo) = await CrearArticuloAsync(cliente, admin);
        var sufijo = articulo.Codigo[1..];

        var existente = new PromocionCarga($"E{sufijo}", "Existente", TipoPromocion.Porcentaje, 5m, Inicio, Fin, Articulos: [articulo.Codigo]);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/promociones", existente)).Cuerpo!.Exitosa);

        const string encabezado = "codigo;nombre;tipo;valor;desde;hasta;articulos;departamentos;sucursales;lleva;paga;cantidad_minima;limite_cliente;dias;hora_desde;hora_hasta;solo_fidelidad;activa";
        var valida = $"I{sufijo};2x1 importado;lleva_paga;;2026-01-01;2030-12-31;{articulo.Codigo};;;2;1;;;lun|mié|vie;08:00;12:00;no;si";
        var actualiza = $"E{sufijo};Existente 15%;porcentaje;15;01/01/2026;31/12/2030;{articulo.Codigo};;;;;;;todos;;;no;si";
        var tipoMalo = $"M{sufijo};Mala;regalo;5;2026-01-01;2030-12-31;{articulo.Codigo};;;;;;;;;;;";
        var sinArticulo = $"N{sufijo};Sin artículo;porcentaje;5;2026-01-01;2030-12-31;NOEXISTE{sufijo};;;;;;;;;;;";

        var validacion = await ImportarAsync(cliente, admin, string.Join("\n", encabezado, valida, tipoMalo, sinArticulo), soloValidar: false);
        Assert.False(validacion.Publicada);
        Assert.Equal([3, 4], validacion.Errores.Select(e => e.Linea));
        Assert.Contains("Tipo de promoción desconocido", validacion.Errores[0].Mensaje);
        Assert.Contains($"No existe el artículo con código 'NOEXISTE{sufijo}'", validacion.Errores[1].Mensaje);
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
        var actualizada = Assert.Single(promociones, p => p.Promocion.Codigo == existente.Codigo).Promocion;
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

        var diez = new PromocionCarga($"A{sufijo}", "Diez por ciento", TipoPromocion.Porcentaje, 10m, Inicio, Fin, Articulos: [articulo.Codigo]);
        var fidelidad = new PromocionCarga($"B{sufijo}", "Especial fidelidad", TipoPromocion.PrecioEspecial, 85m, Inicio, Fin,
            Departamentos: [departamento.Codigo], SoloFidelidad: true);
        var inactiva = new PromocionCarga($"C{sufijo}", "Mitad inactiva", TipoPromocion.Porcentaje, 50m, Inicio, Fin, Articulos: [articulo.Codigo], Activa: false);
        foreach (var promocion in new[] { diez, fidelidad, inactiva })
            Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/promociones", promocion)).Cuerpo!.Exitosa);

        var momento = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.FromHours(-4));
        var dos = await SimularAsync(cliente, admin, new SolicitudSimulacionPromociones(articulo.Codigo, 2, CentralEnPruebas.Sucursal, momento, ConFidelidad: false));
        Assert.Equal((diez.Codigo, 180m, 200m), (Ganadora(dos), dos.Total, dos.BrutoDetalle));
        Assert.Equal(3, dos.Candidatas.Count);
        Assert.Contains("fidelidad", Assert.Single(dos.Candidatas, c => c.Codigo == fidelidad.Codigo).Motivo);
        Assert.Contains("inactiva", Assert.Single(dos.Candidatas, c => c.Codigo == inactiva.Codigo).Motivo);

        var conFidelidad = await SimularAsync(cliente, admin, new SolicitudSimulacionPromociones(articulo.Codigo, 2, CentralEnPruebas.Sucursal, momento, ConFidelidad: true));
        Assert.Equal((fidelidad.Codigo, 170m), (Ganadora(conFidelidad), conFidelidad.Total));

        // Desde 10 unidades el mayor (900) iguala al detalle con 10% (900): la caja cobra por mayor y no aplica la oferta.
        var mayor = await SimularAsync(cliente, admin, new SolicitudSimulacionPromociones(articulo.Codigo, 10, CentralEnPruebas.Sucursal, momento, ConFidelidad: false));
        Assert.Equal(((string?)null, 900m, 900m), (Ganadora(mayor), mayor.Total, mayor.BrutoMayor!.Value));

        using var inexistente = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/promociones/simular", admin,
            new SolicitudSimulacionPromociones($"NOEXISTE{sufijo}", 1, CentralEnPruebas.Sucursal, momento, false)));
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
        var departamento = new DepartamentoCarga(Codigos.Siguiente(), $"Departamento promociones {sufijo}");
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/departamentos", departamento)).Cuerpo!.Exitosa);
        var categoria = new CategoriaCarga(Codigos.Siguiente(), $"Categoría promociones {sufijo}", departamento.Codigo);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/categorias", categoria)).Cuerpo!.Exitosa);

        var unidad = (await ObtenerAsync<List<DatosMaestroCentral<UnidadMedidaCarga>>>(cliente, admin, "/api/maestros/unidades-medida")).First().Dato;
        var impuesto = (await ObtenerAsync<List<DatosMaestroCentral<ImpuestoCarga>>>(cliente, admin, "/api/maestros/impuestos")).First(i => i.Dato.Activo).Dato;
        var articulo = new ArticuloCarga($"S{sufijo}", $"Simulado {sufijo}", departamento.Codigo, unidad.Codigo, impuesto.Codigo, 100m,
            PrecioMayor: 90m, CantidadMinimaMayor: 10m, CategoriaCodigo: categoria.Codigo);
        var creado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/articulos", articulo);
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);
        return (departamento, articulo);
    }

    /// <summary>Código de la oferta que aplicaría la caja; nulo si ninguna.</summary>
    private static string? Ganadora(ResultadoSimulacionPromociones resultado) =>
        resultado.Candidatas.SingleOrDefault(c => c.Id == resultado.GanadoraId)?.Codigo;

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
