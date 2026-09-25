using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Pagos;

public enum TipoFormaPago
{
    Efectivo,
    Tarjeta,
    Transferencia,
    Cheque,
    BonoRegalo,
    NotaCredito,
    PrestamoBancario,
    TarjetaRegalo,
    Puntos,
    MonedaExtranjera,
}

/// <summary>
/// Moneda del maestro del Central. La moneda local de la caja se elige con el parámetro General.MonedaLocal;
/// las demás se cobran a la tasa del día.
/// </summary>
public sealed class Moneda : Entidad
{
    public const int LargoCodigo = 3;
    public const int LargoMaximoNombre = 50;
    public const int LargoMaximoSimbolo = 5;

    private Moneda()
    {
    }

    /// <summary>Código ISO 4217, ej. "DOP", "USD".</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Símbolo con que se muestran los montos en pantallas y tickets, ej. "RD$".</summary>
    public string Simbolo { get; private set; } = string.Empty;

    public bool Activa { get; private set; } = true;

    public static Moneda Crear(string codigo, string nombre, string simbolo)
    {
        var moneda = new Moneda
        {
            Codigo = FormaPago.ValidarMoneda(codigo),
        };
        moneda.Actualizar(nombre, simbolo);
        return moneda;
    }

    public void Actualizar(string nombre, string simbolo)
    {
        Nombre = Validar.Texto(nombre, "Nombre de la moneda", LargoMaximoNombre);
        Simbolo = Validar.Texto(simbolo, "Símbolo de la moneda", LargoMaximoSimbolo);
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}

/// <summary>Forma de pago configurable (RF-184). El orden define su posición en la pantalla de cobro (RF-150).</summary>
/// <summary>
/// Con qué equipo cobra una forma de pago de tarjeta. Es del maestro y no de la caja, porque es lo que distingue
/// «Tarjeta» —que se cobra en un equipo aparte y solo se registra su número de aprobación— de «CardNet» y «Azul», que
/// cobran en su propio panel de firma y devuelven la aprobación y el BIN sin que nadie digite nada.
/// </summary>
public enum TerminalFormaPago
{
    /// <summary>No habla con ningún equipo: el cajero digita el número de aprobación del volante.</summary>
    Ninguno,
    CardNet,
    Azul,
}

public sealed class FormaPago : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 50;

    private FormaPago()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public TipoFormaPago Tipo { get; private set; }

    /// <summary>Código ISO 4217 de la moneda, ej. "DOP", "USD".</summary>
    public string Moneda { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    /// <summary>La gaveta solo abre con medios físicos como efectivo, cheque o transferencia (RF-112).</summary>
    public bool AbreGaveta { get; private set; }

    /// <summary>La devuelta solo aplica a medios en efectivo (RF-30).</summary>
    public bool PermiteDevuelta { get; private set; }

    public bool RequiereReferencia { get; private set; }
    public bool RequiereBanco { get; private set; }

    /// <summary>Bonos y tarjetas de regalo no se consumen en facturas con comprobante fiscal (RF-117, RF-119).</summary>
    public bool PermiteComprobanteFiscal { get; private set; } = true;

    public bool Activa { get; private set; } = true;

    /// <summary>
    /// Equipo con el que cobra, si es de tarjeta. Con un terminal, el cobro se hace en su panel y no se digita nada; sin
    /// él, el cajero registra el número de aprobación del volante. Una caja solo tiene un terminal, así que las formas de
    /// pago del otro procesador no se le ofrecen.
    /// </summary>
    public TerminalFormaPago Terminal { get; private set; }

    /// <summary>Cobra en su propio panel de firma: la caja le manda el monto y él devuelve la aprobación y el BIN.</summary>
    public bool CobraPorTerminal => Terminal != TerminalFormaPago.Ninguno;

    public static FormaPago Crear(string codigo, string nombre, TipoFormaPago tipo, int orden, string moneda)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de forma de pago no válido.");

        var forma = new FormaPago
        {
            Codigo = Validar.Texto(codigo, "Código de forma de pago", LargoMaximoCodigo).ToUpperInvariant(),
            Tipo = tipo,
            Moneda = ValidarMoneda(moneda),
        };

        var (abreGaveta, permiteDevuelta, requiereReferencia, requiereBanco, permiteComprobanteFiscal) = ValoresPorTipo(tipo);
        forma.Configurar(nombre, orden, abreGaveta, permiteDevuelta, requiereReferencia, requiereBanco, permiteComprobanteFiscal);
        return forma;
    }

    public void Configurar(string nombre, int orden, bool abreGaveta, bool permiteDevuelta, bool requiereReferencia, bool requiereBanco,
        bool permiteComprobanteFiscal, TerminalFormaPago terminal = TerminalFormaPago.Ninguno)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(orden);
        if (!Enum.IsDefined(terminal))
            throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "Terminal de forma de pago no válido.");
        if (terminal != TerminalFormaPago.Ninguno && Tipo != TipoFormaPago.Tarjeta)
            throw new ArgumentException("Solo una forma de pago de tarjeta cobra por un terminal.", nameof(terminal));

        Nombre = Validar.Texto(nombre, "Nombre de forma de pago", LargoMaximoNombre);
        Orden = orden;
        AbreGaveta = abreGaveta;
        PermiteDevuelta = permiteDevuelta;
        RequiereReferencia = requiereReferencia;
        RequiereBanco = requiereBanco;
        PermiteComprobanteFiscal = permiteComprobanteFiscal;
        Terminal = terminal;
    }

    /// <summary>Valores sugeridos según el tipo; se pueden ajustar con <see cref="Configurar"/>.</summary>
    public static (bool AbreGaveta, bool PermiteDevuelta, bool RequiereReferencia, bool RequiereBanco, bool PermiteComprobanteFiscal) ValoresPorTipo(TipoFormaPago tipo) =>
        tipo switch
        {
            TipoFormaPago.Efectivo => (true, true, false, false, true),
            TipoFormaPago.MonedaExtranjera => (true, true, false, false, true),
            TipoFormaPago.Tarjeta => (false, false, true, false, true),
            TipoFormaPago.Transferencia => (true, false, true, true, true),
            TipoFormaPago.Cheque => (true, false, true, true, true),
            TipoFormaPago.PrestamoBancario => (false, false, true, true, true),
            TipoFormaPago.NotaCredito => (false, false, true, false, true),
            TipoFormaPago.BonoRegalo => (false, false, true, false, false),
            TipoFormaPago.TarjetaRegalo => (false, false, true, false, false),
            TipoFormaPago.Puntos => (false, false, false, false, true),
            _ => (false, false, false, false, true),
        };

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    internal static string ValidarMoneda(string moneda)
    {
        var codigo = Validar.Texto(moneda, "Moneda", 3).ToUpperInvariant();
        return codigo.Length == 3 && codigo.All(char.IsAsciiLetterUpper)
            ? codigo
            : throw new ArgumentException("La moneda debe ser un código ISO de 3 letras (ej. DOP, USD).", nameof(moneda));
    }
}

public sealed class Banco : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 100;
    public const int LargoMaximoRutaLogo = 260;

    private Banco()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? RutaLogo { get; private set; }
    public bool Activo { get; private set; } = true;

    public static Banco Crear(string codigo, string nombre, string? rutaLogo = null)
    {
        var banco = new Banco
        {
            Codigo = Validar.Texto(codigo, "Código de banco", LargoMaximoCodigo).ToUpperInvariant(),
        };
        banco.Actualizar(nombre, rutaLogo);
        return banco;
    }

    public void Actualizar(string nombre, string? rutaLogo)
    {
        Nombre = Validar.Texto(nombre, "Nombre de banco", LargoMaximoNombre);
        RutaLogo = Validar.TextoOpcional(rutaLogo, "Ruta del logo", LargoMaximoRutaLogo);
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public enum TipoDenominacion
{
    Billete,
    Moneda,
}

/// <summary>Denominación de billete o moneda por moneda, para cuadres por denominación (RF-184, RN-23).</summary>
public sealed class Denominacion : Entidad
{
    private Denominacion()
    {
    }

    public string Moneda { get; private set; } = string.Empty;
    public decimal Valor { get; private set; }
    public TipoDenominacion Tipo { get; private set; }
    public bool Activa { get; private set; } = true;

    public static Denominacion Crear(string moneda, decimal valor, TipoDenominacion tipo)
    {
        if (valor <= 0)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "El valor de la denominación debe ser mayor que cero.");
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de denominación no válido.");

        return new Denominacion
        {
            Moneda = FormaPago.ValidarMoneda(moneda),
            Valor = valor,
            Tipo = tipo,
        };
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;
}
