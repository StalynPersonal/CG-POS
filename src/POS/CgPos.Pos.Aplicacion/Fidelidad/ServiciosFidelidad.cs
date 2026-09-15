using CgPos.Contratos.Fidelidad;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Fidelidad;

/// <summary>Programa de fidelidad en la caja (M11): la cédula identifica al cliente y todo funciona sin conexión con el último saldo sincronizado.</summary>
public interface IServicioFidelidad
{
    /// <summary>Saldo de puntos, nivel y próximo vencimiento del miembro (RF-240).</summary>
    Task<RespuestaFidelidad> ConsultarAsync(SesionUsuario sesion, string cedula, CancellationToken cancelacion = default);

    /// <summary>Inscribe al cliente desde la caja sin interrumpir la venta (RF-237); la inscripción viaja al Central.</summary>
    Task<RespuestaFidelidad> InscribirAsync(SesionUsuario sesion, SolicitudInscripcionFidelidad solicitud, CancellationToken cancelacion = default);
}
