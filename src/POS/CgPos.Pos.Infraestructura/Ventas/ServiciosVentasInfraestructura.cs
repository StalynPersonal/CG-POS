using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Ventas;

internal static class ConversionesVenta
{
    public static DatosTurno ADatos(this Turno turno) =>
        new(turno.Id, turno.Numero, turno.FechaOperacion, turno.CajaId, turno.UsuarioActualId, turno.UsuarioActualNombre, turno.FondoInicial, turno.Estado, turno.AbiertoEn);

    public static DatosVenta ADatos(this Venta venta)
    {
        var totales = venta.CalcularTotales();

        var lineas = venta.Lineas
            .OrderBy(l => l.LineaAnuladaNumero ?? l.NumeroLinea)
            .ThenBy(l => l.EsReverso ? 1 : 0)
            .ThenBy(l => l.NumeroLinea)
            .Select(l => new DatosLineaVenta(
                l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo, l.UnidadMedidaCodigo,
                l.DecimalesCantidad, l.Cantidad, l.PrecioUnitario, l.ImporteConImpuesto, l.PorcentajeImpuesto, l.Lista, l.MotivoPrecio,
                l.LeidaDeBalanza, l.EsReverso, l.LineaAnuladaNumero, l.Anulada))
            .ToList();

        return new DatosVenta(
            venta.Id,
            venta.NumeroTransaccion,
            venta.Estado,
            venta.TurnoId,
            venta.UsuarioNombre,
            venta.IniciadaEn,
            lineas,
            new DatosTotalesVenta(
                totales.Subtotal,
                totales.Impuesto,
                totales.Total,
                totales.CantidadLineas,
                totales.CantidadArticulos,
                totales.Desglose.Select(d => new DatosDesgloseImpuesto(d.Porcentaje, d.IndicadorFacturacion, d.Base, d.Impuesto, d.Total)).ToList()));
    }

    public static ArticuloParaVenta AArticuloParaVenta(this DatosArticuloVenta datos) =>
        new(
            datos.ArticuloId, datos.Codigo, datos.CodigoLeido, datos.Descripcion, datos.Tipo, datos.FamiliaId, datos.PermiteDescuentoManual,
            datos.UnidadMedidaCodigo, datos.PermiteDecimales, datos.DecimalesCantidad, datos.ImpuestoId, datos.PorcentajeImpuesto,
            datos.IndicadorFacturacion, datos.PrecioDetalle, datos.PrecioMayor, datos.CantidadMinimaMayor, datos.PrecioMinimo,
            datos.PesoLeido, datos.PrecioLeido);

    public static CodigoResultadoVenta ACodigoResultado(this CodigoErrorVenta codigo) => codigo switch
    {
        CodigoErrorVenta.SinPrecio => CodigoResultadoVenta.SinPrecio,
        CodigoErrorVenta.RequiereBalanza => CodigoResultadoVenta.RequiereBalanza,
        CodigoErrorVenta.CantidadInvalida => CodigoResultadoVenta.CantidadInvalida,
        CodigoErrorVenta.LineaNoEncontrada => CodigoResultadoVenta.LineaNoEncontrada,
        CodigoErrorVenta.MotivoRequerido => CodigoResultadoVenta.MotivoRequerido,
        _ => CodigoResultadoVenta.VentaNoEditable,
    };
}

internal sealed class ServicioTurnos(
    ContextoDatosPos contexto,
    GeneradorSecuencias secuencias,
    IParametros parametros,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioTurnos
{
    public async Task<DatosEstadoTurno> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var turno = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        var fondoSugerido = await parametros.ObtenerDecimalAsync(ClavesParametros.FondoPredeterminado, sesion.CajaId, 0m, cancelacion);

        return new DatosEstadoTurno(
            turno?.ADatos(),
            fondoSugerido,
            turno is null && sesion.TienePermiso(CatalogoPermisos.AbrirTurno),
            turno is not null && turno.UsuarioActualId != sesion.UsuarioId);
    }

    public async Task<RespuestaTurno> AbrirAsync(SesionUsuario sesion, decimal? fondoInicial, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.AbrirTurno))
            return new RespuestaTurno(CodigoResultadoTurno.SinPermiso, "No tiene permiso para abrir turno.", null);

        var abierto = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);
        if (abierto is not null)
            return new RespuestaTurno(CodigoResultadoTurno.YaExisteTurnoAbierto, $"La caja ya tiene abierto el turno {abierto.Numero} de {abierto.UsuarioActualNombre}.", abierto.ADatos());

        var cajaOperativa = await (
                from caja in contexto.Cajas
                join sucursal in contexto.Sucursales on caja.SucursalId equals sucursal.Id
                where caja.Id == sesion.CajaId
                select caja.Habilitada && sucursal.Activa)
            .SingleOrDefaultAsync(cancelacion);
        if (!cajaOperativa)
            return new RespuestaTurno(CodigoResultadoTurno.CajaNoOperativa, "La caja está deshabilitada o su sucursal inactiva.", null);

        var fondo = fondoInicial ?? await parametros.ObtenerDecimalAsync(ClavesParametros.FondoPredeterminado, sesion.CajaId, 0m, cancelacion);
        if (fondo < 0)
            return new RespuestaTurno(CodigoResultadoTurno.FondoInvalido, "El fondo de caja no puede ser negativo.", null);

        var ahora = reloj.GetUtcNow();
        var numero = await secuencias.SiguienteAsync(sesion.CajaId, TiposSecuencia.Turno, cancelacion);
        var turno = Turno.Abrir(sesion.CajaId, sesion.SucursalId, numero, DateOnly.FromDateTime(reloj.GetLocalNow().DateTime),
            sesion.UsuarioId, sesion.Nombre, fondo, ahora);

        contexto.Turnos.Add(turno);
        auditoria.Registrar(new EntradaAuditoria("Caja.TurnoAbierto", "Turno", turno.Id.ToString(),
            Detalle: new { turno.Numero, Fondo = turno.FondoInicial, Caja = sesion.CajaCodigo },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // El índice único de turno abierto por caja ganó una carrera con otra apertura simultánea.
            contexto.ChangeTracker.Clear();
            return new RespuestaTurno(CodigoResultadoTurno.YaExisteTurnoAbierto, "La caja ya tiene un turno abierto.", null);
        }

        return new RespuestaTurno(CodigoResultadoTurno.Correcto, null, turno.ADatos());
    }
}

internal sealed class ServicioVentas(
    ContextoDatosPos contexto,
    IConsultaArticulos consultaArticulos,
    IValidadorAutorizaciones autorizaciones,
    GeneradorSecuencias secuencias,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioVentas
{
    private const string TipoEntidadVenta = "Venta";

    public async Task<RespuestaVenta> ObtenerActualAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var venta = await contexto.Ventas.Include(v => v.Lineas)
            .Where(v => v.TurnoId == turno!.Id && v.UsuarioId == sesion.UsuarioId && v.Estado == EstadoVenta.EnCurso)
            .OrderByDescending(v => v.IniciadaEn)
            .FirstOrDefaultAsync(cancelacion)
            ?? await IniciarVentaAsync(sesion, turno!, cancelacion);

        return Correcta(venta);
    }

    public async Task<RespuestaVenta> AgregarArticuloAsync(SesionUsuario sesion, Guid ventaId, string codigo, decimal? cantidad, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.RegistrarVenta))
            return new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "No tiene permiso para registrar ventas.", null, CatalogoPermisos.RegistrarVenta);

        if (!TryInterpretarEntrada(codigo, cantidad, out var codigoLimpio, out var cantidadFinal))
            return new RespuestaVenta(CodigoResultadoVenta.CantidadInvalida, "Formato no válido. Use código o cantidad*código (ej. 12*7891114119695).", null);

        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var articulo = await consultaArticulos.BuscarPorCodigoAsync(codigoLimpio, cancelacion);
        if (articulo is null)
            return new RespuestaVenta(CodigoResultadoVenta.ArticuloNoEncontrado, $"No se encontró el artículo {codigoLimpio}.", venta!.ADatos());

        return await EjecutarAsync(venta!, () => venta!.AgregarArticulo(articulo.AArticuloParaVenta(), cantidadFinal, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> CambiarCantidadAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, decimal cantidad, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.CambiarCantidad(numeroLinea, cantidad, reloj.GetUtcNow()), cancelacion);
    }

    public Task<RespuestaVenta> EliminarLineaAsync(SesionUsuario sesion, Guid ventaId, int numeroLinea, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EliminarAsync(sesion, ventaId, autorizacionId, venta => venta.EliminarLinea(numeroLinea, reloj.GetUtcNow()), cancelacion);

    public Task<RespuestaVenta> EliminarPorCodigoAsync(SesionUsuario sesion, Guid ventaId, string codigo, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        EliminarAsync(sesion, ventaId, autorizacionId, venta => venta.EliminarPorCodigo(codigo, reloj.GetUtcNow()), cancelacion);

    public async Task<RespuestaVenta> LimpiarAsync(SesionUsuario sesion, Guid ventaId, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        // Una venta vacía no se anula: no hay nada que limpiar y no se gasta un número.
        if (venta!.Lineas.Count == 0)
            return Correcta(venta);

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.LimpiarPantalla, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.LimpiarPantalla, venta);

        var totales = venta.CalcularTotales();
        venta.Anular("Pantalla limpiada", sesion.UsuarioId, sesion.Nombre, reloj.GetUtcNow());
        auditoria.Registrar(new EntradaAuditoria("Ventas.PantallaLimpiada", TipoEntidadVenta, venta.NumeroTransaccion,
            Detalle: new { Lineas = totales.CantidadLineas, totales.Total },
            Motivo: permiso.Motivo,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null));
        await contexto.SaveChangesAsync(cancelacion);

        var (turno, rechazoTurno) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        return rechazoTurno ?? Correcta(await IniciarVentaAsync(sesion, turno!, cancelacion));
    }

    private async Task<RespuestaVenta> EliminarAsync(SesionUsuario sesion, Guid ventaId, Guid? autorizacionId, Func<Venta, LineaVenta> eliminar, CancellationToken cancelacion)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.EliminarLinea, autorizacionId, TipoEntidadVenta, venta!.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.EliminarLinea, venta);

        return await EjecutarAsync(venta, () =>
        {
            var reverso = eliminar(venta);
            var original = venta.Lineas.Single(l => l.NumeroLinea == reverso.LineaAnuladaNumero);
            auditoria.Registrar(new EntradaAuditoria("Ventas.LineaEliminada", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Linea = original.NumeroLinea, original.CodigoInterno, original.Descripcion, original.Cantidad, Importe = original.ImporteConImpuesto },
                Motivo: permiso.Motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null));
        }, cancelacion);
    }

    private async Task<RespuestaVenta> EjecutarAsync(Venta venta, Action operacion, CancellationToken cancelacion)
    {
        try
        {
            operacion();
            await contexto.SaveChangesAsync(cancelacion);
            return Correcta(venta);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            // Se descarta todo lo pendiente (incluida una autorización marcada como usada): no se consume si la operación falla.
            contexto.ChangeTracker.Clear();
            var ventaActual = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleAsync(v => v.Id == venta.Id, cancelacion);
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, ventaActual.ADatos());
        }
    }

    private async Task<Venta> IniciarVentaAsync(SesionUsuario sesion, Turno turno, CancellationToken cancelacion)
    {
        var codigoSucursal = await contexto.Sucursales.Where(s => s.Id == sesion.SucursalId).Select(s => s.Codigo).SingleAsync(cancelacion);
        var secuencia = await secuencias.SiguienteAsync(sesion.CajaId, TiposSecuencia.Transaccion, cancelacion);

        var venta = Venta.Iniciar(sesion.SucursalId, codigoSucursal, sesion.CajaId, sesion.CajaCodigo, turno.Id, secuencia,
            sesion.UsuarioId, sesion.Nombre, reloj.GetUtcNow());
        contexto.Ventas.Add(venta);
        await contexto.SaveChangesAsync(cancelacion);
        return venta;
    }

    private async Task<(Turno? Turno, RespuestaVenta? Rechazo)> TurnoDelUsuarioAsync(SesionUsuario sesion, CancellationToken cancelacion)
    {
        var turno = await contexto.Turnos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto, cancelacion);

        if (turno is null)
            return (null, new RespuestaVenta(CodigoResultadoVenta.TurnoNoAbierto, "Debe abrir turno antes de vender.", null));
        if (turno.UsuarioActualId != sesion.UsuarioId)
            return (null, new RespuestaVenta(CodigoResultadoVenta.TurnoDeOtroUsuario, $"La caja tiene abierto el turno {turno.Numero} de {turno.UsuarioActualNombre}.", null));

        return (turno, null);
    }

    private async Task<(Venta? Venta, RespuestaVenta? Rechazo)> CargarVentaEditableAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return (null, rechazo);

        var venta = await contexto.Ventas.Include(v => v.Lineas).SingleOrDefaultAsync(v => v.Id == ventaId, cancelacion);
        if (venta is null || venta.CajaId != sesion.CajaId || venta.UsuarioId != sesion.UsuarioId || venta.TurnoId != turno!.Id)
            return (null, new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, "La venta no existe o no pertenece a su turno.", null));
        if (venta.Estado != EstadoVenta.EnCurso)
            return (null, new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, $"La venta {venta.NumeroTransaccion} ya no se puede modificar.", venta.ADatos()));

        return (venta, null);
    }

    private static RespuestaVenta Correcta(Venta venta) => new(CodigoResultadoVenta.Correcto, null, venta.ADatos());

    private static RespuestaVenta SinPermiso(ResultadoPermiso permiso, string codigoPermiso, Venta venta) =>
        permiso.AutorizacionRechazada
            ? new RespuestaVenta(CodigoResultadoVenta.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció. Solicítela de nuevo.", venta.ADatos(), codigoPermiso)
            : new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "Esta operación requiere autorización de un supervisor.", venta.ADatos(), codigoPermiso);

    /// <summary>Acepta "código" o "cantidad*código" (RF-14). La cantidad explícita tiene prioridad.</summary>
    internal static bool TryInterpretarEntrada(string? entrada, decimal? cantidad, out string codigo, out decimal? cantidadFinal)
    {
        codigo = entrada?.Trim() ?? string.Empty;
        cantidadFinal = cantidad;

        var posicion = codigo.IndexOf('*');
        if (posicion > 0 && cantidad is null)
        {
            if (!decimal.TryParse(codigo[..posicion], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var multiplicador))
                return false;

            cantidadFinal = multiplicador;
            codigo = codigo[(posicion + 1)..].Trim();
        }

        return codigo.Length > 0 && !codigo.Contains('*');
    }
}

internal sealed class ValidadorAutorizaciones(ContextoDatosPos contexto, TimeProvider reloj) : IValidadorAutorizaciones
{
    public async Task<ResultadoPermiso> VerificarAsync(SesionUsuario sesion, string permiso, Guid? autorizacionId, string tipoEntidad, string? entidadId,
        CancellationToken cancelacion = default)
    {
        if (sesion.TienePermiso(permiso))
            return ResultadoPermiso.PermisoPropio;

        if (autorizacionId is not { } id)
            return new ResultadoPermiso(false, false, false, null, null, null);

        var ahora = reloj.GetUtcNow();
        var autorizacion = await contexto.AutorizacionesOtorgadas.SingleOrDefaultAsync(a => a.Id == id, cancelacion);
        if (autorizacion is null || !autorizacion.PuedeUsarse(permiso, sesion.UsuarioId, sesion.CajaId, ahora))
            return new ResultadoPermiso(false, false, true, null, null, null);

        autorizacion.MarcarUsada(ahora, tipoEntidad, entidadId);
        return new ResultadoPermiso(true, true, false, autorizacion.SupervisorId, autorizacion.SupervisorNombre, autorizacion.Motivo);
    }
}

/// <summary>
/// Indicador de conexión (RF-192). Mientras no exista el Central (Fase C11/H2), la caja se informa sin conexión
/// y cuenta los documentos de la bandeja de salida que aún no se confirman.
/// </summary>
internal sealed class ServicioEstadoSincronizacion(ContextoDatosPos contexto, IConfiguration configuracion) : IEstadoSincronizacion
{
    public async Task<DatosEstadoSincronizacion> ObtenerAsync(CancellationToken cancelacion = default)
    {
        var pendientes = await contexto.BandejaSalida.CountAsync(m => m.Estado != EstadoMensajeSalida.Confirmado, cancelacion);
        var ultima = await contexto.BandejaSalida
            .Where(m => m.Estado == EstadoMensajeSalida.Confirmado)
            .MaxAsync(m => m.ConfirmadoEn, cancelacion);

        return new DatosEstadoSincronizacion(
            CentralConfigurado: !string.IsNullOrWhiteSpace(configuracion["Central:Url"]),
            EnLinea: false,
            DocumentosPendientes: pendientes,
            UltimaSincronizacion: ultima);
    }
}
