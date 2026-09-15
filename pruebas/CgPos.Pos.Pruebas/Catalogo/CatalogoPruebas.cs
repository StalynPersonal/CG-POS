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

/// <summary>Maestros, precios, búsqueda, padrón DGII e importaciones contra SQL Server real.</summary>
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
        await AplicarAsync(escenario.Paquete());

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
        await AplicarAsync(escenario.Paquete());

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
        await AplicarAsync(escenario.Paquete());

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>();

        Assert.Null(await consulta.BuscarPorCodigoAsync(escenario.CodigoInactivo));
        Assert.Null(await consulta.BuscarPorCodigoAsync(escenario.CodigoFueraDePos));
        Assert.Empty(await consulta.BuscarAsync($"descontinuado {escenario.Sufijo}"));
        Assert.Empty(await consulta.BuscarAsync($"almacén {escenario.Sufijo}"));
    }

    [SkippableFact]
    public async Task Busqueda_por_varias_palabras_familia_y_listado_de_no_codificados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarAsync(escenario.Paquete());

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaArticulos>();

        var cemento = await consulta.BuscarAsync($"gris {escenario.Sufijo} cemento");
        Assert.Equal(escenario.ArticuloCemento, Assert.Single(cemento).ArticuloId);
        Assert.Equal(485m, cemento[0].PrecioDetalle);

        var porFamilia = await consulta.BuscarAsync(escenario.Sufijo, escenario.FamiliaVegetales);
        Assert.Equal(2, porFamilia.Count);

        var noCodificados = await consulta.ListarNoCodificadosAsync(escenario.FamiliaVegetales);
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
        var familiaInexistente = Guid.CreateVersion7();
        var conErrores = paquete with
        {
            Articulos =
            [
                .. paquete.Articulos!,
                new ArticuloCarga(Guid.CreateVersion7(), $"MAL-{escenario.Sufijo}", "Sin familia", familiaInexistente, escenario.UnidadUnidad, escenario.ImpuestoItbis18, 10m),
                new ArticuloCarga(Guid.CreateVersion7(), $"DUP-{escenario.Sufijo}", "Código repetido", escenario.FamiliaFerreteria, escenario.UnidadUnidad, escenario.ImpuestoItbis18, 10m,
                    CodigosBarras: [escenario.BarrasCincel]),
            ],
        };

        var error = await Assert.ThrowsAsync<CargaMaestrosInvalidaExcepcion>(() => AplicarAsync(conErrores));

        Assert.Contains(error.Errores, e => e.Contains("familia inexistente"));
        Assert.Contains(error.Errores, e => e.Contains(escenario.BarrasCincel));

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        Assert.False(await contexto.Articulos.AnyAsync(a => a.Id == escenario.ArticuloCincel));
    }

    [SkippableFact]
    public async Task Consulta_de_documento_combina_padron_dgii_y_cliente_registrado()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarAsync(escenario.Paquete());
        var rncSinCliente = EscenarioCatalogo.RncAleatorioValido();
        await ImportarPadronAsync(
            $"{escenario.RncCliente}|CONSTRUCTORA {escenario.Sufijo} SRL|CONSTRUCTORA|CONSTRUCCION|||||01/01/2010|ACTIVO|NORMAL",
            $"{rncSinCliente}|EMPRESA SUSPENDIDA {escenario.Sufijo}||COMERCIO|||||01/01/2015|SUSPENDIDO|NORMAL");

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambito.ServiceProvider.GetRequiredService<IConsultaDocumentos>();

        var conCliente = await consulta.ConsultarAsync($"{escenario.RncCliente[..3]}-{escenario.RncCliente[3..8]}-{escenario.RncCliente[8..]}");
        Assert.Equal(TipoDocumentoIdentidad.Rnc, conCliente.Tipo);
        Assert.True(conCliente.DigitoVerificadorValido);
        Assert.True(conCliente.EnPadron);
        Assert.True(conCliente.ContribuyenteActivo);
        Assert.NotNull(conCliente.Cliente);
        Assert.Equal(TipoComprobante.FacturaCreditoFiscal, conCliente.Cliente.TipoComprobante);
        Assert.Equal(ListaPrecio.Mayor, conCliente.Cliente.ListaPrecio);
        Assert.Equal("Oficina", conCliente.Cliente.Direcciones[0].Alias); // la principal primero
        Assert.Equal(2, conCliente.Cliente.Direcciones.Count);

        var suspendido = await consulta.ConsultarAsync(rncSinCliente);
        Assert.True(suspendido.EnPadron);
        Assert.False(suspendido.ContribuyenteActivo);
        Assert.Null(suspendido.Cliente);

        var noEnPadron = await consulta.ConsultarAsync(EscenarioCatalogo.RncAleatorioValido());
        Assert.True(noEnPadron.DigitoVerificadorValido);
        Assert.False(noEnPadron.EnPadron);

        var invalido = await consulta.ConsultarAsync("123");
        Assert.False(invalido.FormatoValido);
    }

    [SkippableFact]
    public async Task Importador_de_padron_inserta_actualiza_y_descarta_lineas_invalidas()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var rncUno = EscenarioCatalogo.RncAleatorioValido();
        var rncDos = EscenarioCatalogo.RncAleatorioValido();

        var primera = await ImportarPadronAsync(
            $"{rncUno}|EMPRESA UNO SRL|UNO|COMERCIO|||||01/01/2010|ACTIVO|NORMAL",
            $"{rncDos}|EMPRESA DOS SRL||SERVICIOS|||||01/01/2011|ACTIVO|NORMAL",
            $"{rncDos}|EMPRESA DOS SRL (REPETIDA)||SERVICIOS|||||01/01/2011|ACTIVO|NORMAL",
            "ESTO-NO-ES-UN-RNC|LINEA INVALIDA");

        Assert.Equal(4, primera.LineasLeidas);
        Assert.Equal(3, primera.RegistrosValidos);
        Assert.Equal(1, primera.LineasDescartadas);

        await ImportarPadronAsync($"{rncUno}|EMPRESA UNO SRL|UNO|COMERCIO|||||01/01/2010|DADO DE BAJA|NORMAL");

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var uno = await contexto.ContribuyentesDgii.SingleAsync(c => c.Documento == rncUno);
        var dos = await contexto.ContribuyentesDgii.SingleAsync(c => c.Documento == rncDos);

        Assert.Equal("DADO DE BAJA", uno.Estado);
        Assert.False(uno.EstaActivo);
        Assert.Equal("EMPRESA DOS SRL (REPETIDA)", dos.RazonSocial); // ante repetidos gana la última línea
        Assert.Equal("NORMAL", dos.RegimenPago);
    }

    [SkippableFact]
    public async Task Importador_csv_guarda_las_lineas_validas_e_informa_errores_por_linea()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();
        await AplicarAsync(escenario.Paquete());
        var codigoNuevo = $"NUEVO-{escenario.Sufijo}";
        var barrasNuevo = "8" + Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);

        var csv = string.Join("\n",
            "codigo;descripcion;familia;unidad;impuesto;precio_detalle;precio_mayor;cantidad_minima_mayor;codigos_barras;tipo",
            $"{codigoNuevo};\"Martillo; mango de fibra\";{escenario.CodigoFerreteria};{escenario.CodigoLibra};{escenario.CodigoItbis18};325.50;300;6;{barrasNuevo};Normal",
            $"{escenario.CodigoCemento};Cemento gris 42.5 kg {escenario.Sufijo};{escenario.CodigoFerreteria};{escenario.CodigoLibra};{escenario.CodigoItbis18};510;450;12;{escenario.BarrasCemento};Normal",
            $"MALA-{escenario.Sufijo};Familia que no existe;NOEXISTE;{escenario.CodigoLibra};{escenario.CodigoItbis18};10;;;;Normal",
            $"MALB-{escenario.Sufijo};Precio mal escrito;{escenario.CodigoFerreteria};{escenario.CodigoLibra};{escenario.CodigoItbis18};10,50;;;;Normal");

        ResultadoImportacionArticulos resultado;
        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            await using var contenido = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            resultado = await ambito.ServiceProvider.GetRequiredService<IImportadorArticulos>().ImportarCsvAsync(contenido, "Importación CSV");
        }

        Assert.Equal(1, resultado.Creados);
        Assert.Equal(1, resultado.Actualizados);
        Assert.Equal(3, resultado.PreciosRegistrados); // nuevo: detalle + mayor; cemento: detalle 510
        Assert.Equal(2, resultado.Errores.Count);
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
        await AplicarAsync(escenario.Paquete());

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var cobro = await ambito.ServiceProvider.GetRequiredService<IConsultaCatalogoCobro>().ObtenerAsync();

        var mias = cobro.FormasPago.Where(f => f.Codigo.EndsWith(escenario.Sufijo)).ToList();
        Assert.Equal(new[] { $"EFE{escenario.Sufijo}", $"TAR{escenario.Sufijo}", $"NC{escenario.Sufijo}" }, mias.Select(f => f.Codigo));
        Assert.True(mias[0].AbreGaveta);
        Assert.Contains(cobro.Bancos, b => b.Id == escenario.Banco);
        Assert.Contains(cobro.TiposTarjeta, t => t.Id == escenario.TipoTarjeta);
        Assert.Contains(cobro.Denominaciones, d => d.Id == escenario.BilleteMil);
    }

    private Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete) => AplicarAsync(paquete, baseDatos.Servicios!);

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

    private async Task<ResultadoImportacionPadron> ImportarPadronAsync(params string[] lineas)
    {
        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        await using var contenido = new MemoryStream(Encoding.Latin1.GetBytes(string.Join("\n", lineas)));
        return await ambito.ServiceProvider.GetRequiredService<IImportadorPadronDgii>().ImportarAsync(contenido);
    }
}
