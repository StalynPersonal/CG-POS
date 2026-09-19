namespace CgPos.Dominio.Comun;

/// <summary>
/// Hora con la que trabaja el sistema: la de República Dominicana (UTC-4), con su desfase guardado junto al valor.
///
/// Las fechas se guardan como <c>datetimeoffset</c>, así que llevan la hora local y el desfase (<c>11:41:21 -04:00</c>).
/// Quien mire la base ve la hora real del negocio, y el sistema sigue pudiendo ordenar y comparar operaciones de
/// cualquier caja sin ambigüedad, porque el desfase viaja con el dato.
///
/// El desfase es fijo porque el país no tiene horario de verano desde el año 2000. Si algún día cambia, se cambia aquí.
/// </summary>
public static class RelojNegocio
{
    /// <summary>Desfase de República Dominicana respecto a UTC.</summary>
    public static readonly TimeSpan Desfase = TimeSpan.FromHours(-4);

    /// <summary>Hora actual del negocio. Se usa en lugar de <c>GetUtcNow</c> en todo lo que se guarda o se muestra.</summary>
    public static DateTimeOffset Ahora(this TimeProvider reloj) => reloj.GetUtcNow().Local();

    /// <summary>El mismo instante, expresado en la hora del negocio.</summary>
    public static DateTimeOffset Local(this DateTimeOffset momento) => momento.ToOffset(Desfase);

    /// <summary>Día de operación de un momento: el día que era aquí, no en UTC.</summary>
    public static DateOnly Dia(this DateTimeOffset momento) => DateOnly.FromDateTime(momento.Local().DateTime);
}
