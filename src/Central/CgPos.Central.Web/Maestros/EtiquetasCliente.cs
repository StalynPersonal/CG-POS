using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;

namespace CgPos.Central.Web.Maestros;

public static class EtiquetasCliente
{
    /// <summary>Comprobantes que se le pueden predeterminar a un cliente (la nota de crédito sale de una devolución).</summary>
    public static IReadOnlyList<TipoComprobante> Comprobantes { get; } =
        [TipoComprobante.FacturaConsumo, TipoComprobante.FacturaCreditoFiscal, TipoComprobante.RegimenesEspeciales, TipoComprobante.Gubernamental];

    public static IReadOnlyList<TipoDocumentoIdentidad> Documentos { get; } = Enum.GetValues<TipoDocumentoIdentidad>();

    public static IReadOnlyList<ListaPrecio> Listas { get; } = Enum.GetValues<ListaPrecio>();

    public static string Comprobante(TipoComprobante tipo) => $"E{(int)tipo} · {ReglasComprobante.Nombre(tipo)}";

    public static string Documento(TipoDocumentoIdentidad tipo) => tipo switch
    {
        TipoDocumentoIdentidad.Rnc => "RNC",
        TipoDocumentoIdentidad.Pasaporte => "Pasaporte",
        _ => "Cédula",
    };

    public static string Lista(ListaPrecio lista) => lista == ListaPrecio.Mayor ? "Por mayor" : "Detalle";
}
