using CgPos.Dominio.Fidelidad;

namespace CgPos.Contratos.Central;

/// <summary>Miembro del programa con el saldo oficial que lleva el Central (RF-240, RF-242).</summary>
/// <param name="CalculadoEn">Cuándo se recalculó el saldo por última vez; nulo si nunca tuvo movimientos.</param>
public sealed record DatosMiembroFidelidadCentral(
    Guid Id,
    string Cedula,
    string Nombre,
    string? Telefono,
    string? Correo,
    string? Nivel,
    int Puntos,
    int PuntosPorVencer,
    DateOnly? ProximoVencimiento,
    int Vencidos,
    DateTimeOffset? CalculadoEn,
    DateTimeOffset? InscritoEn,
    bool Activo);

public sealed record PaginaMiembrosFidelidadCentral(IReadOnlyList<DatosMiembroFidelidadCentral> Elementos, int Total);

/// <param name="Origen">De una caja al sincronizar o de un ajuste hecho en el Central.</param>
public sealed record DatosMovimientoPuntosCentral(
    Guid Id,
    TipoMovimientoPuntos Tipo,
    OrigenMovimientoPuntos Origen,
    int Puntos,
    string Documento,
    string? SucursalCodigo,
    string? CajaCodigo,
    DateTimeOffset Fecha,
    DateOnly? VenceEn,
    string? Usuario,
    string? Motivo);

/// <param name="Puntos">Positivo a favor del cliente, negativo en su contra.</param>
public sealed record SolicitudAjustePuntos(int Puntos, string Motivo);

public sealed record RespuestaAjustePuntos(bool Exitosa, string? Mensaje, DatosMiembroFidelidadCentral? Miembro = null);
