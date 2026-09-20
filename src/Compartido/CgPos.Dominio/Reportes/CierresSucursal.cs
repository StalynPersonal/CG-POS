using CgPos.Dominio.Comun;
using CgPos.Dominio.Pagos;

namespace CgPos.Dominio.Reportes;

/// <summary>
/// Cierre consolidado de una sucursal para un día de operación (RF-264): agrupa los cierres de turno de todas sus cajas, fija el efectivo
/// que se debe depositar por moneda y registra los depósitos con su diferencia. Se hace una vez por sucursal y día y no se modifica:
/// un cierre de caja que llegue después queda a la vista como cambio posterior.
/// </summary>
public sealed class CierreSucursal : Entidad
{
    public const int LargoMaximoTexto = 200;
    public const int LargoMaximoObservacion = 500;

    private readonly List<CierreSucursalFormaPago> _formasPago = [];
    private readonly List<DepositoCierreSucursal> _depositos = [];

    private CierreSucursal()
    {
    }

    public int SucursalId { get; private set; }

    /// <summary>
    /// Número del cierre con la numeración del Central. Nulo si falta su secuencia: el cierre se guarda igual, porque cuadrar
    /// el día no puede depender de una configuración.
    /// </summary>
    public string? NumeroCentral { get; private set; }

    public DateOnly FechaOperacion { get; private set; }
    public int CantidadCierres { get; private set; }
    public int CantidadVentas { get; private set; }
    public decimal TotalVentas { get; private set; }
    public decimal TotalEsperado { get; private set; }
    public decimal TotalDeclarado { get; private set; }

    /// <summary>Faltante (negativo) o sobrante de los cierres de caja: declarado menos esperado.</summary>
    public decimal Diferencia { get; private set; }

    public string? Observacion { get; private set; }

    /// <summary>Le pone el número del Central; solo se hace una vez, al cerrar.</summary>
    public void AsignarNumeroCentral(string numero)
    {
        if (NumeroCentral is not null)
            return;

        NumeroCentral = Validar.Texto(numero, "Número del Central", LargoMaximoTexto);
    }
    public string CerradoPor { get; private set; } = string.Empty;
    public DateTimeOffset CerradoEn { get; private set; }

    public IReadOnlyList<CierreSucursalFormaPago> FormasPago => _formasPago;
    public IReadOnlyList<DepositoCierreSucursal> Depositos => _depositos;

    /// <summary>Efectivo que se debe depositar por moneda: lo declarado en efectivo en los cierres de caja.</summary>
    public IReadOnlyDictionary<string, decimal> EfectivoADepositar => ADepositar(_formasPago.Select(f => (f.Tipo, f.Moneda, f.Declarado)));

    /// <summary>Depositado menos lo que había que depositar, por moneda.</summary>
    public IReadOnlyDictionary<string, decimal> DiferenciaDeposito => DiferenciaPorMoneda(EfectivoADepositar, _depositos.Select(d => (d.Moneda, d.Monto)));

    /// <param name="cierres">Cierres de turno de las cajas de la sucursal en ese día; se exige al menos uno.</param>
    /// <exception cref="ArgumentException">Sin cierres, con cierres de otra sucursal o día, o con un depósito inválido.</exception>
    public static CierreSucursal Consolidar(int sucursalId, DateOnly fechaOperacion, IReadOnlyCollection<CierreTurnoCentral> cierres,
        IEnumerable<DepositoSolicitado> depositos, string? observacion, string cerradoPor, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cierres);
        ArgumentNullException.ThrowIfNull(depositos);
        if (cierres.Count == 0)
            throw new ArgumentException("No hay cierres de caja de la sucursal en ese día para consolidar.", nameof(cierres));
        if (cierres.Any(c => c.SucursalId != sucursalId || c.FechaOperacion != fechaOperacion))
            throw new ArgumentException("Todos los cierres deben ser de la sucursal y el día que se consolidan.", nameof(cierres));

        var cierre = new CierreSucursal
        {
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            FechaOperacion = fechaOperacion,
            CantidadCierres = cierres.Count,
            CantidadVentas = cierres.Sum(c => c.CantidadVentas),
            TotalVentas = cierres.Sum(c => c.TotalVentas),
            TotalEsperado = cierres.Sum(c => c.TotalEsperado),
            TotalDeclarado = cierres.Sum(c => c.TotalDeclarado),
            Diferencia = cierres.Sum(c => c.Diferencia),
            Observacion = Validar.TextoOpcional(observacion, "Observación", LargoMaximoObservacion),
            CerradoPor = Validar.Texto(cerradoPor, "Usuario", LargoMaximoTexto),
            CerradoEn = ahora,
        };

        foreach (var forma in AgruparFormasPago(cierres))
            cierre._formasPago.Add(new CierreSucursalFormaPago
            {
                Tipo = forma.Tipo,
                Nombre = forma.Nombre,
                Moneda = forma.Moneda,
                Transacciones = forma.Transacciones,
                Esperado = forma.Esperado,
                Declarado = forma.Declarado,
                Diferencia = forma.Diferencia,
            });

        var monedasEfectivo = cierre.EfectivoADepositar.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var deposito in depositos)
        {
            var moneda = Validar.Texto(deposito.Moneda, "Moneda del depósito", CierreTurnoCentral.LargoMaximoMoneda).ToUpperInvariant();
            if (!monedasEfectivo.Contains(moneda))
                throw new ArgumentException($"No hay efectivo en {moneda} para depositar en los cierres de la sucursal.", nameof(depositos));
            if (deposito.Monto <= 0)
                throw new ArgumentException("El monto de cada depósito debe ser mayor que cero.", nameof(depositos));

            cierre._depositos.Add(new DepositoCierreSucursal
            {
                Moneda = moneda,
                BancoCodigo = Validar.Texto(deposito.BancoCodigo, "Banco del depósito", Banco.LargoMaximoCodigo).ToUpperInvariant(),
                BancoNombre = Validar.Texto(deposito.BancoNombre, "Banco del depósito", Banco.LargoMaximoNombre),
                NumeroBoleta = Validar.Texto(deposito.NumeroBoleta, "Número de la boleta de depósito", DepositoCierreSucursal.LargoMaximoBoleta),
                Monto = decimal.Round(deposito.Monto, 2, MidpointRounding.AwayFromZero),
                FechaDeposito = deposito.FechaDeposito,
            });
        }

        if (cierre._depositos.GroupBy(d => (d.BancoCodigo, d.NumeroBoleta)).Any(g => g.Count() > 1))
            throw new ArgumentException("Una boleta de depósito está repetida.", nameof(depositos));

        return cierre;
    }

    /// <summary>Formas de pago de todos los cierres sumadas por tipo, nombre y moneda.</summary>
    public static IReadOnlyList<(TipoFormaPago Tipo, string Nombre, string Moneda, int Transacciones, decimal Esperado, decimal Declarado, decimal Diferencia)>
        AgruparFormasPago(IEnumerable<CierreTurnoCentral> cierres) =>
        cierres.SelectMany(c => c.FormasPago)
            .GroupBy(f => (f.Tipo, f.Nombre, Moneda: f.Moneda.ToUpperInvariant()))
            .Select(g => (g.Key.Tipo, g.Key.Nombre, g.Key.Moneda, g.Sum(f => f.Transacciones), g.Sum(f => f.Esperado), g.Sum(f => f.Declarado),
                g.Sum(f => f.Diferencia)))
            .OrderBy(f => f.Tipo)
            .ThenBy(f => f.Nombre, StringComparer.Ordinal)
            .ToList();

    /// <summary>El efectivo (moneda local o extranjera) es lo que se deposita; tarjetas, transferencias y demás los liquida el banco.</summary>
    public static IReadOnlyDictionary<string, decimal> ADepositar(IEnumerable<(TipoFormaPago Tipo, string Moneda, decimal Declarado)> formas) =>
        formas.Where(f => f.Tipo is TipoFormaPago.Efectivo or TipoFormaPago.MonedaExtranjera)
            .GroupBy(f => f.Moneda.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Declarado), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, decimal> DiferenciaPorMoneda(IReadOnlyDictionary<string, decimal> aDepositar,
        IEnumerable<(string Moneda, decimal Monto)> depositos)
    {
        var depositado = depositos.GroupBy(d => d.Moneda.ToUpperInvariant()).ToDictionary(g => g.Key, g => g.Sum(d => d.Monto), StringComparer.OrdinalIgnoreCase);
        return aDepositar.Keys.Concat(depositado.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(m => m, m => depositado.GetValueOrDefault(m) - aDepositar.GetValueOrDefault(m), StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class CierreSucursalFormaPago : Entidad
{
    public int CierreSucursalId { get; internal set; }
    public TipoFormaPago Tipo { get; internal set; }
    public string Nombre { get; internal set; } = string.Empty;
    public string Moneda { get; internal set; } = string.Empty;
    public int Transacciones { get; internal set; }
    public decimal Esperado { get; internal set; }
    public decimal Declarado { get; internal set; }
    public decimal Diferencia { get; internal set; }
}

/// <summary>Depósito bancario del efectivo de la sucursal, con su boleta.</summary>
public sealed class DepositoCierreSucursal : Entidad
{
    public const int LargoMaximoBoleta = 50;

    public int CierreSucursalId { get; internal set; }
    public string Moneda { get; internal set; } = string.Empty;
    public string BancoCodigo { get; internal set; } = string.Empty;
    public string BancoNombre { get; internal set; } = string.Empty;
    public string NumeroBoleta { get; internal set; } = string.Empty;
    public decimal Monto { get; internal set; }
    public DateOnly FechaDeposito { get; internal set; }
}

public sealed record DepositoSolicitado(string Moneda, string BancoCodigo, string BancoNombre, string NumeroBoleta, decimal Monto, DateOnly FechaDeposito);
