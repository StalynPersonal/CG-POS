using System.Text.Json;
using CgPos.Contracts.Serializacion;
using CgPos.Domain.Auditoria;
using CgPos.Pos.Application.Abstracciones;
using CgPos.Pos.Infrastructure.Persistencia;

namespace CgPos.Pos.Infrastructure.Auditoria;

internal sealed class AuditoriaEscritor(PosDbContext db, TimeProvider reloj) : IAuditoria
{
    public void Registrar(EntradaAuditoria entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var registro = RegistroAuditoria.Crear(
            ocurridoEn: reloj.GetUtcNow(),
            accion: entrada.Accion,
            tipoEntidad: entrada.TipoEntidad,
            entidadId: entrada.EntidadId,
            detalleJson: entrada.Detalle is null ? null : JsonSerializer.Serialize(entrada.Detalle, OpcionesJson.Predeterminadas),
            motivo: entrada.Motivo,
            usuarioId: entrada.Usuario?.Id,
            usuarioNombre: entrada.Usuario?.Nombre,
            autorizadoPorId: entrada.AutorizadoPor?.Id,
            autorizadoPorNombre: entrada.AutorizadoPor?.Nombre);

        db.Auditoria.Add(registro);
    }
}
