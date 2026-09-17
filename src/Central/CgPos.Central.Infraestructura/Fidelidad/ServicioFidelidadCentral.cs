using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Fidelidad;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Fidelidad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Fidelidad;

internal sealed class ServicioFidelidadCentral(
    ContextoDatosCentral contexto,
    RecalculadorPuntos recalculador,
    IAuditoriaCentral auditoria,
    TimeProvider reloj) : IServicioFidelidadCentral
{
    public async Task<PaginaMiembrosFidelidadCentral> ListarAsync(string? buscar, bool soloConPuntos, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioFidelidadCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);

        var consulta = contexto.MiembrosFidelidad.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            consulta = consulta.Where(m => m.Cedula.Contains(texto) || m.Nombre.Contains(texto) || (m.Telefono != null && m.Telefono.Contains(texto))
                                           || (m.Correo != null && m.Correo.Contains(texto)));
        }

        if (soloConPuntos)
        {
            var conPuntos = contexto.SaldosPuntos.AsNoTracking().Where(s => s.Puntos != 0).Select(s => s.MiembroId);
            consulta = consulta.Where(m => conPuntos.Contains(m.Id));
        }

        var total = await consulta.CountAsync(cancelacion);
        var filas = await consulta.OrderBy(m => m.Cedula).Skip(pagina * tamano).Take(tamano).ToListAsync(cancelacion);
        return new PaginaMiembrosFidelidadCentral(await DatosAsync(filas, cancelacion), total);
    }

    public async Task<DatosMiembroFidelidadCentral?> ObtenerAsync(int miembroId, CancellationToken cancelacion = default)
    {
        var fila = await contexto.MiembrosFidelidad.AsNoTracking().SingleOrDefaultAsync(m => m.Id == miembroId, cancelacion);
        return fila is null ? null : (await DatosAsync([fila], cancelacion))[0];
    }

    public async Task<IReadOnlyList<DatosMovimientoPuntosCentral>> ListarMovimientosAsync(int miembroId, CancellationToken cancelacion = default)
    {
        var cedula = await contexto.MiembrosFidelidad.AsNoTracking().Where(m => m.Id == miembroId).Select(m => m.Cedula).SingleOrDefaultAsync(cancelacion);
        var movimientos = await contexto.MovimientosPuntos.AsNoTracking()
            .Where(m => m.Cedula == cedula)
            .OrderByDescending(m => m.Fecha)
            .ToListAsync(cancelacion);

        var idsCajas = movimientos.Select(m => m.CajaId).OfType<int>().Distinct().ToList();
        var cajas = idsCajas.Count == 0
            ? []
            : await contexto.Cajas.AsNoTracking().Where(c => idsCajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Codigo.ToString("00"), cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo.ToString("00"), cancelacion);

        return movimientos.Select(m => new DatosMovimientoPuntosCentral(m.Id, m.Tipo, m.Origen, m.Puntos, m.Documento,
            m.SucursalId is { } sucursal ? sucursales.GetValueOrDefault(sucursal) : null,
            m.CajaId is { } caja ? cajas.GetValueOrDefault(caja) : null,
            m.Fecha, m.VenceEn, m.Usuario, m.Motivo)).ToList();
    }

    public async Task<RespuestaAjustePuntos> AjustarAsync(int miembroId, int puntos, string motivo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (puntos == 0)
            return new RespuestaAjustePuntos(false, "El ajuste no puede ser de cero puntos.");
        if (string.IsNullOrWhiteSpace(motivo))
            return new RespuestaAjustePuntos(false, "El ajuste necesita un motivo.");

        var miembro = await contexto.MiembrosFidelidad.AsNoTracking().SingleOrDefaultAsync(m => m.Id == miembroId, cancelacion);
        if (miembro is null)
            return new RespuestaAjustePuntos(false, "El miembro del programa no existe.");
        var ahora = reloj.GetUtcNow();
        var saldoActual = await contexto.SaldosPuntos.AsNoTracking().Where(s => s.MiembroId == miembroId).Select(s => s.Puntos).FirstOrDefaultAsync(cancelacion);
        if (puntos < 0 && saldoActual + puntos < 0)
            return new RespuestaAjustePuntos(false, $"El miembro solo tiene {saldoActual} puntos.");

        try
        {
            // Los puntos que el Central regala vencen como los demás, con el plazo que ya tenga configurado el miembro.
            contexto.MovimientosPuntos.Add(MovimientoPuntosCentral.Ajuste(miembro.Cedula, puntos, actor.Nombre, motivo, null, ahora));
        }
        catch (ArgumentException excepcion)
        {
            return new RespuestaAjustePuntos(false, ValidacionMaestros.MensajeError(excepcion));
        }

        await recalculador.RecalcularAsync(miembro.Cedula, actor.Nombre, cancelacion: cancelacion);
        auditoria.Registrar(new EntradaAuditoria("Fidelidad.PuntosAjustados", "MiembroFidelidad", miembroId.ToString(),
            Detalle: new { miembro.Cedula, miembro.Nombre, Puntos = puntos, Motivo = motivo, Usuario = actor.Nombre }));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaAjustePuntos(true, null, await ObtenerAsync(miembroId, cancelacion));
    }

    public async Task<int> VencerPuntosAsync(int maximo, CancellationToken cancelacion = default)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var pendientes = await contexto.SaldosPuntos
            .Where(s => s.ProximoVencimiento != null && s.ProximoVencimiento < hoy)
            .OrderBy(s => s.ProximoVencimiento)
            .Take(Math.Max(maximo, 1))
            .Select(s => s.Cedula)
            .ToListAsync(cancelacion);

        foreach (var cedula in pendientes)
            await recalculador.RecalcularAsync(cedula, "Vencimiento de puntos", cancelacion: cancelacion);

        if (pendientes.Count > 0)
            await contexto.SaveChangesAsync(cancelacion);

        return pendientes.Count;
    }

    private async Task<IReadOnlyList<DatosMiembroFidelidadCentral>> DatosAsync(IReadOnlyList<MiembroFidelidad> miembros, CancellationToken cancelacion)
    {
        if (miembros.Count == 0)
            return [];

        var ids = miembros.Select(m => m.Id).ToList();
        var saldos = await contexto.SaldosPuntos.AsNoTracking().Where(s => ids.Contains(s.MiembroId)).ToDictionaryAsync(s => s.MiembroId, cancelacion);
        var niveles = await contexto.NivelesFidelidad.AsNoTracking().ToDictionaryAsync(n => n.Id, n => n.Nombre, cancelacion);

        return miembros.Select(miembro =>
        {
            var saldo = saldos.GetValueOrDefault(miembro.Id);
            return new DatosMiembroFidelidadCentral(miembro.Id, miembro.Cedula, miembro.Nombre, miembro.Telefono, miembro.Correo,
                miembro.NivelId is { } nivelId ? niveles.GetValueOrDefault(nivelId) : null,
                saldo?.Puntos ?? 0, saldo?.PuntosPorVencer ?? 0, saldo?.ProximoVencimiento, saldo?.Vencidos ?? 0, saldo?.CalculadoEn,
                miembro.InscritoEn, miembro.Activo);
        }).ToList();
    }
}
