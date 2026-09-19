using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Sincronizacion;

internal sealed class ServicioMonitorCentral(ContextoDatosCentral contexto, IParametrosCentral parametros, IAuditoriaCentral auditoria, TimeProvider reloj)
    : IServicioMonitorCentral
{
    public async Task<DatosMonitorCentral> ObtenerAsync(CancellationToken cancelacion = default)
    {
        var minutosSinComunicacion = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MonitorMinutosSinComunicacion, cancelacion);
        var minutosAlertaDgii = await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.MonitorMinutosAlertaDgii, cancelacion);
        var ahora = reloj.Ahora();
        var limiteComunicacion = ahora.AddMinutes(-minutosSinComunicacion);
        var limiteDgii = ahora.AddMinutes(-minutosAlertaDgii);

        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToListAsync(cancelacion);
        var estados = await contexto.EstadosSincronizacionCaja.AsNoTracking().ToDictionaryAsync(e => e.CajaId, cancelacion);
        var comprobantes = await contexto.ComprobantesRecibidos.AsNoTracking()
            .GroupBy(c => new { c.CajaId, c.EstadoDgii })
            .Select(g => new { g.Key.CajaId, g.Key.EstadoDgii, Cantidad = g.Count(), MasAntiguo = g.Min(c => c.RecibidoEn) })
            .ToListAsync(cancelacion);
        var sinResultado = (await contexto.ComprobantesRecibidos.AsNoTracking()
                .Where(c => (c.EstadoDgii == EstadoEnvioDgii.Pendiente || c.EstadoDgii == EstadoEnvioDgii.Enviado) && c.RecibidoEn < limiteDgii)
                .GroupBy(c => c.CajaId)
                .Select(g => new { CajaId = g.Key, Cantidad = g.Count() })
                .ToListAsync(cancelacion))
            .ToDictionary(c => c.CajaId, c => c.Cantidad);
        var conflictos = (await contexto.ConflictosSincronizacion.AsNoTracking()
                .Where(c => c.ResueltoEn == null)
                .GroupBy(c => c.CajaId)
                .Select(g => new { CajaId = g.Key, Cantidad = g.Count() })
                .ToListAsync(cancelacion))
            .ToDictionary(c => c.CajaId, c => c.Cantidad);
        var conFallo = await contexto.ComprobantesRecibidos.CountAsync(c => c.EstadoDgii == EstadoEnvioDgii.Pendiente && c.IntentosEnvio > 0, cancelacion);

        int Contar(int cajaId, params EstadoEnvioDgii[] estadosDgii) =>
            comprobantes.Where(c => c.CajaId == cajaId && estadosDgii.Contains(c.EstadoDgii)).Sum(c => c.Cantidad);

        var datosCajas = cajas
            .Select(caja =>
            {
                var estado = estados.GetValueOrDefault(caja.Id);
                var ultima = new[] { estado?.UltimaRecepcionEn, estado?.UltimaDescargaEn }.Max();
                var pendientes = Contar(caja.Id, EstadoEnvioDgii.Pendiente, EstadoEnvioDgii.Enviado);
                var rechazados = Contar(caja.Id, EstadoEnvioDgii.Rechazado);
                var abiertos = conflictos.GetValueOrDefault(caja.Id);

                var alertas = new List<string>();
                if (caja.Habilitada && ultima is null)
                    alertas.Add("Nunca se ha comunicado con el Central");
                else if (caja.Habilitada && ultima < limiteComunicacion)
                    alertas.Add($"Sin comunicación por más de {minutosSinComunicacion} minutos");
                if (estado is { UltimoRechazoEn: { } rechazoEn } && (estado.UltimaRecepcionEn is null || rechazoEn >= estado.UltimaRecepcionEn))
                    alertas.Add("Su último mensaje fue rechazado por el Central");
                if (sinResultado.GetValueOrDefault(caja.Id) is var atrasados and > 0)
                    alertas.Add($"{atrasados} e-CF sin resultado de la DGII por más de {minutosAlertaDgii} minutos");
                if (rechazados > 0)
                    alertas.Add($"{rechazados} e-CF rechazados por la DGII");
                if (abiertos > 0)
                    alertas.Add($"{abiertos} conflicto(s) de sincronización abierto(s)");

                return new DatosEstadoCaja(caja.Id, caja.Codigo, caja.Nombre, sucursales.GetValueOrDefault(caja.SucursalId) ?? string.Empty, caja.Habilitada, ultima,
                    estado?.UltimaRecepcionEn, estado?.UltimaDescargaEn, estado?.MensajesRecibidos ?? 0, estado?.Duplicados ?? 0, estado?.Rechazados ?? 0,
                    estado?.UltimoRechazoEn, estado?.UltimoError, pendientes, rechazados, abiertos, alertas);
            })
            .OrderByDescending(c => c.Alertas.Count > 0)
            .ThenBy(c => c.SucursalCodigo, StringComparer.Ordinal)
            .ThenBy(c => c.CajaCodigo, StringComparer.Ordinal)
            .ToList();

        var porEstado = Enum.GetValues<EstadoEnvioDgii>()
            .Select(e => new DatosConteoEstadoDgii(e, comprobantes.Where(c => c.EstadoDgii == e).Sum(c => c.Cantidad)))
            .ToList();
        var pendienteMasAntiguo = comprobantes.Where(c => c.EstadoDgii == EstadoEnvioDgii.Pendiente).Select(c => (DateTimeOffset?)c.MasAntiguo).Min();

        return new DatosMonitorCentral(ahora, cajas.Count(c => c.Habilitada), datosCajas.Count(c => c.Alertas.Count > 0), conflictos.Values.Sum(),
            porEstado, conFallo, pendienteMasAntiguo, datosCajas);
    }

    public async Task<PaginaComprobantesDgii> BuscarComprobantesAsync(EstadoEnvioDgii? estado, int? sucursalId, int? cajaId, string? buscar, bool soloConFallo, int pagina, int tamano,
        CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioMonitorCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);

        var consulta = contexto.ComprobantesRecibidos.AsNoTracking();
        if (estado is { } filtroEstado)
            consulta = consulta.Where(c => c.EstadoDgii == filtroEstado);
        if (sucursalId is { } filtroSucursal)
            consulta = consulta.Where(c => c.SucursalId == filtroSucursal);
        if (cajaId is { } filtroCaja)
            consulta = consulta.Where(c => c.CajaId == filtroCaja);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim().ToUpperInvariant();
            consulta = consulta.Where(c => c.Encf.Contains(texto) || c.TrackId == buscar.Trim());
        }

        if (soloConFallo)
            consulta = consulta.Where(c => c.EstadoDgii == EstadoEnvioDgii.Pendiente && c.IntentosEnvio > 0);

        var total = await consulta.CountAsync(cancelacion);
        var filas = await consulta
            .OrderByDescending(c => c.RecibidoEn)
            .Skip(pagina * tamano)
            .Take(tamano)
            .Select(c => new
            {
                c.Id, c.Encf, c.TipoComprobante, c.CajaId, c.SucursalId, c.FechaFirma, c.RecibidoEn, c.EstadoDgii, c.EstadoDgiiEn, c.MensajeDgii, c.TrackId,
                c.IntentosEnvio, c.ProximoIntentoEn,
            })
            .ToListAsync(cancelacion);

        var idsCajas = filas.Select(f => f.CajaId).Distinct().ToList();
        var cajas = await contexto.Cajas.AsNoTracking().Where(c => idsCajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, cancelacion);

        return new PaginaComprobantesDgii(
            filas.Select(f => new DatosComprobanteDgii(f.Id, f.Encf, f.TipoComprobante, f.CajaId, cajas.GetValueOrDefault(f.CajaId)?.Codigo ?? string.Empty,
                cajas.GetValueOrDefault(f.CajaId)?.Nombre ?? string.Empty, sucursales.GetValueOrDefault(f.SucursalId)?.Codigo ?? string.Empty,
                sucursales.GetValueOrDefault(f.SucursalId)?.Nombre ?? string.Empty, f.FechaFirma, f.RecibidoEn, f.EstadoDgii, f.EstadoDgiiEn, f.MensajeDgii, f.TrackId,
                f.IntentosEnvio, f.ProximoIntentoEn)).ToList(),
            total);
    }

    public async Task<string?> ObtenerXmlAsync(int comprobanteId, CancellationToken cancelacion = default) =>
        await contexto.ComprobantesRecibidos.AsNoTracking().Where(c => c.Id == comprobanteId).Select(c => c.XmlFirmado).SingleOrDefaultAsync(cancelacion);

    public async Task<ResultadoAdministracion> ReenviarAsync(int comprobanteId, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (await contexto.ComprobantesRecibidos.SingleOrDefaultAsync(c => c.Id == comprobanteId, cancelacion) is not { } comprobante)
            return ResultadoAdministracion.Inexistente("El comprobante no existe.");

        var estadoAnterior = comprobante.EstadoDgii;
        try
        {
            comprobante.PrepararReenvio(reloj.Ahora());
        }
        catch (InvalidOperationException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        auditoria.Registrar(new EntradaAuditoria("Dgii.ReenvioDirigido", "ComprobanteRecibido", comprobante.Id.ToString(),
            Detalle: new { comprobante.Encf, EstadoAnterior = estadoAnterior, Usuario = actor.Nombre }));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(comprobante.Id);
    }

    public async Task<IReadOnlyList<DatosConflictoSincronizacion>> ListarConflictosAsync(bool abiertos, CancellationToken cancelacion = default)
    {
        var conflictos = await contexto.ConflictosSincronizacion.AsNoTracking()
            .Where(c => abiertos ? c.ResueltoEn == null : c.ResueltoEn != null)
            .OrderByDescending(c => c.UltimaOcurrenciaEn)
            .Take(IServicioMonitorCentral.MaximoConflictos)
            .ToListAsync(cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);

        return conflictos
            .Select(c => new DatosConflictoSincronizacion(c.Id, c.CajaId, cajas.GetValueOrDefault(c.CajaId) ?? string.Empty, c.MensajeId, c.TipoMensaje, c.Tipo, c.Detalle,
                c.DetectadoEn, c.Ocurrencias, c.UltimaOcurrenciaEn, c.ResueltoEn, c.ResueltoPor, c.Resolucion))
            .ToList();
    }

    public async Task<ResultadoAdministracion> ResolverConflictoAsync(int conflictoId, string resolucion, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (await contexto.ConflictosSincronizacion.SingleOrDefaultAsync(c => c.Id == conflictoId, cancelacion) is not { } conflicto)
            return ResultadoAdministracion.Inexistente("El conflicto no existe.");

        try
        {
            conflicto.Resolver(reloj.Ahora(), actor.Nombre, resolucion);
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
        {
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }

        auditoria.Registrar(new EntradaAuditoria("Sincronizacion.ConflictoResuelto", "ConflictoSincronizacion", conflicto.Id.ToString(),
            Detalle: new { conflicto.Tipo, conflicto.CajaId, conflicto.MensajeId, Resolucion = conflicto.Resolucion }));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(conflicto.Id);
    }
}
