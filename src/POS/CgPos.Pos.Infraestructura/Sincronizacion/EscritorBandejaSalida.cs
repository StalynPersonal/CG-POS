using System.Text.Json;
using CgPos.Contratos.Serializacion;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

internal sealed class EscritorBandejaSalida(ContextoDatosPos contexto, TimeProvider reloj) : IBandejaSalida
{
    public Guid Encolar<T>(string tipoMensaje, Guid agregadoId, T contenido)
    {
        var json = JsonSerializer.Serialize(contenido, OpcionesJson.Predeterminadas);
        var mensaje = MensajeSalida.Crear(tipoMensaje, agregadoId, json, reloj.GetUtcNow());
        contexto.BandejaSalida.Add(mensaje);
        return mensaje.Id;
    }
}
