using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Actualizaciones;

/// <summary>
/// Actualización remota del Agente de las cajas (H7). El Central publica un paquete y su hash; cada caja lo descarga cuando puede,
/// lo instala con su script y reporta la versión que quedó, para seguir el despliegue sucursal por sucursal.
/// </summary>
public interface IServicioActualizacionesCaja
{
    /// <returns>La versión publicada y su hash; nulo si no hay ninguna publicada o el paquete no está en la carpeta configurada.</returns>
    Task<DatosActualizacionCaja?> PublicadaAsync(CancellationToken cancelacion = default);

    /// <returns>El paquete publicado para descargarlo; nulo si no hay ninguno.</returns>
    Task<Stream?> AbrirPaqueteAsync(CancellationToken cancelacion = default);

    /// <summary>La caja informa qué versión quedó instalada.</summary>
    Task ReportarVersionAsync(int cajaId, string version, CancellationToken cancelacion = default);

    /// <summary>Versión instalada en cada caja, para ver el avance del despliegue desde el Central Manager.</summary>
    Task<IReadOnlyList<DatosVersionCaja>> VersionesAsync(CancellationToken cancelacion = default);
}
