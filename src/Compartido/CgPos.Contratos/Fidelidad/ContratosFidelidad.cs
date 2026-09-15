using CgPos.Dominio.Fidelidad;

namespace CgPos.Contratos.Fidelidad;

/// <summary>Miembro del programa con el saldo disponible en la caja (RF-240).</summary>
/// <param name="ValorEnMoneda">Lo que valen los puntos disponibles al canjearlos; nulo si no se configuró el valor del punto.</param>
/// <param name="SaldoSincronizadoEn">Cuándo el Central calculó el saldo base; nulo si aún no sincroniza.</param>
public sealed record DatosMiembroFidelidad(
    Guid Id,
    string Cedula,
    string Nombre,
    string? Telefono,
    string? Correo,
    string? Nivel,
    int SaldoDisponible,
    decimal? ValorEnMoneda,
    int PuntosPorVencer,
    DateOnly? ProximoVencimiento,
    DateTimeOffset? SaldoSincronizadoEn,
    bool PendienteDeConfirmar);

/// <param name="Nombre">Si se omite se toma del padrón DGII.</param>
public sealed record SolicitudInscripcionFidelidad(string Cedula, string? Nombre, string? Telefono, string? Correo);

public enum CodigoResultadoFidelidad
{
    Correcto,
    CedulaInvalida,
    NoInscrito,
    YaInscrito,
    NombreRequerido,
    DatosInvalidos,
}

public sealed record RespuestaFidelidad(CodigoResultadoFidelidad Resultado, string? Mensaje, DatosMiembroFidelidad? Miembro)
{
    public bool Exitosa => Resultado == CodigoResultadoFidelidad.Correcto;
}

/// <summary>Mensaje para el Central: inscripción hecha en caja (RF-237).</summary>
public sealed record DocumentoInscripcionFidelidad(Guid MiembroId, string Cedula, string Nombre, string? Telefono, string? Correo, Guid CajaId, Guid SucursalId,
    string UsuarioNombre, DateTimeOffset InscritoEn);

/// <summary>Mensaje para el Central: puntos acumulados, canjeados o reversados en la caja.</summary>
public sealed record DocumentoMovimientoPuntos(Guid Id, Guid MiembroId, string Cedula, TipoMovimientoPuntos Tipo, int Puntos, Guid? VentaId, Guid? DevolucionId,
    string Documento, Guid CajaId, Guid SucursalId, DateTimeOffset Fecha, DateOnly? VenceEn);
