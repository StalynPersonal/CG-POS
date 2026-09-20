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
    private readonly List<AjusteCierreTurno> _ajustes = [];

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

    /// <summary>Correcciones hechas desde el Central, en orden. Lo que declaró la caja se puede reconstruir con ellas.</summary>
    public IReadOnlyList<AjusteCierreTurno> Ajustes => _ajustes;

    /// <summary>Alguien corrigió este cierre desde el Central.</summary>
    public bool Ajustado => _ajustes.Count > 0;

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

    /// <summary>
    /// Vuelve a aplicar lo que informó la caja. Un cierre ya corregido desde el Central no se pisa: el reenvío del mismo
    /// mensaje traería otra vez las cifras viejas y borraría la corrección.
    /// </summary>
    public void Actualizar(string? usuarioNombre, bool ciego, decimal fondoInicial, int cantidadVentas, decimal totalVentas, decimal totalRetiros,
        decimal totalEsperado, decimal totalDeclarado, decimal diferencia, DateTimeOffset ahora)
    {
        if (Ajustado)
            return;

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
        if (Ajustado)
            return;

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

    /// <summary>
    /// Corrige lo declarado en una forma de pago de este cierre: es lo que se hace cuando el cuadre salió mal y la caja ya
    /// no puede volver atrás. No borra nada; queda el ajuste con lo anterior, lo nuevo, el motivo y quién lo hizo, y el
    /// cierre recalcula su declarado y su diferencia.
    /// </summary>
    /// <param name="formaPagoId">Forma de pago de este cierre que se corrige.</param>
    /// <param name="declarado">Lo que de verdad había.</param>
    /// <exception cref="ArgumentException">La forma no es de este cierre, el monto es negativo o falta el motivo.</exception>
    public AjusteCierreTurno Ajustar(int formaPagoId, decimal declarado, string motivo, string usuarioNombre, DateTimeOffset ahora)
    {
        var forma = _formasPago.SingleOrDefault(f => f.Id == formaPagoId)
            ?? throw new ArgumentException("La forma de pago no es de este cierre.", nameof(formaPagoId));
        if (declarado < 0)
            throw new ArgumentException("Lo declarado no puede ser negativo.", nameof(declarado));
        if (declarado == forma.Declarado)
            throw new ArgumentException($"Lo declarado en {forma.Nombre} ya es {declarado:N2}: no hay nada que corregir.", nameof(declarado));

        var ajuste = AjusteCierreTurno.Registrar(Id, forma.Id, forma.Nombre, forma.Moneda, forma.Declarado, declarado,
            Validar.Texto(motivo, "Motivo", LargoMaximoMotivo), Validar.Texto(usuarioNombre, "Usuario", LargoMaximoTexto), ahora);
        _ajustes.Add(ajuste);

        forma.Declarado = declarado;
        forma.Diferencia = declarado - forma.Esperado;

        // El total del cierre es el de su moneda, igual que lo calcula la caja: lo de otras monedas se cuadra aparte.
        var locales = _formasPago.Where(f => f.Moneda == Moneda).ToList();
        TotalDeclarado = locales.Sum(f => f.Declarado);
        Diferencia = TotalDeclarado - TotalEsperado;
        return ajuste;
    }
}

/// <summary>
/// Corrección de un cierre hecha desde el Central. Es el único camino para arreglar un cuadre mal hecho: la caja cierra y
/// no puede deshacerlo, así que lo que quedó mal se corrige aquí, con motivo y responsable, y sin borrar lo que informó.
/// </summary>
public sealed class AjusteCierreTurno : Entidad
{
    private AjusteCierreTurno()
    {
    }

    public int CierreId { get; private set; }
    public int FormaPagoId { get; private set; }
    public string FormaPagoNombre { get; private set; } = string.Empty;
    public string Moneda { get; private set; } = string.Empty;

    /// <summary>Lo que la caja había declarado antes de esta corrección.</summary>
    public decimal DeclaradoAnterior { get; private set; }

    public decimal DeclaradoNuevo { get; private set; }
    public string Motivo { get; private set; } = string.Empty;
    public string AjustadoPorNombre { get; private set; } = string.Empty;
    public DateTimeOffset AjustadoEn { get; private set; }

    /// <summary>Cuánto se movió el declarado: positivo si apareció dinero, negativo si faltaba.</summary>
    public decimal Movimiento => DeclaradoNuevo - DeclaradoAnterior;

    internal static AjusteCierreTurno Registrar(int cierreId, int formaPagoId, string formaPagoNombre, string moneda, decimal anterior, decimal nuevo,
        string motivo, string usuarioNombre, DateTimeOffset ahora) =>
        new()
        {
            CierreId = cierreId,
            FormaPagoId = formaPagoId,
            FormaPagoNombre = formaPagoNombre,
            Moneda = moneda,
            DeclaradoAnterior = anterior,
            DeclaradoNuevo = nuevo,
            Motivo = motivo,
            AjustadoPorNombre = usuarioNombre,
            AjustadoEn = ahora,
        };
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
