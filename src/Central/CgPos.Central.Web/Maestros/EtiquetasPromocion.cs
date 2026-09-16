using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Promociones;

namespace CgPos.Central.Web.Maestros;

public enum EstadoPromocion
{
    Vigente,
    Programada,
    Vencida,
    Inactiva,
}

public static class EtiquetasPromocion
{
    public static IReadOnlyList<TipoPromocion> Tipos { get; } = Enum.GetValues<TipoPromocion>();

    public static IReadOnlyList<(DiasSemana Dia, string Nombre)> Dias { get; } =
    [
        (DiasSemana.Lunes, "Lun"), (DiasSemana.Martes, "Mar"), (DiasSemana.Miercoles, "Mié"), (DiasSemana.Jueves, "Jue"),
        (DiasSemana.Viernes, "Vie"), (DiasSemana.Sabado, "Sáb"), (DiasSemana.Domingo, "Dom"),
    ];

    public static string Tipo(TipoPromocion tipo) => tipo switch
    {
        TipoPromocion.Porcentaje => "Porcentaje de descuento",
        TipoPromocion.MontoPorUnidad => "Monto de descuento por unidad",
        TipoPromocion.PrecioEspecial => "Precio especial",
        TipoPromocion.LlevaPaga => "Lleva X, paga Y",
        _ => "Precio desde una cantidad",
    };

    /// <summary>Etiqueta del valor según el tipo; nula si el tipo no usa valor (lleva X paga Y).</summary>
    public static string? EtiquetaValor(TipoPromocion tipo) => tipo switch
    {
        TipoPromocion.Porcentaje => "% de descuento",
        TipoPromocion.MontoPorUnidad => "Descuento por unidad (con ITBIS)",
        TipoPromocion.PrecioEspecial => "Precio especial por unidad (con ITBIS)",
        TipoPromocion.PrecioPorCantidad => "Precio por unidad desde la cantidad (con ITBIS)",
        _ => null,
    };

    public static string Horario(PromocionCarga promocion)
    {
        var dias = promocion.Dias == DiasSemana.Todos
            ? "Todos los días"
            : string.Join(" ", Dias.Where(d => promocion.Dias.HasFlag(d.Dia)).Select(d => d.Nombre));
        return promocion is { HoraDesde: { } desde, HoraHasta: { } hasta } ? $"{dias} · {desde:HH\\:mm}–{hasta:HH\\:mm}" : dias;
    }

    public static EstadoPromocion Estado(PromocionCarga promocion, DateTimeOffset ahora) =>
        !promocion.Activa ? EstadoPromocion.Inactiva
        : ahora < promocion.VigenteDesde ? EstadoPromocion.Programada
        : ahora > promocion.VigenteHasta ? EstadoPromocion.Vencida
        : EstadoPromocion.Vigente;

    /// <summary>Combina fecha y hora locales del navegador en un momento con su zona horaria.</summary>
    public static DateTimeOffset? Momento(DateTime? fecha, TimeSpan? hora)
    {
        if (fecha is not { } dia)
            return null;

        var local = dia.Date + (hora ?? TimeSpan.Zero);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
