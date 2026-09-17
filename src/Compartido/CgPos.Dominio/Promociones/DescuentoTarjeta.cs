using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Promociones;

/// <summary>
/// Descuento que otorga un banco al pagar con ciertas tarjetas (RF-98). Se identifica por el BIN (los primeros dígitos de la tarjeta,
/// que entrega el terminal o digita el cajero) y se aplica como descuento de la factura antes de emitir el e-CF, para que el
/// comprobante fiscal salga con el monto realmente cobrado.
/// </summary>
public sealed class DescuentoTarjeta : Entidad
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoBines = 400;
    public const int LargoMinimoBin = 4;
    public const int LargoMaximoBin = 8;

    private DescuentoTarjeta()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>BIN de cada tarjeta que participa, separados por coma (ej. "401234,455678").</summary>
    public string Bines { get; private set; } = string.Empty;

    public TipoDescuentoTarjeta Tipo { get; private set; }

    /// <summary>Porcentaje o monto fijo, según el tipo.</summary>
    public decimal Valor { get; private set; }

    /// <summary>Compra mínima para que aplique; nulo si no hay mínimo.</summary>
    public decimal? MontoMinimo { get; private set; }

    /// <summary>Tope del descuento en moneda; nulo si no hay tope.</summary>
    public decimal? MontoMaximo { get; private set; }

    public int? BancoId { get; private set; }
    public DateTimeOffset VigenteDesde { get; private set; }
    public DateTimeOffset VigenteHasta { get; private set; }
    public DiasSemana Dias { get; private set; } = DiasSemana.Todos;
    public bool Activo { get; private set; } = true;

    public static DescuentoTarjeta Crear(string codigo, string nombre, string bines, TipoDescuentoTarjeta tipo, decimal valor, decimal? montoMinimo,
        decimal? montoMaximo, int? bancoId, DateTimeOffset vigenteDesde, DateTimeOffset vigenteHasta, DiasSemana dias, bool activo)
    {
        var descuento = new DescuentoTarjeta
        {
            Codigo = Validar.Texto(codigo, "Código del descuento", LargoMaximoCodigo).ToUpperInvariant(),
        };

        descuento.Actualizar(nombre, bines, tipo, valor, montoMinimo, montoMaximo, bancoId, vigenteDesde, vigenteHasta, dias, activo);
        return descuento;
    }

    public void Actualizar(string nombre, string bines, TipoDescuentoTarjeta tipo, decimal valor, decimal? montoMinimo, decimal? montoMaximo, int? bancoId,
        DateTimeOffset vigenteDesde, DateTimeOffset vigenteHasta, DiasSemana dias, bool activo)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de descuento de tarjeta no válido.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor);
        if (tipo == TipoDescuentoTarjeta.Porcentaje && valor > 100m)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "El porcentaje de descuento no puede pasar de 100.");
        if (vigenteHasta < vigenteDesde)
            throw new ArgumentException("La vigencia del descuento termina antes de empezar.", nameof(vigenteHasta));

        var lista = NormalizarBines(bines);
        if (lista.Length == 0)
            throw new ArgumentException($"Indique al menos un BIN de {LargoMinimoBin} a {LargoMaximoBin} dígitos.", nameof(bines));

        Nombre = Validar.Texto(nombre, "Nombre del descuento", LargoMaximoNombre);
        Bines = string.Join(',', lista) is { Length: > LargoMaximoBines } largo
            ? throw new ArgumentException($"La lista de BIN no puede exceder {LargoMaximoBines} caracteres ({largo.Length}).", nameof(bines))
            : string.Join(',', lista);
        Tipo = tipo;
        Valor = valor;
        MontoMinimo = montoMinimo;
        MontoMaximo = montoMaximo;
        BancoId = bancoId;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
        Dias = dias;
        Activo = activo;
    }

    /// <summary>Aplica a esa tarjeta, en esa fecha y por ese monto.</summary>
    public bool AplicaA(string? bin, decimal total, DateTimeOffset ahoraLocal)
    {
        var digitos = SoloDigitos(bin);
        if (digitos.Length < LargoMinimoBin || !Activo)
            return false;

        if (ahoraLocal < VigenteDesde || ahoraLocal > VigenteHasta || (Dias & (DiasSemana)(1 << (int)ahoraLocal.DayOfWeek)) == DiasSemana.Ninguno)
            return false;

        if (MontoMinimo is { } minimo && total < minimo)
            return false;

        // El BIN configurado puede ser más corto que el que entrega el terminal: basta con que la tarjeta empiece por él.
        return Bines.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(configurado => digitos.StartsWith(configurado, StringComparison.Ordinal));
    }

    /// <summary>Descuento en moneda para ese total, con el tope configurado.</summary>
    public decimal Calcular(decimal total)
    {
        var descuento = Tipo == TipoDescuentoTarjeta.Porcentaje ? total * Valor / 100m : Valor;
        if (MontoMaximo is { } tope && descuento > tope)
            descuento = tope;

        return decimal.Round(Math.Min(descuento, total), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Los primeros dígitos de la tarjeta, sin espacios ni guiones.</summary>
    public static string SoloDigitos(string? bin) => new((bin ?? string.Empty).Where(char.IsAsciiDigit).Take(LargoMaximoBin).ToArray());

    private static string[] NormalizarBines(string? bines) =>
        (bines ?? string.Empty)
        .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(SoloDigitos)
        .Where(bin => bin.Length >= LargoMinimoBin)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

public enum TipoDescuentoTarjeta
{
    Porcentaje,
    Monto,
}
