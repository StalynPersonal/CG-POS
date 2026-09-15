using CgPos.Dominio.Comun;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Turnos;

public enum TipoMovimientoCaja
{
    /// <summary>Retiro parcial de efectivo durante el turno (RF-261).</summary>
    Retiro,

    /// <summary>Cambio del usuario que opera el turno sin cerrarlo (RF-260).</summary>
    Relevo,
}

/// <summary>Retiro de efectivo o relevo de cajero registrado en un turno; ambos van al Central.</summary>
public sealed class MovimientoCaja : Entidad
{
    public const int LargoMaximoMotivo = 250;

    private MovimientoCaja()
    {
    }

    public Guid TurnoId { get; private set; }
    public Guid CajaId { get; private set; }
    public TipoMovimientoCaja Tipo { get; private set; }

    /// <summary>Correlativo por tipo dentro del turno.</summary>
    public int Numero { get; private set; }

    public decimal Monto { get; private set; }
    public string Moneda { get; private set; } = Venta.MonedaLocal;
    public string? Motivo { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;

    /// <summary>En un relevo, quien operaba el turno antes.</summary>
    public Guid? UsuarioAnteriorId { get; private set; }

    public string? UsuarioAnteriorNombre { get; private set; }
    public Guid? AutorizadoPorId { get; private set; }
    public string? AutorizadoPorNombre { get; private set; }
    public DateTimeOffset Fecha { get; private set; }

    public static MovimientoCaja Retiro(Turno turno, int numero, decimal monto, string? motivo, Guid usuarioId, string usuarioNombre,
        Guid? autorizadoPorId, string? autorizadoPorNombre, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(turno);
        if (!turno.EstaAbierto)
            throw new InvalidOperationException("No se puede retirar efectivo de un turno cerrado.");

        var redondeado = decimal.Round(monto, 2, MidpointRounding.AwayFromZero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(redondeado, nameof(monto));

        return Crear(turno, TipoMovimientoCaja.Retiro, numero, redondeado, motivo, usuarioId, usuarioNombre, autorizadoPorId, autorizadoPorNombre, ahora);
    }

    internal static MovimientoCaja Relevo(Turno turno, int numero, Guid usuarioId, string usuarioNombre, Guid? autorizadoPorId, string? autorizadoPorNombre,
        DateTimeOffset ahora)
    {
        var movimiento = Crear(turno, TipoMovimientoCaja.Relevo, numero, 0m, null, usuarioId, usuarioNombre, autorizadoPorId, autorizadoPorNombre, ahora);
        movimiento.UsuarioAnteriorId = turno.UsuarioActualId;
        movimiento.UsuarioAnteriorNombre = turno.UsuarioActualNombre;
        return movimiento;
    }

    private static MovimientoCaja Crear(Turno turno, TipoMovimientoCaja tipo, int numero, decimal monto, string? motivo, Guid usuarioId, string usuarioNombre,
        Guid? autorizadoPorId, string? autorizadoPorNombre, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1);

        return new MovimientoCaja
        {
            Id = Guid.CreateVersion7(),
            TurnoId = turno.Id,
            CajaId = turno.CajaId,
            Tipo = tipo,
            Numero = numero,
            Monto = monto,
            Motivo = Validar.TextoOpcional(motivo, "Motivo", LargoMaximoMotivo),
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", Turno.LargoMaximoUsuario),
            AutorizadoPorId = autorizadoPorId,
            AutorizadoPorNombre = Validar.TextoOpcional(autorizadoPorNombre, "Autorizado por", Turno.LargoMaximoUsuario),
            Fecha = ahora,
        };
    }
}

public enum EstadoCierre
{
    Vigente,

    /// <summary>El turno se reabrió con autorización (RF-266); el cierre queda como historial.</summary>
    Reabierto,
}

public enum CodigoErrorCierre
{
    TurnoCerrado,
    MontoInvalido,
    FormaPagoDesconocida,
    ConteoNoCoincide,
    MotivoRequerido,
    YaReabierto,
}

public sealed class ReglaCierreExcepcion(CodigoErrorCierre codigo, string mensaje) : Exception(mensaje)
{
    public CodigoErrorCierre Codigo { get; } = codigo;
}

public sealed record FormaPagoCuadre(Guid FormaPagoId, string Codigo, string Nombre, TipoFormaPago Tipo, string Moneda, int Orden);

/// <param name="MontoRecibido">En la moneda de la forma de pago, con la devuelta incluida.</param>
/// <param name="MontoAplicado">En pesos, lo que abonó a la factura.</param>
public sealed record PagoCuadre(Guid FormaPagoId, decimal MontoRecibido, decimal MontoAplicado);

/// <param name="Esperado">En la moneda de la forma de pago.</param>
public sealed record EsperadoFormaPago(Guid FormaPagoId, string Codigo, string Nombre, TipoFormaPago Tipo, string Moneda, int Orden, decimal Esperado, int Transacciones);

public sealed record DeclaradoFormaPago(Guid FormaPagoId, decimal Monto);

public sealed record ConteoDenominacion(Guid DenominacionId, string Moneda, decimal Valor, TipoDenominacion Tipo, int Cantidad);

/// <summary>Cálculo de lo esperado en la caja por forma de pago (RF-86, RF-263).</summary>
public static class ReglasCuadre
{
    public static bool EsEfectivo(TipoFormaPago tipo) => tipo is TipoFormaPago.Efectivo or TipoFormaPago.MonedaExtranjera;

    /// <summary>
    /// El efectivo cuenta lo recibido en su moneda. El efectivo en pesos descuenta la devuelta de todas las ventas (siempre sale en pesos) y los
    /// retiros, y suma el fondo solo si el fondo forma parte del cuadre (RF-4). Los demás medios cuentan lo aplicado a las facturas.
    /// </summary>
    public static IReadOnlyList<EsperadoFormaPago> CalcularEsperados(IEnumerable<FormaPagoCuadre> formas, IEnumerable<PagoCuadre> pagos, decimal devuelta,
        decimal retiros, decimal fondoInicial, bool fondoEnCuadre)
    {
        var listaPagos = pagos.ToList();
        var ordenadas = formas.OrderBy(f => f.Orden).ToList();
        // La devuelta y los retiros salen del efectivo en pesos que recibió pagos; si ninguno los recibió, del primero por orden.
        var efectivosLocales = ordenadas.Where(f => f.Tipo == TipoFormaPago.Efectivo && f.Moneda == Venta.MonedaLocal).ToList();
        var efectivoLocal = efectivosLocales.FirstOrDefault(f => listaPagos.Any(p => p.FormaPagoId == f.FormaPagoId)) ?? efectivosLocales.FirstOrDefault();

        return ordenadas.Select(forma =>
            {
                var propios = listaPagos.Where(p => p.FormaPagoId == forma.FormaPagoId).ToList();
                var monto = EsEfectivo(forma.Tipo) ? propios.Sum(p => p.MontoRecibido) : propios.Sum(p => p.MontoAplicado);
                if (forma == efectivoLocal)
                    monto += (fondoEnCuadre ? fondoInicial : 0m) - devuelta - retiros;

                return new EsperadoFormaPago(forma.FormaPagoId, forma.Codigo, forma.Nombre, forma.Tipo, forma.Moneda, forma.Orden, Redondear(monto), propios.Count);
            })
            .ToList();
    }

    /// <summary>Efectivo en pesos que físicamente hay en la gaveta, con el fondo, para validar retiros.</summary>
    public static decimal EfectivoLocalEnGaveta(IEnumerable<EsperadoFormaPago> esperados, decimal fondoInicial, bool fondoEnCuadre)
    {
        var locales = esperados.Where(e => e.Tipo == TipoFormaPago.Efectivo && e.Moneda == Venta.MonedaLocal).ToList();
        return locales.Sum(e => e.Esperado) + (fondoEnCuadre ? 0m : fondoInicial);
    }

    internal static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Cierre de un turno (RF-102 a RF-106, RF-262, RF-263): lo declarado por el cajero contra lo esperado por forma de pago, con el conteo de
/// efectivo por denominaciones (RN-23). En cierre ciego el cajero declara sin ver lo esperado. Registrarlo cierra el turno.
/// </summary>
public sealed class CierreTurno : Entidad
{
    public const int LargoMaximoMotivo = 250;

    private readonly List<CierreFormaPago> _formasPago = [];
    private readonly List<CierreDenominacion> _denominaciones = [];

    private CierreTurno()
    {
    }

    public Guid TurnoId { get; private set; }
    public Guid CajaId { get; private set; }
    public Guid SucursalId { get; private set; }
    public long TurnoNumero { get; private set; }

    /// <summary>1 para el primer cierre del turno; aumenta si el turno se reabre y se vuelve a cerrar.</summary>
    public int Numero { get; private set; }

    public DateOnly FechaOperacion { get; private set; }
    public DateTimeOffset AbiertoEn { get; private set; }
    public bool Ciego { get; private set; }
    public decimal FondoInicial { get; private set; }
    public bool FondoEnCuadre { get; private set; }
    public int CantidadVentas { get; private set; }
    public decimal TotalVentas { get; private set; }
    public decimal TotalRetiros { get; private set; }

    /// <summary>Totales en pesos; las monedas extranjeras se cuadran aparte, en su moneda.</summary>
    public decimal TotalEsperado { get; private set; }

    public decimal TotalDeclarado { get; private set; }

    /// <summary>Declarado menos esperado: positivo es sobrante, negativo faltante.</summary>
    public decimal Diferencia { get; private set; }

    public Guid UsuarioId { get; private set; }
    public string UsuarioNombre { get; private set; } = string.Empty;
    public DateTimeOffset CerradoEn { get; private set; }
    public EstadoCierre Estado { get; private set; }
    public Guid? ReabiertoPorId { get; private set; }
    public string? ReabiertoPorNombre { get; private set; }
    public DateTimeOffset? ReabiertoEn { get; private set; }
    public string? MotivoReapertura { get; private set; }

    public IReadOnlyList<CierreFormaPago> FormasPago => _formasPago;
    public IReadOnlyList<CierreDenominacion> Denominaciones => _denominaciones;

    public static CierreTurno Registrar(Turno turno, int numero, bool ciego, bool fondoEnCuadre, int cantidadVentas, decimal totalVentas, decimal totalRetiros,
        IReadOnlyCollection<EsperadoFormaPago> esperados, IReadOnlyCollection<DeclaradoFormaPago> declarados, IReadOnlyCollection<ConteoDenominacion> conteo,
        Guid usuarioId, string usuarioNombre, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(turno);
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1);

        if (!turno.EstaAbierto)
            throw new ReglaCierreExcepcion(CodigoErrorCierre.TurnoCerrado, "El turno ya está cerrado.");
        if (declarados.Any(d => d.Monto < 0) || conteo.Any(c => c.Cantidad < 0))
            throw new ReglaCierreExcepcion(CodigoErrorCierre.MontoInvalido, "Los montos y cantidades declarados no pueden ser negativos.");
        if (declarados.Any(d => esperados.All(e => e.FormaPagoId != d.FormaPagoId)))
            throw new ReglaCierreExcepcion(CodigoErrorCierre.FormaPagoDesconocida, "Se declaró una forma de pago que no existe en la caja.");
        if (declarados.GroupBy(d => d.FormaPagoId).Any(grupo => grupo.Count() > 1))
            throw new ReglaCierreExcepcion(CodigoErrorCierre.FormaPagoDesconocida, "Una forma de pago se declaró más de una vez.");

        var cierre = new CierreTurno
        {
            Id = Guid.CreateVersion7(),
            TurnoId = turno.Id,
            CajaId = turno.CajaId,
            SucursalId = turno.SucursalId,
            TurnoNumero = turno.Numero,
            Numero = numero,
            FechaOperacion = turno.FechaOperacion,
            AbiertoEn = turno.AbiertoEn,
            Ciego = ciego,
            FondoInicial = turno.FondoInicial,
            FondoEnCuadre = fondoEnCuadre,
            CantidadVentas = cantidadVentas,
            TotalVentas = ReglasCuadre.Redondear(totalVentas),
            TotalRetiros = ReglasCuadre.Redondear(totalRetiros),
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", Turno.LargoMaximoUsuario),
            CerradoEn = ahora,
            Estado = EstadoCierre.Vigente,
        };

        foreach (var item in conteo.Where(c => c.Cantidad > 0).OrderBy(c => c.Moneda).ThenByDescending(c => c.Valor))
            cierre._denominaciones.Add(CierreDenominacion.Crear(cierre.Id, item));

        // El conteo por denominaciones es lo declarado de la forma de efectivo de esa moneda que recibió pagos (o la primera por orden).
        var destinosConteo = esperados.Where(e => ReglasCuadre.EsEfectivo(e.Tipo))
            .GroupBy(e => e.Moneda)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.OrderByDescending(e => e.Transacciones > 0).ThenBy(e => e.Orden).First().FormaPagoId);

        foreach (var esperado in esperados.OrderBy(e => e.Orden))
        {
            var declaracion = declarados.FirstOrDefault(d => d.FormaPagoId == esperado.FormaPagoId);
            var declarado = ReglasCuadre.Redondear(declaracion?.Monto ?? 0m);
            var contado = cierre._denominaciones.Where(d => d.Moneda == esperado.Moneda).ToList();

            if (contado.Count > 0 && destinosConteo.TryGetValue(esperado.Moneda, out var destino) && destino == esperado.FormaPagoId)
            {
                var totalContado = contado.Sum(d => d.Importe);
                if (declaracion is not null && declarado != totalContado)
                    throw new ReglaCierreExcepcion(CodigoErrorCierre.ConteoNoCoincide,
                        $"El efectivo declarado en {esperado.Nombre} ({declarado:N2}) no coincide con el conteo por denominaciones ({totalContado:N2}).");
                declarado = totalContado;
            }

            cierre._formasPago.Add(CierreFormaPago.Crear(cierre.Id, esperado, declarado));
        }

        var locales = cierre._formasPago.Where(f => f.Moneda == Venta.MonedaLocal).ToList();
        cierre.TotalEsperado = locales.Sum(f => f.Esperado);
        cierre.TotalDeclarado = locales.Sum(f => f.Declarado);
        cierre.Diferencia = cierre.TotalDeclarado - cierre.TotalEsperado;

        turno.Cerrar(ahora);
        return cierre;
    }

    /// <summary>Marca el cierre como reabierto (RF-266). Quien llama reabre también el turno.</summary>
    public void Reabrir(Guid usuarioId, string usuarioNombre, string? motivo, DateTimeOffset ahora)
    {
        if (Estado != EstadoCierre.Vigente)
            throw new ReglaCierreExcepcion(CodigoErrorCierre.YaReabierto, "El cierre ya fue reabierto.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaCierreExcepcion(CodigoErrorCierre.MotivoRequerido, "La reapertura de un cierre requiere motivo.");

        Estado = EstadoCierre.Reabierto;
        ReabiertoPorId = Validar.Id(usuarioId, "Usuario");
        ReabiertoPorNombre = Validar.Texto(usuarioNombre, "Usuario", Turno.LargoMaximoUsuario);
        MotivoReapertura = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo);
        ReabiertoEn = ahora;
    }
}

public sealed class CierreFormaPago : Entidad
{
    private CierreFormaPago()
    {
    }

    public Guid CierreTurnoId { get; private set; }
    public Guid FormaPagoId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public TipoFormaPago Tipo { get; private set; }
    public string Moneda { get; private set; } = Venta.MonedaLocal;
    public int Orden { get; private set; }
    public int Transacciones { get; private set; }
    public decimal Esperado { get; private set; }
    public decimal Declarado { get; private set; }
    public decimal Diferencia { get; private set; }

    internal static CierreFormaPago Crear(Guid cierreId, EsperadoFormaPago esperado, decimal declarado) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CierreTurnoId = cierreId,
            FormaPagoId = esperado.FormaPagoId,
            Codigo = esperado.Codigo,
            Nombre = esperado.Nombre,
            Tipo = esperado.Tipo,
            Moneda = esperado.Moneda,
            Orden = esperado.Orden,
            Transacciones = esperado.Transacciones,
            Esperado = esperado.Esperado,
            Declarado = declarado,
            Diferencia = declarado - esperado.Esperado,
        };
}

public sealed class CierreDenominacion : Entidad
{
    private CierreDenominacion()
    {
    }

    public Guid CierreTurnoId { get; private set; }
    public Guid DenominacionId { get; private set; }
    public string Moneda { get; private set; } = Venta.MonedaLocal;
    public decimal Valor { get; private set; }
    public TipoDenominacion Tipo { get; private set; }
    public int Cantidad { get; private set; }
    public decimal Importe { get; private set; }

    internal static CierreDenominacion Crear(Guid cierreId, ConteoDenominacion conteo) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CierreTurnoId = cierreId,
            DenominacionId = conteo.DenominacionId,
            Moneda = conteo.Moneda,
            Valor = conteo.Valor,
            Tipo = conteo.Tipo,
            Cantidad = conteo.Cantidad,
            Importe = ReglasCuadre.Redondear(conteo.Valor * conteo.Cantidad),
        };
}
