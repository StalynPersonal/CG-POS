using System.Globalization;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Globalizacion;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// Constancia de la mercancía entregada, para que la firme quien la recibe (RF-254). Sale en carta porque la sucursal imprime
/// en una impresora normal, no en la de tickets de 42 columnas de la caja. No es un comprobante fiscal: la factura ya se
/// emitió cuando se cobró.
/// </summary>
internal sealed class GeneradorPdfConstanciaEntrega
{
    private static readonly HojaPdf Hoja = HojaPdf.CartaVertical;
    private static readonly CultureInfo Cultura = CulturaRd.Crear();

    public byte[] Generar(PendienteEntrega pendiente, EntregaPendiente entrega, string empresaNombre, string? rnc, string? sucursal)
    {
        ArgumentNullException.ThrowIfNull(pendiente);
        ArgumentNullException.ThrowIfNull(entrega);

        var lineas = new List<(string Texto, bool Negrita)>
        {
            (empresaNombre, true),
        };

        if (rnc is { Length: > 0 })
            lineas.Add(($"RNC {rnc}", false));
        if (sucursal is { Length: > 0 })
            lineas.Add((sucursal, false));

        lineas.Add((string.Empty, false));
        lineas.Add(("CONSTANCIA DE ENTREGA", true));
        lineas.Add(($"Pendiente: {pendiente.Numero}    Entrega N° {entrega.Numero}", false));
        lineas.Add(($"Factura: {pendiente.VentaNumero}    Fecha: {entrega.Fecha.ToLocalTime().ToString("dd/MM/yyyy h:mm tt", Cultura)}", false));
        lineas.Add((string.Empty, false));

        // A quién pertenece la mercancía y a dónde iba.
        if (pendiente.ClienteNombre is { Length: > 0 } cliente)
            lineas.Add(($"Cliente: {cliente}", false));
        if (pendiente.ClienteDocumento is { Length: > 0 } documento)
            lineas.Add(($"RNC/Cédula: {documento}", false));

        lineas.Add(pendiente.Metodo == MetodoEntrega.Envio
            ? ($"Envío a: {Destino(pendiente)}", false)
            : ($"Retiro en: {pendiente.SucursalRetiroNombre}", false));
        if (pendiente.Transportista is { Length: > 0 } transportista)
            lineas.Add(($"Transportista: {transportista}", false));

        lineas.Add((string.Empty, false));
        lineas.AddRange(Detalle(entrega));
        lineas.Add((string.Empty, false));

        // Lo que queda por entregar, para que el cliente sepa qué falta.
        var porEntregar = pendiente.Lineas.Where(l => l.CantidadPendiente > 0).ToList();
        if (porEntregar.Count > 0)
        {
            lineas.Add(("Queda pendiente por entregar:", true));
            foreach (var linea in porEntregar.OrderBy(l => l.NumeroLineaVenta))
                lineas.Add(($"  {linea.CantidadPendiente.ToString("0.###", Cultura)} {linea.UnidadMedidaCodigo} · {linea.CodigoInterno} {linea.Descripcion}", false));

            lineas.Add((string.Empty, false));
        }

        lineas.Add(($"Entregado por: {entrega.UsuarioNombre}", false));
        lineas.Add((string.Empty, false));
        lineas.Add((string.Empty, false));
        lineas.Add(("_______________________________", false));
        lineas.Add(($"Recibe: {entrega.RecibeNombre}", false));
        lineas.Add(($"Cédula: {entrega.RecibeCedula}", false));

        var paginas = lineas.Chunk(Hoja.Lineas).ToList();
        return EnsambladorPdf.Crear(paginas, Hoja);
    }

    private static string Destino(PendienteEntrega pendiente) =>
        string.Join(", ", new[] { pendiente.Direccion, pendiente.Sector, pendiente.Ciudad }.Where(p => !string.IsNullOrWhiteSpace(p)));

    /// <summary>Lo que se entrega en este acto, en columnas de ancho fijo.</summary>
    private static IEnumerable<(string Texto, bool Negrita)> Detalle(EntregaPendiente entrega)
    {
        const int Cantidad = 10;
        const int Serial = 20;
        var descripcion = Math.Max(20, Hoja.Columnas - Cantidad - Serial - 2);

        string Fila(string cantidad, string texto, string serial) =>
            $"{cantidad.PadLeft(Cantidad)} {Recortar(texto, descripcion).PadRight(descripcion)} {Recortar(serial, Serial)}";

        yield return (Fila("Cantidad", "Artículo", "Serial"), true);
        yield return (new string('-', Hoja.Columnas), false);

        foreach (var linea in entrega.Lineas.OrderBy(l => l.NumeroLineaVenta))
            yield return (Fila(linea.Cantidad.ToString("0.###", Cultura), linea.Descripcion, linea.Serial ?? string.Empty), false);

        yield return (new string('-', Hoja.Columnas), false);
    }

    private static string Recortar(string texto, int ancho) => texto.Length <= ancho ? texto : texto[..ancho];
}
