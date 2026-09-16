using System.Collections.Concurrent;
using CgPos.Central.Aplicacion.Dgii;

namespace CgPos.Central.Pruebas.Soporte;

/// <summary>DGII de prueba: responde lo que cada prueba programe por e-NCF o por trackId; lo no programado se recibe y luego se acepta.</summary>
public sealed class ClienteDgiiPrueba : IClienteDgii
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RespuestaDgii>> _envios = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<RespuestaDgii>> _consultas = new();

    public ConcurrentQueue<string> Enviados { get; } = new();

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
}
