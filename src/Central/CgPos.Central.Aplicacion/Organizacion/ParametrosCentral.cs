using System.Globalization;
using CgPos.Dominio.Organizacion;

namespace CgPos.Central.Aplicacion.Organizacion;

/// <summary>Parámetros generales que rigen el propio Central. Los de sucursal y caja se distribuyen a las cajas.</summary>
public interface IParametrosCentral
{
    Task<string?> ObtenerAsync(string clave, CancellationToken cancelacion = default);
}

public static class ParametrosCentralExtensiones
{
    /// <exception cref="ParametroNoConfiguradoExcepcion">El parámetro no existe o está vacío.</exception>
    public static async Task<string> ObtenerRequeridoAsync(this IParametrosCentral parametros, string clave, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cancelacion) is { Length: > 0 } valor ? valor : throw new ParametroNoConfiguradoExcepcion(clave);

    /// <exception cref="ParametroNoConfiguradoExcepcion">No existe o no es un entero mayor que cero.</exception>
    public static async Task<int> ObtenerEnteroPositivoAsync(this IParametrosCentral parametros, string clave, CancellationToken cancelacion = default) =>
        int.TryParse(await parametros.ObtenerRequeridoAsync(clave, cancelacion), NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) && valor > 0
            ? valor
            : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser un número entero mayor que cero");

    /// <summary>Para reglas que el negocio puede no activar: <c>false</c> si no está configurado, error si está mal escrito.</summary>
    public static async Task<bool> ObtenerBooleanoOpcionalAsync(this IParametrosCentral parametros, string clave, CancellationToken cancelacion = default) =>
        await parametros.ObtenerAsync(clave, cancelacion) is not { Length: > 0 } texto ? false
        : bool.TryParse(texto, out var valor) ? valor
        : throw new ParametroNoConfiguradoExcepcion(clave, "debe ser true o false");
}

/// <summary>Claves de los parámetros del Central. Los valores numéricos usan formato invariante.</summary>
public static class ClavesParametrosCentral
{
    /// <summary>Intentos de contraseña fallidos seguidos que bloquean al usuario del Central.</summary>
    public const string IntentosMaximos = "Central.Seguridad.IntentosMaximos";

    /// <summary>Minutos que dura el bloqueo por intentos fallidos.</summary>
    public const string MinutosBloqueo = "Central.Seguridad.MinutosBloqueo";

    /// <summary>Minutos de vigencia del token de acceso; al vencer, la pantalla lo renueva sin pedir la contraseña.</summary>
    public const string MinutosTokenAcceso = "Central.Seguridad.MinutosToken";

    /// <summary>Minutos sin actividad tras los que la sesión vence y hay que volver a ingresar.</summary>
    public const string MinutosInactividad = "Central.Seguridad.MinutosInactividad";

    /// <summary>Horas máximas de una sesión, aunque se siga renovando.</summary>
    public const string HorasSesion = "Central.Seguridad.HorasSesion";

    /// <summary>Largo mínimo de las contraseñas del Central.</summary>
    public const string LargoMinimoContrasena = "Central.Seguridad.LargoMinimoContrasena";

    /// <summary>Exige mayúscula, minúscula, número y símbolo, sin contener el usuario. Opcional: sin él solo se exige el largo.</summary>
    public const string ContrasenaCompleja = "Central.Seguridad.ContrasenaCompleja";

    /// <summary>Minutos de vigencia del token con el que una caja se comunica con el Central.</summary>
    public const string MinutosTokenDispositivo = "Central.Dispositivos.MinutosToken";

    /// <summary>Activa el envío a la DGII de los e-CF recibidos. Opcional: sin él no se envía nada.</summary>
    public const string DgiiHabilitado = "Central.Dgii.Habilitado";

    /// <summary>Dirección base (https) de los servicios de e-CF de la DGII: ambiente de pruebas, certificación o producción.</summary>
    public const string DgiiUrlBase = "Central.Dgii.UrlBase";

    /// <summary>Segundos entre ciclos de envío y consulta.</summary>
    public const string DgiiSegundosCiclo = "Central.Dgii.SegundosCiclo";

    /// <summary>Máximo de e-CF que se envían (y de resultados que se consultan) por ciclo.</summary>
    public const string DgiiLoteEnvio = "Central.Dgii.LoteEnvio";

    /// <summary>Minutos de espera tras el primer envío fallido; se duplica en cada fallo seguido.</summary>
    public const string DgiiMinutosReintento = "Central.Dgii.MinutosReintento";

    /// <summary>Espera máxima entre reintentos de envío.</summary>
    public const string DgiiMinutosMaximoReintento = "Central.Dgii.MinutosMaximoReintento";

    /// <summary>Segundos entre consultas del resultado de un e-CF recibido por la DGII.</summary>
    public const string DgiiSegundosConsultaEstado = "Central.Dgii.SegundosConsultaEstado";

    /// <summary>Minutos sin mensajes ni descargas tras los que el monitor alerta que una caja habilitada no se comunica.</summary>
    public const string MonitorMinutosSinComunicacion = "Central.Monitor.MinutosSinComunicacion";

    /// <summary>Minutos desde la recepción tras los que un e-CF sin resultado de la DGII es una alerta.</summary>
    public const string MonitorMinutosAlertaDgii = "Central.Monitor.MinutosAlertaDgii";

    /// <summary>Minutos que el Central retiene el saldo de una nota de crédito mientras una caja termina de cobrar.</summary>
    public const string NotasCreditoMinutosReserva = "Central.NotasCredito.MinutosReserva";

    /// <summary>Meses desde la emisión hasta los que se puede habilitar una nota de crédito vencida (RF-40).</summary>
    public const string NotasCreditoMesesMaximoProrroga = "Central.NotasCredito.MesesMaximoProrroga";
}
