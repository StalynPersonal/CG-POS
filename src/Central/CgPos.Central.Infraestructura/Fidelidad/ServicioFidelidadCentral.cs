using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Fidelidad;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Sincronizacion;
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

        var consulta = contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.MiembroFidelidad);
        if (MaestroCentral.NormalizarBusqueda(buscar) is { } buscado)
        {
            var patron = $"%{buscado.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")}%";
            consulta = consulta.Where(m => m.TextoBusqueda != null && EF.Functions.Like(m.TextoBusqueda, patron));
        }

        if (soloConPuntos)
        {
            var conPuntos = contexto.SaldosPuntos.AsNoTracking().Where(s => s.Puntos != 0).Select(s => s.MiembroId);
            consulta = consulta.Where(m => conPuntos.Contains(m.Id));
        }

        var total = await consulta.CountAsync(cancelacion);
        var filas = await consulta.OrderBy(m => m.Codigo).Skip(pagina * tamano).Take(tamano).ToListAsync(cancelacion);
        return new PaginaMiembrosFidelidadCentral(await DatosAsync(filas, cancelacion), total);
    }

    public async Task<DatosMiembroFidelidadCentral?> ObtenerAsync(Guid miembroId, CancellationToken cancelacion = default)
    {
        var fila = await contexto.MaestrosCentral.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Tipo == TipoMaestro.MiembroFidelidad && m.Id == miembroId, cancelacion);
        return fila is null ? null : (await DatosAsync([fila], cancelacion))[0];
    }

    public async Task<IReadOnlyList<DatosMovimientoPuntosCentral>> ListarMovimientosAsync(Guid miembroId, CancellationToken cancelacion = default)
    {
        var movimientos = await contexto.MovimientosPuntos.AsNoTracking()
            .Where(m => m.MiembroId == miembroId)
            .OrderByDescending(m => m.Fecha)
            .ToListAsync(cancelacion);

        var idsCajas = movimientos.Select(m => m.CajaId).OfType<Guid>().Distinct().ToList();
        var cajas = idsCajas.Count == 0
            ? []
            : await contexto.Cajas.AsNoTracking().Where(c => idsCajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);

        return movimientos.Select(m => new DatosMovimientoPuntosCentral(m.Id, m.Tipo, m.Origen, m.Puntos, m.Documento,
            m.SucursalId is { } sucursal ? sucursales.GetValueOrDefault(sucursal) : null,
            m.CajaId is { } caja ? cajas.GetValueOrDefault(caja) : null,
            m.Fecha, m.VenceEn, m.Usuario, m.Motivo)).ToList();
    }

    public async Task<RespuestaAjustePuntos> AjustarAsync(Guid miembroId, int puntos, string motivo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (puntos == 0)
            return new RespuestaAjustePuntos(false, "El ajuste no puede ser de cero puntos.");
        if (string.IsNullOrWhiteSpace(motivo))
            return new RespuestaAjustePuntos(false, "El ajuste necesita un motivo.");

        var fila = await contexto.MaestrosCentral.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Tipo == TipoMaestro.MiembroFidelidad && m.Id == miembroId, cancelacion);
        if (fila is null)
            return new RespuestaAjustePuntos(false, "El miembro del programa no existe.");

        var miembro = FormatoMaestros.Leer<MiembroFidelidadCarga>(fila);
        var ahora = reloj.GetUtcNow();
        var saldoActual = await contexto.SaldosPuntos.AsNoTracking().Where(s => s.MiembroId == miembroId).Select(s => s.Puntos).FirstOrDefaultAsync(cancelacion);
        if (puntos < 0 && saldoActual + puntos < 0)
            return new RespuestaAjustePuntos(false, $"El miembro solo tiene {saldoActual} puntos.");

        try
        {
            // Los puntos que el Central regala vencen como los demás, con el plazo que ya tenga configurado el miembro.
            contexto.MovimientosPuntos.Add(MovimientoPuntosCentral.Ajuste(miembroId, miembro.Cedula, puntos, actor.Nombre, motivo, null, ahora));
        }
        catch (ArgumentException excepcion)
        {
            return new RespuestaAjustePuntos(false, ValidacionMaestros.MensajeError(excepcion));
        }

        await recalculador.RecalcularAsync(miembroId, miembro.Cedula, actor.Nombre, cancelacion: cancelacion);
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
            .Select(s => new { s.MiembroId, s.Cedula })
            .ToListAsync(cancelacion);

        foreach (var miembro in pendientes)
            await recalculador.RecalcularAsync(miembro.MiembroId, miembro.Cedula, "Vencimiento de puntos", cancelacion: cancelacion);

        if (pendientes.Count > 0)
            await contexto.SaveChangesAsync(cancelacion);

        return pendientes.Count;
    }

    private async Task<IReadOnlyList<DatosMiembroFidelidadCentral>> DatosAsync(IReadOnlyList<MaestroCentral> filas, CancellationToken cancelacion)
    {
        if (filas.Count == 0)
            return [];

        var miembros = filas.Select(FormatoMaestros.Leer<MiembroFidelidadCarga>).ToList();
        var ids = miembros.Select(m => m.Id).ToList();
        var saldos = await contexto.SaldosPuntos.AsNoTracking().Where(s => ids.Contains(s.MiembroId)).ToDictionaryAsync(s => s.MiembroId, cancelacion);
        var niveles = (await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.NivelFidelidad).ToListAsync(cancelacion))
            .Select(FormatoMaestros.Leer<NivelFidelidadCarga>)
            .ToDictionary(n => n.Id, n => n.Nombre);

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
