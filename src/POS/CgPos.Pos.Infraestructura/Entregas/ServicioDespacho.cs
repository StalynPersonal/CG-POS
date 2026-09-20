using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Tickets;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Pos.Infraestructura.Entregas;

internal sealed class ServicioDespacho(
    ContextoDatosPos contexto,
    IValidadorAutorizaciones autorizaciones,
    IParametros parametros,
    IImpresoraTicket impresora,
    IBandejaSalida bandejaSalida,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioDespacho
{
    private const string TipoEntidad = "PendienteEntrega";

    private IQueryable<PendienteEntrega> Pendientes =>
        contexto.PendientesEntrega.Include(p => p.Lineas).Include(p => p.Entregas).ThenInclude(e => e.Lineas);

    public async Task<RespuestaBusquedaPendientes> BuscarAsync(SesionUsuario sesion, string codigo, CancellationToken cancelacion = default)
    {
        var buscado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return new RespuestaBusquedaPendientes(CodigoResultadoPendiente.OperacionInvalida, "Escanee el voucher del pendiente o la factura.", []);

        var pendientes = await Pendientes.AsNoTracking().Where(p => p.Numero == buscado).ToListAsync(cancelacion);
        if (pendientes.Count == 0)
        {
            // Por la factura: número de transacción o e-NCF impreso.
            var ventaId = await contexto.Ventas.AsNoTracking().Where(v => v.NumeroTransaccion == buscado).Select(v => (int?)v.Id).FirstOrDefaultAsync(cancelacion)
                ?? await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.Encf == buscado && d.TipoOrigen == OrigenComprobante.Venta).Select(d => (int?)d.VentaId).FirstOrDefaultAsync(cancelacion);
            if (ventaId is { } id)
                pendientes = await Pendientes.AsNoTracking().Where(p => p.VentaId == id).OrderBy(p => p.Numero).ToListAsync(cancelacion);
        }

        return pendientes.Count == 0
            ? new RespuestaBusquedaPendientes(CodigoResultadoPendiente.NoEncontrado,
                $"No hay pendientes de entrega para {buscado} en esta caja. Los de otras cajas o sucursales se consultan con el Central.", [])
            : new RespuestaBusquedaPendientes(CodigoResultadoPendiente.Correcto, null, pendientes.Select(p => p.ADatos()).ToList());
    }

    public async Task<IReadOnlyList<DatosPendienteEntrega>> ListarAbiertosAsync(SesionUsuario sesion, CancellationToken cancelacion = default) =>
        (await Pendientes.AsNoTracking()
            .Where(p => p.Estado != EstadoPendiente.Entregado && p.Estado != EstadoPendiente.Anulado)
            .OrderBy(p => p.FechaComprometida == null)
            .ThenBy(p => p.FechaComprometida)
            .ThenBy(p => p.CreadoEn)
            .Take(100)
            .ToListAsync(cancelacion))
        .Select(p => p.ADatos())
        .ToList();

    public async Task<RespuestaPendiente> CambiarEstadoAsync(SesionUsuario sesion, int pendienteId, SolicitudEstadoPendiente solicitud, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.DespacharPendiente))
            return SinPermiso();

        var pendiente = await Pendientes.SingleOrDefaultAsync(p => p.Id == pendienteId, cancelacion);
        if (pendiente is null)
            return NoEncontrado();

        var anterior = pendiente.Estado;
        try
        {
            pendiente.CambiarEstado(solicitud.Estado, sesion.Nombre, reloj.Ahora());
        }
        catch (ReglaPendienteExcepcion excepcion)
        {
            return new RespuestaPendiente(CodigoResultadoPendiente.OperacionInvalida, excepcion.Message, pendiente.ADatos());
        }

        await RegistrarAsync("Entregas.EstadoCambiado", pendiente, sesion, new { Anterior = anterior, Nuevo = pendiente.Estado }, cancelacion);
        await contexto.SaveChangesAsync(cancelacion);
        return new RespuestaPendiente(CodigoResultadoPendiente.Correcto, $"Pendiente {pendiente.Numero}: {NombreEstado(pendiente.Estado)}.", pendiente.ADatos());
    }

    public async Task<RespuestaPendiente> EntregarAsync(SesionUsuario sesion, int pendienteId, SolicitudEntregaPendiente solicitud, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.DespacharPendiente))
            return SinPermiso();

        var pendiente = await Pendientes.SingleOrDefaultAsync(p => p.Id == pendienteId, cancelacion);
        if (pendiente is null)
            return NoEncontrado();

        EntregaPendiente entrega;
        try
        {
            entrega = pendiente.Entregar(solicitud.Lineas ?? [], solicitud.RecibeNombre, solicitud.RecibeCedula, sesion.Nombre, reloj.Ahora());
        }
        catch (Exception excepcion) when (excepcion is ReglaPendienteExcepcion or ArgumentException)
        {
            return new RespuestaPendiente(CodigoResultadoPendiente.OperacionInvalida, excepcion.Message, pendiente.ADatos());
        }

        await RegistrarAsync("Entregas.EntregaRegistrada", pendiente, sesion, new
        {
            entrega.Numero,
            entrega.RecibeNombre,
            entrega.RecibeCedula,
            Lineas = entrega.Lineas.Select(l => new { l.NumeroLineaVenta, l.Cantidad, l.Serial }),
            pendiente.Estado,
        }, cancelacion);
        await contexto.SaveChangesAsync(cancelacion);

        // La constancia se imprime después de guardar: un fallo de la impresora no deshace la entrega.
        var datos = pendiente.ADatos();
        var encabezado = await contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion);
        var impresion = await impresora.ImprimirAsync(
            GeneradorTicket.GenerarConstanciaEntrega(encabezado, datos, datos.Entregas.Single(e => e.Numero == entrega.Numero)), cancelacion);

        var mensaje = pendiente.Estado == EstadoPendiente.Entregado
            ? $"Pendiente {pendiente.Numero} entregado completo."
            : $"Entrega parcial registrada: el pendiente {pendiente.Numero} sigue abierto.";
        return new RespuestaPendiente(CodigoResultadoPendiente.Correcto, impresion.Correcto ? mensaje : $"{mensaje} {impresion.Mensaje}", datos);
    }

    public async Task<RespuestaPendiente> AnularAsync(SesionUsuario sesion, int pendienteId, SolicitudAnularPendiente solicitud, CancellationToken cancelacion = default)
    {
        var ahora = reloj.Ahora();

        // Se valida sobre una copia sin seguimiento antes de pedir la clave del supervisor.
        var copia = await Pendientes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == pendienteId, cancelacion);
        if (copia is null)
            return NoEncontrado();
        try
        {
            copia.Anular(solicitud.Motivo, sesion.Nombre, ahora);
        }
        catch (Exception excepcion) when (excepcion is ReglaPendienteExcepcion or ArgumentException)
        {
            return new RespuestaPendiente(CodigoResultadoPendiente.OperacionInvalida, excepcion.Message, (await Pendientes.AsNoTracking().SingleAsync(p => p.Id == pendienteId, cancelacion)).ADatos());
        }

        var pendiente = await Pendientes.SingleAsync(p => p.Id == pendienteId, cancelacion);
        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.AnularPendiente, solicitud.AutorizacionId, TipoEntidad, pendiente.Numero, cancelacion);
        if (!permiso.Permitido)
            return new RespuestaPendiente(
                permiso.AutorizacionRechazada ? CodigoResultadoPendiente.AutorizacionInvalida : CodigoResultadoPendiente.RequiereAutorizacion,
                permiso.AutorizacionRechazada ? "La autorización no es válida, ya se usó o venció." : "Anular un pendiente de entrega requiere autorización de un supervisor.",
                pendiente.ADatos(), CatalogoPermisos.AnularPendiente);

        pendiente.Anular(solicitud.Motivo, permiso.SupervisorNombre ?? sesion.Nombre, ahora);
        await RegistrarAsync("Entregas.PendienteAnulado", pendiente, sesion, new { pendiente.Numero, pendiente.VentaNumero }, cancelacion, pendiente.MotivoAnulacion,
            permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null);
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaPendiente(CodigoResultadoPendiente.Correcto, $"Pendiente {pendiente.Numero} anulado: la mercancía queda liberada.", pendiente.ADatos());
    }

    /// <summary>Cada cambio del pendiente va al Central con el documento completo y queda auditado.</summary>
    private async Task RegistrarAsync(string accion, PendienteEntrega pendiente, SesionUsuario sesion, object detalle, CancellationToken cancelacion,
        string? motivo = null, UsuarioAuditoria? autorizadoPor = null)
    {
        bandejaSalida.Encolar("Entregas.PendienteActualizado", pendiente.Numero, await contexto.PendienteAsync(pendiente, cancelacion));
        auditoria.Registrar(new EntradaAuditoria(accion, TipoEntidad, pendiente.Numero, Detalle: detalle, Motivo: motivo,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre), AutorizadoPor: autorizadoPor));
    }

    private static RespuestaPendiente SinPermiso() =>
        new(CodigoResultadoPendiente.SinPermiso, "Su usuario no tiene permiso para operar el despacho de pendientes.", null, CatalogoPermisos.DespacharPendiente);

    private static RespuestaPendiente NoEncontrado() => new(CodigoResultadoPendiente.NoEncontrado, "El pendiente no existe en esta caja.", null);

    private static string NombreEstado(EstadoPendiente estado) => estado switch
    {
        EstadoPendiente.EnPreparacion => "en preparación",
        EstadoPendiente.Preparado => "preparado",
        EstadoPendiente.Despachado => "despachado",
        EstadoPendiente.Parcial => "entrega parcial",
        EstadoPendiente.Entregado => "entregado",
        EstadoPendiente.Anulado => "anulado",
        _ => "pendiente",
    };
}
