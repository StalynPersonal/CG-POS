using System.Text.Json;
using CgPos.Contracts.Serializacion;
using CgPos.Pos.Application.Abstracciones;
using CgPos.Pos.Application.Sincronizacion;
using CgPos.Pos.Infrastructure.Persistencia;

namespace CgPos.Pos.Infrastructure.Sincronizacion;

internal sealed class OutboxEscritor(PosDbContext db, TimeProvider reloj) : IOutbox
{
    public Guid Encolar<T>(string tipoMensaje, Guid agregadoId, T contenido)
    {
        var json = JsonSerializer.Serialize(contenido, OpcionesJson.Predeterminadas);
        var mensaje = MensajeOutbox.Crear(tipoMensaje, agregadoId, json, reloj.GetUtcNow());
        db.Outbox.Add(mensaje);
        return mensaje.Id;
    }
}
