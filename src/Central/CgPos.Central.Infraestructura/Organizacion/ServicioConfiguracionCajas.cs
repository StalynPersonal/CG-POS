using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

internal sealed class ServicioConfiguracionCajas(ContextoDatosCentral contexto, IPublicadorMaestros publicador) : IServicioConfiguracionCajas
{
    public async Task<IReadOnlyList<DatosSecuenciaEcfCentral>> ListarSecuenciasAsync(CancellationToken cancelacion = default)
    {
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, cancelacion);

        // e-NCF de largo fijo con ceros a la izquierda: el mayor en texto es la mayor secuencia recibida.
        var ultimos = (await contexto.ComprobantesRecibidos.AsNoTracking()
                .GroupBy(c => new { c.CajaId, c.TipoComprobante })
                .Select(g => new { g.Key.CajaId, g.Key.TipoComprobante, Encf = g.Max(c => c.Encf)! })
                .ToListAsync(cancelacion))
            .ToDictionary(u => (u.CajaId, u.TipoComprobante), u => long.Parse(u.Encf[3..], System.Globalization.CultureInfo.InvariantCulture));

        return (await CargarAsync<SecuenciaEcfCarga>(TipoMaestro.SecuenciaEcf, cancelacion))
            .Where(s => cajas.ContainsKey(s.CajaId))
            .Select(s =>
            {
                var caja = cajas[s.CajaId];
                long? ultimo = ultimos.TryGetValue((s.CajaId, s.TipoComprobante), out var secuencia) && secuencia >= s.Desde && secuencia <= s.Hasta ? secuencia : null;
                return new DatosSecuenciaEcfCentral(s.Id, s.CajaId, caja.Codigo, sucursales.GetValueOrDefault(caja.SucursalId) ?? string.Empty, s.TipoComprobante,
                    s.Desde, s.Hasta, s.VenceEn, s.Activa, ultimo);
            })
            .OrderBy(s => s.SucursalCodigo, StringComparer.Ordinal)
            .ThenBy(s => s.CajaCodigo, StringComparer.Ordinal)
            .ThenBy(s => (int)s.TipoComprobante)
            .ThenBy(s => s.Desde)
            .ToList();
    }

    public Task<ResultadoAdministracion> AsignarSecuenciaAsync(SolicitudSecuenciaEcf solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (!TiposEcfCaja.Asignables.Contains(solicitud.TipoComprobante))
            return Task.FromResult(ResultadoAdministracion.Error("A una caja solo se le asignan rangos de los e-CF que emite (E31, E32, E34, E44 y E45)."));

        var secuencia = new SecuenciaEcfCarga(Guid.CreateVersion7(), solicitud.CajaId, solicitud.TipoComprobante, solicitud.Desde, solicitud.Hasta, solicitud.VenceEn);
        return PublicarAsync(() => publicador.PublicarAsync(new PaqueteMaestros(SecuenciasEcf: [secuencia]), actor.Nombre, cancelacion), secuencia.Id);
    }

    public async Task<ResultadoAdministracion> ActualizarSecuenciaAsync(Guid secuenciaId, SolicitudActualizarSecuenciaEcf solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        if (await BuscarAsync<SecuenciaEcfCarga>(TipoMaestro.SecuenciaEcf, secuenciaId, cancelacion) is not { } anterior)
            return ResultadoAdministracion.Inexistente("El rango de e-CF no existe.");

        var secuencia = anterior with { Hasta = solicitud.Hasta, VenceEn = solicitud.VenceEn, Activa = solicitud.Activa };
        return await PublicarAsync(() => publicador.PublicarAsync(new PaqueteMaestros(SecuenciasEcf: [secuencia]), actor.Nombre, cancelacion), secuencia.Id);
    }

    public async Task<IReadOnlyList<DatosRolCaja>> ListarRolesCajaAsync(CancellationToken cancelacion = default)
    {
        var usuariosPorRol = (await CargarAsync<UsuarioCarga>(TipoMaestro.UsuarioCaja, cancelacion)).GroupBy(u => u.RolId).ToDictionary(g => g.Key, g => g.Count());

        return (await CargarAsync<RolCarga>(TipoMaestro.RolCaja, cancelacion))
            .Select(r => new DatosRolCaja(r.Id, r.Codigo, r.Nombre, r.Nivel, r.Permisos ?? [], r.Activo, usuariosPorRol.GetValueOrDefault(r.Id)))
            .OrderBy(r => r.Nivel)
            .ThenBy(r => r.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<ResultadoAdministracion> GuardarRolCajaAsync(Guid? rolId, SolicitudRolCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (rolId is { } id && await BuscarAsync<RolCarga>(TipoMaestro.RolCaja, id, cancelacion) is null)
            return ResultadoAdministracion.Inexistente("El rol de caja no existe.");

        var rol = new RolCarga(rolId ?? Guid.CreateVersion7(), solicitud.Codigo?.Trim() ?? string.Empty, solicitud.Nombre ?? string.Empty, solicitud.Nivel,
            solicitud.Permisos ?? [], solicitud.Activo);
        return await PublicarAsync(() => publicador.PublicarSeguridadCajasAsync([rol], [], [], actor.Nombre, cancelacion), rol.Id);
    }

    public async Task<IReadOnlyList<DatosUsuarioCaja>> ListarUsuariosCajaAsync(CancellationToken cancelacion = default)
    {
        var roles = (await CargarAsync<RolCarga>(TipoMaestro.RolCaja, cancelacion)).ToDictionary(r => r.Id, r => r.Nombre);

        return (await CargarAsync<UsuarioCarga>(TipoMaestro.UsuarioCaja, cancelacion))
            .Select(u => new DatosUsuarioCaja(u.Id, u.Codigo, u.Nombre, u.RolId, roles.GetValueOrDefault(u.RolId) ?? string.Empty, u.Cajas ?? [],
                u.PinHash is not null, u.CredencialBarrasHash is not null, u.Activo))
            .OrderBy(u => u.Codigo, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ResultadoAdministracion> GuardarUsuarioCajaAsync(Guid? usuarioId, SolicitudUsuarioCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        UsuarioCarga? anterior = null;
        if (usuarioId is { } id && (anterior = await BuscarAsync<UsuarioCarga>(TipoMaestro.UsuarioCaja, id, cancelacion)) is null)
            return ResultadoAdministracion.Inexistente("El usuario de caja no existe.");

        var pinNuevo = string.IsNullOrWhiteSpace(solicitud.Pin) ? null : solicitud.Pin.Trim();
        var carneNuevo = string.IsNullOrWhiteSpace(solicitud.Carne) ? null : solicitud.Carne.Trim();

        var usuario = new UsuarioCarga(
            usuarioId ?? Guid.CreateVersion7(),
            solicitud.Codigo?.Trim() ?? string.Empty,
            solicitud.Nombre ?? string.Empty,
            solicitud.RolId,
            solicitud.Cajas ?? [],
            Pin: pinNuevo,
            PinHash: pinNuevo is null ? anterior?.PinHash : null,
            CredencialBarras: carneNuevo,
            CredencialBarrasHash: carneNuevo is not null || solicitud.QuitarCarne ? null : anterior?.CredencialBarrasHash,
            Activo: solicitud.Activo);

        return await PublicarAsync(() => publicador.PublicarSeguridadCajasAsync([], [usuario], [], actor.Nombre, cancelacion), usuario.Id);
    }

    private async Task<List<T>> CargarAsync<T>(TipoMaestro tipo, CancellationToken cancelacion) =>
        (await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == tipo).ToListAsync(cancelacion)).Select(FormatoMaestros.Leer<T>).ToList();

    private async Task<T?> BuscarAsync<T>(TipoMaestro tipo, Guid id, CancellationToken cancelacion) where T : class =>
        await contexto.MaestrosCentral.AsNoTracking().SingleOrDefaultAsync(m => m.Tipo == tipo && m.Id == id, cancelacion) is { } fila ? FormatoMaestros.Leer<T>(fila) : null;

    private static async Task<ResultadoAdministracion> PublicarAsync(Func<Task<ResultadoPublicacion>> publicar, Guid id)
    {
        try
        {
            await publicar();
            return ResultadoAdministracion.Correcto(id);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }
    }
}
