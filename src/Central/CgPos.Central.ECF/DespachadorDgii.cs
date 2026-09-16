using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dgii;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.ECF;

internal sealed class DespachadorDgii(
    ContextoDatosCentral contexto,
    IClienteDgii cliente,
    IParametrosCentral parametros,
    IAuditoriaCentral auditoria,
    TimeProvider reloj,
    ILogger<DespachadorDgii> registro) : IDespachadorDgii
{
    public async Task<ResultadoCicloDgii> ProcesarAsync(CancellationToken cancelacion = default)
    {
        if (!await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.DgiiHabilitado, cancelacion))
            return ResultadoCicloDgii.Deshabilitado;

        var lote = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DgiiLoteEnvio, cancelacion);
        var reintento = TimeSpan.FromMinutes(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DgiiMinutosReintento, cancelacion));
        var reintentoMaximo = TimeSpan.FromMinutes(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DgiiMinutosMaximoReintento, cancelacion));
        var consulta = TimeSpan.FromSeconds(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.DgiiSegundosConsultaEstado, cancelacion));
        int enviados = 0, aceptados = 0, rechazados = 0, fallidos = 0, enProceso = 0;

        void Contar(EstadoEnvioDgii estado)
        {
            if (estado == EstadoEnvioDgii.Rechazado) rechazados++; else aceptados++;
        }

        var ahora = reloj.GetUtcNow();
        var pendientes = await contexto.ComprobantesRecibidos
            .Where(c => c.EstadoDgii == EstadoEnvioDgii.Pendiente && (c.ProximoIntentoEn == null || c.ProximoIntentoEn <= ahora))
            .OrderBy(c => c.RecibidoEn)
            .Take(lote)
            .ToListAsync(cancelacion);

        foreach (var comprobante in pendientes)
        {
            var respuesta = await LlamarAsync(
                () => cliente.EnviarAsync(new ComprobanteParaDgii(comprobante.Id, comprobante.Encf, comprobante.TipoComprobante, comprobante.XmlFirmado, comprobante.EsResumenConsumo), cancelacion),
                cancelacion);
            ahora = reloj.GetUtcNow();

            switch (respuesta.Resultado)
            {
                case ResultadoRespuestaDgii.EnProceso when !string.IsNullOrWhiteSpace(respuesta.TrackId):
                    comprobante.RegistrarEnvio(respuesta.TrackId, ahora, ahora + consulta);
                    enviados++;
                    enProceso++;
                    break;
                case ResultadoRespuestaDgii.Aceptado or ResultadoRespuestaDgii.AceptadoCondicional or ResultadoRespuestaDgii.Rechazado:
                    Contar(Finalizar(comprobante, respuesta, ahora));
                    enviados++;
                    break;
                default:
                    var motivo = respuesta.Resultado == ResultadoRespuestaDgii.EnProceso
                        ? "La DGII no devolvió el identificador de la recepción (trackId)."
                        : respuesta.Mensaje ?? "No se pudo enviar el e-CF a la DGII.";
                    comprobante.RegistrarFalloEnvio(motivo, ahora, ahora + Espera(comprobante.IntentosEnvio + 1, reintento, reintentoMaximo));
                    registro.LogWarning("No se envió el e-CF {Encf} a la DGII (intento {Intento}): {Motivo}", comprobante.Encf, comprobante.IntentosEnvio, motivo);
                    fallidos++;
                    break;
            }

            // Cada comprobante se guarda al procesarlo: un corte a mitad del lote no repite envíos ya hechos.
            await contexto.SaveChangesAsync(cancelacion);
        }

        ahora = reloj.GetUtcNow();
        var porConsultar = await contexto.ComprobantesRecibidos
            .Where(c => c.EstadoDgii == EstadoEnvioDgii.Enviado && c.TrackId != null && (c.ProximoIntentoEn == null || c.ProximoIntentoEn <= ahora))
            .OrderBy(c => c.EnviadoEn)
            .Take(lote)
            .ToListAsync(cancelacion);

        foreach (var comprobante in porConsultar)
        {
            var respuesta = await LlamarAsync(() => cliente.ConsultarAsync(comprobante.TrackId!, cancelacion), cancelacion);
            ahora = reloj.GetUtcNow();

            switch (respuesta.Resultado)
            {
                case ResultadoRespuestaDgii.Aceptado or ResultadoRespuestaDgii.AceptadoCondicional or ResultadoRespuestaDgii.Rechazado:
                    Contar(Finalizar(comprobante, respuesta, ahora));
                    break;
                case ResultadoRespuestaDgii.EnProceso:
                    comprobante.ProgramarConsulta(ahora + consulta);
                    enProceso++;
                    break;
                default:
                    comprobante.ProgramarConsulta(ahora + reintento, respuesta.Mensaje ?? "No se pudo consultar el resultado en la DGII.");
                    registro.LogWarning("No se consultó el resultado del e-CF {Encf} en la DGII: {Motivo}", comprobante.Encf, respuesta.Mensaje);
                    fallidos++;
                    break;
            }

            await contexto.SaveChangesAsync(cancelacion);
        }

        return new ResultadoCicloDgii(true, enviados, aceptados, rechazados, fallidos, enProceso);
    }

    /// <summary>Espera antes del intento <paramref name="intento"/>: se duplica desde el reintento configurado hasta el máximo.</summary>
    internal static TimeSpan Espera(int intento, TimeSpan reintento, TimeSpan maximo)
    {
        var minutos = reintento.TotalMinutes * Math.Pow(2, Math.Clamp(intento - 1, 0, 20));
        return TimeSpan.FromMinutes(Math.Min(minutos, maximo.TotalMinutes));
    }

    private EstadoEnvioDgii Finalizar(ComprobanteRecibido comprobante, RespuestaDgii respuesta, DateTimeOffset ahora)
    {
        var estado = respuesta.Resultado switch
        {
            ResultadoRespuestaDgii.Aceptado => EstadoEnvioDgii.Aceptado,
            ResultadoRespuestaDgii.AceptadoCondicional => EstadoEnvioDgii.AceptadoCondicional,
            _ => EstadoEnvioDgii.Rechazado,
        };
        comprobante.RegistrarResultado(estado, respuesta.Mensaje, ahora, respuesta.TrackId);

        // Lo aceptado sin observaciones es lo normal; se audita lo que requiere atención.
        if (estado != EstadoEnvioDgii.Aceptado)
        {
            auditoria.Registrar(new EntradaAuditoria($"Dgii.{estado}", "ComprobanteRecibido", comprobante.Id.ToString(),
                Detalle: new { comprobante.Encf, comprobante.CajaId, comprobante.TrackId, respuesta.Mensaje }));
            registro.LogWarning("La DGII respondió {Estado} al e-CF {Encf}: {Mensaje}", estado, comprobante.Encf, respuesta.Mensaje);
        }

        return estado;
    }

    /// <summary>Un error inesperado del cliente no detiene el lote: se trata como fallo reintentable.</summary>
    private async Task<RespuestaDgii> LlamarAsync(Func<Task<RespuestaDgii>> llamada, CancellationToken cancelacion)
    {
        try
        {
            return await llamada();
        }
        catch (Exception excepcion) when (!cancelacion.IsCancellationRequested)
        {
            registro.LogWarning(excepcion, "Error al comunicarse con la DGII");
            return RespuestaDgii.Fallo(excepcion.Message);
        }
    }
}
