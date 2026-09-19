using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Maestros;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

/// <summary>
/// Rangos de e-CF, roles y usuarios de las cajas desde el Manager. El Manager trabaja con los Id del Central; lo que se publica para las cajas va por código.
/// </summary>
internal sealed class ServicioConfiguracionCajas(ContextoDatosCentral contexto, IPublicadorMaestros publicador, IParametrosCentral parametros)
    : IServicioConfiguracionCajas
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

        // Quién asignó el rango y cuándo: son las columnas que llevan todos los maestros del Central.
        var asignaciones = await contexto.SecuenciasEcf.AsNoTracking()
            .Select(s => new
            {
                s.Id,
                En = EF.Property<DateTimeOffset>(s, Persistencia.Configuraciones.ColumnasMaestro.ModificadoEn),
                Por = EF.Property<string>(s, Persistencia.Configuraciones.ColumnasMaestro.ModificadoPor),
            })
            .ToDictionaryAsync(s => s.Id, cancelacion);

        return (await contexto.SecuenciasEcf.AsNoTracking().ToListAsync(cancelacion))
            .Where(s => cajas.ContainsKey(s.CajaId))
            .Select(s =>
            {
                var caja = cajas[s.CajaId];
                long? ultimo = ultimos.TryGetValue((s.CajaId, s.TipoComprobante), out var secuencia) && secuencia >= s.Desde && secuencia <= s.Hasta ? secuencia : null;
                var asignacion = asignaciones.GetValueOrDefault(s.Id);
                return new DatosSecuenciaEcfCentral(s.Id, s.CajaId, caja.Codigo, sucursales.GetValueOrDefault(caja.SucursalId) ?? string.Empty,
                    s.TipoComprobante, s.Desde, s.Hasta, s.VenceEn, s.Activa, ultimo,
                    Proximo: (ultimo ?? s.Ultimo) + 1,
                    AsignadoEn: asignacion?.En ?? default,
                    AsignadoPor: asignacion?.Por ?? string.Empty);
            })
            .OrderBy(s => s.SucursalCodigo, StringComparer.Ordinal)
            .ThenBy(s => s.CajaCodigo, StringComparer.Ordinal)
            .ThenBy(s => (int)s.TipoComprobante)
            .ThenBy(s => s.Desde)
            .ToList();
    }

    public async Task<ResultadoAdministracion> AsignarSecuenciaAsync(SolicitudSecuenciaEcf solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (!TiposEcfCaja.Asignables.Contains(solicitud.TipoComprobante))
            return ResultadoAdministracion.Error("A una caja solo se le asignan rangos de los e-CF que emite (E31, E32, E34, E44 y E45).");
        var (sucursal, caja) = await CodigosCajaAsync(solicitud.CajaId, cancelacion);
        if (sucursal.Length == 0)
            return ResultadoAdministracion.Error("La caja no existe.");
        if (await contexto.SecuenciasEcf.AnyAsync(s => s.TipoComprobante == solicitud.TipoComprobante && s.Desde == solicitud.Desde, cancelacion))
            return ResultadoAdministracion.Error("Ya hay un rango de ese tipo que empieza en ese número.");

        if (solicitud.Proximo is { } proximo && (proximo < solicitud.Desde || proximo > solicitud.Hasta + 1))
            return ResultadoAdministracion.Error($"El próximo número debe estar entre {solicitud.Desde} y {solicitud.Hasta}.");

        var secuencia = new SecuenciaEcfCarga(sucursal, caja, solicitud.TipoComprobante, solicitud.Desde, solicitud.Hasta, solicitud.VenceEn,
            Proximo: solicitud.Proximo);
        return await PublicarAsync(() => publicador.PublicarAsync(new PaqueteMaestros(SecuenciasEcf: [secuencia]), actor.Nombre, cancelacion),
            async () => await contexto.SecuenciasEcf.Where(s => s.TipoComprobante == secuencia.TipoComprobante && s.Desde == secuencia.Desde)
                .Select(s => (int?)s.Id).SingleOrDefaultAsync(cancelacion));
    }

    public async Task<ResultadoAdministracion> ActualizarSecuenciaAsync(int secuenciaId, SolicitudActualizarSecuenciaEcf solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        var resolutor = new ResolutorCodigosCentral(contexto);
        if ((await TablasMaestros.SecuenciasEcf.PorIdAsync(contexto, resolutor, secuenciaId, cancelacion))?.Dato is not { } anterior)
            return ResultadoAdministracion.Inexistente("El rango de e-CF no existe.");

        var secuencia = anterior with { Hasta = solicitud.Hasta, VenceEn = solicitud.VenceEn, Activa = solicitud.Activa };
        return await PublicarAsync(() => publicador.PublicarAsync(new PaqueteMaestros(SecuenciasEcf: [secuencia]), actor.Nombre, cancelacion),
            () => Task.FromResult<int?>(secuenciaId));
    }

    public async Task<IReadOnlyList<DatosRolCaja>> ListarRolesCajaAsync(CancellationToken cancelacion = default)
    {
        var usuariosPorRol = await contexto.UsuariosCaja.AsNoTracking().GroupBy(u => u.RolId).Select(g => new { g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Cantidad, cancelacion);

        return (await contexto.RolesCaja.AsNoTracking().Include(r => r.PermisosAsignados).ToListAsync(cancelacion))
            .Select(r => new DatosRolCaja(r.Id, r.Codigo, r.Nombre, r.Nivel, r.PermisosAsignados.Select(p => p.PermisoCodigo).OrderBy(p => p, StringComparer.Ordinal).ToList(),
                r.Activo, usuariosPorRol.GetValueOrDefault(r.Id)))
            .OrderBy(r => r.Nivel)
            .ThenBy(r => r.Nombre, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<ResultadoAdministracion> GuardarRolCajaAsync(int? rolId, SolicitudRolCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var codigo = solicitud.Codigo?.Trim() ?? string.Empty;
        if (rolId is { } id)
        {
            if (await contexto.RolesCaja.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancelacion) is not { } actual)
                return ResultadoAdministracion.Inexistente("El rol de caja no existe.");
            if (!string.Equals(actual.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
                return ResultadoAdministracion.Error($"Rol de caja '{codigo}': el código del rol no se puede cambiar.");
        }
        else if (await contexto.RolesCaja.AnyAsync(r => r.Codigo == codigo, cancelacion))
        {
            return ResultadoAdministracion.Error($"Ya existe un rol de caja con el código '{codigo}'.");
        }

        var rol = new RolCarga(codigo, solicitud.Nombre ?? string.Empty, solicitud.Nivel, solicitud.Permisos ?? [], solicitud.Activo);
        return await PublicarAsync(() => publicador.PublicarSeguridadCajasAsync([rol], [], [], actor.Nombre, cancelacion),
            async () => await contexto.RolesCaja.Where(r => r.Codigo == codigo).Select(r => (int?)r.Id).SingleOrDefaultAsync(cancelacion));
    }

    public async Task<IReadOnlyList<DatosUsuarioCaja>> ListarUsuariosCajaAsync(CancellationToken cancelacion = default)
    {
        var roles = await contexto.RolesCaja.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Nombre, cancelacion);

        return (await contexto.UsuariosCaja.AsNoTracking().Include(u => u.CajasAsignadas).ToListAsync(cancelacion))
            .Select(u => new DatosUsuarioCaja(u.Id, u.Codigo, u.Nombre, u.RolId, roles.GetValueOrDefault(u.RolId) ?? string.Empty,
                u.CajasAsignadas.Select(c => c.CajaId).ToList(), u.ClaveHash is not null, u.Activo))
            .OrderBy(u => u.Codigo, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ResultadoAdministracion> GuardarUsuarioCajaAsync(int? usuarioId, SolicitudUsuarioCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var codigo = solicitud.Codigo?.Trim() ?? string.Empty;
        if (usuarioId is { } id)
        {
            if (await contexto.UsuariosCaja.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancelacion) is not { } actual)
                return ResultadoAdministracion.Inexistente("El usuario de caja no existe.");
            if (!string.Equals(actual.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
                return ResultadoAdministracion.Error($"Usuario de caja '{codigo}': el código del usuario no se puede cambiar.");
        }
        else if (await contexto.UsuariosCaja.AnyAsync(u => u.Codigo == codigo, cancelacion))
        {
            return ResultadoAdministracion.Error($"Ya existe un usuario de caja con el código '{codigo}'.");
        }

        var claveNueva = string.IsNullOrEmpty(solicitud.Clave) ? null : solicitud.Clave;
        if (claveNueva is not null)
        {
            var largoMinimo = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.LargoMinimoContrasena, cancelacion);
            if (claveNueva.Length < largoMinimo)
                return ResultadoAdministracion.Error($"La clave debe tener al menos {largoMinimo} caracteres.");
        }

        var rolCodigo = await contexto.RolesCaja.Where(r => r.Id == solicitud.RolId).Select(r => r.Codigo).SingleOrDefaultAsync(cancelacion);
        if (rolCodigo is null)
            return ResultadoAdministracion.Error("El rol de caja no existe.");

        var cajas = new List<CajaReferencia>();
        foreach (var cajaId in (solicitud.Cajas ?? []).Distinct())
        {
            var (sucursal, caja) = await CodigosCajaAsync(cajaId, cancelacion);
            if (sucursal.Length == 0)
                return ResultadoAdministracion.Error("Una de las cajas no existe.");
            cajas.Add(new CajaReferencia(sucursal, caja));
        }

        var usuario = new UsuarioCarga(codigo, solicitud.Nombre ?? string.Empty, rolCodigo, cajas, Clave: claveNueva, Activo: solicitud.Activo);
        return await PublicarAsync(() => publicador.PublicarSeguridadCajasAsync([], [usuario], [], actor.Nombre, cancelacion),
            async () => await contexto.UsuariosCaja.Where(u => u.Codigo == codigo).Select(u => (int?)u.Id).SingleOrDefaultAsync(cancelacion));
    }

    /// <summary>Códigos de sucursal y caja; vacíos si la caja no existe.</summary>
    private async Task<(string Sucursal, string Caja)> CodigosCajaAsync(int cajaId, CancellationToken cancelacion)
    {
        var codigos = await contexto.Cajas.AsNoTracking().Where(c => c.Id == cajaId)
            .Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, Caja = c.Codigo })
            .SingleOrDefaultAsync(cancelacion);
        return codigos is null ? (string.Empty, string.Empty) : (codigos.Sucursal, codigos.Caja);
    }

    private static async Task<ResultadoAdministracion> PublicarAsync(Func<Task<ResultadoPublicacion>> publicar, Func<Task<int?>> id)
    {
        try
        {
            await publicar();
            return ResultadoAdministracion.Correcto(await id());
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }
    }
}
