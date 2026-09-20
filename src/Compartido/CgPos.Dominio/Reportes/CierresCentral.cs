using CgPos.Dominio.Comun;
using CgPos.Dominio.Pagos;

namespace CgPos.Dominio.Reportes;

/// <summary>
/// Cierre de turno informado por una caja (RF-267), para el reporte de cuadres del Central: lo esperado, lo declarado y la diferencia,
/// con el detalle por forma de pago. Se identifica por la caja y el número del turno, así que un reenvío del mismo cierre actualiza la fila en vez de duplicarla.
/// </summary>
public sealed class CierreTurnoCentral : Entidad
{
    public const int LargoMaximoTexto = 200;
    public const int LargoMaximoMoneda = 3;
    public const int LargoMaximoMotivo = 500;

    private readonly List<CierreFormaPagoCentral> _formasPago = [];

    private CierreTurnoCentral()
    {
    }

    public long TurnoNumero { get; private set; }
    public int Numero { get; private set; }
    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public DateOnly FechaOperacion { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;
    public string Moneda { get; private set; } = string.Empty;
    public bool Ciego { get; private set; }
    public decimal FondoInicial { get; private set; }
    public int CantidadVentas { get; private set; }
    public decimal TotalVentas { get; private set; }
    public decimal TotalRetiros { get; private set; }
    public decimal TotalEsperado { get; private set; }
    public decimal TotalDeclarado { get; private set; }
    public decimal Diferencia { get; private set; }
    public DateTimeOffset AbiertoEn { get; private set; }
    public DateTimeOffset CerradoEn { get; private set; }
    public DateTimeOffset RegistradoEn { get; private set; }

    public IReadOnlyList<CierreFormaPagoCentral> FormasPago => _formasPago;

    /// <summary>Faltó o sobró dinero respecto de lo esperado.</summary>
    public bool ConDiferencia => Diferencia != 0m;

    public static CierreTurnoCentral Registrar(long turnoNumero, int numero, int sucursalId, int cajaId, DateOnly fechaOperacion,
        string? usuarioNombre, string moneda, bool ciego, decimal fondoInicial, int cantidadVentas, decimal totalVentas, decimal totalRetiros,
        decimal totalEsperado, decimal totalDeclarado, decimal diferencia, DateTimeOffset abiertoEn, DateTimeOffset cerradoEn, DateTimeOffset ahora)
    {
        var cierre = new CierreTurnoCentral
        {
            TurnoNumero = turnoNumero,
            Numero = numero,
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            FechaOperacion = fechaOperacion,
            Moneda = Validar.Texto(moneda, "Moneda", LargoMaximoMoneda).ToUpperInvariant(),
            AbiertoEn = abiertoEn,
            CerradoEn = cerradoEn,
        };

        cierre.Actualizar(usuarioNombre, ciego, fondoInicial, cantidadVentas, totalVentas, totalRetiros, totalEsperado, totalDeclarado, diferencia, ahora);
        return cierre;
    }

    public void Actualizar(string? usuarioNombre, bool ciego, decimal fondoInicial, int cantidadVentas, decimal totalVentas, decimal totalRetiros,
        decimal totalEsperado, decimal totalDeclarado, decimal diferencia, DateTimeOffset ahora)
    {
        UsuarioNombre = Validar.TextoOpcional(usuarioNombre, "Usuario", LargoMaximoTexto) ?? string.Empty;
        Ciego = ciego;
        FondoInicial = fondoInicial;
        CantidadVentas = cantidadVentas;
        TotalVentas = totalVentas;
        TotalRetiros = totalRetiros;
        TotalEsperado = totalEsperado;
        TotalDeclarado = totalDeclarado;
        Diferencia = diferencia;
        RegistradoEn = ahora;
    }

    public void ReemplazarFormasPago(IEnumerable<(TipoFormaPago Tipo, string Nombre, string Moneda, int Transacciones, decimal Esperado, decimal Declarado, decimal Diferencia)> formas)
    {
        ArgumentNullException.ThrowIfNull(formas);
        _formasPago.Clear();
        foreach (var forma in formas)
        {
            _formasPago.Add(new CierreFormaPagoCentral
            {
                CierreId = Id,
                Tipo = forma.Tipo,
                Nombre = Validar.TextoOpcional(forma.Nombre, "Forma de pago", LargoMaximoTexto) ?? string.Empty,
                Moneda = Validar.TextoOpcional(forma.Moneda, "Moneda", LargoMaximoMoneda)?.ToUpperInvariant() ?? Moneda,
                Transacciones = forma.Transacciones,
                Esperado = forma.Esperado,
                Declarado = forma.Declarado,
                Diferencia = forma.Diferencia,
            });
        }
    }
}

public sealed class CierreFormaPagoCentral : Entidad
{
    public int CierreId { get; internal set; }
    public TipoFormaPago Tipo { get; internal set; }
    public string Nombre { get; internal set; } = string.Empty;
    public string Moneda { get; internal set; } = string.Empty;
    public int Transacciones { get; internal set; }
    public decimal Esperado { get; internal set; }
    public decimal Declarado { get; internal set; }
    public decimal Diferencia { get; internal set; }
}
