using System.Collections.Concurrent;
using CgPos.Central.Aplicacion.Dgii;

namespace CgPos.Central.Pruebas.Soporte;

/// <summary>
/// DGII de prueba: responde lo que cada prueba programe por e-NCF o por trackId. Lo no programado se recibe y luego se acepta, la búsqueda de un
/// envío anterior no encuentra nada y las anulaciones se aceptan.
/// </summary>
public sealed class ClienteDgiiPrueba : IClienteDgii
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RespuestaDgii>> _envios = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RespuestaDgii>> _consultas = new();
    private readonly ConcurrentDictionary<string, RespuestaDgii> _recuperaciones = new();
    private readonly ConcurrentQueue<RespuestaAnulacionDgii> _anulacionesProgramadas = new();

    public ConcurrentQueue<string> Enviados { get; } = new();

    public ConcurrentQueue<string> Anulaciones { get; } = new();

    public void ProgramarEnvio(string encf, params RespuestaDgii[] respuestas)
    {
        foreach (var respuesta in respuestas)
            _envios.GetOrAdd(encf, _ => new()).Enqueue(respuesta);
    }

    public void ProgramarConsulta(string trackId, params RespuestaDgii[] respuestas)
    {
        foreach (var respuesta in respuestas)
            _consultas.GetOrAdd(trackId, _ => new()).Enqueue(respuesta);
    }

    /// <summary>La DGII ya tiene un envío anterior del e-NCF.</summary>
    public void ProgramarRecuperacion(string encf, RespuestaDgii respuesta) => _recuperaciones[encf] = respuesta;

    public void ProgramarAnulacion(RespuestaAnulacionDgii respuesta) => _anulacionesProgramadas.Enqueue(respuesta);

    public Task<RespuestaDgii> EnviarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default)
    {
        Enviados.Enqueue(comprobante.Encf);
        return Task.FromResult(_envios.TryGetValue(comprobante.Encf, out var cola) && cola.TryDequeue(out var respuesta)
            ? respuesta
            : new RespuestaDgii(ResultadoRespuestaDgii.EnProceso, $"T-{comprobante.Encf}"));
    }

    public Task<RespuestaDgii> ConsultarAsync(string trackId, CancellationToken cancelacion = default) =>
        Task.FromResult(_consultas.TryGetValue(trackId, out var cola) && cola.TryDequeue(out var respuesta)
            ? respuesta
            : new RespuestaDgii(ResultadoRespuestaDgii.Aceptado, trackId));

    public Task<RespuestaDgii?> RecuperarAsync(ComprobanteParaDgii comprobante, CancellationToken cancelacion = default) =>
        Task.FromResult(_recuperaciones.TryGetValue(comprobante.Encf, out var respuesta) ? respuesta : null);

    public Task<RespuestaAnulacionDgii> AnularAsync(string xmlAnulacion, CancellationToken cancelacion = default)
    {
        Anulaciones.Enqueue(xmlAnulacion);
        return Task.FromResult(_anulacionesProgramadas.TryDequeue(out var respuesta)
            ? respuesta with { XmlFirmado = xmlAnulacion }
            : new RespuestaAnulacionDgii(true, false, "Anulación aceptada.", xmlAnulacion));
    }
}
