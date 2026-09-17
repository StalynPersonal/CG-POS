using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Fidelidad;

/// <summary>Nivel o categoría del programa de fidelidad con su beneficio en la acumulación (RF-241). Lo define el Central.</summary>
public sealed class NivelFidelidad : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 60;

    private NivelFidelidad()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public int Orden { get; private set; }

    /// <summary>Multiplica los puntos que acumula el cliente del nivel (ej. 1.5 = 50 % más).</summary>
    public decimal FactorAcumulacion { get; private set; }

    public bool Activo { get; private set; } = true;

    public static NivelFidelidad Crear(string codigo, string nombre, int orden, decimal factorAcumulacion, Guid? id = null)
    {
        var nivel = new NivelFidelidad
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código del nivel", LargoMaximoCodigo).ToUpperInvariant(),
        };
        nivel.Actualizar(nombre, orden, factorAcumulacion);
        return nivel;
    }

    public void Actualizar(string nombre, int orden, decimal factorAcumulacion)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factorAcumulacion);
        Nombre = Validar.Texto(nombre, "Nombre del nivel", LargoMaximoNombre);
        Orden = orden;
        FactorAcumulacion = factorAcumulacion;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

public enum TipoReglaAcumulacion
{
    /// <summary>Todo lo comprado.</summary>
    Monto,
    Departamento,
    Articulo,

    /// <summary>Todo lo comprado en un día de la semana.</summary>
    DiaSemana,

    /// <summary>Lo vendido con una promoción.</summary>
    Promocion,

    Categoria,
    Marca,
}

/// <summary>Línea cobrada con lo necesario para acumular puntos.</summary>
public sealed record LineaPuntuable(Guid ArticuloId, Guid DepartamentoId, Guid? PromocionId, decimal Importe, Guid? CategoriaId = null, Guid? MarcaId = null);

/// <summary>
/// Regla de acumulación configurable (RF-238): otorga <see cref="Puntos"/> por cada <see cref="MontoBase"/> comprado de lo que
/// abarca (todo, un departamento, una categoría, una marca, un artículo, un día o una promoción) dentro de su vigencia.
/// </summary>
public sealed class ReglaAcumulacion : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 100;

    private ReglaAcumulacion()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public TipoReglaAcumulacion Tipo { get; private set; }

    /// <summary>Departamento, categoría, marca, artículo o promoción según el tipo.</summary>
    public Guid? ReferenciaId { get; private set; }

    public DayOfWeek? DiaSemana { get; private set; }
    public decimal MontoBase { get; private set; }
    public decimal Puntos { get; private set; }
    public DateTimeOffset? VigenteDesde { get; private set; }
    public DateTimeOffset? VigenteHasta { get; private set; }
    public bool Activa { get; private set; } = true;

    public static ReglaAcumulacion Crear(string codigo, string nombre, TipoReglaAcumulacion tipo, decimal montoBase, decimal puntos, Guid? referenciaId,
        DayOfWeek? diaSemana, DateTimeOffset? vigenteDesde, DateTimeOffset? vigenteHasta, Guid? id = null)
    {
        var regla = new ReglaAcumulacion
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de la regla", LargoMaximoCodigo).ToUpperInvariant(),
        };
        regla.Actualizar(nombre, tipo, montoBase, puntos, referenciaId, diaSemana, vigenteDesde, vigenteHasta);
        return regla;
    }

    public void Actualizar(string nombre, TipoReglaAcumulacion tipo, decimal montoBase, decimal puntos, Guid? referenciaId, DayOfWeek? diaSemana,
        DateTimeOffset? vigenteDesde, DateTimeOffset? vigenteHasta)
    {
        if (!Enum.IsDefined(tipo))
            throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de regla de acumulación no válido.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(montoBase);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        if (tipo is TipoReglaAcumulacion.Departamento or TipoReglaAcumulacion.Categoria or TipoReglaAcumulacion.Marca or TipoReglaAcumulacion.Articulo
                or TipoReglaAcumulacion.Promocion && referenciaId is null)
            throw new ArgumentException($"La regla de tipo {tipo} debe indicar a qué se aplica.", nameof(referenciaId));
        if (tipo == TipoReglaAcumulacion.DiaSemana && diaSemana is null)
            throw new ArgumentException("La regla por día debe indicar el día de la semana.", nameof(diaSemana));
        if (vigenteDesde > vigenteHasta)
            throw new ArgumentException("La vigencia de la regla termina antes de empezar.", nameof(vigenteHasta));

        Nombre = Validar.Texto(nombre, "Nombre de la regla", LargoMaximoNombre);
        Tipo = tipo;
        MontoBase = montoBase;
        Puntos = puntos;
        ReferenciaId = tipo is TipoReglaAcumulacion.Monto or TipoReglaAcumulacion.DiaSemana ? null : referenciaId;
        DiaSemana = tipo == TipoReglaAcumulacion.DiaSemana ? diaSemana : null;
        VigenteDesde = vigenteDesde;
        VigenteHasta = vigenteHasta;
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    /// <param name="ahoraLocal">Fecha y hora local de la caja, para el día de la semana.</param>
    public bool AplicaA(LineaPuntuable linea, DateTimeOffset ahoraLocal) =>
        Activa
        && (VigenteDesde is null || ahoraLocal >= VigenteDesde)
        && (VigenteHasta is null || ahoraLocal <= VigenteHasta)
        && Tipo switch
        {
            TipoReglaAcumulacion.Monto => true,
            TipoReglaAcumulacion.Departamento => linea.DepartamentoId == ReferenciaId,
            TipoReglaAcumulacion.Categoria => linea.CategoriaId is not null && linea.CategoriaId == ReferenciaId,
            TipoReglaAcumulacion.Marca => linea.MarcaId is not null && linea.MarcaId == ReferenciaId,
            TipoReglaAcumulacion.Articulo => linea.ArticuloId == ReferenciaId,
            TipoReglaAcumulacion.DiaSemana => ahoraLocal.DayOfWeek == DiaSemana,
            TipoReglaAcumulacion.Promocion => linea.PromocionId is not null && linea.PromocionId == ReferenciaId,
            _ => false,
        };

    public decimal PuntosPor(decimal importe) => importe <= 0 ? 0m : importe / MontoBase * Puntos;
}

/// <summary>
/// Cliente inscrito en el programa de fidelidad: la cédula es su identificador y PIN (RF-236, RN-20). El Central envía su nivel y
/// el último saldo conocido; la caja suma sus propios movimientos posteriores para operar sin conexión (RF-243).
/// </summary>
public sealed class MiembroFidelidad : Entidad
{
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoTelefono = 20;
    public const int LargoMaximoCorreo = 150;

    private MiembroFidelidad()
    {
    }

    /// <summary>Cédula normalizada (11 dígitos).</summary>
    public string Cedula { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public string? Telefono { get; private set; }
    public string? Correo { get; private set; }
    public Guid? NivelId { get; private set; }

    /// <summary>Saldo que calculó el Central al <see cref="SaldoSincronizadoEn"/>.</summary>
    public int SaldoSincronizado { get; private set; }

    public DateTimeOffset? SaldoSincronizadoEn { get; private set; }

    /// <summary>Puntos del saldo sincronizado que vencen en <see cref="ProximoVencimiento"/> (RF-242).</summary>
    public int PuntosPorVencer { get; private set; }

    public DateOnly? ProximoVencimiento { get; private set; }
    public DateTimeOffset InscritoEn { get; private set; }

    /// <summary>Se inscribió en esta caja y el Central aún no lo confirma.</summary>
    public bool InscritoEnCaja { get; private set; }

    public bool Activo { get; private set; } = true;

    public static string ValidarCedula(string? cedula)
    {
        var normalizada = DocumentoIdentidad.Normalizar(cedula);
        return normalizada.Length == DocumentoIdentidad.LargoCedula && normalizada.All(char.IsAsciiDigit)
            ? normalizada
            : throw new ArgumentException("La cédula debe tener 11 dígitos.", nameof(cedula));
    }

    /// <summary>Inscripción desde la caja sin interrumpir la venta (RF-237); la confirma el Central al sincronizar.</summary>
    public static MiembroFidelidad Inscribir(string cedula, string nombre, string? telefono, string? correo, DateTimeOffset ahora, Guid? id = null)
    {
        var miembro = new MiembroFidelidad
        {
            Id = id ?? Guid.CreateVersion7(),
            Cedula = ValidarCedula(cedula),
            InscritoEn = ahora,
            InscritoEnCaja = true,
        };
        miembro.ActualizarContacto(nombre, telefono, correo);
        return miembro;
    }

    /// <summary>Miembro que envía el Central con su saldo al día.</summary>
    public static MiembroFidelidad DesdeCentral(string cedula, string nombre, DateTimeOffset inscritoEn, Guid id)
    {
        var miembro = new MiembroFidelidad { Id = id, Cedula = ValidarCedula(cedula), InscritoEn = inscritoEn };
        miembro.ActualizarContacto(nombre, null, null);
        return miembro;
    }

    public void ActualizarContacto(string nombre, string? telefono, string? correo)
    {
        var nombreValido = Validar.Texto(nombre, "Nombre del cliente", LargoMaximoNombre);
        var telefonoValido = Validar.TextoOpcional(telefono, "Teléfono", LargoMaximoTelefono);
        var correoValido = Validar.TextoOpcional(correo, "Correo", LargoMaximoCorreo);
        if (correoValido is not null && !correoValido.Contains('@'))
            throw new ArgumentException("El correo no tiene un formato válido.", nameof(correo));

        Nombre = nombreValido;
        Telefono = telefonoValido;
        Correo = correoValido;
    }

    public void AsignarNivel(Guid? nivelId) => NivelId = nivelId;

    /// <summary>Saldo recalculado por el Central; confirma también la inscripción hecha en caja.</summary>
    public void SincronizarSaldo(int saldo, DateTimeOffset saldoAl, int puntosPorVencer, DateOnly? proximoVencimiento)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(puntosPorVencer);
        SaldoSincronizado = saldo;
        SaldoSincronizadoEn = saldoAl;
        PuntosPorVencer = puntosPorVencer;
        ProximoVencimiento = proximoVencimiento;
        InscritoEnCaja = false;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    /// <summary>
    /// Saldo disponible en la caja: el último saldo sincronizado (sin los puntos que ya vencieron) más los movimientos de la caja
    /// posteriores a esa sincronización, sin las acumulaciones vencidas (RF-240, RF-242, RF-243).
    /// </summary>
    public int SaldoDisponible(IEnumerable<MovimientoPuntos> movimientos, DateOnly hoy)
    {
        var sincronizado = SaldoSincronizado - (ProximoVencimiento is { } vence && vence < hoy ? PuntosPorVencer : 0);
        var locales = movimientos
            .Where(m => m.MiembroId == Id && (SaldoSincronizadoEn is null || m.Fecha > SaldoSincronizadoEn))
            .Where(m => !(m.Tipo == TipoMovimientoPuntos.Acumulacion && m.VenceEn is { } venceEn && venceEn < hoy))
            .Sum(m => m.Puntos);
        return sincronizado + locales;
    }
}

public enum TipoMovimientoPuntos
{
    Acumulacion,
    Canje,

    /// <summary>Reverso de lo acumulado por una devolución (RF-244, RN-21).</summary>
    Reverso,

    /// <summary>Ajuste manual hecho en el Central, a favor o en contra, con motivo y responsable.</summary>
    Ajuste,
}

/// <summary>Movimiento de puntos hecho en la caja; va al Central, que lleva el saldo oficial.</summary>
public sealed class MovimientoPuntos : Entidad
{
    public const int LargoMaximoDocumento = 40;

    private MovimientoPuntos()
    {
    }

    public Guid MiembroId { get; private set; }
    public string Cedula { get; private set; } = string.Empty;
    public TipoMovimientoPuntos Tipo { get; private set; }

    /// <summary>Positivo al acumular; negativo al canjear o reversar.</summary>
    public int Puntos { get; private set; }

    public Guid? VentaId { get; private set; }
    public Guid? DevolucionId { get; private set; }

    /// <summary>Número de la transacción o de la nota de crédito.</summary>
    public string Documento { get; private set; } = string.Empty;

    public Guid CajaId { get; private set; }
    public DateTimeOffset Fecha { get; private set; }

    /// <summary>Fecha hasta la que valen los puntos acumulados; nula si el programa no configura vencimiento.</summary>
    public DateOnly? VenceEn { get; private set; }

    public static MovimientoPuntos Acumulacion(MiembroFidelidad miembro, int puntos, Guid ventaId, string documento, Guid cajaId, DateTimeOffset ahora, DateOnly? venceEn)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        var movimiento = Crear(miembro, TipoMovimientoPuntos.Acumulacion, puntos, documento, cajaId, ahora);
        movimiento.VentaId = ventaId;
        movimiento.VenceEn = venceEn;
        return movimiento;
    }

    public static MovimientoPuntos Canje(MiembroFidelidad miembro, int puntos, Guid ventaId, string documento, Guid cajaId, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        var movimiento = Crear(miembro, TipoMovimientoPuntos.Canje, -puntos, documento, cajaId, ahora);
        movimiento.VentaId = ventaId;
        return movimiento;
    }

    public static MovimientoPuntos Reverso(MiembroFidelidad miembro, int puntos, Guid ventaId, Guid devolucionId, string documento, Guid cajaId, DateTimeOffset ahora)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        var movimiento = Crear(miembro, TipoMovimientoPuntos.Reverso, -puntos, documento, cajaId, ahora);
        movimiento.VentaId = ventaId;
        movimiento.DevolucionId = devolucionId;
        return movimiento;
    }

    private static MovimientoPuntos Crear(MiembroFidelidad miembro, TipoMovimientoPuntos tipo, int puntos, string documento, Guid cajaId, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(miembro);
        return new MovimientoPuntos
        {
            Id = Guid.CreateVersion7(),
            MiembroId = miembro.Id,
            Cedula = miembro.Cedula,
            Tipo = tipo,
            Puntos = puntos,
            Documento = Validar.Texto(documento, "Documento", LargoMaximoDocumento),
            CajaId = Validar.Id(cajaId, "Caja"),
            Fecha = ahora,
        };
    }
}

/// <summary>Cálculo de puntos del programa de fidelidad.</summary>
public static class ReglasFidelidad
{
    /// <summary>
    /// Puntos de una compra (RF-238): cada línea toma la regla que más puntos le da; el total se multiplica por el factor del nivel
    /// (RF-241) y por la parte de la factura que no se pagó con puntos. Se redondea hacia abajo.
    /// </summary>
    /// <param name="proporcionPuntuable">Entre 0 y 1: lo cobrado sin puntos sobre el total.</param>
    public static int CalcularPuntos(IEnumerable<LineaPuntuable> lineas, IReadOnlyCollection<ReglaAcumulacion> reglas, decimal factorNivel,
        decimal proporcionPuntuable, DateTimeOffset ahoraLocal)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentOutOfRangeException.ThrowIfNegative(factorNivel);

        var proporcion = Math.Clamp(proporcionPuntuable, 0m, 1m);
        var total = lineas.Sum(linea => reglas.Where(r => r.AplicaA(linea, ahoraLocal)).Select(r => r.PuntosPor(linea.Importe)).DefaultIfEmpty(0m).Max());
        return (int)decimal.Floor(total * factorNivel * proporcion);
    }

    /// <summary>Puntos que valen un monto al canjear (hacia arriba: no se regala la fracción).</summary>
    public static int PuntosParaMonto(decimal monto, decimal valorPunto)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valorPunto);
        return monto <= 0 ? 0 : (int)decimal.Ceiling(monto / valorPunto);
    }

    /// <summary>
    /// Puntos a reversar por una devolución (RF-244, RN-21): la parte proporcional de lo acumulado en la factura; si la devolución
    /// completa la factura, todo lo que quede por reversar.
    /// </summary>
    public static int PuntosAReversar(int acumulados, int yaReversados, decimal totalFactura, decimal totalDevolucion, bool completaFactura)
    {
        var pendientes = Math.Max(0, acumulados - yaReversados);
        if (pendientes == 0 || totalFactura <= 0)
            return 0;

        return completaFactura
            ? pendientes
            : Math.Min(pendientes, (int)decimal.Floor(acumulados * Math.Min(totalDevolucion, totalFactura) / totalFactura));
    }
}
