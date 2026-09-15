using CgPos.Dominio.Fiscal;

namespace CgPos.Contratos.Central;

/// <summary>Tipos de e-CF que emite una caja y que por eso se le asignan rangos.</summary>
public static class TiposEcfCaja
{
    public static IReadOnlyList<TipoComprobante> Asignables { get; } =
    [
        TipoComprobante.FacturaConsumo,
        TipoComprobante.FacturaCreditoFiscal,
        TipoComprobante.NotaCredito,
        TipoComprobante.RegimenesEspeciales,
        TipoComprobante.Gubernamental,
    ];
}

/// <param name="UltimoRecibido">Última secuencia de ese tipo que el Central recibió de la caja dentro del rango; nulo si ninguna.</param>
public sealed record DatosSecuenciaEcfCentral(
    Guid Id,
    Guid CajaId,
    string CajaCodigo,
    string SucursalCodigo,
    TipoComprobante TipoComprobante,
    long Desde,
    long Hasta,
    DateOnly VenceEn,
    bool Activa,
    long? UltimoRecibido);

public sealed record SolicitudSecuenciaEcf(Guid CajaId, TipoComprobante TipoComprobante, long Desde, long Hasta, DateOnly VenceEn);

/// <summary>Un rango solo se amplía, se prorroga o se desactiva: la caja, el tipo y el inicio no cambian.</summary>
public sealed record SolicitudActualizarSecuenciaEcf(long Hasta, DateOnly VenceEn, bool Activa);

public sealed record DatosRolCaja(Guid Id, string Codigo, string Nombre, int Nivel, IReadOnlyList<string> Permisos, bool Activo, int Usuarios);

/// <param name="Codigo">No cambia después de crear el rol.</param>
/// <param name="Permisos">Códigos del catálogo de permisos de la caja.</param>
public sealed record SolicitudRolCaja(string Codigo, string Nombre, int Nivel, IReadOnlyList<string> Permisos, bool Activo = true);

public sealed record DatosUsuarioCaja(Guid Id, string Codigo, string Nombre, Guid RolId, string RolNombre, IReadOnlyList<Guid> Cajas, bool TienePin, bool TieneCarne, bool Activo);

/// <param name="Codigo">No cambia después de crear el usuario.</param>
/// <param name="Pin">PIN nuevo (4 a 8 dígitos). Obligatorio al crear; vacío conserva el actual.</param>
/// <param name="Carne">Código de barras del carné nuevo; vacío conserva el actual.</param>
/// <param name="QuitarCarne">Retira el carné actual.</param>
public sealed record SolicitudUsuarioCaja(
    string Codigo,
    string Nombre,
    Guid RolId,
    IReadOnlyList<Guid> Cajas,
    string? Pin = null,
    string? Carne = null,
    bool QuitarCarne = false,
    bool Activo = true);
