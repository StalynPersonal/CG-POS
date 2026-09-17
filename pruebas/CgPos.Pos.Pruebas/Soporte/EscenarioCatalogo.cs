using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Pruebas.Soporte;

/// <summary>
/// Maestros de catálogo con códigos únicos por escenario, para pruebas que comparten base. Los Id son los que les da la caja al aplicar el paquete:
/// se leen con <see cref="ResolverIdsAsync"/>.
/// </summary>
public sealed class EscenarioCatalogo
{
    public string Sufijo { get; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    // Códigos numéricos de los catálogos
    public int CodigoFerreteria { get; } = Codigos.Siguiente();
    public int CodigoVegetales { get; } = Codigos.Siguiente();
    public int CodigoCategoriaHerramientas { get; } = Codigos.Siguiente();
    public int CodigoCategoriaVegetales { get; } = Codigos.Siguiente();
    public int CodigoUnidad { get; } = Codigos.Siguiente();
    public int CodigoLibraNumerico { get; } = Codigos.Siguiente();
    public int CodigoTipoTarjeta { get; } = Codigos.Siguiente();
    public int CodigoTopeGeneral { get; } = Codigos.Siguiente();
    public int CodigoMotivoDevolucion { get; } = Codigos.Siguiente();
    public int CodigoNivelOro { get; } = Codigos.Siguiente();
    public int CodigoReglaGeneral { get; } = Codigos.Siguiente();
    public int CodigoReglaFerreteria { get; } = Codigos.Siguiente();

    // Id locales (después de aplicar el paquete)
    public Guid DepartamentoFerreteria { get; private set; }
    public Guid DepartamentoVegetales { get; private set; }
    public Guid CategoriaHerramientas { get; private set; }
    public Guid CategoriaVegetales { get; private set; }
    public Guid UnidadUnidad { get; private set; }
    public Guid UnidadLibra { get; private set; }
    public Guid ImpuestoItbis18 { get; private set; }
    public Guid ImpuestoExento { get; private set; }

    public Guid ArticuloCincel { get; private set; }
    public Guid ArticuloCemento { get; private set; }
    public Guid ArticuloCombo { get; private set; }
    public Guid ArticuloTomate { get; private set; }
    public Guid ArticuloCebolla { get; private set; }
    public Guid ArticuloInactivo { get; private set; }
    public Guid ArticuloFueraDePos { get; private set; }
    public Guid ArticuloTaladro { get; private set; }

    public Guid Cliente { get; private set; }
    public Guid DireccionOficina { get; private set; }
    public Guid DireccionObra { get; private set; }
    public Guid FormaEfectivo { get; private set; }
    public Guid FormaTarjeta { get; private set; }
    public Guid Banco { get; private set; }
    public Guid TipoTarjeta { get; private set; }
    public Guid BilleteMil { get; private set; }
    public Guid FormaNotaCredito { get; private set; }
    public Guid TopeGeneral { get; private set; }
    public Guid MotivoDevolucion { get; private set; }
    public Guid FormaPuntos { get; private set; }
    public Guid NivelOro { get; private set; }
    public Guid ReglaGeneral { get; private set; }
    public Guid ReglaFerreteria { get; private set; }
    public Guid Miembro { get; private set; }
    public string CedulaMiembro { get; } = CedulaAleatoriaValida();

    /// <summary>Saldo que el Central sincronizó para el miembro del escenario.</summary>
    public const int SaldoMiembro = 500;

    public string CodigoCliente => $"CLI{Sufijo}";
    public string CodigoLibra => $"LB{Sufijo}";
    public string CodigoItbis18 => $"ITBIS18{Sufijo}";
    public string CodigoExento => $"EXE{Sufijo}";
    public string CodigoCincel => $"CIN-{Sufijo}";
    public string BarrasCincel { get; } = CodigoBarrasAleatorio();
    public string ProveedorCincel => $"TRA-{Sufijo}";
    public string CodigoCemento => $"CEM-{Sufijo}";
    public string BarrasCemento { get; } = CodigoBarrasAleatorio();
    public string CodigoCombo => $"KIT-{Sufijo}";
    public string CodigoTaladro => $"TAL-{Sufijo}";
    public const decimal TaraCebolla = 0.050m;
    public string PluTomate { get; } = PluAleatorio();
    public string PluCebolla { get; } = PluAleatorio();
    public string CodigoInactivo => $"INA-{Sufijo}";
    public string CodigoFueraDePos => $"NOP-{Sufijo}";
    public string CodigoEfectivo => $"EFE{Sufijo}";
    public string CodigoTarjeta => $"TAR{Sufijo}";
    public string CodigoNotaCredito => $"NC{Sufijo}";
    public string CodigoPuntos => $"PUN{Sufijo}";
    public string CodigoBanco => $"BAN{Sufijo}";
    public string RncCliente { get; } = RncAleatorioValido();

    /// <summary>Lee los Id que la caja les dio a los maestros del escenario (después de aplicar <see cref="Paquete"/>).</summary>
    public async Task ResolverIdsAsync(ContextoDatosPos contexto)
    {
        var departamentos = await contexto.Departamentos.Where(d => d.Codigo == CodigoFerreteria || d.Codigo == CodigoVegetales).ToDictionaryAsync(d => d.Codigo, d => d.Id);
        (DepartamentoFerreteria, DepartamentoVegetales) = (departamentos[CodigoFerreteria], departamentos[CodigoVegetales]);
        var categorias = await contexto.Categorias.Where(c => c.Codigo == CodigoCategoriaHerramientas || c.Codigo == CodigoCategoriaVegetales)
            .ToDictionaryAsync(c => c.Codigo, c => c.Id);
        (CategoriaHerramientas, CategoriaVegetales) = (categorias[CodigoCategoriaHerramientas], categorias[CodigoCategoriaVegetales]);
        var unidades = await contexto.UnidadesMedida.Where(u => u.Codigo == CodigoUnidad || u.Codigo == CodigoLibraNumerico).ToDictionaryAsync(u => u.Codigo, u => u.Id);
        (UnidadUnidad, UnidadLibra) = (unidades[CodigoUnidad], unidades[CodigoLibraNumerico]);
        var impuestos = await contexto.Impuestos.Where(i => i.Codigo == CodigoItbis18 || i.Codigo == CodigoExento).ToDictionaryAsync(i => i.Codigo, i => i.Id);
        (ImpuestoItbis18, ImpuestoExento) = (impuestos[CodigoItbis18], impuestos[CodigoExento]);

        var codigosArticulos = new[] { CodigoCincel, CodigoCemento, CodigoCombo, PluTomate, PluCebolla, CodigoInactivo, CodigoFueraDePos, CodigoTaladro };
        var articulos = await contexto.Articulos.Where(a => codigosArticulos.Contains(a.Codigo)).ToDictionaryAsync(a => a.Codigo, a => a.Id);
        (ArticuloCincel, ArticuloCemento, ArticuloCombo, ArticuloTomate) = (articulos[CodigoCincel], articulos[CodigoCemento], articulos[CodigoCombo], articulos[PluTomate]);
        (ArticuloCebolla, ArticuloInactivo, ArticuloFueraDePos, ArticuloTaladro) =
            (articulos[PluCebolla], articulos[CodigoInactivo], articulos[CodigoFueraDePos], articulos[CodigoTaladro]);

        var cliente = await contexto.Clientes.Include(c => c.Direcciones).SingleAsync(c => c.Codigo == CodigoCliente);
        Cliente = cliente.Id;
        (DireccionOficina, DireccionObra) = (cliente.Direcciones.Single(d => d.Alias == "Oficina").Id, cliente.Direcciones.Single(d => d.Alias == "Obra").Id);

        var formas = await contexto.FormasPago.Where(f => f.Codigo.EndsWith(Sufijo)).ToDictionaryAsync(f => f.Codigo, f => f.Id);
        (FormaEfectivo, FormaTarjeta, FormaNotaCredito, FormaPuntos) = (formas[CodigoEfectivo], formas[CodigoTarjeta], formas[CodigoNotaCredito], formas[CodigoPuntos]);
        Banco = await contexto.Bancos.Where(b => b.Codigo == CodigoBanco).Select(b => b.Id).SingleAsync();
        TipoTarjeta = await contexto.TiposTarjeta.Where(t => t.Codigo == CodigoTipoTarjeta).Select(t => t.Id).SingleAsync();
        BilleteMil = await contexto.Denominaciones.Where(d => d.Moneda == "DOP" && d.Valor == ValorBillete && d.Tipo == TipoDenominacion.Billete).Select(d => d.Id).SingleAsync();
        TopeGeneral = await contexto.TopesDescuento.Where(t => t.Codigo == CodigoTopeGeneral).Select(t => t.Id).SingleAsync();
        MotivoDevolucion = await contexto.MotivosDevolucion.Where(m => m.Codigo == CodigoMotivoDevolucion).Select(m => m.Id).SingleAsync();
        NivelOro = await contexto.NivelesFidelidad.Where(n => n.Codigo == CodigoNivelOro).Select(n => n.Id).SingleAsync();
        var reglas = await contexto.ReglasAcumulacion.Where(r => r.Codigo == CodigoReglaGeneral || r.Codigo == CodigoReglaFerreteria).ToDictionaryAsync(r => r.Codigo, r => r.Id);
        (ReglaGeneral, ReglaFerreteria) = (reglas[CodigoReglaGeneral], reglas[CodigoReglaFerreteria]);
        Miembro = await contexto.MiembrosFidelidad.Where(m => m.Cedula == CedulaMiembro).Select(m => m.Id).SingleAsync();
    }

    public PaqueteMaestros Paquete(decimal precioCemento = 485m, DateTimeOffset? vigenciaPrecios = null) =>
        new(
            Monedas:
            [
                new MonedaCarga("DOP", "Peso dominicano", "RD$"),
                new MonedaCarga("USD", "Dólar estadounidense", "US$"),
            ],
            Departamentos:
            [
                new DepartamentoCarga(CodigoFerreteria, $"Ferretería {Sufijo}"),
                new DepartamentoCarga(CodigoVegetales, $"Vegetales {Sufijo}", PermiteDescuentoManual: false, EsNoCodificada: true),
            ],
            Categorias:
            [
                new CategoriaCarga(CodigoCategoriaHerramientas, $"Herramientas {Sufijo}", CodigoFerreteria),
                new CategoriaCarga(CodigoCategoriaVegetales, $"Vegetales {Sufijo}", CodigoVegetales),
            ],
            UnidadesMedida:
            [
                new UnidadMedidaCarga(CodigoUnidad, $"UN{Sufijo}", "Unidad"),
                new UnidadMedidaCarga(CodigoLibraNumerico, CodigoLibra, "Libra", PermiteDecimales: true, Decimales: 3),
            ],
            Impuestos:
            [
                new ImpuestoCarga(CodigoItbis18, "ITBIS 18%", 18m, 1),
                new ImpuestoCarga(CodigoExento, "Exento", 0m, 4),
            ],
            Articulos:
            [
                new ArticuloCarga(CodigoCincel, $"Cincel de punta SDS {Sufijo}", CodigoFerreteria, CodigoUnidad, CodigoItbis18, 850m,
                    Costo: 540m, CodigosBarras: [BarrasCincel], CodigosProveedor: [ProveedorCincel], PreciosVigentesDesde: vigenciaPrecios, CategoriaCodigo: CodigoCategoriaHerramientas),
                new ArticuloCarga(CodigoCemento, $"Cemento gris 42.5 kg {Sufijo}", CodigoFerreteria, CodigoUnidad, CodigoItbis18, precioCemento,
                    PrecioMayor: 450m, CantidadMinimaMayor: 12m, Costo: 350m, PrecioMinimo: 440m, CodigosBarras: [BarrasCemento], PreciosVigentesDesde: vigenciaPrecios,
                    CategoriaCodigo: CodigoCategoriaHerramientas),
                new ArticuloCarga(CodigoCombo, $"Combo herramientas {Sufijo}", CodigoFerreteria, CodigoUnidad, CodigoItbis18, 2500m,
                    PrecioMayor: 2300m, Tipo: TipoArticulo.ComboKit, PreciosVigentesDesde: vigenciaPrecios, CategoriaCodigo: CodigoCategoriaHerramientas),
                new ArticuloCarga(PluTomate, $"Tomate {Sufijo}", CodigoVegetales, CodigoLibraNumerico, CodigoExento, 45m,
                    Tipo: TipoArticulo.Pesado, MostrarEnCatalogo: true, PreciosVigentesDesde: vigenciaPrecios, CategoriaCodigo: CodigoCategoriaVegetales),
                new ArticuloCarga(PluCebolla, $"Cebolla {Sufijo}", CodigoVegetales, CodigoLibraNumerico, CodigoExento, 55m,
                    Tipo: TipoArticulo.Pesado, MostrarEnCatalogo: true, PreciosVigentesDesde: vigenciaPrecios, Tara: TaraCebolla, CategoriaCodigo: CodigoCategoriaVegetales),
                new ArticuloCarga(CodigoTaladro, $"Taladro inalámbrico {Sufijo}", CodigoFerreteria, CodigoUnidad, CodigoItbis18, 6950m,
                    Tipo: TipoArticulo.Serializado, PreciosVigentesDesde: vigenciaPrecios, CategoriaCodigo: CodigoCategoriaHerramientas),
                new ArticuloCarga(CodigoInactivo, $"Artículo descontinuado {Sufijo}", CodigoFerreteria, CodigoUnidad, CodigoItbis18, 100m,
                    Activo: false, PreciosVigentesDesde: vigenciaPrecios, CategoriaCodigo: CodigoCategoriaHerramientas),
                new ArticuloCarga(CodigoFueraDePos, $"Artículo solo almacén {Sufijo}", CodigoFerreteria, CodigoUnidad, CodigoItbis18, 100m,
                    VentaEnPos: false, PreciosVigentesDesde: vigenciaPrecios, CategoriaCodigo: CodigoCategoriaHerramientas),
            ],
            Clientes:
            [
                new ClienteCarga(CodigoCliente, TipoDocumentoIdentidad.Rnc, RncCliente, $"Constructora {Sufijo} SRL", TipoComprobante.FacturaCreditoFiscal,
                    ListaPrecio: ListaPrecio.Mayor,
                    Direcciones:
                    [
                        new DireccionClienteCarga("Obra", "Calle de prueba 25"),
                        new DireccionClienteCarga("Oficina", "Av. Ficticia 100", EsPrincipal: true),
                    ]),
            ],
            FormasPago:
            [
                new FormaPagoCarga(CodigoTarjeta, "Tarjeta", TipoFormaPago.Tarjeta, 2, "DOP"),
                new FormaPagoCarga(CodigoEfectivo, "Efectivo", TipoFormaPago.Efectivo, 1, "DOP"),
                new FormaPagoCarga(CodigoNotaCredito, "Nota de crédito", TipoFormaPago.NotaCredito, 3, "DOP", RequiereReferencia: true, PermiteDevuelta: false),
                new FormaPagoCarga(CodigoPuntos, "Puntos", TipoFormaPago.Puntos, 4, "DOP"),
            ],
            // Programa de fidelidad: 1 punto por cada 100 en todo y 2 en ferretería; el nivel Oro acumula 50 % más.
            NivelesFidelidad: [new NivelFidelidadCarga(CodigoNivelOro, "Oro", 2, 1.5m)],
            ReglasAcumulacion:
            [
                new ReglaAcumulacionCarga(CodigoReglaGeneral, "Compras en general", CgPos.Dominio.Fidelidad.TipoReglaAcumulacion.Monto, 100m, 1m),
                new ReglaAcumulacionCarga(CodigoReglaFerreteria, "Ferretería doble", CgPos.Dominio.Fidelidad.TipoReglaAcumulacion.Departamento, 100m, 2m,
                    CodigoFerreteria.ToString()),
            ],
            MiembrosFidelidad:
            [
                new MiembroFidelidadCarga(CedulaMiembro, $"Miembro {Sufijo}", NivelCodigo: CodigoNivelOro, SaldoPuntos: SaldoMiembro,
                    SaldoAl: EscenarioSeguridad.Inicio.AddDays(-1)),
            ],
            MotivosDevolucion: [new MotivoDevolucionCarga(CodigoMotivoDevolucion, "Artículo defectuoso")],
            Bancos: [new BancoCarga(CodigoBanco, $"Banco {Sufijo}")],
            TiposTarjeta: [new TipoTarjetaCarga(CodigoTipoTarjeta, "Visa prueba")],
            Denominaciones: [new DenominacionCarga("DOP", ValorBillete, TipoDenominacion.Billete)],
            // Sin topes configurados no hay descuento manual: las pruebas trabajan con un tope general amplio.
            TopesDescuento: [new TopeDescuentoCarga(CodigoTopeGeneral, 1, 100m, null)]);

    /// <summary>
    /// Valor único por escenario (moneda + valor + tipo es único en la base), fijo entre cargas:
    /// una denominación no puede cambiar de valor.
    /// </summary>
    public decimal ValorBillete { get; } = 1000m + Random.Shared.Next(1, 999_000);

    /// <summary>Etiqueta de balanza EAN-13 con el peso del tomate (formato predeterminado: prefijo 21, 3 decimales).</summary>
    public string EtiquetaPesoTomate(decimal pesoEnLibras)
    {
        var datos = "21" + PluTomate + ((long)(pesoEnLibras * 1000)).ToString("D5");
        return datos + InterpreteCodigoBalanza.CalcularDigitoControlGs1(datos);
    }

    public static string RncAleatorioValido()
    {
        while (true)
        {
            var baseRnc = Random.Shared.Next(10_000_000, 99_999_999).ToString();
            for (var digito = 0; digito <= 9; digito++)
            {
                var candidato = baseRnc + digito;
                if (DocumentoIdentidad.RncValido(candidato))
                    return candidato;
            }
        }
    }

    public static string CedulaAleatoriaValida()
    {
        while (true)
        {
            var candidata = Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString() + Random.Shared.Next(0, 10);
            if (DocumentoIdentidad.CedulaValida(candidata))
                return candidata;
        }
    }

    private static string CodigoBarrasAleatorio() =>
        "9" + string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)));

    private static string PluAleatorio() => Random.Shared.Next(10_000, 99_999).ToString();
}
