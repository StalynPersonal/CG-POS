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

    /// <summary>Minutos entre revisiones de los puntos de fidelidad que ya vencieron (RF-242).</summary>
    public const string FidelidadMinutosCicloVencimiento = "Central.Fidelidad.MinutosCicloVencimiento";

    /// <summary>Máximo de miembros cuyo saldo se recalcula y republica por ciclo de vencimiento.</summary>
    public const string FidelidadLoteVencimiento = "Central.Fidelidad.LoteVencimiento";

    /// <summary>Carpeta del servidor donde se dejan los paquetes del Agente que descargan las cajas. Opcional: sin ella no hay actualización remota.</summary>
    public const string ActualizacionesCarpetaPaquetes = "Central.Actualizaciones.CarpetaPaquetes";

    /// <summary>Versión del Agente que deben instalar las cajas; el paquete se llama "cgpos-agente-{versión}.zip".</summary>
    public const string ActualizacionesVersionPublicada = "Central.Actualizaciones.VersionPublicada";

    /// <summary>Archivo del padrón de la DGII en el servidor, que las cajas descargan e importan. Opcional.</summary>
    public const string PadronArchivo = "Central.Padron.Archivo";

    /// <summary>Versión del padrón publicado (ej. la fecha de la DGII); la caja solo lo importa si cambió.</summary>
    public const string PadronVersion = "Central.Padron.Version";

    /// <summary>Servidor SMTP de la empresa desde el que el Central envía correos. Opcional: sin él no se envía nada.</summary>
    public const string CorreoServidor = "Central.Correo.Servidor";

    public const string CorreoPuerto = "Central.Correo.Puerto";

    public const string CorreoUsarTls = "Central.Correo.UsarTls";

    /// <summary>Usuario del buzón; su contraseña va en la configuración del servidor (Correo:Contrasena), nunca en los parámetros.</summary>
    public const string CorreoUsuario = "Central.Correo.Usuario";

    public const string CorreoRemitente = "Central.Correo.Remitente";

    public const string CorreoNombreRemitente = "Central.Correo.NombreRemitente";

    /// <summary>Avisa por correo al cliente cuando su pedido queda preparado (RF-256).</summary>
    public const string DespachoAvisarPreparado = "Central.Despacho.AvisarPreparado";

    /// <summary>Minutos entre revisiones de pedidos preparados sin avisar.</summary>
    public const string DespachoMinutosCicloAvisos = "Central.Despacho.MinutosCicloAvisos";

    /// <summary>Máximo de avisos por ciclo.</summary>
    public const string DespachoLoteAvisos = "Central.Despacho.LoteAvisos";
}
