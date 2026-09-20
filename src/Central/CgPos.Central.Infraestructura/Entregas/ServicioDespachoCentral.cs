using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Auditoria;
using CgPos.Central.Aplicacion.Entregas;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Reportes;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Entregas;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Entregas;

internal sealed class ServicioDespachoCentral(
    ContextoDatosCentral contexto,
    IAuditoriaCentral auditoria,
    GeneradorPdfConstanciaEntrega generadorPdf,
    TimeProvider reloj) : IServicioDespachoCentral
{
    private const string TipoEntidad = "PendienteEntrega";

    private DateOnly Hoy => reloj.Ahora().Dia();

    public async Task<PaginaPendientesCentral> ListarAsync(string? buscar, EstadoPendiente? estado, MetodoEntrega? metodo, int? sucursalId, bool soloAtrasados,
        bool soloAbiertos, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioDespachoCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);
        var hoy = Hoy;

        var consulta = contexto.PendientesEntrega.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var texto = buscar.Trim();
            consulta = consulta.Where(p => p.Numero.Contains(texto)
                || p.VentaNumero.Contains(texto)
                || (p.ClienteNombre != null && p.ClienteNombre.Contains(texto))
                || (p.ClienteDocumento != null && p.ClienteDocumento.Contains(texto))
                || (p.Telefono != null && p.Telefono.Contains(texto))
                || (p.AlmacenNombre != null && p.AlmacenNombre.Contains(texto))
                || (p.Ciudad != null && p.Ciudad.Contains(texto)));
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
            .Include(p => p.Lineas)
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
        var pendiente = await CargarAsync(pendienteId, seguimiento: false, cancelacion);
        return pendiente is null ? null : new DetallePendienteCentral((await ResumenesAsync([pendiente], cancelacion))[0], Documento(pendiente));
    }

    public async Task<ResumenDespachoCentral> ResumenAsync(CancellationToken cancelacion = default)
    {
        var hoy = Hoy;
        var abiertos = contexto.PendientesEntrega.AsNoTracking().Where(p => p.Estado != EstadoPendiente.Entregado && p.Estado != EstadoPendiente.Anulado);
        var inicioDia = new DateTimeOffset(hoy.ToDateTime(TimeOnly.MinValue), reloj.Ahora().Offset);

        return new ResumenDespachoCentral(
            await abiertos.CountAsync(cancelacion),
            await abiertos.CountAsync(p => p.FechaComprometida != null && p.FechaComprometida < hoy, cancelacion),
            await abiertos.CountAsync(p => p.Metodo == MetodoEntrega.RetiroAlmacen, cancelacion),
            await abiertos.CountAsync(p => p.Metodo == MetodoEntrega.Envio, cancelacion),
            await contexto.PendientesEntrega.AsNoTracking().CountAsync(p => p.Estado == EstadoPendiente.Entregado && p.ActualizadoEn >= inicioDia, cancelacion));
    }

    public async Task<ResultadoAdministracion> CambiarEstadoAsync(int pendienteId, SolicitudEstadoPendiente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (await CargarAsync(pendienteId, seguimiento: true, cancelacion) is not { } pendiente)
            return ResultadoAdministracion.Inexistente("El pendiente de entrega no existe.");

        var anterior = pendiente.Estado;
        try
        {
            pendiente.CambiarEstado(solicitud.Estado, actor.Nombre, reloj.Ahora());
        }
        catch (ReglaPendienteExcepcion excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        auditoria.Registrar(new EntradaAuditoria("Despacho.EstadoCambiado", TipoEntidad, pendiente.Numero,
            Detalle: new { Anterior = anterior, Nuevo = pendiente.Estado, pendiente.VentaNumero }, Usuario: actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(pendiente.Id);
    }

    public async Task<ResultadoAdministracion> EntregarAsync(int pendienteId, SolicitudEntregaPendiente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (await CargarAsync(pendienteId, seguimiento: true, cancelacion) is not { } pendiente)
            return ResultadoAdministracion.Inexistente("El pendiente de entrega no existe.");

        EntregaPendiente entrega;
        try
        {
            entrega = pendiente.Entregar(solicitud.Lineas ?? [], solicitud.RecibeNombre, solicitud.RecibeCedula, actor.Nombre, reloj.Ahora());
        }
        catch (ReglaPendienteExcepcion excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }

        auditoria.Registrar(new EntradaAuditoria("Despacho.Entregado", TipoEntidad, pendiente.Numero,
            Detalle: new
            {
                pendiente.VentaNumero,
                Entrega = entrega.Numero,
                entrega.RecibeNombre,
                entrega.RecibeCedula,
                Estado = pendiente.Estado,
                Lineas = entrega.Lineas.Select(l => new { l.NumeroLineaVenta, l.Descripcion, l.Cantidad, l.Serial }),
            },
            Usuario: actor));

        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(entrega.Numero);
    }

    public async Task<ResultadoAdministracion> AnularAsync(int pendienteId, SolicitudAnularPendiente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (await CargarAsync(pendienteId, seguimiento: true, cancelacion) is not { } pendiente)
            return ResultadoAdministracion.Inexistente("El pendiente de entrega no existe.");

        try
        {
            pendiente.Anular(solicitud.Motivo, actor.Nombre, reloj.Ahora());
        }
        catch (ReglaPendienteExcepcion excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        auditoria.Registrar(new EntradaAuditoria("Despacho.Anulado", TipoEntidad, pendiente.Numero,
            Detalle: new { pendiente.VentaNumero, pendiente.Unidades },
            Motivo: pendiente.MotivoAnulacion,
            Usuario: actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(pendiente.Id);
    }

    public async Task<byte[]?> ConstanciaAsync(int pendienteId, int numeroEntrega, CancellationToken cancelacion = default)
    {
        if (await CargarAsync(pendienteId, seguimiento: false, cancelacion) is not { } pendiente)
            return null;
        if (pendiente.Entregas.FirstOrDefault(e => e.Numero == numeroEntrega) is not { } entrega)
            return null;

        var sucursal = await contexto.Sucursales.AsNoTracking().Where(s => s.Id == pendiente.SucursalId).Select(s => s.Nombre)
            .FirstOrDefaultAsync(cancelacion);
        var empresa = await contexto.Empresas.AsNoTracking()
            .Select(e => new { Nombre = e.NombreComercial ?? e.RazonSocial, e.Rnc })
            .FirstOrDefaultAsync(cancelacion);

        return generadorPdf.Generar(pendiente, entrega, empresa?.Nombre ?? string.Empty, empresa?.Rnc, sucursal);
    }

    /// <summary>El pendiente con sus líneas y entregas; con seguimiento cuando se va a modificar.</summary>
    private async Task<PendienteEntrega?> CargarAsync(int pendienteId, bool seguimiento, CancellationToken cancelacion)
    {
        var consulta = contexto.PendientesEntrega.Include(p => p.Lineas).Include(p => p.Entregas).ThenInclude(e => e.Lineas).AsQueryable();
        if (!seguimiento)
            consulta = consulta.AsNoTracking();

        return await consulta.SingleOrDefaultAsync(p => p.Id == pendienteId, cancelacion);
    }

    /// <summary>El pendiente en el mismo formato que informa la caja: es lo que espera la pantalla de detalle.</summary>
    private static DocumentoPendienteEntrega Documento(PendienteEntrega pendiente) =>
        new(pendiente.Numero, pendiente.VentaNumero, pendiente.Metodo, pendiente.Estado, null, pendiente.AlmacenNombre, pendiente.Direccion,
            pendiente.Sector, pendiente.Ciudad, pendiente.Referencia, pendiente.Telefono, pendiente.Transportista, pendiente.CostoEnvio,
            pendiente.FechaComprometida, pendiente.Comentario, pendiente.ClienteDocumento, pendiente.ClienteNombre, pendiente.VendidoPorNombre,
            pendiente.AutorizadoPorNombre, pendiente.CreadoEn, pendiente.ActualizadoEn, pendiente.ActualizadoPorNombre, pendiente.MotivoAnulacion,
            pendiente.Lineas.OrderBy(l => l.NumeroLineaVenta)
                .Select(l => new DatosLineaPendiente(l.NumeroLineaVenta, l.CodigoInterno, l.Descripcion, l.UnidadMedidaCodigo, l.DecimalesCantidad,
                    l.Serializado, l.Cantidad, l.CantidadEntregada, l.Serial))
                .ToList(),
            pendiente.Entregas.OrderBy(e => e.Numero)
                .Select(e => new DatosEntregaPendiente(e.Numero, e.RecibeNombre, e.RecibeCedula, e.UsuarioNombre, e.Fecha,
                    e.Lineas.Select(l => new DatosLineaEntregaPendiente(l.NumeroLineaVenta, l.Descripcion, l.Cantidad, l.Serial)).ToList()))
                .ToList());

    private async Task<IReadOnlyList<DatosPendienteCentralResumen>> ResumenesAsync(IReadOnlyList<PendienteEntrega> pendientes, CancellationToken cancelacion)
    {
        if (pendientes.Count == 0)
            return [];

        var hoy = Hoy;
        var idsCajas = pendientes.Select(p => p.CajaId).Distinct().ToList();
        var cajas = await contexto.Cajas.AsNoTracking().Where(c => idsCajas.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Codigo, cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Codigo, cancelacion);

        return pendientes.Select(p => new DatosPendienteCentralResumen(p.Id, p.Numero, p.VentaNumero,
            sucursales.GetValueOrDefault(p.SucursalId) ?? string.Empty, cajas.GetValueOrDefault(p.CajaId) ?? string.Empty, p.Metodo, p.Estado,
            p.Metodo == MetodoEntrega.Envio ? p.Ciudad : p.AlmacenNombre, p.ClienteNombre, p.ClienteDocumento, p.Telefono, p.FechaComprometida,
            p.EstaAtrasado(hoy), p.Unidades, p.UnidadesEntregadas, p.CreadoEn, p.ActualizadoEn)).ToList();
    }
}
