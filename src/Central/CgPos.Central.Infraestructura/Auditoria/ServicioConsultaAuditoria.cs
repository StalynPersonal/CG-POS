using System.Text.Json;
using CgPos.Central.Aplicacion.Auditoria;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Auditoria;

internal sealed class ServicioConsultaAuditoria(ContextoDatosCentral contexto) : IServicioConsultaAuditoria
{
    public async Task<PaginaAuditoria> ListarAsync(FiltroAuditoria filtro, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var consulta = contexto.Auditoria.AsNoTracking();

        if (filtro.Desde is { } desde)
        {
            var inicio = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            consulta = consulta.Where(registro => registro.OcurridoEn >= inicio);
        }

        if (filtro.Hasta is { } hasta)
        {
            // El día "hasta" entra completo: se compara contra el arranque del día siguiente.
            var fin = new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            consulta = consulta.Where(registro => registro.OcurridoEn < fin);
        }

        if (filtro.Accion is { Length: > 0 } accion)
            consulta = consulta.Where(registro => registro.Accion == accion);

        if (filtro.TipoEntidad is { Length: > 0 } tipoEntidad)
            consulta = consulta.Where(registro => registro.TipoEntidad == tipoEntidad);

        if (filtro.Usuario is { Length: > 0 } usuario)
            consulta = consulta.Where(registro => registro.UsuarioNombre == usuario);

        if (filtro.SoloConCambios)
            consulta = consulta.Where(registro => registro.Cambios != null);

        if (filtro.Buscar is { Length: > 0 } buscar)
        {
            var texto = buscar.Trim();
            consulta = consulta.Where(registro =>
                registro.Accion.Contains(texto)
                || registro.TipoEntidad.Contains(texto)
                || (registro.EntidadId != null && registro.EntidadId.Contains(texto))
                || (registro.Motivo != null && registro.Motivo.Contains(texto))
                || (registro.UsuarioNombre != null && registro.UsuarioNombre.Contains(texto))
                || (registro.Cambios != null && registro.Cambios.Contains(texto)));
        }

        var total = await consulta.CountAsync(cancelacion);
        var tamano = Math.Clamp(filtro.Tamano, 1, IServicioConsultaAuditoria.TamanoMaximoPagina);

        var registros = await consulta
            .OrderByDescending(registro => registro.OcurridoEn)
            .ThenByDescending(registro => registro.Id)
            .Skip(Math.Max(filtro.Pagina, 0) * tamano)
            .Take(tamano)
            .Select(registro => new
            {
                registro.Id,
                registro.OcurridoEn,
                registro.Accion,
                registro.TipoEntidad,
                registro.EntidadId,
                registro.Motivo,
                registro.UsuarioNombre,
                registro.AutorizadoPorNombre,
                registro.Detalle,
                registro.Cambios,
            })
            .ToListAsync(cancelacion);

        var elementos = registros
            .Select(registro => new DatosRegistroAuditoria(
                registro.Id, registro.OcurridoEn, registro.Accion, registro.TipoEntidad, registro.EntidadId,
                registro.Motivo, registro.UsuarioNombre, registro.AutorizadoPorNombre, registro.Detalle,
                LeerCambios(registro.Cambios)))
            .ToList();

        return new PaginaAuditoria(elementos, total);
    }

    public async Task<OpcionesAuditoria> OpcionesAsync(CancellationToken cancelacion = default)
    {
        var acciones = await contexto.Auditoria.AsNoTracking()
            .Select(registro => registro.Accion).Distinct().OrderBy(accion => accion).ToListAsync(cancelacion);

        var entidades = await contexto.Auditoria.AsNoTracking()
            .Select(registro => registro.TipoEntidad).Distinct().OrderBy(entidad => entidad).ToListAsync(cancelacion);

        var usuarios = await contexto.Auditoria.AsNoTracking()
            .Where(registro => registro.UsuarioNombre != null)
            .Select(registro => registro.UsuarioNombre!).Distinct().OrderBy(usuario => usuario).ToListAsync(cancelacion);

        return new OpcionesAuditoria(acciones, entidades, usuarios);
    }

    /// <summary>Un registro viejo o con un JSON que no se entiende se muestra igual, solo que sin el antes y el después.</summary>
    private static IReadOnlyList<DatosEntidadAuditoria> LeerCambios(string? cambios)
    {
        if (string.IsNullOrWhiteSpace(cambios))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<DatosEntidadAuditoria>>(cambios, OpcionesJson.Predeterminadas) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
