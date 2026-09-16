using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Fidelidad;

/// <summary>
/// Saldo oficial de puntos del programa de fidelidad (RF-240, RF-242). El Central suma lo que informan todas las cajas,
/// vence los puntos que cumplieron su plazo y publica el saldo en el maestro del miembro para que las cajas canjeen sin conexión.
/// </summary>
public interface IServicioFidelidadCentral
{
    public const int TamanoMaximoPagina = 100;

    Task<PaginaMiembrosFidelidadCentral> ListarAsync(string? buscar, bool soloConPuntos, int pagina, int tamano, CancellationToken cancelacion = default);

    Task<DatosMiembroFidelidadCentral?> ObtenerAsync(Guid miembroId, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosMovimientoPuntosCentral>> ListarMovimientosAsync(Guid miembroId, CancellationToken cancelacion = default);

    /// <summary>Ajuste manual a favor o en contra, con motivo y responsable; queda en la auditoría y baja a las cajas.</summary>
    Task<RespuestaAjustePuntos> AjustarAsync(Guid miembroId, int puntos, string motivo, UsuarioAuditoria actor, CancellationToken cancelacion = default);

    /// <summary>Recalcula los miembros cuyos puntos ya vencieron, para que las cajas reciban el saldo al día (RF-242).</summary>
    /// <returns>Cuántos miembros cambiaron de saldo.</returns>
    Task<int> VencerPuntosAsync(int maximo, CancellationToken cancelacion = default);
}
