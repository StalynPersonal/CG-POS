using System.Text.Json;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Auditoria;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Auditoria;

internal sealed class EscritorAuditoria(ContextoDatosPos contexto, TimeProvider reloj) : IAuditoria
{
    public void Registrar(EntradaAuditoria entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        var registro = RegistroAuditoria.Crear(
            ocurridoEn: reloj.Ahora(),
            accion: entrada.Accion,
            tipoEntidad: entrada.TipoEntidad,
            entidadId: entrada.EntidadId,
            detalleJson: entrada.Detalle is null ? null : JsonSerializer.Serialize(entrada.Detalle, OpcionesJson.Predeterminadas),
            motivo: entrada.Motivo,
            usuarioId: entrada.Usuario?.Id,
            usuarioNombre: entrada.Usuario?.Nombre,
            autorizadoPorId: entrada.AutorizadoPor?.Id,
            autorizadoPorNombre: entrada.AutorizadoPor?.Nombre);

        contexto.Auditoria.Add(registro);
    }
}
