using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Devoluciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Ecf;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Tickets;
using CgPos.Pos.Infraestructura.Ventas;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Devoluciones;

internal static class ConversionesDevolucion
{
    public static DatosNotaCredito ADatos(this Devolucion devolucion, DocumentoElectronico? documento, DateOnly hoy) =>
        new(devolucion.Id, devolucion.Numero, devolucion.VentaOrigenId, devolucion.VentaOrigenNumero, devolucion.EncfOrigen, devolucion.VentaOrigenCobradaEn,
            devolucion.ClienteTipoDocumento, devolucion.ClienteDocumento, devolucion.ClienteNombre, devolucion.MotivoCodigo, devolucion.MotivoNombre,
            devolucion.Observacion, devolucion.UsuarioNombre, devolucion.AutorizadoPorNombre, devolucion.RetieneImpuesto, devolucion.EsTotal,
            devolucion.Subtotal, devolucion.Impuesto, devolucion.ImpuestoRetenido, devolucion.Total, devolucion.Saldo, devolucion.VenceEn,
            devolucion.EstadoSaldo(hoy), devolucion.CreadaEn,
            devolucion.Lineas.OrderBy(l => l.NumeroLineaOrigen)
                .Select(l => new DatosLineaNotaCredito(l.NumeroLineaOrigen, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.UnidadMedidaCodigo,
                    l.DecimalesCantidad, l.Cantidad, l.PrecioUnitario, l.PorcentajeImpuesto, l.Base, l.Impuesto, l.ImpuestoRetenido, l.Importe, l.Serial))
                .ToList(),
            documento is null
                ? null
                : new DatosComprobanteElectronico(documento.Encf, documento.TipoComprobante, documento.CodigoSeguridad, documento.FechaFirma,
                    documento.UrlTimbre, documento.Estado));

    /// <summary>Cantidad e importe ya devueltos por línea de la factura (RF-42).</summary>
    public static Dictionary<int, DevueltoLinea> Devuelto(this IEnumerable<Devolucion> devoluciones) =>
        devoluciones.SelectMany(d => d.Lineas)
            .GroupBy(l => l.NumeroLineaOrigen)
            .ToDictionary(grupo => grupo.Key, grupo => new DevueltoLinea(grupo.Sum(l => l.Cantidad), grupo.Sum(l => l.ImporteFactura)));
}

internal sealed class ServicioDevoluciones(
    ContextoDatosPos contexto,
    IValidadorAutorizaciones autorizaciones,
    IParametros parametros,
    IConsultaDocumentos consultaDocumentos,
    EmisionComprobantes emisorEcf,
    GeneradorSecuencias secuencias,
    IImpresoraTicket impresora,
    IBandejaSalida bandejaSalida,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioDevoluciones
{
    private const string TipoEntidadDevolucion = "Devolucion";

    private DateOnly Hoy => DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

    public async Task<RespuestaFacturaDevolucion> BuscarFacturaAsync(SesionUsuario sesion, string numero, CancellationToken cancelacion = default)
    {
        var codigo = (numero ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length == 0)
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.FacturaNoEncontrada, "Escanee o digite el número de la factura.", null);

        var venta = await BuscarVentaAsync(codigo, seguimiento: false, cancelacion);
        if (venta is null)
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.FacturaNoEncontrada,
                $"La factura {codigo} no existe en esta caja. Verifique el número; las facturas de otra caja o sucursal se devuelven cuando la caja esté conectada al Central.",
                null);
        if (venta.Estado != EstadoVenta.Cobrada)
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.FacturaNoCobrada, $"La transacción {venta.NumeroTransaccion} no es una factura cobrada.", null);

        var factura = await ArmarFacturaAsync(sesion, venta, cancelacion);
        if (factura.Lineas.All(l => l.CantidadDisponible <= 0))
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.TodoDevuelto,
                $"Todos los artículos de la factura {venta.NumeroTransaccion} ya fueron devueltos.", factura);

        return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.Correcto, null, factura);
    }

    public async Task<RespuestaDevolucion> RegistrarAsync(SesionUsuario sesion, SolicitudDevolucion solicitud, CancellationToken cancelacion = default)
    {
        var venta = await contexto.Ventas.AsNoTracking().Include(v => v.Lineas).SingleOrDefaultAsync(v => v.Id == solicitud.VentaId, cancelacion);
        if (venta is null)
            return Rechazo(CodigoResultadoDevolucion.FacturaNoEncontrada, "La factura no existe en esta caja.");

        var motivo = await contexto.MotivosDevolucion.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Codigo == (solicitud.MotivoCodigo ?? string.Empty).ToUpper() && m.Activo, cancelacion);
        var cliente = await ClienteAsync(venta, solicitud, cancelacion);

        var diasRetencion = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasRetencionImpuestoDevolucion, sesion.CajaId, cancelacion);
        var mesesVigencia = await parametros.ObtenerEnteroAsync(ClavesParametros.MesesVigenciaNotaCredito, sesion.CajaId, cancelacion);
        var encfOrigen = await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == venta.Id).Select(d => d.Encf).FirstOrDefaultAsync(cancelacion);
        var lineas = solicitud.Lineas.Select(l => new LineaSolicitadaDevolucion(l.NumeroLinea, l.Cantidad, l.Serial)).ToList();
        var ahora = reloj.GetUtcNow();

        Devolucion Armar(IReadOnlyDictionary<int, DevueltoLinea> devuelto, string numero, Guid? turnoId, ResultadoPermiso? permiso) =>
            Devolucion.Registrar(venta, encfOrigen, lineas, devuelto, cliente, motivo?.Codigo, motivo?.Nombre, solicitud.Observacion, numero, turnoId,
                sesion.UsuarioId, sesion.Nombre, permiso?.SupervisorId ?? sesion.UsuarioId, permiso?.SupervisorNombre ?? sesion.Nombre,
                diasRetencion, mesesVigencia, Hoy, ahora, reloj.LocalTimeZone);

        // Se validan las reglas antes de pedir la clave del encargado.
        try
        {
            Armar(await DevueltoAsync(venta.Id, cancelacion), "VALIDACION", null, null);
        }
        catch (ReglaDevolucionExcepcion excepcion)
        {
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, excepcion.Message);
        }

        var permiso = await autorizaciones.VerificarAsync(sesion, CatalogoPermisos.AutorizarDevolucion, solicitud.AutorizacionId, "Venta", venta.NumeroTransaccion,
            cancelacion);
        if (!permiso.Permitido)
            return permiso.AutorizacionRechazada
                ? Rechazo(CodigoResultadoDevolucion.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", CatalogoPermisos.AutorizarDevolucion)
                : Rechazo(CodigoResultadoDevolucion.RequiereAutorizacion, "La devolución requiere la autorización del encargado.", CatalogoPermisos.AutorizarDevolucion);

        // Número, nota de crédito, e-CF, mensaje para el Central y auditoría en una sola transacción.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        Devolucion devolucion;
        EmisionEcf emision;
        try
        {
            var secuencia = await secuencias.SiguienteAsync(sesion.CajaId, TiposSecuencia.NotaCredito, cancelacion);
            var turnoId = await contexto.Turnos.AsNoTracking()
                .Where(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancelacion);

            // Lo ya devuelto se relee dentro de la transacción para no devolver dos veces lo mismo (RF-42).
            devolucion = Armar(await DevueltoAsync(venta.Id, cancelacion), $"NC-{sesion.CajaCodigo}-{secuencia:00000000}", turnoId, permiso);
            contexto.Devoluciones.Add(devolucion);

            emision = await emisorEcf.EmitirNotaCreditoAsync(devolucion, cancelacion);
            devolucion.AsignarComprobante(emision.Documento.Encf);
        }
        catch (ReglaDevolucionExcepcion excepcion)
        {
            await transaccion.RollbackAsync(cancelacion);
            contexto.ChangeTracker.Clear();
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, excepcion.Message);
        }
        catch (EmisionEcfExcepcion excepcion)
        {
            await transaccion.RollbackAsync(cancelacion);
            contexto.ChangeTracker.Clear();
            return Rechazo(excepcion.Codigo switch
            {
                CodigoResultadoVenta.CertificadoNoCargado => CodigoResultadoDevolucion.CertificadoNoCargado,
                CodigoResultadoVenta.ComprobanteNoDisponible => CodigoResultadoDevolucion.ComprobanteNoDisponible,
                _ => CodigoResultadoDevolucion.EcfInvalido,
            }, excepcion.Message);
        }

        var datos = devolucion.ADatos(emision.Documento, Hoy);
        bandejaSalida.Encolar("Devolucion.NotaCreditoEmitida", devolucion.Id,
            new DocumentoNotaCreditoEmitida(datos, devolucion.SucursalId, devolucion.CajaId, devolucion.TurnoId, emision.ParaCentral));
        auditoria.Registrar(new EntradaAuditoria("Devoluciones.NotaCreditoEmitida", TipoEntidadDevolucion, devolucion.Numero,
            Detalle: new
            {
                Factura = venta.NumeroTransaccion,
                devolucion.Encf,
                devolucion.Total,
                devolucion.RetieneImpuesto,
                devolucion.EsTotal,
                Lineas = devolucion.Lineas.Select(l => new { l.NumeroLineaOrigen, l.CodigoInterno, l.Cantidad, l.Importe, l.Serial }),
            },
            Motivo: $"{devolucion.MotivoNombre}{(devolucion.Observacion is null ? null : $": {devolucion.Observacion}")}",
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre),
            AutorizadoPor: permiso.SupervisorId is { } supervisorId ? new UsuarioAuditoria(supervisorId, permiso.SupervisorNombre!) : null));

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
            await transaccion.CommitAsync(cancelacion);
        }
        catch
        {
            EmisionComprobantes.DescartarArchivo(emision);
            throw;
        }

        var aviso = await ImprimirAsync(sesion, datos, esCopia: false, cancelacion);
        return new RespuestaDevolucion(CodigoResultadoDevolucion.Correcto,
            $"Nota de crédito {devolucion.Encf} por RD${devolucion.Total:N2} emitida.{(aviso is null ? null : $" {aviso}")}", NotaCredito: datos);
    }

    public async Task<RespuestaSaldoNotaCredito> ConsultarNotaCreditoAsync(SesionUsuario sesion, string codigo, CancellationToken cancelacion = default)
    {
        var buscado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nota = buscado.Length == 0
            ? null
            : await contexto.Devoluciones.AsNoTracking().FirstOrDefaultAsync(d => d.Encf == buscado || d.Numero == buscado, cancelacion);
        if (nota is null)
            return new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.NotaCreditoNoEncontrada,
                $"La nota de crédito {buscado} no existe en esta caja. Verifique el número; las de otra sucursal se validan con el Central.", null);

        var estado = nota.EstadoSaldo(Hoy);
        var datos = new DatosSaldoNotaCredito(nota.Id, nota.Numero, nota.Encf, nota.ClienteNombre, nota.Total, nota.Saldo, nota.VenceEn, estado);
        return estado switch
        {
            EstadoNotaCredito.Consumida => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.NotaCreditoConsumida,
                $"La nota de crédito {nota.Encf ?? nota.Numero} ya fue consumida.", datos),
            EstadoNotaCredito.Vencida => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.NotaCreditoVencida,
                $"La nota de crédito {nota.Encf ?? nota.Numero} venció el {nota.VenceEn:dd/MM/yyyy}.", datos),
            _ => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.Correcto, $"Saldo disponible: RD${nota.Saldo:N2}.", datos),
        };
    }

    public async Task<RespuestaDevolucion> ReimprimirAsync(SesionUsuario sesion, Guid devolucionId, CancellationToken cancelacion = default)
    {
        var devolucion = await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas)
            .SingleOrDefaultAsync(d => d.Id == devolucionId && d.CajaId == sesion.CajaId, cancelacion);
        if (devolucion is null)
            return Rechazo(CodigoResultadoDevolucion.NotaCreditoNoEncontrada, "La nota de crédito no existe en esta caja.");

        var documento = await contexto.DocumentosElectronicos.AsNoTracking().SingleOrDefaultAsync(d => d.VentaId == devolucion.Id, cancelacion);
        var datos = devolucion.ADatos(documento, Hoy);
        var aviso = await ImprimirAsync(sesion, datos, esCopia: true, cancelacion);

        auditoria.Registrar(new EntradaAuditoria("Devoluciones.NotaCreditoReimpresa", TipoEntidadDevolucion, devolucion.Numero,
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaDevolucion(CodigoResultadoDevolucion.Correcto, aviso ?? "Nota de crédito reimpresa.", NotaCredito: datos);
    }

    public async Task<IReadOnlyList<DatosMotivoDevolucion>> ListarMotivosAsync(CancellationToken cancelacion = default) =>
        await contexto.MotivosDevolucion.AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Nombre)
            .Select(m => new DatosMotivoDevolucion(m.Codigo, m.Nombre))
            .ToListAsync(cancelacion);

    private async Task<Venta?> BuscarVentaAsync(string codigo, bool seguimiento, CancellationToken cancelacion)
    {
        var consulta = contexto.Ventas.Include(v => v.Lineas).AsQueryable();
        if (!seguimiento)
            consulta = consulta.AsNoTracking();

        var venta = await consulta.FirstOrDefaultAsync(v => v.NumeroTransaccion == codigo, cancelacion);
        if (venta is not null || codigo.Length != DocumentoElectronico.LargoEncf)
            return venta;

        // También por el e-NCF impreso en la factura.
        var ventaId = await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.Encf == codigo).Select(d => (Guid?)d.VentaId).FirstOrDefaultAsync(cancelacion);
        return ventaId is null ? null : await consulta.FirstOrDefaultAsync(v => v.Id == ventaId, cancelacion);
    }

    private async Task<Dictionary<int, DevueltoLinea>> DevueltoAsync(Guid ventaId, CancellationToken cancelacion) =>
        (await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas).Where(d => d.VentaOrigenId == ventaId).ToListAsync(cancelacion)).Devuelto();

    private async Task<DatosFacturaDevolucion> ArmarFacturaAsync(SesionUsuario sesion, Venta venta, CancellationToken cancelacion)
    {
        var previas = await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas).Where(d => d.VentaOrigenId == venta.Id)
            .OrderBy(d => d.CreadaEn).ToListAsync(cancelacion);
        var devuelto = previas.Devuelto();
        var encf = await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == venta.Id).Select(d => d.Encf).FirstOrDefaultAsync(cancelacion);
        var diasRetencion = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasRetencionImpuestoDevolucion, sesion.CajaId, cancelacion);

        var cobradaEn = venta.CobradaEn!.Value;
        var dias = Hoy.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cobradaEn, reloj.LocalTimeZone).DateTime).DayNumber;

        var lineas = venta.Lineas.Where(l => l.EstaActiva).OrderBy(l => l.NumeroLinea).Select(l =>
        {
            var previo = devuelto.GetValueOrDefault(l.NumeroLinea) ?? new DevueltoLinea(0m, 0m);
            return new DatosLineaFacturaDevolucion(l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo,
                l.UnidadMedidaCodigo, l.DecimalesCantidad, l.PermiteDecimales, l.Cantidad, previo.Cantidad, Math.Max(0m, l.Cantidad - previo.Cantidad),
                decimal.Round(l.ImporteConImpuesto / l.Cantidad, 2, MidpointRounding.AwayFromZero), l.ImporteConImpuesto - previo.ImporteFactura,
                l.PorcentajeImpuesto, l.TipoArticulo == TipoArticulo.Serializado);
        }).ToList();

        var cliente = venta.ClienteNombre is { } nombre
            ? new DatosClienteVenta(venta.ClienteId, venta.ClienteTipoDocumento, venta.ClienteDocumento, nombre)
            : null;

        return new DatosFacturaDevolucion(venta.Id, venta.NumeroTransaccion, encf, venta.TipoComprobante, cobradaEn, dias, dias > diasRetencion, diasRetencion,
            cliente, lineas, await ListarMotivosAsync(cancelacion),
            previas.Select(d => new DatosNotaCreditoResumen(d.Id, d.Numero, d.Encf, d.Total, d.CreadaEn)).ToList());
    }

    /// <summary>El cliente de la factura; si no tiene, el documento digitado con el nombre del padrón DGII o el digitado (RF-160).</summary>
    private async Task<ClienteDevolucion?> ClienteAsync(Venta venta, SolicitudDevolucion solicitud, CancellationToken cancelacion)
    {
        if (venta.ClienteDocumento is { } documentoFactura)
            return new ClienteDevolucion(venta.ClienteTipoDocumento, documentoFactura, venta.ClienteNombre ?? documentoFactura);

        var validacion = DocumentoIdentidad.Validar(solicitud.ClienteDocumento);
        if (!validacion.EsValido)
            return null;

        var nombre = string.IsNullOrWhiteSpace(solicitud.ClienteNombre)
            ? (await consultaDocumentos.ConsultarAsync(validacion.Documento, cancelacion)).RazonSocial
            : solicitud.ClienteNombre.Trim();

        return nombre is null ? null : new ClienteDevolucion(validacion.Tipo, validacion.Documento, nombre);
    }

    /// <summary>Copia del cliente (con código de barras para consumo) y copia de contabilidad, con textos distintos (RF-163).</summary>
    private async Task<string?> ImprimirAsync(SesionUsuario sesion, DatosNotaCredito datos, bool esCopia, CancellationToken cancelacion)
    {
        var encabezado = await contexto.EncabezadoTicketAsync(parametros, reloj.LocalTimeZone, sesion.CajaId, cancelacion);
        // Las políticas son textos del negocio: si no se configuraron, no se imprimen.
        var politica = await parametros.ObtenerAsync(ClavesParametros.PoliticaNotaCredito, sesion.CajaId, cancelacion);
        var politicaContabilidad = await parametros.ObtenerAsync(ClavesParametros.PoliticaNotaCreditoContabilidad, sesion.CajaId, cancelacion);

        var cliente = await impresora.ImprimirAsync(GeneradorTicket.GenerarNotaCredito(encabezado, datos, copiaContabilidad: false, politica, esCopia), cancelacion);
        var contabilidad = await impresora.ImprimirAsync(GeneradorTicket.GenerarNotaCredito(encabezado, datos, copiaContabilidad: true, politicaContabilidad, esCopia),
            cancelacion);

        return cliente.Correcto && contabilidad.Correcto ? null : cliente.Mensaje ?? contabilidad.Mensaje;
    }

    private static RespuestaDevolucion Rechazo(CodigoResultadoDevolucion codigo, string mensaje, string? permiso = null) => new(codigo, mensaje, permiso);
}
