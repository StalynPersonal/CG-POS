using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Contratos.Central;

/// <param name="UltimaComunicacionEn">Lo más reciente entre el último mensaje recibido y la última descarga de maestros.</param>
/// <param name="ComprobantesPendientes">e-CF de la caja aún sin resultado de la DGII (pendientes o en proceso).</param>
/// <param name="VentasEnContingencia">Ventas que la caja cobró sin poder firmar su e-CF y que aún no lo tienen (RF-224).</param>
/// <param name="Alertas">Lo que requiere atención, en texto para el usuario; vacía si la caja está al día.</param>
public sealed record DatosEstadoCaja(
    Guid CajaId,
    string CajaCodigo,
    string CajaNombre,
    string SucursalCodigo,
    bool Habilitada,
    DateTimeOffset? UltimaComunicacionEn,
    DateTimeOffset? UltimaRecepcionEn,
    DateTimeOffset? UltimaDescargaEn,
    long MensajesRecibidos,
    long Duplicados,
    long Rechazados,
    DateTimeOffset? UltimoRechazoEn,
    string? UltimoError,
    int ComprobantesPendientes,
    int ComprobantesRechazados,
    int ConflictosAbiertos,
    IReadOnlyList<string> Alertas,
    int VentasEnContingencia = 0);

public sealed record DatosConteoEstadoDgii(EstadoEnvioDgii Estado, int Cantidad);

/// <param name="ComprobantesConFallo">Pendientes cuyo envío a la DGII ya falló al menos una vez.</param>
/// <param name="PendienteMasAntiguoDesde">Recepción del e-CF pendiente de envío más antiguo.</param>
public sealed record DatosMonitorCentral(
    DateTimeOffset GeneradoEn,
    int CajasHabilitadas,
    int CajasConAlerta,
    int ConflictosAbiertos,
    IReadOnlyList<DatosConteoEstadoDgii> ComprobantesPorEstado,
    int ComprobantesConFallo,
    DateTimeOffset? PendienteMasAntiguoDesde,
    IReadOnlyList<DatosEstadoCaja> Cajas,
    int VentasEnContingencia = 0);

public sealed record DatosComprobanteDgii(
    Guid Id,
    string Encf,
    TipoComprobante TipoComprobante,
    Guid CajaId,
    string CajaCodigo,
    string CajaNombre,
    string SucursalCodigo,
    string SucursalNombre,
    DateTimeOffset FechaFirma,
    DateTimeOffset RecibidoEn,
    EstadoEnvioDgii EstadoDgii,
    DateTimeOffset? EstadoDgiiEn,
    string? MensajeDgii,
    string? TrackId,
    int IntentosEnvio,
    DateTimeOffset? ProximoIntentoEn);

public sealed record PaginaComprobantesDgii(IReadOnlyList<DatosComprobanteDgii> Elementos, int Total);

public sealed record DatosConflictoSincronizacion(
    Guid Id,
    Guid CajaId,
    string CajaCodigo,
    Guid MensajeId,
    string TipoMensaje,
    TipoConflictoSincronizacion Tipo,
    string Detalle,
    DateTimeOffset DetectadoEn,
    int Ocurrencias,
    DateTimeOffset UltimaOcurrenciaEn,
    DateTimeOffset? ResueltoEn,
    string? ResueltoPor,
    string? Resolucion);

public sealed record SolicitudResolverConflicto(string Resolucion);
