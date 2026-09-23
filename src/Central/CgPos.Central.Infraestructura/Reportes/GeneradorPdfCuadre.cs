using System.Globalization;
using CgPos.Dominio.Globalizacion;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Reportes;
using CgPos.Dominio.Turnos;

namespace CgPos.Central.Infraestructura.Reportes;

/// <summary>
/// El cuadre de un turno en hoja carta, para volver a imprimirlo: lo esperado, lo que contó el supervisor, la diferencia,
/// el detalle del efectivo por denominaciones y lo que pasó en el turno. Es el papel que se archiva con el depósito.
/// </summary>
internal static class GeneradorPdfCuadre
{
    private static readonly HojaPdf Hoja = HojaPdf.CartaVertical;
    private static readonly CultureInfo Cultura = CulturaRd.Crear();

    public static byte[] Crear(CierreTurnoCentral cierre, string sucursal, string caja, EmpresaEnDocumento empresa)
    {
        ArgumentNullException.ThrowIfNull(cierre);
        ArgumentNullException.ThrowIfNull(empresa);

        var lineas = new List<(string Texto, bool Negrita)>
        {
            (empresa.Nombre, true),
            ($"RNC {empresa.Rnc}", false),
            (string.Empty, false),
            ($"CUADRE DE TURNO {cierre.TurnoNumero}", true),
            ($"Sucursal: {sucursal}    Caja: {caja}    Día: {cierre.FechaOperacion.ToString("dd/MM/yyyy", Cultura)}", false),
            ($"Cajera: {cierre.UsuarioNombre}", false),
            ($"Abierto: {Momento(cierre.AbiertoEn)}    Cerrado: {Momento(cierre.CerradoEn)}", false),
            (cierre.PendienteDeCuadre
                ? "Pendiente de cuadre: nadie ha contado el dinero todavía."
                : $"Cuadrado por: {cierre.CuadradoPor} el {Momento(cierre.CuadradoEn!.Value)}", false),
            (string.Empty, false),
            ($"Ventas del turno: {cierre.CantidadVentas} por {cierre.TotalVentas.ToString("N2", Cultura)}", false),
            ($"Fondo inicial: {cierre.FondoInicial.ToString("N2", Cultura)}    Retiros: {cierre.TotalRetiros.ToString("N2", Cultura)}", false),
            (string.Empty, false),
        };

        lineas.AddRange(FormasPago(cierre));
        lineas.Add((string.Empty, false));
        lineas.AddRange(Denominaciones(cierre));
        lineas.AddRange(Movimientos(cierre));
        lineas.AddRange(Correcciones(cierre));

        lineas.Add((string.Empty, false));
        lineas.Add((string.Empty, false));
        lineas.Add(("_______________________________        _______________________________", false));
        lineas.Add(("      Cajera                                   Supervisor", false));

        var paginas = lineas.Chunk(Hoja.Lineas).ToList();
        return EnsambladorPdf.Crear(paginas, Hoja);
    }

    /// <summary>Lo esperado contra lo declarado en cada forma de pago: es el cuadre propiamente dicho.</summary>
    private static IEnumerable<(string Texto, bool Negrita)> FormasPago(CierreTurnoCentral cierre)
    {
        const int Monto = 14;
        var nombre = Math.Max(20, Hoja.Columnas - (Monto * 3) - 8);

        string Fila(string texto, string esperado, string declarado, string diferencia) =>
            $"{Recortar(texto, nombre).PadRight(nombre)} {esperado.PadLeft(Monto)} {declarado.PadLeft(Monto)} {diferencia.PadLeft(Monto)}";

        yield return (Fila("Forma de pago", "Esperado", "Declarado", "Diferencia"), true);
        yield return (new string('-', Hoja.Columnas), false);

        foreach (var forma in cierre.FormasPago.OrderBy(f => f.Moneda, StringComparer.Ordinal).ThenBy(f => f.Nombre, StringComparer.Ordinal))
        {
            var etiqueta = forma.Moneda == cierre.Moneda ? forma.Nombre : $"{forma.Nombre} ({forma.Moneda})";
            yield return (Fila($"{etiqueta} · {forma.Transacciones}", forma.Esperado.ToString("N2", Cultura),
                cierre.PendienteDeCuadre ? string.Empty : forma.Declarado.ToString("N2", Cultura),
                cierre.PendienteDeCuadre ? string.Empty : forma.Diferencia.ToString("N2", Cultura)), false);
        }

        yield return (new string('-', Hoja.Columnas), false);
        yield return (Fila($"TOTAL {cierre.Moneda}", cierre.TotalEsperado.ToString("N2", Cultura),
            cierre.PendienteDeCuadre ? string.Empty : cierre.TotalDeclarado.ToString("N2", Cultura),
            cierre.PendienteDeCuadre ? string.Empty : cierre.Diferencia.ToString("N2", Cultura)), true);

        if (!cierre.PendienteDeCuadre)
            yield return ($"Resultado: {Resultado(cierre.Diferencia)}", true);
    }

    /// <summary>El efectivo contado, billete por billete: con esto se vuelve a armar el conteo si alguien lo discute.</summary>
    private static IEnumerable<(string Texto, bool Negrita)> Denominaciones(CierreTurnoCentral cierre)
    {
        if (cierre.Denominaciones.Count == 0)
            yield break;

        yield return ("Efectivo contado", true);
        foreach (var denominacion in cierre.Denominaciones.OrderBy(d => d.Moneda, StringComparer.Ordinal).ThenByDescending(d => d.Valor))
        {
            var tipo = denominacion.Tipo == TipoDenominacion.Billete ? "Billete" : "Moneda";
            yield return ($"  {tipo} {denominacion.Moneda} {denominacion.Valor.ToString("N2", Cultura).PadLeft(10)} "
                + $"x {denominacion.Cantidad.ToString(Cultura).PadLeft(4)} = {denominacion.Importe.ToString("N2", Cultura).PadLeft(12)}", false);
        }

        foreach (var moneda in cierre.Denominaciones.Select(d => d.Moneda).Distinct().OrderBy(m => m, StringComparer.Ordinal))
        {
            var total = cierre.Denominaciones.Where(d => d.Moneda == moneda).Sum(d => d.Importe);
            yield return ($"  Total contado {moneda}: {total.ToString("N2", Cultura)}", true);
        }

        yield return (string.Empty, false);
    }

    /// <summary>Retiros, reembolsos y relevos: explican por qué el efectivo esperado no es todo lo que se cobró.</summary>
    private static IEnumerable<(string Texto, bool Negrita)> Movimientos(CierreTurnoCentral cierre)
    {
        if (cierre.Movimientos.Count == 0)
            yield break;

        yield return ("Movimientos del turno", true);
        foreach (var movimiento in cierre.Movimientos.OrderBy(m => m.Fecha))
        {
            var detalle = movimiento.Tipo == TipoMovimientoCaja.Relevo
                ? $"entregó {movimiento.UsuarioAnteriorNombre} · recibió {movimiento.UsuarioNombre}"
                : $"{movimiento.Monto.ToString("N2", Cultura)} {movimiento.Moneda} · {movimiento.UsuarioNombre}";
            yield return ($"  {Momento(movimiento.Fecha)} · {Etiqueta(movimiento.Tipo)} {movimiento.Numero} · {detalle}", false);

            if (movimiento.Motivo is { Length: > 0 } motivo)
                yield return ($"      Motivo: {Recortar(motivo, Hoja.Columnas - 14)}", false);
            if (movimiento.AutorizadoPorNombre is { Length: > 0 } autorizo)
                yield return ($"      Autorizó: {autorizo}", false);
        }

        yield return (string.Empty, false);
    }

    /// <summary>Las correcciones hechas en el Central, para que el papel no diga una cosa y el sistema otra.</summary>
    private static IEnumerable<(string Texto, bool Negrita)> Correcciones(CierreTurnoCentral cierre)
    {
        if (cierre.Ajustes.Count == 0)
            yield break;

        yield return ("Correcciones", true);
        foreach (var ajuste in cierre.Ajustes.OrderBy(a => a.AjustadoEn))
        {
            yield return ($"  {Momento(ajuste.AjustadoEn)} · {ajuste.FormaPagoNombre}: "
                + $"{ajuste.DeclaradoAnterior.ToString("N2", Cultura)} → {ajuste.DeclaradoNuevo.ToString("N2", Cultura)} · {ajuste.AjustadoPorNombre}", false);
            yield return ($"      Motivo: {Recortar(ajuste.Motivo, Hoja.Columnas - 14)}", false);
        }

        yield return (string.Empty, false);
    }

    private static string Etiqueta(TipoMovimientoCaja tipo) => tipo switch
    {
        TipoMovimientoCaja.Retiro => "Retiro",
        TipoMovimientoCaja.Reembolso => "Reembolso",
        _ => "Relevo",
    };

    private static string Resultado(decimal diferencia) => diferencia switch
    {
        < 0m => $"FALTANTE de {Math.Abs(diferencia).ToString("N2", Cultura)}",
        > 0m => $"SOBRANTE de {diferencia.ToString("N2", Cultura)}",
        _ => "CUADRADO",
    };

    private static string Momento(DateTimeOffset momento) => momento.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura);

    private static string Recortar(string texto, int ancho) => texto.Length <= ancho ? texto : texto[..ancho];
}
