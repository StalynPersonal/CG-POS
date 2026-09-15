using System.Globalization;
using System.Text;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Globalizacion;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Perifericos;

namespace CgPos.Pos.Infraestructura.Tickets;

/// <summary>Datos del encabezado del ticket que no están en la venta.</summary>
internal sealed record EncabezadoTicket(
    string EmpresaNombre,
    string EmpresaRnc,
    string? EmpresaDireccion,
    string? EmpresaTelefono,
    string SucursalNombre,
    string? SucursalDireccion,
    string CajaCodigo);

/// <summary>
/// Ticket de venta para impresora térmica de 80 mm (42 columnas): texto plano y ESC/POS con la página de códigos PC858 para
/// acentos y ñ. Muestra descuentos por línea y totalizados (RF-84), pagos con su referencia (RF-32), tasa (RF-212) y devuelta.
/// El NCF y el código QR del e-CF se agregan en la Fase C7.
/// </summary>
internal static class GeneradorTicket
{
    public const int Ancho = 42;

    private enum Estilo
    {
        Normal,
        Negrita,
        Centrado,
        Titulo,
    }

    static GeneradorTicket() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static DocumentoImpresion Generar(EncabezadoTicket encabezado, DatosVenta venta, bool esCopia)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));
        void Importe(string etiqueta, decimal monto, Estilo estilo = Estilo.Normal) => Agregar(Columnas(etiqueta, monto.ToString("N2", cultura)), estilo);

        Agregar(encabezado.EmpresaNombre.ToUpper(cultura), Estilo.Titulo);
        Agregar($"RNC {encabezado.EmpresaRnc}", Estilo.Centrado);
        if (encabezado.EmpresaDireccion is { } direccionEmpresa)
            foreach (var parte in Envolver(direccionEmpresa))
                Agregar(parte, Estilo.Centrado);
        if (encabezado.EmpresaTelefono is { } telefono)
            Agregar($"Tel. {telefono}", Estilo.Centrado);
        Agregar($"{encabezado.SucursalNombre} · Caja {encabezado.CajaCodigo}", Estilo.Centrado);
        if (esCopia)
            Agregar("*** COPIA ***", Estilo.Negrita);
        Separador();

        Agregar($"{ReglasComprobante.Nombre(venta.TipoComprobante).ToUpper(cultura)} (E{(int)venta.TipoComprobante})", Estilo.Negrita);
        Agregar("NCF: pendiente de emisión e-CF");
        Agregar($"Transacción: {venta.NumeroTransaccion}");
        Agregar($"Fecha: {(venta.CobradaEn ?? venta.IniciadaEn).ToOffset(TimeSpan.FromHours(-4)).ToString("dd/MM/yyyy h:mm tt", cultura)}");
        Agregar($"Cajero: {venta.UsuarioNombre}");
        if (venta.Cliente is { } cliente)
        {
            foreach (var parte in Envolver($"Cliente: {cliente.Nombre}"))
                Agregar(parte);
            if (cliente.Documento is { } documento)
                Agregar($"{(cliente.TipoDocumento?.ToString() ?? "Documento").ToUpper(cultura)}: {documento}");
        }
        Separador();

        foreach (var linea in venta.Lineas.Where(l => !l.EsReverso && !l.Anulada))
        {
            foreach (var parte in Envolver(linea.Descripcion))
                Agregar(parte);

            var cantidad = linea.Cantidad.ToString("N" + linea.DecimalesCantidad, cultura);
            Agregar(Columnas($"  {cantidad} {linea.UnidadMedidaCodigo} x {linea.PrecioUnitario.ToString("N2", cultura)}", linea.ImporteBruto.ToString("N2", cultura)));
            if (linea.DescuentoPromocion > 0)
                Agregar(Columnas($"  Oferta {linea.PromocionDescripcion}", $"-{linea.DescuentoPromocion.ToString("N2", cultura)}"));
            if (linea.DescuentoManual > 0)
                Agregar(Columnas("  Descuento", $"-{linea.DescuentoManual.ToString("N2", cultura)}"));
            if (linea.DescuentoFactura > 0)
                Agregar(Columnas("  Desc. factura", $"-{linea.DescuentoFactura.ToString("N2", cultura)}"));
            if (linea.Serial is { } serial)
                Agregar($"  Serial: {serial}");
        }
        Separador();

        Importe("SUBTOTAL (sin ITBIS)", venta.Totales.Subtotal);
        foreach (var tasa in venta.Totales.Desglose.Where(d => d.Impuesto > 0))
            Importe($"ITBIS {tasa.Porcentaje.ToString("0.##", cultura)}%", tasa.Impuesto);
        if (venta.Totales.Descuento > 0)
            Importe("DESCUENTOS", -venta.Totales.Descuento);
        Importe("TOTAL RD$", venta.Totales.Total, Estilo.Titulo);
        if (venta.RedondeoEfectivo != 0)
            Importe("Redondeo efectivo", venta.RedondeoEfectivo);
        if (venta.TotalCobrado is { } cobrado && cobrado != venta.Totales.Total)
            Importe("TOTAL COBRADO", cobrado, Estilo.Negrita);

        if (venta.Pagos is { Count: > 0 } pagos)
        {
            Separador();
            foreach (var pago in pagos)
            {
                var detalle = pago.Moneda == Venta.MonedaLocal
                    ? pago.FormaPagoNombre
                    : $"{pago.FormaPagoNombre} {pago.MontoRecibido.ToString("N2", cultura)} {pago.Moneda} @ {pago.TasaCambio?.ToString("N2", cultura)}";
                Importe(detalle, pago.MontoAplicado);

                var referencia = string.Join(" ", new[]
                {
                    pago.TipoTarjetaNombre,
                    pago.UltimosDigitos is { } digitos ? $"****{digitos}" : null,
                    pago.BancoNombre,
                    pago.Referencia is { } numero ? $"Ref. {numero}" : null,
                    pago.AprobacionManual ? "(manual)" : null,
                }.Where(texto => !string.IsNullOrWhiteSpace(texto)));
                if (referencia.Length > 0)
                    Agregar($"  {referencia}");
            }

            Importe("DEVUELTA", venta.Devuelta, Estilo.Titulo);
        }

        Separador();
        Agregar($"Artículos: {venta.Totales.CantidadArticulos.ToString("0.###", cultura)}");
        Agregar("¡Gracias por su compra!", Estilo.Centrado);

        var texto = string.Join('\n', lineas.Select(l => l.Estilo is Estilo.Centrado or Estilo.Titulo ? Centrar(l.Texto) : l.Texto));
        return new DocumentoImpresion($"ticket-{venta.NumeroTransaccion}{(esCopia ? "-copia" : null)}", texto, EscPos(lineas));
    }

    private static byte[] EscPos(IEnumerable<(string Texto, Estilo Estilo)> lineas)
    {
        var codificacion = Encoding.GetEncoding(858);
        using var bytes = new MemoryStream();
        void Escribir(params byte[] datos) => bytes.Write(datos);

        Escribir(0x1B, 0x40);       // ESC @: iniciar
        Escribir(0x1B, 0x74, 19);   // ESC t 19: página de códigos PC858

        foreach (var (texto, estilo) in lineas)
        {
            Escribir(0x1B, 0x61, (byte)(estilo is Estilo.Centrado or Estilo.Titulo ? 1 : 0)); // alineación
            Escribir(0x1B, 0x45, (byte)(estilo is Estilo.Negrita or Estilo.Titulo ? 1 : 0)); // negrita
            Escribir(0x1D, 0x21, (byte)(estilo == Estilo.Titulo ? 0x01 : 0x00));             // doble alto en títulos
            bytes.Write(codificacion.GetBytes(texto));
            Escribir(0x0A);
        }

        Escribir(0x1D, 0x21, 0x00);
        Escribir(0x1B, 0x64, 4);          // avanzar 4 líneas
        Escribir(0x1D, 0x56, 0x42, 0x00); // corte parcial
        return bytes.ToArray();
    }

    private static string Columnas(string izquierda, string derecha)
    {
        var espacio = Ancho - derecha.Length - 1;
        var recortada = izquierda.Length > espacio ? izquierda[..espacio] : izquierda;
        return recortada.PadRight(espacio) + " " + derecha;
    }

    private static string Centrar(string texto) =>
        texto.Length >= Ancho ? texto : new string(' ', (Ancho - texto.Length) / 2) + texto;

    private static IEnumerable<string> Envolver(string texto)
    {
        var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actual = new StringBuilder();
        foreach (var palabra in palabras)
        {
            if (actual.Length > 0 && actual.Length + 1 + palabra.Length > Ancho)
            {
                yield return actual.ToString();
                actual.Clear();
            }

            if (actual.Length > 0)
                actual.Append(' ');
            actual.Append(palabra.Length > Ancho ? palabra[..Ancho] : palabra);
        }

        if (actual.Length > 0)
            yield return actual.ToString();
    }
}
