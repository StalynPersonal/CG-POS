using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;

namespace CgPos.Pos.Pruebas.Soporte;

/// <summary>Maestros de catálogo con códigos e Ids únicos por escenario, para pruebas que comparten base.</summary>
public sealed class EscenarioCatalogo
{
    public string Sufijo { get; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    public Guid FamiliaFerreteria { get; } = Guid.CreateVersion7();
    public Guid FamiliaVegetales { get; } = Guid.CreateVersion7();
    public Guid UnidadUnidad { get; } = Guid.CreateVersion7();
    public Guid UnidadLibra { get; } = Guid.CreateVersion7();
    public Guid ImpuestoItbis18 { get; } = Guid.CreateVersion7();
    public Guid ImpuestoExento { get; } = Guid.CreateVersion7();

    public Guid ArticuloCincel { get; } = Guid.CreateVersion7();
    public Guid ArticuloCemento { get; } = Guid.CreateVersion7();
    public Guid ArticuloCombo { get; } = Guid.CreateVersion7();
    public Guid ArticuloTomate { get; } = Guid.CreateVersion7();
    public Guid ArticuloCebolla { get; } = Guid.CreateVersion7();
    public Guid ArticuloInactivo { get; } = Guid.CreateVersion7();
    public Guid ArticuloFueraDePos { get; } = Guid.CreateVersion7();

    public Guid Cliente { get; } = Guid.CreateVersion7();
    public Guid DireccionOficina { get; } = Guid.CreateVersion7();
    public Guid DireccionObra { get; } = Guid.CreateVersion7();
    public Guid FormaEfectivo { get; } = Guid.CreateVersion7();
    public Guid FormaTarjeta { get; } = Guid.CreateVersion7();
    public Guid Banco { get; } = Guid.CreateVersion7();
    public Guid TipoTarjeta { get; } = Guid.CreateVersion7();
    public Guid BilleteMil { get; } = Guid.CreateVersion7();
    public Guid FormaNotaCredito { get; } = Guid.CreateVersion7();
    public Guid TopeGeneral { get; } = Guid.CreateVersion7();
    public Guid MotivoDevolucion { get; } = Guid.CreateVersion7();
    public string CodigoMotivoDevolucion => $"DEF{Sufijo}";

    public string CodigoFerreteria => $"FER{Sufijo}";
    public string CodigoLibra => $"LB{Sufijo}";
    public string CodigoItbis18 => $"ITBIS18{Sufijo}";
    public string CodigoCincel => $"CIN-{Sufijo}";
    public string BarrasCincel { get; } = CodigoBarrasAleatorio();
    public string ProveedorCincel => $"TRA-{Sufijo}";
    public string CodigoCemento => $"CEM-{Sufijo}";
    public string BarrasCemento { get; } = CodigoBarrasAleatorio();
    public string CodigoCombo => $"KIT-{Sufijo}";
    public Guid ArticuloTaladro { get; } = Guid.CreateVersion7();
    public string CodigoTaladro => $"TAL-{Sufijo}";
    public const decimal TaraCebolla = 0.050m;
    public string PluTomate { get; } = PluAleatorio();
    public string PluCebolla { get; } = PluAleatorio();
    public string CodigoInactivo => $"INA-{Sufijo}";
    public string CodigoFueraDePos => $"NOP-{Sufijo}";
    public string RncCliente { get; } = RncAleatorioValido();

    /// <summary>Mismos Ids que los datos de desarrollo: el código de moneda es único y las pruebas comparten base.</summary>
    public static readonly Guid MonedaPesos = Guid.Parse("01990000-0000-7000-8001-000000001001");

    public static readonly Guid MonedaDolares = Guid.Parse("01990000-0000-7000-8001-000000001002");

    public PaqueteMaestros Paquete(decimal precioCemento = 485m, DateTimeOffset? vigenciaPrecios = null) =>
        new(
            Monedas:
            [
                new MonedaCarga(MonedaPesos, "DOP", "Peso dominicano", "RD$"),
                new MonedaCarga(MonedaDolares, "USD", "Dólar estadounidense", "US$"),
            ],
            Familias:
            [
                new FamiliaCarga(FamiliaFerreteria, CodigoFerreteria, $"Ferretería {Sufijo}"),
                new FamiliaCarga(FamiliaVegetales, $"VEG{Sufijo}", $"Vegetales {Sufijo}", PermiteDescuentoManual: false, EsNoCodificada: true),
            ],
            UnidadesMedida:
            [
                new UnidadMedidaCarga(UnidadUnidad, $"UN{Sufijo}", "Unidad"),
                new UnidadMedidaCarga(UnidadLibra, CodigoLibra, "Libra", PermiteDecimales: true, Decimales: 3),
            ],
            Impuestos:
            [
                new ImpuestoCarga(ImpuestoItbis18, CodigoItbis18, "ITBIS 18%", 18m, 1),
                new ImpuestoCarga(ImpuestoExento, $"EXE{Sufijo}", "Exento", 0m, 4),
            ],
            Articulos:
            [
                new ArticuloCarga(ArticuloCincel, CodigoCincel, $"Cincel de punta SDS {Sufijo}", FamiliaFerreteria, UnidadUnidad, ImpuestoItbis18, 850m,
                    Costo: 540m, CodigosBarras: [BarrasCincel], CodigosProveedor: [ProveedorCincel], PreciosVigentesDesde: vigenciaPrecios),
                new ArticuloCarga(ArticuloCemento, CodigoCemento, $"Cemento gris 42.5 kg {Sufijo}", FamiliaFerreteria, UnidadUnidad, ImpuestoItbis18, precioCemento,
                    PrecioMayor: 450m, CantidadMinimaMayor: 12m, Costo: 350m, PrecioMinimo: 440m, CodigosBarras: [BarrasCemento], PreciosVigentesDesde: vigenciaPrecios),
                new ArticuloCarga(ArticuloCombo, CodigoCombo, $"Combo herramientas {Sufijo}", FamiliaFerreteria, UnidadUnidad, ImpuestoItbis18, 2500m,
                    PrecioMayor: 2300m, Tipo: TipoArticulo.ComboKit, PreciosVigentesDesde: vigenciaPrecios),
                new ArticuloCarga(ArticuloTomate, PluTomate, $"Tomate {Sufijo}", FamiliaVegetales, UnidadLibra, ImpuestoExento, 45m,
                    Tipo: TipoArticulo.Pesado, MostrarEnCatalogo: true, PreciosVigentesDesde: vigenciaPrecios),
                new ArticuloCarga(ArticuloCebolla, PluCebolla, $"Cebolla {Sufijo}", FamiliaVegetales, UnidadLibra, ImpuestoExento, 55m,
                    Tipo: TipoArticulo.Pesado, MostrarEnCatalogo: true, PreciosVigentesDesde: vigenciaPrecios, Tara: TaraCebolla),
                new ArticuloCarga(ArticuloTaladro, CodigoTaladro, $"Taladro inalámbrico {Sufijo}", FamiliaFerreteria, UnidadUnidad, ImpuestoItbis18, 6950m,
                    Tipo: TipoArticulo.Serializado, PreciosVigentesDesde: vigenciaPrecios),
                new ArticuloCarga(ArticuloInactivo, CodigoInactivo, $"Artículo descontinuado {Sufijo}", FamiliaFerreteria, UnidadUnidad, ImpuestoItbis18, 100m,
                    Activo: false, PreciosVigentesDesde: vigenciaPrecios),
                new ArticuloCarga(ArticuloFueraDePos, CodigoFueraDePos, $"Artículo solo almacén {Sufijo}", FamiliaFerreteria, UnidadUnidad, ImpuestoItbis18, 100m,
                    VentaEnPos: false, PreciosVigentesDesde: vigenciaPrecios),
            ],
            Clientes:
            [
                new ClienteCarga(Cliente, TipoDocumentoIdentidad.Rnc, RncCliente, $"Constructora {Sufijo} SRL", TipoComprobante.FacturaCreditoFiscal,
                    ListaPrecio: ListaPrecio.Mayor,
                    Direcciones:
                    [
                        new DireccionClienteCarga(DireccionObra, "Obra", "Calle de prueba 25"),
                        new DireccionClienteCarga(DireccionOficina, "Oficina", "Av. Ficticia 100", EsPrincipal: true),
                    ]),
            ],
            FormasPago:
            [
                new FormaPagoCarga(FormaTarjeta, $"TAR{Sufijo}", "Tarjeta", TipoFormaPago.Tarjeta, 2, "DOP"),
                new FormaPagoCarga(FormaEfectivo, $"EFE{Sufijo}", "Efectivo", TipoFormaPago.Efectivo, 1, "DOP"),
                new FormaPagoCarga(FormaNotaCredito, $"NC{Sufijo}", "Nota de crédito", TipoFormaPago.NotaCredito, 3, "DOP", RequiereReferencia: true, PermiteDevuelta: false),
            ],
            MotivosDevolucion: [new MotivoDevolucionCarga(MotivoDevolucion, CodigoMotivoDevolucion, "Artículo defectuoso")],
            Bancos: [new BancoCarga(Banco, $"BAN{Sufijo}", $"Banco {Sufijo}")],
            TiposTarjeta: [new TipoTarjetaCarga(TipoTarjeta, $"TT{Sufijo}", "Visa prueba")],
            Denominaciones: [new DenominacionCarga(BilleteMil, "DOP", ValorBillete, TipoDenominacion.Billete)],
            // Sin topes configurados no hay descuento manual: las pruebas trabajan con un tope general amplio.
            TopesDescuento: [new TopeDescuentoCarga(TopeGeneral, 1, 100m, null)]);

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

    private static string CodigoBarrasAleatorio() =>
        "9" + string.Concat(Enumerable.Range(0, 12).Select(_ => Random.Shared.Next(0, 10)));

    private static string PluAleatorio() => Random.Shared.Next(10_000, 99_999).ToString();
}
