using System.Text;
using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Catalogo;

/// <summary>Maestros, precios, búsqueda e importaciones contra SQL Server real.</summary>
public class CatalogoPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    [SkippableFact]
    public async Task Carga_de_maestros_es_idempotente_y_la_bitacora_solo_registra_cambios_de_precio()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();

        var primera = await AplicarAsync(escenario.Paquete());
        var segunda = await AplicarAsync(escenario.Paquete());
        var tercera = await AplicarAsync(escenario.Paquete(precioCemento: 499m));
        await ResolverAsync(escenario);

        Assert.True(primera.Creados > 0);
        Assert.Equal(10, primera.PreciosRegistrados); // 8 precios detalle + 2 por mayor
        Assert.Equal(0, segunda.Creados);
        Assert.Equal(0, segunda.PreciosRegistrados);
        Assert.Equal(1, tercera.PreciosRegistrados);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var historial = await ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>().ObtenerHistorialPreciosAsync(escenario.ArticuloCemento);
        Assert.Equal(3, historial.Count);
        Assert.Equal(499m, historial.First(p => p.Lista == ListaPrecio.Detalle).Precio);
        Assert.All(historial, p => Assert.Equal("Pruebas", p.Origen));
    }

    [SkippableFact]
    public async Task Busca_por_codigo_de_barras_de_proveedor_e_interno_con_precios_e_impuesto()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>();

        var porBarras = await consulta.BuscarPorCodigoAsync(escenario.BarrasCincel);
        var porProveedor = await consulta.BuscarPorCodigoAsync(escenario.ProveedorCincel);
        var porInterno = await consulta.BuscarPorCodigoAsync(escenario.CodigoCemento);

        Assert.Equal(OrigenCodigoLeido.CodigoBarras, porBarras!.OrigenCodigo);
        Assert.Equal(escenario.ArticuloCincel, porBarras.ArticuloId);
        Assert.Equal(850m, porBarras.PrecioDetalle);
        Assert.Equal(18m, porBarras.PorcentajeImpuesto);
        Assert.Equal(1, porBarras.IndicadorFacturacion);

        Assert.Equal(OrigenCodigoLeido.CodigoProveedor, porProveedor!.OrigenCodigo);
        Assert.Equal(escenario.ArticuloCincel, porProveedor.ArticuloId);

        Assert.Equal(OrigenCodigoLeido.CodigoInterno, porInterno!.OrigenCodigo);
        Assert.Equal(485m, porInterno.PrecioDetalle);
        Assert.Equal(450m, porInterno.PrecioMayor);
        Assert.Equal(12m, porInterno.CantidadMinimaMayor);

        Assert.Null(await consulta.BuscarPorCodigoAsync("CODIGO-QUE-NO-EXISTE"));
    }

    [SkippableFact]
    public async Task Etiqueta_de_balanza_trae_el_articulo_pesado_y_su_peso()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        // El formato de las etiquetas lo configura un usuario; sin él la caja no interpreta etiquetas de balanza.
        await ConfigurarFormatoBalanzaAsync();

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var articulo = await ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>()
            .BuscarPorCodigoAsync(escenario.EtiquetaPesoTomate(2.345m));

        Assert.NotNull(articulo);
        Assert.Equal(escenario.ArticuloTomate, articulo.ArticuloId);
        Assert.Equal(OrigenCodigoLeido.EtiquetaBalanza, articulo.OrigenCodigo);
        Assert.Equal(2.345m, articulo.PesoLeido);
        Assert.Equal(TipoArticulo.Pesado, articulo.Tipo);
        Assert.True(articulo.PermiteDecimales);
        Assert.Equal(0m, articulo.PorcentajeImpuesto);
    }

    /// <summary>Formato EAN-13 de balanza como parámetros generales (idempotente: la base es de toda la clase).</summary>
    private async Task ConfigurarFormatoBalanzaAsync()
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var valores = new Dictionary<string, string>
        {
            [ClavesParametros.BalanzaPrefijoPeso] = "21",
            [ClavesParametros.BalanzaPrefijoPrecio] = "22",
            [ClavesParametros.BalanzaDigitosCodigoArticulo] = "5",
            [ClavesParametros.BalanzaDigitosValor] = "5",
            [ClavesParametros.BalanzaDecimalesPeso] = "3",
            [ClavesParametros.BalanzaDecimalesPrecio] = "2",
        };

        foreach (var (clave, valor) in valores)
            if (!await contexto.Parametros.AnyAsync(p => p.Clave == clave && p.CajaId == null && p.SucursalId == null))
                contexto.Parametros.Add(Parametro.Crear(clave, valor));

        await contexto.SaveChangesAsync();
    }

    [SkippableFact]
    public async Task Articulos_inactivos_o_no_marcados_para_caja_no_se_venden()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>();

        Assert.Null(await consulta.BuscarPorCodigoAsync(escenario.CodigoInactivo));
        Assert.Null(await consulta.BuscarPorCodigoAsync(escenario.CodigoFueraDePos));
        Assert.Empty(await consulta.BuscarAsync($"descontinuado {escenario.Sufijo}"));
        Assert.Empty(await consulta.BuscarAsync($"almacén {escenario.Sufijo}"));
    }

    [SkippableFact]
    public async Task Busqueda_por_varias_palabras_departamento_y_listado_de_no_codificados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>();

        var cemento = await consulta.BuscarAsync($"gris {escenario.Sufijo} cemento");
        Assert.Equal(escenario.ArticuloCemento, Assert.Single(cemento).ArticuloId);
        Assert.Equal(485m, cemento[0].PrecioDetalle);

        var porDepartamento = await consulta.BuscarAsync(escenario.Sufijo, escenario.DepartamentoVegetales);
        Assert.Equal(2, porDepartamento.Count);

        var noCodificados = await consulta.ListarNoCodificadosAsync(escenario.DepartamentoVegetales);
        Assert.Equal(new[] { $"Cebolla {escenario.Sufijo}", $"Tomate {escenario.Sufijo}" }, noCodificados.Select(a => a.Descripcion));
    }

    [SkippableFact]
    public async Task Precio_programado_a_futuro_aplica_solo_desde_su_vigencia()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        var reloj = new RelojPrueba(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero));
        await using var proveedor = baseDatos.CrearProveedor(servicios => servicios.AddSingleton<TimeProvider>(reloj));

        await AplicarAsync(escenario.Paquete(), proveedor);
        await AplicarAsync(escenario.Paquete(precioCemento: 520m, vigenciaPrecios: reloj.Ahora.AddDays(2)), proveedor);

        Assert.Equal(485m, (await BuscarAsync(proveedor, escenario.CodigoCemento))!.PrecioDetalle);

        reloj.Avanzar(TimeSpan.FromDays(2));
        Assert.Equal(520m, (await BuscarAsync(proveedor, escenario.CodigoCemento))!.PrecioDetalle);
    }

    [SkippableFact]
    public async Task Paquete_con_referencias_invalidas_o_codigos_repetidos_se_rechaza_completo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        var paquete = escenario.Paquete();

        Task<CargaMaestrosInvalidaExcepcion> RechazoAsync(ArticuloCarga articulo) =>
            Assert.ThrowsAsync<CargaMaestrosInvalidaExcepcion>(() => AplicarAsync(paquete with { Articulos = [.. paquete.Articulos!, articulo] }));

        var sinDepartamento = await RechazoAsync(new ArticuloCarga($"MAL-{escenario.Sufijo}", "Sin departamento", 999_999, escenario.CodigoUnidad, escenario.CodigoItbis18, 10m,
            CategoriaCodigo: escenario.CodigoCategoriaHerramientas));
        var repetido = await RechazoAsync(new ArticuloCarga($"DUP-{escenario.Sufijo}", "Código repetido", escenario.CodigoFerreteria, escenario.CodigoUnidad, escenario.CodigoItbis18, 10m,
            CodigosBarras: [escenario.BarrasCincel], CategoriaCodigo: escenario.CodigoCategoriaHerramientas));
        var otraCategoria = await RechazoAsync(new ArticuloCarga($"CATV-{escenario.Sufijo}", "Categoría de otro departamento", escenario.CodigoFerreteria, escenario.CodigoUnidad,
            escenario.CodigoItbis18, 10m, CategoriaCodigo: escenario.CodigoCategoriaVegetales));
        var sinCategoria = await RechazoAsync(new ArticuloCarga($"SINC-{escenario.Sufijo}", "Sin categoría", escenario.CodigoFerreteria, escenario.CodigoUnidad,
            escenario.CodigoItbis18, 10m));

        Assert.Contains(sinDepartamento.Errores, e => e.Contains("departamento") && e.Contains("999999"));
        Assert.Contains(repetido.Errores, e => e.Contains(escenario.BarrasCincel));
        Assert.Contains(otraCategoria.Errores, e => e.Contains("no es de su departamento"));
        Assert.Contains(sinCategoria.Errores, e => e.Contains("categoría"));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        Assert.False(await contexto.Articulos.AnyAsync(a => a.Codigo == escenario.CodigoCincel));
    }

    [SkippableFact]
    public async Task Forma_de_pago_con_moneda_fuera_del_maestro_se_rechaza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        var error = await Assert.ThrowsAsync<CargaMaestrosInvalidaExcepcion>(() => AplicarAsync(new PaqueteMaestros(
            FormasPago: [new FormaPagoCarga($"EUR{escenario.Sufijo}", "Euros", CgPos.Dominio.Pagos.TipoFormaPago.MonedaExtranjera, 9, "EUR")])));

        Assert.Contains(error.Errores, e => e.Contains("EUR") && e.Contains("maestro de monedas"));
    }

    [SkippableFact]
    public async Task Consulta_de_documento_devuelve_el_cliente_del_maestro_o_solo_su_validacion()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaDocumentos>();

        // Un cliente del maestro llega con todos sus datos, aunque el documento venga con guiones.
        var conCliente = await consulta.ConsultarAsync($"{escenario.RncCliente[..3]}-{escenario.RncCliente[3..8]}-{escenario.RncCliente[8..]}");
        Assert.Equal(TipoDocumentoIdentidad.Rnc, conCliente.Tipo);
        Assert.True(conCliente.DigitoVerificadorValido);
        Assert.NotNull(conCliente.Cliente);
        Assert.Equal(TipoComprobante.FacturaCreditoFiscal, conCliente.Cliente.TipoComprobante);
        Assert.Equal(ListaPrecio.Mayor, conCliente.Cliente.ListaPrecio);
        Assert.Equal("Oficina", conCliente.Cliente.Direcciones[0].Alias); // la principal primero
        Assert.Equal(2, conCliente.Cliente.Direcciones.Count);

        // Un RNC válido que no está en el maestro: la caja lo valida, pero no tiene cliente que ofrecer.
        var sinCliente = await consulta.ConsultarAsync(EscenarioCatalogo.RncAleatorioValido());
        Assert.True(sinCliente.FormatoValido);
        Assert.True(sinCliente.DigitoVerificadorValido);
        Assert.Null(sinCliente.Cliente);

        var invalido = await consulta.ConsultarAsync("123");
        Assert.False(invalido.FormatoValido);
    }

    [SkippableFact]
    public async Task Importador_csv_guarda_las_lineas_validas_e_informa_errores_por_linea()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);
        var codigoNuevo = $"NUEVO-{escenario.Sufijo}";
        var barrasNuevo = "8" + Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);

        var csv = string.Join("\n",
            "codigo;descripcion;departamento;categoria;unidad;impuesto;precio_detalle;precio_mayor;cantidad_minima_mayor;codigos_barras;tipo",
            $"{codigoNuevo};\"Martillo; mango de fibra\";{escenario.CodigoFerreteria};{escenario.CodigoCategoriaHerramientas};{escenario.CodigoLibraNumerico};{escenario.CodigoItbis18};325.50;300;6;{barrasNuevo};Normal",
            $"{escenario.CodigoCemento};Cemento gris 42.5 kg {escenario.Sufijo};{escenario.CodigoFerreteria};{escenario.CodigoCategoriaHerramientas};{escenario.CodigoLibraNumerico};{escenario.CodigoItbis18};510;450;12;{escenario.BarrasCemento};Normal",
            $"MALA-{escenario.Sufijo};Departamento que no existe;NOEXISTE;{escenario.CodigoCategoriaHerramientas};{escenario.CodigoLibraNumerico};{escenario.CodigoItbis18};10;;;;Normal",
            $"MALB-{escenario.Sufijo};Precio mal escrito;{escenario.CodigoFerreteria};{escenario.CodigoCategoriaHerramientas};{escenario.CodigoLibraNumerico};{escenario.CodigoItbis18};10,50;;;;Normal",
            $"MALC-{escenario.Sufijo};Sin categoría;{escenario.CodigoFerreteria};;{escenario.CodigoLibraNumerico};{escenario.CodigoItbis18};10;;;;Normal");

        ResultadoImportacionArticulos resultado;
        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            await using var contenido = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            resultado = await ambito.ServiceProvider.GetRequiredService<IImportadorArticulos>().ImportarCsvAsync(contenido, "Importación CSV");
        }

        Assert.Equal(1, resultado.Creados);
        Assert.Equal(1, resultado.Actualizados);
        Assert.Equal(3, resultado.PreciosRegistrados); // nuevo: detalle + mayor; cemento: detalle 510
        Assert.Equal(3, resultado.Errores.Count);
        Assert.Contains(resultado.Errores, e => e.Linea == 6 && e.Mensaje.Contains("categoría"));
        Assert.Contains(resultado.Errores, e => e.Linea == 4 && e.Mensaje.Contains("NOEXISTE"));
        Assert.Contains(resultado.Errores, e => e.Linea == 5 && e.Mensaje.Contains("precio_detalle"));

        await using var ambitoLectura = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambitoLectura.ServiceProvider.GetRequiredService<IConsultaArticulos>();
        var nuevo = await consulta.BuscarPorCodigoAsync(barrasNuevo);
        Assert.Equal("Martillo; mango de fibra", nuevo!.Descripcion);
        Assert.Equal(325.50m, nuevo.PrecioDetalle);
        Assert.Equal(510m, (await consulta.BuscarPorCodigoAsync(escenario.CodigoCemento))!.PrecioDetalle);
    }

    [SkippableFact]
    public async Task Catalogo_de_cobro_sale_ordenado_por_uso()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarYResolverAsync(escenario);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var cobro = await ambito.ServiceProvider.GetRequiredService<IConsultaCatalogoCobro>().ObtenerAsync();

        var mias = cobro.FormasPago.Where(f => f.Codigo.EndsWith(escenario.Sufijo)).ToList();
        Assert.Equal(new[] { $"EFE{escenario.Sufijo}", $"TAR{escenario.Sufijo}", $"NC{escenario.Sufijo}", $"PUN{escenario.Sufijo}" }, mias.Select(f => f.Codigo));
        Assert.True(mias[0].AbreGaveta);
        Assert.Contains(cobro.Bancos, b => b.Id == escenario.Banco);
        Assert.Contains(cobro.TiposTarjeta, t => t.Id == escenario.TipoTarjeta);
        Assert.Contains(cobro.Denominaciones, d => d.Id == escenario.BilleteMil);
    }

    [SkippableFact]
    public async Task El_documento_corregido_en_el_Central_actualiza_al_mismo_cliente_en_la_caja()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        var paquete = escenario.Paquete();
        await AplicarYResolverAsync(escenario);

        var corregido = EscenarioCatalogo.RncAleatorioValido();
        var cliente = paquete.Clientes!.Single(c => c.Codigo == escenario.CodigoCliente);
        await AplicarAsync(new PaqueteMaestros(Clientes: [cliente with { Documento = corregido }]));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var enCaja = await contexto.Clientes.AsNoTracking().SingleAsync(c => c.Id == escenario.Cliente);
        Assert.Equal((TipoDocumentoIdentidad.Rnc, corregido), (enCaja.TipoDocumento, enCaja.Documento));
        Assert.False(await contexto.Clientes.AnyAsync(c => c.Documento == escenario.RncCliente));
    }

    private Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete) => AplicarAsync(paquete, baseDatos.Servicios!);

    private async Task AplicarYResolverAsync(EscenarioCatalogo escenario)
    {
        await AplicarAsync(escenario.Paquete());
        await ResolverAsync(escenario);
    }

    private async Task ResolverAsync(EscenarioCatalogo escenario)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        await escenario.ResolverIdsAsync(ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>());
    }

    private static async Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete, IServiceProvider proveedor)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<ICargaMaestros>().AplicarAsync(paquete, "Pruebas");
    }

    private static async Task<DatosArticuloVenta?> BuscarAsync(IServiceProvider proveedor, string codigo)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>().BuscarPorCodigoAsync(codigo);
    }

}
