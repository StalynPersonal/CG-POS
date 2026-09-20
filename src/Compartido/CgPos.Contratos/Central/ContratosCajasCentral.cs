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
    int Id,
    int CajaId,
    string CajaCodigo,
    string SucursalCodigo,
    TipoComprobante TipoComprobante,
    /// <summary>Letra con la que empieza el e-NCF de este rango.</summary>
    string Serie,
    long Desde,
    long Hasta,
    DateOnly VenceEn,
    bool Activa,
    long? UltimoRecibido,
    long Proximo,
    DateTimeOffset AsignadoEn,
    string AsignadoPor);

public sealed record SolicitudSecuenciaEcf(int CajaId, TipoComprobante TipoComprobante, long Desde, long Hasta, DateOnly VenceEn, long? Proximo = null,
    string? Serie = null);

/// <summary>Un rango solo se amplía, se prorroga o se desactiva: la caja, el tipo y el inicio no cambian.</summary>
public sealed record SolicitudActualizarSecuenciaEcf(long Hasta, DateOnly VenceEn, bool Activa);

public sealed record DatosRolCaja(int Id, string Codigo, string Nombre, int Nivel, IReadOnlyList<string> Permisos, bool Activo, int Usuarios);

/// <param name="Codigo">No cambia después de crear el rol.</param>
/// <param name="Permisos">Códigos del catálogo de permisos de la caja.</param>
public sealed record SolicitudRolCaja(string Codigo, string Nombre, int Nivel, IReadOnlyList<string> Permisos, bool Activo = true);

public sealed record DatosUsuarioCaja(int Id, string Codigo, string Nombre, int RolId, string RolNombre, IReadOnlyList<int> Cajas, bool TieneClave, bool Activo);

/// <param name="Codigo">No cambia después de crear el usuario.</param>
/// <param name="Clave">Clave nueva. Obligatoria al crear; vacía conserva la actual.</param>
public sealed record SolicitudUsuarioCaja(
    string Codigo,
    string Nombre,
    int RolId,
    IReadOnlyList<int> Cajas,
    string? Clave = null,
    bool Activo = true);

/// <summary>Anulación de un rango de e-NCF no utilizados de una caja, informada a la DGII (ANECF).</summary>
public sealed record DatosAnulacionEcf(
    int Id,
    int SecuenciaId,
    string CajaCodigo,
    string SucursalCodigo,
    TipoComprobante TipoComprobante,
    long Desde,
    long Hasta,
    long Cantidad,
    string Motivo,
    string UsuarioNombre,
    DateTimeOffset SolicitadaEn,
    EstadoAnulacionEcf Estado,
    string? RespuestaDgii);

/// <param name="Desde">Primera secuencia a anular (sin el prefijo E y el tipo).</param>
/// <param name="Hasta">Última secuencia a anular, incluida.</param>
public sealed record SolicitudAnulacionEcf(long Desde, long Hasta, string Motivo);
