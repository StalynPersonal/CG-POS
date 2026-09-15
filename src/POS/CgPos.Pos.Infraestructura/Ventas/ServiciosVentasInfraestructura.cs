using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Perifericos;
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

    public static DatosVenta ADatos(this Venta venta, decimal montoIdentificacion)
    {
        var totales = venta.CalcularTotales();

        var lineas = venta.Lineas
            .OrderBy(l => l.LineaAnuladaNumero ?? l.NumeroLinea)
            .ThenBy(l => l.EsReverso ? 1 : 0)
            .ThenBy(l => l.NumeroLinea)
            .Select(l => new DatosLineaVenta(
                l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo, l.UnidadMedidaCodigo,
                l.DecimalesCantidad, l.Cantidad, l.PrecioUnitario, l.ImporteConImpuesto, l.PorcentajeImpuesto, l.Lista, l.MotivoPrecio,
                l.LeidaDeBalanza, l.EsReverso, l.LineaAnuladaNumero, l.Anulada, l.Serial))
            .ToList();

        var cliente = venta.ClienteNombre is { } nombre
            ? new DatosClienteVenta(venta.ClienteId, venta.ClienteTipoDocumento, venta.ClienteDocumento, nombre)
            : null;

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
                totales.Desglose.Select(d => new DatosDesgloseImpuesto(d.Porcentaje, d.IndicadorFacturacion, d.Base, d.Impuesto, d.Total)).ToList()),
            venta.TipoComprobante,
            cliente,
            venta.LimiteCompra,
            venta.LimiteCompra is { } limite && totales.Total > limite,
            venta.TipoComprobante == TipoComprobante.FacturaConsumo && venta.ClienteDocumento is null && totales.Total >= montoIdentificacion,
            montoIdentificacion);
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
        CodigoErrorVenta.ComprobanteNoPermitido => CodigoResultadoVenta.ComprobanteNoPermitido,
        CodigoErrorVenta.DocumentoRequerido => CodigoResultadoVenta.DocumentoRequerido,
        CodigoErrorVenta.SinLineas => CodigoResultadoVenta.SinLineas,
        CodigoErrorVenta.RequiereSerial => CodigoResultadoVenta.RequiereSerial,
        CodigoErrorVenta.SerialDuplicado => CodigoResultadoVenta.SerialDuplicado,
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
    IConsultaDocumentos consultaDocumentos,
    IValidadorAutorizaciones autorizaciones,
    IParametros parametros,
    IBalanza balanza,
    GeneradorSecuencias secuencias,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioVentas
{
    private const string TipoEntidadVenta = "Venta";

    /// <summary>Se lee de parámetros al validar el turno, que es el primer paso de toda operación.</summary>
    private decimal _montoIdentificacion = ReglasComprobante.MontoIdentificacionConsumoPredeterminado;

    public async Task<RespuestaVenta> ObtenerActualAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var venta = await VentaEnCursoAsync(sesion, turno!, cancelacion) ?? await IniciarVentaAsync(sesion, turno!, cancelacion);
        return Correcta(venta);
    }

    public async Task<RespuestaVenta> AgregarDesdeBalanzaAsync(SesionUsuario sesion, Guid ventaId, string codigo, CancellationToken cancelacion = default)
    {
        if (!sesion.TienePermiso(CatalogoPermisos.RegistrarVenta))
            return new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "No tiene permiso para registrar ventas.", null, CatalogoPermisos.RegistrarVenta);

        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var codigoLimpio = codigo?.Trim() ?? string.Empty;
        var articulo = await consultaArticulos.BuscarPorCodigoAsync(codigoLimpio, cancelacion);
        if (articulo is null)
            return new RespuestaVenta(CodigoResultadoVenta.ArticuloNoEncontrado, $"No se encontró el artículo {codigoLimpio}.", Datos(venta!));
        if (articulo.Tipo != TipoArticulo.Pesado)
            return new RespuestaVenta(CodigoResultadoVenta.CantidadInvalida, $"{articulo.Descripcion} no se vende por peso.", Datos(venta!));

        var lectura = await balanza.LeerPesoAsync(cancelacion);
        if (lectura is not { Estable: true })
            return new RespuestaVenta(CodigoResultadoVenta.BalanzaSinLectura,
                lectura is null ? "La balanza no responde. Verifique que esté encendida y conectada." : "El peso no está estable. Espere a que la balanza se detenga.", Datos(venta!));

        if (ReglasBalanza.PesoNeto(lectura.Peso, articulo.Tara) is not { } neto)
            return new RespuestaVenta(CodigoResultadoVenta.BalanzaSinLectura,
                $"La balanza marca {lectura.Peso:0.000} {lectura.Unidad}: no hay peso neto después de la tara ({articulo.Tara ?? 0:0.000}).", Datos(venta!));

        var pesado = articulo.AArticuloParaVenta() with { PesoLeido = neto, PrecioLeido = null };
        return await EjecutarAsync(venta!, () => venta!.AgregarArticulo(pesado, null, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> AgregarArticuloAsync(SesionUsuario sesion, Guid ventaId, string codigo, decimal? cantidad, string? serial = null, CancellationToken cancelacion = default)
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
            return new RespuestaVenta(CodigoResultadoVenta.ArticuloNoEncontrado, $"No se encontró el artículo {codigoLimpio}.", Datos(venta!));

        return await EjecutarAsync(venta!, () => venta!.AgregarArticulo(articulo.AArticuloParaVenta(), cantidadFinal, reloj.GetUtcNow(), serial), cancelacion);
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

    public Task<RespuestaVenta> LimpiarAsync(SesionUsuario sesion, Guid ventaId, Guid? autorizacionId, CancellationToken cancelacion = default) =>
        AnularYContinuarAsync(sesion, ventaId, CatalogoPermisos.LimpiarPantalla, "Pantalla limpiada", autorizacionId, "Ventas.PantallaLimpiada", cancelacion);

    public async Task<RespuestaVenta> AnularAsync(SesionUsuario sesion, Guid ventaId, string? motivo, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        // Sin autorización el motivo es obligatorio; con autorización se toma el motivo que dio el supervisor.
        if (string.IsNullOrWhiteSpace(motivo) && autorizacionId is null && sesion.TienePermiso(CatalogoPermisos.AnularVenta))
            return new RespuestaVenta(CodigoResultadoVenta.MotivoRequerido, "Indique el motivo de la anulación.", null);

        return await AnularYContinuarAsync(sesion, ventaId, CatalogoPermisos.AnularVenta, motivo, autorizacionId, "Ventas.Anulada", cancelacion);
    }

    public async Task<RespuestaVenta> AsignarClienteAsync(SesionUsuario sesion, Guid ventaId, string documento, string? nombre, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var consulta = await consultaDocumentos.ConsultarAsync(documento ?? string.Empty, cancelacion);

        if (!consulta.FormatoValido || consulta.Tipo is null)
            return new RespuestaVenta(CodigoResultadoVenta.DocumentoInvalido, "Digite un RNC (9 dígitos) o una cédula (11 dígitos).", Datos(venta!));

        // Hay cédulas antiguas que no cumplen el dígito verificador: se aceptan si el padrón o el maestro de clientes las conocen.
        if (!consulta.DigitoVerificadorValido && !consulta.EnPadron && consulta.Cliente is null)
            return new RespuestaVenta(CodigoResultadoVenta.DocumentoInvalido, $"El documento {consulta.Documento} no es válido (dígito verificador).", Datos(venta!));

        ClienteVenta cliente;
        if (consulta.Cliente is { } registrado)
        {
            cliente = new ClienteVenta(registrado.ClienteId, registrado.TipoDocumento, registrado.Documento, registrado.Nombre, registrado.TipoComprobante);
        }
        else
        {
            var nombreFinal = string.IsNullOrWhiteSpace(nombre) ? consulta.RazonSocial : nombre.Trim();
            if (string.IsNullOrWhiteSpace(nombreFinal))
                return new RespuestaVenta(CodigoResultadoVenta.NombreRequerido,
                    $"El documento {consulta.Documento} no está en el padrón DGII ni registrado. Indique el nombre del cliente.", Datos(venta!));

            // Un RNC del padrón factura a crédito fiscal por defecto; una cédula, a consumo.
            var comprobante = consulta.Tipo == TipoDocumentoIdentidad.Rnc ? TipoComprobante.FacturaCreditoFiscal : TipoComprobante.FacturaConsumo;
            cliente = new ClienteVenta(null, consulta.Tipo, consulta.Documento, nombreFinal, comprobante);
        }

        return await EjecutarAsync(venta!, () => venta!.AsignarCliente(cliente, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> QuitarClienteAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.QuitarCliente(reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> CambiarComprobanteAsync(SesionUsuario sesion, Guid ventaId, TipoComprobante tipo, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        if (venta!.TipoComprobante == tipo)
            return Correcta(venta);

        // Primero se valida que el cambio sea posible, para no pedir clave de supervisor en vano.
        try
        {
            ValidarComprobante(venta, tipo);
        }
        catch (ReglaVentaExcepcion excepcion)
        {
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(venta));
        }

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.CambiarComprobante, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, CatalogoPermisos.CambiarComprobante, venta);

        var anterior = venta.TipoComprobante;
        return await EjecutarAsync(venta, () =>
        {
            venta.CambiarComprobante(tipo, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.ComprobanteCambiado", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Anterior = anterior, Nuevo = tipo, venta.ClienteDocumento },
                Motivo: permiso.Motivo,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);
    }

    public async Task<RespuestaVenta> EstablecerLimiteCompraAsync(SesionUsuario sesion, Guid ventaId, decimal? limite, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        return await EjecutarAsync(venta!, () => venta!.EstablecerLimiteCompra(limite, reloj.GetUtcNow()), cancelacion);
    }

    public async Task<RespuestaVenta> PonerEnEsperaAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var resultado = await EjecutarAsync(venta!, () =>
        {
            venta!.PonerEnEspera(reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria("Ventas.PuestaEnEspera", TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Total = venta.CalcularTotales().Total },
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        }, cancelacion);

        return resultado.Exitosa ? await ContinuarConNuevaAsync(sesion, cancelacion) : resultado;
    }

    public async Task<IReadOnlyList<DatosVentaEnEspera>> ListarEnEsperaAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return [];

        var enEspera = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas)
            .Where(v => v.TurnoId == turno!.Id && v.UsuarioId == sesion.UsuarioId && v.Estado == EstadoVenta.EnEspera)
            .ToListAsync(cancelacion);

        return enEspera
            .OrderBy(v => v.PuestaEnEsperaEn)
            .Select(v =>
            {
                var totales = v.CalcularTotales();
                return new DatosVentaEnEspera(v.Id, v.NumeroTransaccion, v.ClienteNombre, totales.Total, totales.CantidadLineas, v.PuestaEnEsperaEn!.Value);
            })
            .ToList();
    }

    public async Task<RespuestaVenta> RetomarAsync(SesionUsuario sesion, Guid ventaId, CancellationToken cancelacion = default)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        if (rechazo is not null)
            return rechazo;

        var enEspera = await contexto.Ventas.Include(v => v.Lineas).SingleOrDefaultAsync(v => v.Id == ventaId, cancelacion);
        if (enEspera is null || enEspera.TurnoId != turno!.Id || enEspera.UsuarioId != sesion.UsuarioId || enEspera.Estado != EstadoVenta.EnEspera)
            return new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, "La factura no está en espera en su turno.", null);

        var ahora = reloj.GetUtcNow();
        var actual = await VentaEnCursoAsync(sesion, turno, cancelacion);
        if (actual is not null)
        {
            if (actual.TieneLineasActivas)
                actual.PonerEnEspera(ahora);
            else if (actual.Lineas.Count == 0)
                contexto.Ventas.Remove(actual); // nunca tuvo artículos: no es un documento
            else
                actual.Anular("Sin artículos al retomar una factura en espera", sesion.UsuarioId, sesion.Nombre, ahora);
        }

        enEspera.Retomar(ahora);
        auditoria.Registrar(new EntradaAuditoria("Ventas.Retomada", TipoEntidadVenta, enEspera.NumeroTransaccion,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return Correcta(enEspera);
    }

    public async Task<RespuestaVenta> SuspenderAsync(SesionUsuario sesion, Guid? autorizacionId, CancellationToken cancelacion = default)
    {
        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.SuspenderVenta, autorizacionId, "Caja", sesion.CajaCodigo, cancelacion);
        if (!permiso.Permitido)
        {
            return permiso.AutorizacionRechazada
                ? new RespuestaVenta(CodigoResultadoVenta.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", null, CatalogoPermisos.SuspenderVenta)
                : new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "Suspender operaciones requiere autorización de un supervisor.", null, CatalogoPermisos.SuspenderVenta);
        }

        auditoria.Registrar(new EntradaAuditoria("Caja.OperacionesSuspendidas", "Caja", sesion.CajaCodigo,
            Motivo: permiso.Motivo,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: Autorizador(permiso)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaVenta(CodigoResultadoVenta.Correcto, null, null);
    }

    private async Task<RespuestaVenta> AnularYContinuarAsync(SesionUsuario sesion, Guid ventaId, string codigoPermiso, string? motivo, Guid? autorizacionId,
        string accionAuditoria, CancellationToken cancelacion)
    {
        var (venta, rechazo) = await CargarVentaEditableAsync(sesion, ventaId, cancelacion);
        if (rechazo is not null)
            return rechazo;

        // Una venta vacía no se anula: no hay nada que limpiar y no se gasta un número.
        if (venta!.Lineas.Count == 0)
            return Correcta(venta);

        var permiso = await autorizaciones.VerificarAsync(sesion, codigoPermiso, autorizacionId, TipoEntidadVenta, venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return SinPermiso(permiso, codigoPermiso, venta);

        var motivoFinal = string.IsNullOrWhiteSpace(motivo) ? permiso.Motivo : motivo.Trim();
        var totales = venta.CalcularTotales();

        var resultado = await EjecutarAsync(venta, () =>
        {
            venta.Anular(motivoFinal ?? string.Empty, sesion.UsuarioId, sesion.Nombre, reloj.GetUtcNow());
            auditoria.Registrar(new EntradaAuditoria(accionAuditoria, TipoEntidadVenta, venta.NumeroTransaccion,
                Detalle: new { Lineas = totales.CantidadLineas, totales.Total },
                Motivo: motivoFinal,
                Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
                AutorizadoPor: Autorizador(permiso)));
        }, cancelacion);

        return resultado.Exitosa ? await ContinuarConNuevaAsync(sesion, cancelacion) : resultado;
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
                AutorizadoPor: Autorizador(permiso)));
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
            return new RespuestaVenta(excepcion.Codigo.ACodigoResultado(), excepcion.Message, Datos(ventaActual));
        }
    }

    private async Task<RespuestaVenta> ContinuarConNuevaAsync(SesionUsuario sesion, CancellationToken cancelacion)
    {
        var (turno, rechazo) = await TurnoDelUsuarioAsync(sesion, cancelacion);
        return rechazo ?? Correcta(await IniciarVentaAsync(sesion, turno!, cancelacion));
    }

    private Task<Venta?> VentaEnCursoAsync(SesionUsuario sesion, Turno turno, CancellationToken cancelacion) =>
        contexto.Ventas.Include(v => v.Lineas)
            .Where(v => v.TurnoId == turno.Id && v.UsuarioId == sesion.UsuarioId && v.Estado == EstadoVenta.EnCurso)
            .OrderByDescending(v => v.IniciadaEn)
            .FirstOrDefaultAsync(cancelacion);

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

        _montoIdentificacion = await parametros.ObtenerDecimalAsync(ClavesParametros.MontoIdentificacionConsumo, sesion.CajaId,
            ReglasComprobante.MontoIdentificacionConsumoPredeterminado, cancelacion);
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
            return (null, new RespuestaVenta(CodigoResultadoVenta.VentaNoEditable, $"La venta {venta.NumeroTransaccion} ya no se puede modificar.", Datos(venta)));

        return (venta, null);
    }

    /// <summary>Aplica las mismas validaciones del dominio sobre una copia del estado, sin modificar la venta.</summary>
    private static void ValidarComprobante(Venta venta, TipoComprobante tipo)
    {
        if (!ReglasComprobante.EsDeVenta(tipo))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.ComprobanteNoPermitido, $"El comprobante {ReglasComprobante.Nombre(tipo)} no se emite en una venta de caja.");
        if (!ReglasComprobante.ClienteCumple(tipo, venta.ClienteTipoDocumento, venta.ClienteDocumento))
            throw new ReglaVentaExcepcion(CodigoErrorVenta.DocumentoRequerido,
                $"El comprobante {ReglasComprobante.Nombre(tipo)} requiere asignar primero un cliente con {(tipo == TipoComprobante.Gubernamental ? "RNC" : "RNC o cédula")}.");
    }

    private DatosVenta Datos(Venta venta) => venta.ADatos(_montoIdentificacion);

    private RespuestaVenta Correcta(Venta venta) => new(CodigoResultadoVenta.Correcto, null, Datos(venta));

    private RespuestaVenta SinPermiso(ResultadoPermiso permiso, string codigoPermiso, Venta venta) =>
        permiso.AutorizacionRechazada
            ? new RespuestaVenta(CodigoResultadoVenta.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció. Solicítela de nuevo.", Datos(venta), codigoPermiso)
            : new RespuestaVenta(CodigoResultadoVenta.RequiereAutorizacion, "Esta operación requiere autorización de un supervisor.", Datos(venta), codigoPermiso);

    private static UsuarioAuditoria? Autorizador(ResultadoPermiso permiso) =>
        permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null;

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
