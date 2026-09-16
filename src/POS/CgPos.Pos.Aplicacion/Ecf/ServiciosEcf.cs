using System.Security.Cryptography.X509Certificates;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Aplicacion.Ecf;

/// <summary>
/// Certificado digital de la caja (RF-217). El PIN solo se usa para abrir el .p12: no se guarda. La clave privada queda en
/// memoria del Agente durante la jornada y se pierde al reiniciarlo, obligando a digitar el PIN de nuevo.
/// </summary>
public interface ICertificadoCaja
{
    bool Configurado { get; }

    bool Cargado { get; }

    DateTimeOffset? VenceEn { get; }

    string? Sujeto { get; }

    /// <returns>Nulo si se cargó; el motivo si no.</returns>
    string? Cargar(string pin);

    /// <summary>Certificado para firmar; nulo si aún no se cargó.</summary>
    X509Certificate2? ObtenerParaFirmar();
}

public interface IServicioEcf
{
    Task<DatosEstadoEcf> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default);

    /// <summary>Carga el certificado con su PIN y lo audita (RF-217).</summary>
    Task<RespuestaCertificado> CargarCertificadoAsync(SesionUsuario sesion, string pin, CancellationToken cancelacion = default);

    Task<IReadOnlyList<DatosDocumentoElectronico>> ListarDocumentosAsync(SesionUsuario sesion, EstadoDocumentoElectronico? estado, int maximo = 100,
        CancellationToken cancelacion = default);
}

/// <param name="Pendientes">Ventas en contingencia que siguen sin e-CF después del intento.</param>
public sealed record ResultadoRegularizacion(int Emitidos, int Pendientes, string? UltimoError);

/// <summary>
/// Emite el e-CF de las ventas cobradas en contingencia en cuanto la caja vuelve a poder firmarlas (RF-224): al cargar el
/// certificado, al recibir una secuencia nueva y en cada ciclo de sincronización.
/// </summary>
public interface IRegularizacionContingencia
{
    Task<ResultadoRegularizacion> RegularizarAsync(Guid cajaId, CancellationToken cancelacion = default);
}

/// <summary>Configuración del e-CF en la caja (sección <c>Ecf</c>).</summary>
public static class ClavesEcf
{
    /// <summary>Ruta del .p12 del certificado de la caja.</summary>
    public const string RutaCertificado = "Ecf:Certificado:Ruta";

    /// <summary>Carpeta base de los XML: se crean Pendientes, Enviados y Errores por fecha (RF-219).</summary>
    public const string CarpetaXml = "Ecf:CarpetaXml";

    /// <summary>Carpeta con los XSD oficiales de la DGII; opcional.</summary>
    public const string CarpetaXsd = "Ecf:CarpetaXsd";

    /// <summary>Pruebas, Certificacion o Produccion.</summary>
    public const string Ambiente = "Ecf:Ambiente";

    /// <summary>Solo desarrollo: crea un certificado autofirmado si no existe y lo carga con este PIN al arrancar.</summary>
    public const string PinDesarrollo = "Ecf:Certificado:PinDesarrollo";

    public const string CarpetaXmlPredeterminada = @"C:\CGPOS\eCF";
}
