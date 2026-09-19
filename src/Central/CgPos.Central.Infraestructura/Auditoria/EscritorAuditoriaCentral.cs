using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Auditoria;

internal sealed class EscritorAuditoriaCentral(ContextoDatosCentral contexto, TimeProvider reloj) : IAuditoriaCentral
{
    public void Registrar(EntradaAuditoria entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        contexto.Auditoria.Add(RegistroAuditoria.Crear(
            ocurridoEn: reloj.Ahora(),
            accion: entrada.Accion,
            tipoEntidad: entrada.TipoEntidad,
            entidadId: entrada.EntidadId,
            detalleJson: entrada.Detalle is null ? null : JsonSerializer.Serialize(entrada.Detalle, OpcionesJson.Predeterminadas),
            motivo: entrada.Motivo,
            usuarioId: entrada.Usuario?.Id,
            usuarioNombre: entrada.Usuario?.Nombre));
    }
}
