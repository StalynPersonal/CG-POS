using System.Text.Json;
using CgPos.Central.Aplicacion.Entregas;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Entregas;

internal sealed class ServicioDespachoCentral(ContextoDatosCentral contexto, TimeProvider reloj) : IServicioDespachoCentral
{
    private DateOnly Hoy => DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

    public async Task<PaginaPendientesCentral> ListarAsync(string? buscar, EstadoPendiente? estado, MetodoEntrega? metodo, int? sucursalId, bool soloAtrasados,
        bool soloAbiertos, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioDespachoCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);
        var hoy = Hoy;

        var consulta = contexto.PendientesEntrega.AsNoTracking();
        if (CgPos.Dominio.Comun.TextoBusqueda.Normalizar(buscar) is { } buscado)
        {
            var patron = $"%{buscado.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")}%";
            consulta = consulta.Where(p => EF.Functions.Like(p.TextoBusqueda, patron));
        }

        if (estado is { } filtroEstado)
            consulta = consulta.Where(p => p.Estado == filtroEstado);
        if (metodo is { } filtroMetodo)
            consulta = consulta.Where(p => p.Metodo == filtroMetodo);
        if (sucursalId is { } sucursal)
            consulta = consulta.Where(p => p.SucursalId == sucursal);
        if (soloAbiertos || soloAtrasados)
            consulta = consulta.Where(p => p.Estado != EstadoPendiente.Entregado && p.Estado != EstadoPendiente.Anulado);
        if (soloAtrasados)
            consulta = consulta.Where(p => p.FechaComprometida != null && p.FechaComprometida < hoy);

        var total = await consulta.CountAsync(cancelacion);
        var pendientes = await consulta
            .OrderBy(p => p.FechaComprometida == null)
            .ThenBy(p => p.FechaComprometida)
            .ThenByDescending(p => p.CreadoEn)
            .Skip(pagina * tamano)
            .Take(tamano)
            .ToListAsync(cancelacion);

        return new PaginaPendientesCentral(await ResumenesAsync(pendientes, cancelacion), total);
    }

    public async Task<DetallePendienteCentral?> ObtenerAsync(int pendienteId, CancellationToken cancelacion = default)
    {
        var pendiente = await contexto.PendientesEntrega.AsNoTracking().SingleOrDefaultAsync(p => p.Id == pendienteId, cancelacion);
        if (pendiente is null)
            return null;

        var documento = JsonSerializer.Deserialize<DocumentoPendienteEntrega>(pendiente.Contenido, OpcionesJson.Predeterminadas);
        return documento is null ? null : new DetallePendienteCentral((await ResumenesAsync([pendiente], cancelacion))[0], documento);
    }

    public async Task<ResumenDespachoCentral> ResumenAsync(CancellationToken cancelacion = default)
    {
        var hoy = Hoy;
        var abiertos = contexto.PendientesEntrega.AsNoTracking().Where(p => p.Estado != EstadoPendiente.Entregado && p.Estado != EstadoPendiente.Anulado);
        var inicioDia = new DateTimeOffset(hoy.ToDateTime(TimeOnly.MinValue), reloj.GetLocalNow().Offset);

        return new ResumenDespachoCentral(
            await abiertos.CountAsync(cancelacion),
            await abiertos.CountAsync(p => p.FechaComprometida != null && p.FechaComprometida < hoy, cancelacion),
            await abiertos.CountAsync(p => p.Metodo == MetodoEntrega.RetiroAlmacen, cancelacion),
            await abiertos.CountAsync(p => p.Metodo == MetodoEntrega.Envio, cancelacion),
            await contexto.PendientesEntrega.AsNoTracking().CountAsync(p => p.Estado == EstadoPendiente.Entregado && p.ActualizadoEn >= inicioDia, cancelacion));
    }

    private async Task<IReadOnlyList<DatosPendienteCentralResumen>> ResumenesAsync(IReadOnlyList<PendienteCentral> pendientes, CancellationToken cancelacion)
    {
        if (pendientes.Count == 0)
            return [];

        var hoy = Hoy;
        var idsCajas = pendientes.Select(p => p.CajaId).Distinct().ToList();
        var cajas = await contexto.Cajas.AsNoTracking().Where(c => idsCajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Codigo.ToString("00"), cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo.ToString("00"), cancelacion);

        return pendientes.Select(p => new DatosPendienteCentralResumen(p.Id, p.Numero, p.VentaNumero,
            sucursales.GetValueOrDefault(p.SucursalId) ?? string.Empty, cajas.GetValueOrDefault(p.CajaId) ?? string.Empty, p.Metodo, p.Estado,
            p.Metodo == MetodoEntrega.Envio ? p.Ciudad : p.AlmacenNombre, p.ClienteNombre, p.ClienteDocumento, p.Telefono, p.FechaComprometida,
            p.EstaAtrasado(hoy), p.Unidades, p.UnidadesEntregadas, p.CreadoEn, p.ActualizadoEn)).ToList();
    }
}
