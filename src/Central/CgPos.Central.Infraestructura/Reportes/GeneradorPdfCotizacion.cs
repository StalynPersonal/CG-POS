using System.Globalization;
using CgPos.Contratos.Central;
using CgPos.Dominio.Globalizacion;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>Datos de la empresa que encabezan la cotización.</summary>
internal sealed record EmpresaEnDocumento(string Nombre, string Rnc, string? Direccion, string? Telefono);

/// <summary>
/// La cotización tal como se le entrega al cliente: hoja carta, con el encabezado de la empresa, a quién va dirigida, el
/// detalle con precios, los totales y hasta cuándo vale. Es un documento comercial, no un comprobante fiscal.
/// </summary>
internal static class GeneradorPdfCotizacion
{
    private static readonly HojaPdf Hoja = HojaPdf.CartaVertical;
    private static readonly CultureInfo Cultura = CulturaRd.Crear();

    public static byte[] Crear(DatosCotizacion cotizacion, EmpresaEnDocumento empresa, string? condiciones)
    {
        ArgumentNullException.ThrowIfNull(cotizacion);
        ArgumentNullException.ThrowIfNull(empresa);

        var lineas = new List<(string Texto, bool Negrita)>
        {
            (empresa.Nombre, true),
            ($"RNC {empresa.Rnc}", false),
        };

        if (empresa.Direccion is { Length: > 0 } direccion)
            lineas.Add((direccion, false));
        if (empresa.Telefono is { Length: > 0 } telefono)
            lineas.Add(($"Tel. {telefono}", false));

        lineas.Add((string.Empty, false));
        lineas.Add(($"COTIZACIÓN {cotizacion.Numero}", true));
        lineas.Add(($"Fecha: {cotizacion.CreadaEn.ToLocalTime().ToString("dd/MM/yyyy", Cultura)}    Válida hasta: {cotizacion.VenceEn.ToString("dd/MM/yyyy", Cultura)}", false));
        lineas.Add((string.Empty, false));

        // A quién va dirigida.
        lineas.Add(($"Cliente: {cotizacion.ClienteNombre}", false));
        if (cotizacion.ClienteDocumento is { Length: > 0 } documento)
            lineas.Add(($"RNC/Cédula: {documento}", false));
        if (cotizacion.ClienteTelefono is { Length: > 0 } telefonoCliente)
            lineas.Add(($"Teléfono: {telefonoCliente}", false));
        if (cotizacion.Observacion is { Length: > 0 } observacion)
            lineas.Add(($"Nota: {observacion}", false));

        lineas.Add((string.Empty, false));
        lineas.AddRange(Detalle(cotizacion));
        lineas.Add((string.Empty, false));

        // Totales, alineados a la derecha del ancho de la hoja.
        lineas.Add((Derecha($"Subtotal: {cotizacion.Subtotal.ToString("N2", Cultura)}"), false));
        if (cotizacion.Descuento > 0)
            lineas.Add((Derecha($"Descuentos: {cotizacion.Descuento.ToString("N2", Cultura)}"), false));
        lineas.Add((Derecha($"ITBIS: {cotizacion.Impuesto.ToString("N2", Cultura)}"), false));
        lineas.Add((Derecha($"TOTAL: {cotizacion.Total.ToString("N2", Cultura)}"), true));

        if (condiciones is { Length: > 0 })
        {
            lineas.Add((string.Empty, false));
            lineas.AddRange(Envolver(condiciones).Select(parte => (parte, false)));
        }

        lineas.Add((string.Empty, false));
        lineas.Add(($"Cotizó: {cotizacion.CreadaPor}", false));
        lineas.Add(("Los precios de esta cotización se respetan hasta la fecha indicada.", false));

        var paginas = lineas.Chunk(Hoja.Lineas).ToList();
        return EnsambladorPdf.Crear(paginas, Hoja);
    }

    /// <summary>El detalle en columnas de ancho fijo: cantidad, descripción, precio, descuento e importe.</summary>
    private static IEnumerable<(string Texto, bool Negrita)> Detalle(DatosCotizacion cotizacion)
    {
        const int Cantidad = 9;
        const int Precio = 12;
        const int Descuento = 11;
        const int Importe = 12;
        var descripcion = Math.Max(20, Hoja.Columnas - Cantidad - Precio - Descuento - Importe - 4);

        string Fila(string cantidad, string texto, string precio, string descuento, string importe) =>
            $"{cantidad.PadLeft(Cantidad)} {Recortar(texto, descripcion).PadRight(descripcion)} {precio.PadLeft(Precio)} "
            + $"{descuento.PadLeft(Descuento)} {importe.PadLeft(Importe)}";

        yield return (Fila("Cantidad", "Artículo", "Precio", "Descuento", "Importe"), true);
        yield return (new string('-', Hoja.Columnas), false);

        foreach (var linea in cotizacion.Lineas)
        {
            yield return (Fila(
                linea.Cantidad.ToString("0.###", Cultura),
                $"{linea.ArticuloCodigo} {linea.Descripcion}",
                linea.PrecioUnitario.ToString("N2", Cultura),
                linea.Descuento == 0 ? string.Empty : linea.Descuento.ToString("N2", Cultura),
                linea.Importe.ToString("N2", Cultura)), false);
        }

        yield return (new string('-', Hoja.Columnas), false);
    }

    private static string Derecha(string texto) => texto.PadLeft(Hoja.Columnas);

    private static string Recortar(string texto, int ancho) => texto.Length <= ancho ? texto : texto[..ancho];

    /// <summary>Parte un texto largo en líneas que quepan a lo ancho de la hoja, sin cortar palabras.</summary>
    private static IEnumerable<string> Envolver(string texto)
    {
        var linea = string.Empty;
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (linea.Length + palabra.Length + 1 > Hoja.Columnas)
            {
                yield return linea;
                linea = palabra;
            }
            else
            {
                linea = linea.Length == 0 ? palabra : $"{linea} {palabra}";
            }
        }

        if (linea.Length > 0)
            yield return linea;
    }
}
