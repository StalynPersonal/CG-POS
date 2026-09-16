using System.Security.Cryptography.X509Certificates;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;
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

/// <summary>Comprobante emitido: el documento de la caja, lo que viaja al Central y hasta cuándo vale la secuencia.</summary>
public sealed record EmisionEcf(DocumentoElectronico Documento, DocumentoElectronicoParaCentral ParaCentral, DateOnly VenceSecuencia);

/// <summary>No se pudo emitir el e-CF: sin certificado, sin secuencia o con el documento inválido.</summary>
public sealed class EmisionEcfExcepcion(CodigoResultadoVenta codigo, string mensaje) : Exception(mensaje)
{
    public CodigoResultadoVenta Codigo { get; } = codigo;
}

/// <summary>
/// Emisión del e-CF de una venta o de una nota de crédito (RF-218). La implementa el ensamblado de facturación electrónica de la caja
/// (<c>CgPos.Pos.ECF</c>), que es el único que conoce el formato, la firma y los rangos de la DGII.
/// </summary>
public interface IEmisorComprobantes
{
    /// <remarks>Debe llamarse con una transacción abierta en el contexto.</remarks>
    Task<EmisionEcf> EmitirAsync(Venta venta, CancellationToken cancelacion);

    /// <summary>Nota de crédito E34 de una devolución, referenciando el e-CF de la factura (RF-227).</summary>
    Task<EmisionEcf> EmitirNotaCreditoAsync(Devolucion devolucion, CancellationToken cancelacion);

    /// <summary>Si el cobro no llegó a guardarse, su XML no debe quedar como pendiente.</summary>
    void DescartarArchivo(EmisionEcf emision);
}

/// <summary>Carpetas de los XML de e-CF bajo la carpeta base: Pendientes, Enviados y Errores, organizadas por fecha (RF-219, RNF-15).</summary>
public static class RutasXmlEcf
{
    public const string Pendientes = "Pendientes";
    public const string Enviados = "Enviados";

    /// <summary>La misma ruta relativa del XML, en la carpeta Enviados; nulo si el XML no está en la carpeta Pendientes de la base.</summary>
    public static string? RutaEnviados(string rutaPendiente, string carpetaBase)
    {
        var pendientes = Path.GetFullPath(Path.Combine(carpetaBase, Pendientes));
        var completa = Path.GetFullPath(rutaPendiente);
        if (!completa.StartsWith(pendientes + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return null;

        return Path.Combine(Path.GetFullPath(Path.Combine(carpetaBase, Enviados)), Path.GetRelativePath(pendientes, completa));
    }
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
