using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Envía la bandeja de salida al Central en orden de creación (RF-270): cada mensaje pasa a en proceso, se envía con su clave de idempotencia
/// y queda confirmado o con error y su próximo intento con espera progresiva. Si no hay comunicación se detiene el lote: la caja sigue operando
/// y reintenta en el próximo ciclo (RF-268). Al confirmarse un e-CF, su XML pasa de Pendientes a Enviados (RF-219, RN-19).
/// </summary>
internal sealed class ProcesadorBandejaSalida(
    ContextoDatosPos contexto,
    IClienteCentral central,
    IEstadoConexionCentral conexion,
    IContextoCaja contextoCaja,
    IRitmosOperacion ritmos,
    OpcionesSincronizacion opciones,
    TimeProvider reloj,
    ILogger<ProcesadorBandejaSalida> registro) : IProcesadorBandejaSalida
{
    /// <summary>Mensajes cuyo documento lleva su e-CF firmado en la propiedad <c>ecf</c>.</summary>
    private static IReadOnlySet<string> TiposConEcf => TiposMensaje.ConEcf;

    public async Task<ResultadoProcesoBandeja> ProcesarAsync(CancellationToken cancelacion = default)
    {
        if (!central.Configurado)
            return new ResultadoProcesoBandeja(0, 0, 0);

        var (sucursalCodigo, cajaCodigo) = (contextoCaja.SucursalCodigo ?? string.Empty, contextoCaja.CajaCodigo ?? string.Empty);
        var esperas = await ritmos.EsperasAsync(cancelacion);
        var ahora = reloj.Ahora();

        // Mensajes que quedaron en proceso por un cierre inesperado de la caja: se vuelven a enviar (el Central no los duplica).
        var interrumpidos = await contexto.BandejaSalida.Where(m => m.Estado == EstadoMensajeSalida.EnProceso).ToListAsync(cancelacion);
        foreach (var interrumpido in interrumpidos)
            interrumpido.RegistrarFallo("Envío interrumpido por un cierre de la caja.", ahora);
        if (interrumpidos.Count > 0)
            await contexto.SaveChangesAsync(cancelacion);

        var mensajes = await contexto.BandejaSalida
            .Where(m => (m.Estado == EstadoMensajeSalida.Pendiente || m.Estado == EstadoMensajeSalida.Error) && m.ProximoIntentoEn <= ahora)
            .OrderBy(m => m.CreadoEn)
            .Take(await ritmos.TamanoLoteAsync(cancelacion))
            .ToListAsync(cancelacion);

        var confirmados = 0;
        var fallidos = 0;
        foreach (var mensaje in mensajes)
        {
            mensaje.MarcarEnProceso();
            await contexto.SaveChangesAsync(cancelacion);

            ResultadoEnvioCentral resultado;
            try
            {
                resultado = await central.EnviarAsync(
                    new MensajeSincronizacion(mensaje.Id, mensaje.TipoMensaje, mensaje.Referencia, mensaje.Contenido, mensaje.HashContenido, sucursalCodigo, cajaCodigo, mensaje.CreadoEn),
                    cancelacion);
            }
            catch (Exception excepcion) when (excepcion is not OperationCanceledException)
            {
                resultado = ResultadoEnvioCentral.SinConexion(excepcion.Message);
            }

            var momento = reloj.Ahora();
            if (resultado.Confirmado)
            {
                mensaje.MarcarConfirmado(momento);
                var borrarOriginal = await ConfirmarComprobanteAsync(mensaje, momento, cancelacion);
                await contexto.SaveChangesAsync(cancelacion);
                borrarOriginal?.Invoke();
                conexion.RegistrarContacto(momento);
                confirmados++;

                // Qué se subió, no solo cuántos: al revisar el registro hace falta saber si era una factura o el ingreso
                // de un cajero, y la referencia es con lo que se busca ese documento en el Central.
                registro.LogInformation("Subido al Central: {Que} ({Referencia}).", TiposMensaje.Nombre(mensaje.TipoMensaje), mensaje.Referencia);
                continue;
            }

            var error = resultado.Error ?? "El Central no confirmó la recepción.";
            mensaje.RegistrarFallo(error, momento + esperas.Para(mensaje.Intentos, rechazado: resultado.CentralRespondio));
            await contexto.SaveChangesAsync(cancelacion);
            fallidos++;

            if (resultado.CentralRespondio)
            {
                registro.LogWarning("El Central rechazó {Que} ({Referencia}): {Error}", TiposMensaje.Nombre(mensaje.TipoMensaje), mensaje.Referencia, error);
                conexion.RegistrarContacto(momento);
                continue;
            }

            // Sin comunicación no tiene sentido seguir con el lote.
            conexion.RegistrarFallo(momento, error);
            break;
        }

        return new ResultadoProcesoBandeja(mensajes.Count, confirmados, fallidos);
    }

    /// <summary>
    /// Marca el e-CF como sincronizado y copia su XML a Enviados. El original en Pendientes se borra solo después de guardar la confirmación:
    /// ningún XML se elimina de la caja antes de que el Central confirme (RN-19).
    /// </summary>
    /// <returns>La acción que borra el XML de Pendientes, o nulo si no hay nada que borrar.</returns>
    private async Task<Action?> ConfirmarComprobanteAsync(MensajeSalida mensaje, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        if (!TiposConEcf.Contains(mensaje.TipoMensaje))
            return null;

        if (EncfDelContenido(mensaje.Contenido) is not { } encf)
            return null;

        var documento = await contexto.DocumentosElectronicos.FirstOrDefaultAsync(d => d.Encf == encf, cancelacion);
        if (documento is not { Estado: EstadoDocumentoElectronico.PendienteSincronizar })
            return null;

        documento.CambiarEstado(EstadoDocumentoElectronico.Sincronizado, "Recibido por el Central.", ahora);

        var origen = documento.RutaXml;
        if (RutasXmlEcf.RutaEnviados(origen, opciones.CarpetaXml) is not { } destino)
            return null;

        try
        {
            if (File.Exists(origen))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                File.Copy(origen, destino, overwrite: true);
            }

            if (!File.Exists(destino))
                return null;

            documento.MoverXml(destino);
            return () => BorrarOriginal(origen);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            // El XML queda en Pendientes; el Central ya lo tiene en el mensaje confirmado.
            registro.LogWarning(excepcion, "No se pudo copiar el XML {Ruta} a Enviados.", origen);
            return null;
        }
    }

    private void BorrarOriginal(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            registro.LogWarning(excepcion, "El XML {Ruta} ya está en Enviados pero no se pudo borrar de Pendientes.", ruta);
        }
    }

    /// <summary>e-NCF del comprobante que lleva el documento en <c>ecf.encf</c>; nulo si no lo lleva o no se puede leer.</summary>
    private static string? EncfDelContenido(string contenido)
    {
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(contenido);
            return json.RootElement.TryGetProperty("ecf", out var ecf) && ecf.ValueKind == System.Text.Json.JsonValueKind.Object
                   && ecf.TryGetProperty("encf", out var encf) && encf.ValueKind == System.Text.Json.JsonValueKind.String
                ? encf.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
