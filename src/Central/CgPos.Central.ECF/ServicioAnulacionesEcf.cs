using System.Globalization;
using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dgii;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using CgPos.ECF.Documentos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.ECF;

internal sealed class ServicioAnulacionesEcf(
    ContextoDatosCentral contexto,
    IClienteDgii cliente,
    IParametrosCentral parametros,
    IAuditoriaCentral auditoria,
    TimeProvider reloj,
    ILogger<ServicioAnulacionesEcf> registro) : IServicioAnulacionesEcf
{
    public async Task<IReadOnlyList<DatosAnulacionEcf>> ListarAsync(CancellationToken cancelacion = default)
    {
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo.ToString("00"), cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, cancelacion);

        return (await contexto.AnulacionesEcf.AsNoTracking().OrderByDescending(a => a.SolicitadaEn).ToListAsync(cancelacion))
            .Select(a => new DatosAnulacionEcf(a.Id, a.SecuenciaId, cajas.GetValueOrDefault(a.CajaId)?.Codigo.ToString("00") ?? string.Empty,
                cajas.TryGetValue(a.CajaId, out var caja) ? sucursales.GetValueOrDefault(caja.SucursalId) ?? string.Empty : string.Empty,
                a.TipoComprobante, a.Desde, a.Hasta, a.Cantidad, a.Motivo, a.UsuarioNombre, a.SolicitadaEn, a.Estado, a.RespuestaDgii))
            .ToList();
    }

    public async Task<ResultadoAdministracion> AnularAsync(int secuenciaId, SolicitudAnulacionEcf solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(actor);

        var secuencia = await contexto.SecuenciasEcf.AsNoTracking().SingleOrDefaultAsync(s => s.Id == secuenciaId, cancelacion);
        if (secuencia is null)
            return ResultadoAdministracion.Inexistente("El rango de e-CF no existe.");

        if (await ValidarAsync(secuencia, solicitud, cancelacion) is { } problema)
            return ResultadoAdministracion.Error(problema);

        var rnc = await contexto.Empresas.AsNoTracking().Select(e => e.Rnc).FirstOrDefaultAsync(cancelacion);
        if (rnc is null)
            return ResultadoAdministracion.Error("La empresa no está configurada.");

        var tipo = (int)secuencia.TipoComprobante;
        var xml = GeneradorXmlAnecf.Generar(rnc, reloj.GetLocalNow(),
            [new RangoAnulacionEcf(tipo, SecuenciaEcf.FormatearEncf(secuencia.TipoComprobante, solicitud.Desde),
                SecuenciaEcf.FormatearEncf(secuencia.TipoComprobante, solicitud.Hasta))]);

        RespuestaAnulacionDgii respuesta;
        try
        {
            respuesta = await cliente.AnularAsync(xml, cancelacion);
        }
        catch (Exception excepcion) when (!cancelacion.IsCancellationRequested)
        {
            registro.LogWarning(excepcion, "No se pudo enviar la anulación de e-NCF a la DGII");
            respuesta = new RespuestaAnulacionDgii(false, true, excepcion.Message, null);
        }

        var estado = respuesta.Aceptada ? EstadoAnulacionEcf.Aceptada : respuesta.Error ? EstadoAnulacionEcf.Fallida : EstadoAnulacionEcf.Rechazada;
        var anulacion = AnulacionEcfCentral.Registrar(secuencia.Id, secuencia.CajaId, secuencia.TipoComprobante, solicitud.Desde, solicitud.Hasta,
            solicitud.Motivo, actor.Nombre, estado, respuesta.Mensaje, respuesta.XmlFirmado, reloj.GetUtcNow());
        contexto.AnulacionesEcf.Add(anulacion);
        auditoria.Registrar(new EntradaAuditoria($"Dgii.Anulacion{estado}", "AnulacionEcf", anulacion.Id.ToString(),
            new { secuencia.CajaId, Tipo = tipo, solicitud.Desde, solicitud.Hasta, anulacion.Cantidad, respuesta.Mensaje }, solicitud.Motivo, actor));
        await contexto.SaveChangesAsync(cancelacion);

        return estado switch
        {
            EstadoAnulacionEcf.Aceptada => ResultadoAdministracion.Correcto(anulacion.Id),
            EstadoAnulacionEcf.Rechazada => ResultadoAdministracion.Error($"La DGII rechazó la anulación: {respuesta.Mensaje}"),
            _ => ResultadoAdministracion.Error($"No se pudo comunicar la anulación a la DGII; vuelva a intentarlo. {respuesta.Mensaje}"),
        };
    }

    private async Task<string?> ValidarAsync(SecuenciaEcf secuencia, SolicitudAnulacionEcf solicitud, CancellationToken cancelacion)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Motivo))
            return "Indique el motivo de la anulación.";
        if (solicitud.Desde > solicitud.Hasta)
            return "El final del tramo no puede ser menor que el inicio.";
        if (solicitud.Desde < secuencia.Desde || solicitud.Hasta > secuencia.Hasta)
            return $"El tramo debe estar dentro del rango asignado ({secuencia.Desde:N0} – {secuencia.Hasta:N0}).";

        // Con el rango activo y vigente la caja podría usar esos números sin conexión: primero se desactiva y la caja lo recibe.
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        if (secuencia.Activa && secuencia.VenceEn >= hoy)
            return "El rango está activo en la caja. Desactívelo y espere a que la caja sincronice antes de anular sus números sin usar.";

        if (!await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.DgiiHabilitado, cancelacion))
            return "El envío a la DGII no está activado en los parámetros del Central.";

        var desde = SecuenciaEcf.FormatearEncf(secuencia.TipoComprobante, solicitud.Desde);
        var hasta = SecuenciaEcf.FormatearEncf(secuencia.TipoComprobante, solicitud.Hasta);
        var usado = await contexto.ComprobantesRecibidos.AsNoTracking()
            .Where(c => c.CajaId == secuencia.CajaId && c.TipoComprobante == secuencia.TipoComprobante
                && string.Compare(c.Encf, desde) >= 0 && string.Compare(c.Encf, hasta) <= 0)
            .OrderByDescending(c => c.Encf)
            .Select(c => c.Encf)
            .FirstOrDefaultAsync(cancelacion);
        if (usado is not null)
            return $"El e-NCF {usado} ya se usó y llegó al Central: anule solo desde la secuencia {long.Parse(usado[3..], CultureInfo.InvariantCulture) + 1:N0}.";

        var anuladas = await contexto.AnulacionesEcf.AsNoTracking()
            .Where(a => a.CajaId == secuencia.CajaId && a.TipoComprobante == secuencia.TipoComprobante && a.Estado == EstadoAnulacionEcf.Aceptada)
            .ToListAsync(cancelacion);
        return anuladas.FirstOrDefault(a => a.Solapa(secuencia.TipoComprobante, solicitud.Desde, solicitud.Hasta)) is { } anterior
            ? $"Parte del tramo ya se anuló ({anterior.Desde:N0} – {anterior.Hasta:N0} el {anterior.SolicitadaEn.ToLocalTime():dd/MM/yyyy})."
            : null;
    }
}
