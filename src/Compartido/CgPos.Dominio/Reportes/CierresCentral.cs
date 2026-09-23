using CgPos.Dominio.Comun;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;

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
    private readonly List<CierreDenominacionCentral> _denominaciones = [];
    private readonly List<CierreMovimientoCentral> _movimientos = [];
    private readonly List<AjusteCierreTurno> _ajustes = [];

    private CierreTurnoCentral()
    {
    }

    public long TurnoNumero { get; private set; }
    public int Numero { get; private set; }
    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public DateOnly FechaOperacion { get; private set; }
    /// <summary>La cajera del turno: la diferencia es suya, aunque la declare el supervisor.</summary>
    public string UsuarioNombre { get; private set; } = string.Empty;

    public string Moneda { get; private set; } = string.Empty;
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

    /// <summary>El efectivo contado por el supervisor al cuadrar, denominación por denominación.</summary>
    public IReadOnlyList<CierreDenominacionCentral> Denominaciones => _denominaciones;

    /// <summary>Lo que pasó durante el turno y explica el efectivo: retiros, reembolsos y relevos, con su motivo y quién autorizó.</summary>
    public IReadOnlyList<CierreMovimientoCentral> Movimientos => _movimientos;

    /// <summary>Quién declaró el cuadre y cuándo; vacío mientras el cierre está pendiente.</summary>
    public string? CuadradoPor { get; private set; }

    public DateTimeOffset? CuadradoEn { get; private set; }

    /// <summary>La caja cerró el turno pero todavía nadie contó el dinero: está esperando al supervisor.</summary>
    public bool PendienteDeCuadre => CuadradoEn is null;

    /// <summary>Correcciones hechas desde el Central, en orden. Lo que declaró la caja se puede reconstruir con ellas.</summary>
    public IReadOnlyList<AjusteCierreTurno> Ajustes => _ajustes;

    /// <summary>Alguien corrigió este cierre desde el Central.</summary>
    public bool Ajustado => _ajustes.Count > 0;

    /// <summary>
    /// Lo que declaró la caja, antes de las correcciones: el declarado de hoy menos lo que movió cada ajuste. Sirve para
    /// mostrar en los reportes las dos cifras, la de la terminal y la corregida.
    /// </summary>
    public decimal DeclaradoPorLaCaja => TotalDeclarado - _ajustes.Where(a => a.Moneda == Moneda).Sum(a => a.Movimiento);

    /// <summary>Faltó o sobró dinero respecto de lo esperado.</summary>
    public bool ConDiferencia => Diferencia != 0m;

    public static CierreTurnoCentral Registrar(long turnoNumero, int numero, int sucursalId, int cajaId, DateOnly fechaOperacion,
        string? usuarioNombre, string moneda, decimal fondoInicial, int cantidadVentas, decimal totalVentas, decimal totalRetiros,
        decimal totalEsperado, DateTimeOffset abiertoEn, DateTimeOffset cerradoEn, DateTimeOffset ahora)
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

        cierre.Actualizar(usuarioNombre, fondoInicial, cantidadVentas, totalVentas, totalRetiros, totalEsperado, ahora);
        return cierre;
    }

    /// <summary>
    /// Vuelve a aplicar lo que informó la caja. Un cierre ya cuadrado o corregido no se pisa: el reenvío del mismo mensaje
    /// traería otra vez las cifras de la caja y borraría el trabajo del supervisor.
    /// </summary>
    public void Actualizar(string? usuarioNombre, decimal fondoInicial, int cantidadVentas, decimal totalVentas, decimal totalRetiros,
        decimal totalEsperado, DateTimeOffset ahora)
    {
        if (Ajustado || !PendienteDeCuadre)
            return;

        UsuarioNombre = Validar.TextoOpcional(usuarioNombre, "Usuario", LargoMaximoTexto) ?? string.Empty;
        FondoInicial = fondoInicial;
        CantidadVentas = cantidadVentas;
        TotalVentas = totalVentas;
        TotalRetiros = totalRetiros;
        TotalEsperado = totalEsperado;
        RegistradoEn = ahora;
    }

    public void ReemplazarFormasPago(IEnumerable<(TipoFormaPago Tipo, string Nombre, string Moneda, int Transacciones, decimal Esperado)> formas)
    {
        ArgumentNullException.ThrowIfNull(formas);
        if (Ajustado || !PendienteDeCuadre)
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
            });
        }
    }

    /// <summary>
    /// Los retiros, reembolsos y relevos del turno, como los informó la caja. No los toca el cuadre: son hechos de la
    /// terminal, y son los que explican por qué el efectivo esperado no es todo lo que se vendió.
    /// </summary>
    public void ReemplazarMovimientos(IEnumerable<MovimientoInformado> movimientos)
    {
        ArgumentNullException.ThrowIfNull(movimientos);

        _movimientos.Clear();
        foreach (var movimiento in movimientos.OrderBy(m => m.Fecha))
            _movimientos.Add(CierreMovimientoCentral.Crear(Id, movimiento, Moneda));
    }

    /// <summary>
    /// El supervisor cuadra el cierre: cuenta el efectivo por denominaciones y declara el total de cada forma de pago. Es lo
    /// que la cajera no hace, porque entrega el dinero sin ver los montos. La diferencia queda a nombre de la cajera.
    /// </summary>
    /// <param name="declarados">Lo contado en cada forma de pago de este cierre.</param>
    /// <param name="conteo">Las denominaciones del efectivo; su suma tiene que cuadrar con lo declarado en efectivo.</param>
    /// <exception cref="ArgumentException">Ya está cuadrado, falta alguna forma, hay montos negativos o el conteo no cuadra.</exception>
    public void Cuadrar(IReadOnlyCollection<DeclaradoFormaPago> declarados, IReadOnlyCollection<ConteoDenominacion> conteo,
        string usuarioNombre, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(conteo);
        if (!PendienteDeCuadre)
            throw new ArgumentException($"El cierre ya lo cuadró {CuadradoPor} el {CuadradoEn:dd/MM/yyyy}.", nameof(declarados));
        if (declarados.Any(d => d.Monto < 0) || conteo.Any(c => c.Cantidad < 0))
            throw new ArgumentException("Los montos y las cantidades contadas no pueden ser negativos.", nameof(declarados));
        if (declarados.Select(d => d.FormaPagoId).Distinct().Count() != declarados.Count)
            throw new ArgumentException("Una forma de pago se declaró más de una vez.", nameof(declarados));
        if (declarados.Any(d => _formasPago.All(f => f.Id != d.FormaPagoId)))
            throw new ArgumentException("Se declaró una forma de pago que no es de este cierre.", nameof(declarados));

        _denominaciones.Clear();
        foreach (var item in conteo.Where(c => c.Cantidad > 0).OrderBy(c => c.Moneda).ThenByDescending(c => c.Valor))
            _denominaciones.Add(CierreDenominacionCentral.Crear(Id, item));

        // El efectivo declarado tiene que ser el que sale del conteo: si no cuadran, alguien se equivocó digitando.
        foreach (var moneda in _denominaciones.Select(d => d.Moneda).Distinct())
        {
            var efectivo = _formasPago.Where(f => f.Moneda == moneda && f.Tipo == TipoFormaPago.Efectivo).ToList();
            if (efectivo.Count == 0)
                continue;

            var contado = _denominaciones.Where(d => d.Moneda == moneda).Sum(d => d.Importe);
            var declaradoEfectivo = declarados.Where(d => efectivo.Any(f => f.Id == d.FormaPagoId)).Sum(d => decimal.Round(d.Monto, 2, MidpointRounding.AwayFromZero));
            if (declaradoEfectivo != contado)
                throw new ArgumentException($"El efectivo declarado en {moneda} ({declaradoEfectivo:N2}) no coincide con el conteo por denominaciones ({contado:N2}).",
                    nameof(conteo));
        }

        foreach (var forma in _formasPago)
        {
            var declarado = decimal.Round(declarados.FirstOrDefault(d => d.FormaPagoId == forma.Id)?.Monto ?? 0m, 2, MidpointRounding.AwayFromZero);
            forma.Declarado = declarado;
            forma.Diferencia = declarado - forma.Esperado;
        }

        var locales = _formasPago.Where(f => f.Moneda == Moneda).ToList();
        TotalDeclarado = locales.Sum(f => f.Declarado);
        Diferencia = TotalDeclarado - TotalEsperado;
        CuadradoPor = Validar.Texto(usuarioNombre, "Usuario", LargoMaximoTexto);
        CuadradoEn = ahora;
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
        if (PendienteDeCuadre)
            throw new ArgumentException("El cierre todavía no se ha cuadrado: no hay nada que corregir.", nameof(formaPagoId));

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

/// <summary>
/// Una denominación contada por el supervisor al cuadrar: cuántos billetes o monedas de ese valor había. La suma tiene que
/// dar el efectivo declarado.
/// </summary>
public sealed class CierreDenominacionCentral : Entidad
{
    private CierreDenominacionCentral()
    {
    }

    public int CierreId { get; private set; }
    public int DenominacionId { get; private set; }
    public string Moneda { get; private set; } = string.Empty;
    public decimal Valor { get; private set; }
    public TipoDenominacion Tipo { get; private set; }
    public int Cantidad { get; private set; }
    public decimal Importe { get; private set; }

    internal static CierreDenominacionCentral Crear(int cierreId, ConteoDenominacion conteo) =>
        new()
        {
            CierreId = cierreId,
            DenominacionId = conteo.DenominacionId,
            Moneda = conteo.Moneda,
            Valor = conteo.Valor,
            Tipo = conteo.Tipo,
            Cantidad = conteo.Cantidad,
            Importe = decimal.Round(conteo.Valor * conteo.Cantidad, 2, MidpointRounding.AwayFromZero),
        };
}

/// <summary>Un movimiento del turno tal como lo informó la caja, para guardarlo en el Central.</summary>
/// <param name="UsuarioAnteriorNombre">Solo en el relevo: a quién se le entregó el turno.</param>
public sealed record MovimientoInformado(
    TipoMovimientoCaja Tipo,
    int Numero,
    decimal Monto,
    string? Moneda,
    string? Motivo,
    string UsuarioNombre,
    string? UsuarioAnteriorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset Fecha);

/// <summary>
/// Retiro, reembolso o relevo ocurrido durante el turno. Es lo que explica la diferencia entre lo que se vendió y el
/// efectivo que se entrega, y en el módulo de cuadre se consulta con su motivo y quién lo autorizó.
/// </summary>
public sealed class CierreMovimientoCentral : Entidad
{
    private CierreMovimientoCentral()
    {
    }

    public int CierreId { get; private set; }
    public TipoMovimientoCaja Tipo { get; private set; }

    /// <summary>El número del comprobante que imprimió la caja, para buscarlo en el papel.</summary>
    public int Numero { get; private set; }

    public decimal Monto { get; private set; }
    public string Moneda { get; private set; } = string.Empty;
    public string? Motivo { get; private set; }

    /// <summary>Quién lo hizo; en el relevo, el cajero que recibe el turno.</summary>
    public string UsuarioNombre { get; private set; } = string.Empty;

    public string? UsuarioAnteriorNombre { get; private set; }
    public string? AutorizadoPorNombre { get; private set; }
    public DateTimeOffset Fecha { get; private set; }

    internal static CierreMovimientoCentral Crear(int cierreId, MovimientoInformado movimiento, string monedaCierre) =>
        new()
        {
            CierreId = cierreId,
            Tipo = movimiento.Tipo,
            Numero = movimiento.Numero,
            Monto = movimiento.Monto,
            Moneda = Validar.TextoOpcional(movimiento.Moneda, "Moneda", CierreTurnoCentral.LargoMaximoMoneda)?.ToUpperInvariant() ?? monedaCierre,
            Motivo = Validar.TextoOpcional(movimiento.Motivo, "Motivo", CierreTurnoCentral.LargoMaximoMotivo),
            UsuarioNombre = Validar.TextoOpcional(movimiento.UsuarioNombre, "Usuario", CierreTurnoCentral.LargoMaximoTexto) ?? string.Empty,
            UsuarioAnteriorNombre = Validar.TextoOpcional(movimiento.UsuarioAnteriorNombre, "Usuario anterior", CierreTurnoCentral.LargoMaximoTexto),
            AutorizadoPorNombre = Validar.TextoOpcional(movimiento.AutorizadoPorNombre, "Autorizó", CierreTurnoCentral.LargoMaximoTexto),
            Fecha = movimiento.Fecha,
        };
}
