using CgPos.Contratos.Ventas;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Devoluciones;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Sincronizacion;
using CgPos.Pos.Infraestructura.Fidelidad;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Tickets;
using CgPos.Pos.Infraestructura.Ventas;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Devoluciones;

internal static class ConversionesDevolucion
{
    public static DatosNotaCredito ADatos(this Devolucion devolucion, DocumentoElectronico? documento, DateOnly hoy, int diasVigencia) =>
        new(devolucion.Id, devolucion.Numero, devolucion.VentaOrigenId, devolucion.VentaOrigenNumero, devolucion.EncfOrigen, devolucion.VentaOrigenCobradaEn,
            devolucion.ClienteTipoDocumento, devolucion.ClienteDocumento, devolucion.ClienteNombre, devolucion.MotivoCodigo, devolucion.MotivoNombre,
            devolucion.Observacion, devolucion.UsuarioNombre, devolucion.AutorizadoPorNombre, devolucion.RetieneImpuesto, devolucion.EsTotal,
            devolucion.Subtotal, devolucion.Impuesto, devolucion.ImpuestoRetenido, devolucion.Total, devolucion.Saldo, devolucion.Moneda,
            devolucion.VenceEn(diasVigencia), devolucion.EstadoSaldo(hoy, diasVigencia), devolucion.CreadaEn, devolucion.FechaEmision,
            devolucion.Lineas.OrderBy(l => l.NumeroLineaOrigen)
                .Select(l => new DatosLineaNotaCredito(l.NumeroLineaOrigen, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.UnidadMedidaCodigo,
                    l.DecimalesCantidad, l.Cantidad, l.PrecioUnitario, l.PorcentajeImpuesto, l.Base, l.Impuesto, l.ImpuestoRetenido, l.Importe, l.Serial))
                .ToList(),
            documento is null
                ? null
                : new DatosComprobanteElectronico(documento.Encf, documento.TipoComprobante, documento.CodigoSeguridad, documento.FechaFirma,
                    documento.UrlTimbre, documento.Estado),
            devolucion.PuntosReversados, devolucion.Reembolso, devolucion.ReembolsoReferencia, devolucion.ReembolsoDetalle, devolucion.EsInterna);

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
    IEmisorComprobantes emisorEcf,
    GeneradorSecuencias secuencias,
    IImpresoraTicket impresora,
    IBandejaSalida bandejaSalida,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioDevoluciones
{
    private const string TipoEntidadDevolucion = "Devolucion";

    private DateOnly Hoy => reloj.Ahora().Dia();

    /// <summary>Días de vigencia de las notas de crédito configurados hoy: se aplican al usarlas, no al emitirlas.</summary>
    private Task<int> DiasVigenciaAsync(SesionUsuario sesion, CancellationToken cancelacion) =>
        parametros.ObtenerEnteroAsync(ClavesParametros.DiasVigenciaNotaCredito, sesion.CajaId, cancelacion);

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
            .FirstOrDefaultAsync(m => m.Codigo == solicitud.MotivoCodigo && m.Activo, cancelacion);
        var cliente = await ClienteAsync(venta, solicitud, cancelacion);

        var diasRetencion = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasRetencionImpuestoDevolucion, sesion.CajaId, cancelacion);
        var diasVigencia = await DiasVigenciaAsync(sesion, cancelacion);
        var encfOrigen = await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == venta.Id).Select(d => d.Encf).FirstOrDefaultAsync(cancelacion);
        var lineas = solicitud.Lineas.Select(l => new LineaSolicitadaDevolucion(l.NumeroLinea, l.Cantidad, l.Serial)).ToList();
        var ahora = reloj.Ahora();

        // La mercancía pendiente de entrega no se devuelve: primero se anula el pendiente (RF-233).
        var porEntregar = await PorEntregarAsync(venta.Id, cancelacion);
        if (porEntregar.Count > 0)
        {
            var devueltoPrevio = await DevueltoAsync(venta.Id, cancelacion);
            foreach (var pedida in lineas.Where(p => porEntregar.ContainsKey(p.NumeroLinea)))
            {
                if (venta.Lineas.FirstOrDefault(l => l.NumeroLinea == pedida.NumeroLinea && l.EstaActiva) is not { } lineaFactura)
                    continue;

                var libre = lineaFactura.Cantidad - (devueltoPrevio.GetValueOrDefault(lineaFactura.NumeroLinea)?.Cantidad ?? 0m) - porEntregar[lineaFactura.NumeroLinea];
                if (pedida.Cantidad > libre)
                    return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida,
                        $"{lineaFactura.Descripcion}: {porEntregar[lineaFactura.NumeroLinea]:0.###} están pendientes de entrega. Anule el pendiente antes de devolverlos.");
            }
        }

        Devolucion Armar(IReadOnlyDictionary<int, DevueltoLinea> devuelto, string numero, int? turnoId, ResultadoPermiso? permiso) =>
            Devolucion.Registrar(venta, encfOrigen, lineas, devuelto, cliente, motivo?.Codigo, motivo?.Nombre, solicitud.Observacion, numero, turnoId,
                sesion.UsuarioId, sesion.Nombre, permiso?.SupervisorId ?? sesion.UsuarioId, permiso?.SupervisorNombre ?? sesion.Nombre,
                diasRetencion, Hoy, ahora, reloj.LocalTimeZone, solicitud.Interna);

        // La nota interna no devuelve dinero: solo ajusta la factura.
        if (solicitud.Interna && solicitud.Reembolso != TipoReembolso.SaldoNotaCredito)
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, "Una nota de crédito interna no devuelve dinero: solo ajusta la factura.");

        // Se validan las reglas antes de pedir la clave del encargado.
        try
        {
            Armar(await DevueltoAsync(venta.Id, cancelacion), "VALIDACION", null, null);
        }
        catch (ReglaDevolucionExcepcion excepcion)
        {
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, excepcion.Message);
        }

        var permisoRequerido = solicitud.Interna ? CatalogoPermisos.AutorizarNotaCreditoInterna : CatalogoPermisos.AutorizarDevolucion;
        var permiso = await autorizaciones.VerificarAsync(sesion, permisoRequerido, solicitud.AutorizacionId, "Venta", venta.NumeroTransaccion, cancelacion);
        if (!permiso.Permitido)
            return permiso.AutorizacionRechazada
                ? Rechazo(CodigoResultadoDevolucion.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", permisoRequerido)
                : Rechazo(CodigoResultadoDevolucion.RequiereAutorizacion, solicitud.Interna
                    ? "La nota de crédito interna requiere la autorización de un supervisor."
                    : "La devolución requiere la autorización del encargado.", permisoRequerido);

        // Número, nota de crédito, e-CF, mensaje para el Central y auditoría en una sola transacción.
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        Devolucion devolucion;
        EmisionEcf? emision = null;
        MovimientoPuntos? reversoPuntos = null;
        try
        {
            var digitos = await NumeracionDocumentos.DigitosAsync(parametros, sesion.CajaId, cancelacion);
            var minimo = await NumeracionDocumentos.MinimoAsync(parametros, CgPos.Dominio.Organizacion.CatalogoParametros.ProximaNotaCredito, sesion.CajaId, cancelacion);
            var secuencia = await secuencias.SiguienteAsync(sesion.CajaId, TiposSecuencia.NotaCredito, cancelacion, minimo);
            var codigoSucursal = await contexto.Sucursales.Where(s => s.Id == sesion.SucursalId).Select(s => s.Codigo).SingleAsync(cancelacion);
            var turnoId = await contexto.Turnos.AsNoTracking()
                .Where(t => t.CajaId == sesion.CajaId && t.Estado == EstadoTurno.Abierto)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(cancelacion);

            // Lo ya devuelto se relee dentro de la transacción para no devolver dos veces lo mismo (RF-42).
            devolucion = Armar(await DevueltoAsync(venta.Id, cancelacion), NumeroDocumento.Formatear(codigoSucursal, sesion.CajaCodigo, TipoDocumentoNumerado.NotaCredito, secuencia, digitos), turnoId, permiso);
            contexto.Devoluciones.Add(devolucion);

            // La nota interna no es un comprobante fiscal: no consume e-NCF ni se le firma XML (no va al 607).
            if (!devolucion.EsInterna)
            {
                emision = await emisorEcf.EmitirNotaCreditoAsync(devolucion, cancelacion);
                devolucion.AsignarComprobante(emision.Documento.Encf);
            }

            // El cliente puede llevarse el dinero en vez del saldo a favor (RF-123); la nota de crédito se emite igual.
            if (solicitud.Reembolso != TipoReembolso.SaldoNotaCredito)
            {
                var problema = await ReembolsarAsync(sesion, solicitud, devolucion, turnoId, permiso, ahora, cancelacion);
                if (problema is not null)
                {
                    await transaccion.RollbackAsync(cancelacion);
                    contexto.ChangeTracker.Clear();
                    DescartarArchivo(emision);
                    return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, problema);
                }
            }

            // La devolución reversa los puntos que acumuló la compra, en proporción a lo devuelto (RF-244, RN-21).
            if (venta.TieneFidelidad && venta.PuntosAcumulados > 0)
            {
                var yaReversados = await contexto.Devoluciones.AsNoTracking().Where(d => d.VentaOrigenId == venta.Id).SumAsync(d => d.PuntosReversados, cancelacion);
                var puntos = ReglasFidelidad.PuntosAReversar(venta.PuntosAcumulados, yaReversados, venta.CalcularTotales().Total, devolucion.Total, devolucion.EsTotal);
                if (puntos > 0)
                {
                    var miembro = await contexto.MiembrosFidelidad.AsNoTracking().SingleAsync(m => m.Id == venta.FidelidadMiembroId, cancelacion);
                    reversoPuntos = MovimientoPuntos.Reverso(miembro, puntos, venta.Id, devolucion.Id, devolucion.Numero, devolucion.CajaId, ahora);
                    contexto.MovimientosPuntos.Add(reversoPuntos);
                    devolucion.RegistrarReversoPuntos(puntos);
                }
            }
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

        var datos = devolucion.ADatos(emision?.Documento, Hoy, diasVigencia);
        var turnoNumero = devolucion.TurnoId is { } turnoDevolucion
            ? await contexto.Turnos.AsNoTracking().Where(t => t.Id == turnoDevolucion).Select(t => (long?)t.Numero).SingleOrDefaultAsync(cancelacion)
            : null;
        bandejaSalida.Encolar("Devolucion.NotaCreditoEmitida", devolucion.Numero, DocumentosParaCentral.NotaCreditoEmitida(datos, turnoNumero, emision?.ParaCentral));
        if (reversoPuntos is not null)
            bandejaSalida.Encolar("Fidelidad.MovimientoPuntos", DocumentosParaCentral.ReferenciaPuntos(reversoPuntos), DocumentosParaCentral.MovimientoPuntos(reversoPuntos));
        auditoria.Registrar(new EntradaAuditoria(devolucion.EsInterna ? "Devoluciones.NotaCreditoInterna" : "Devoluciones.NotaCreditoEmitida",
            TipoEntidadDevolucion, devolucion.Numero,
            Detalle: new
            {
                Factura = venta.NumeroTransaccion,
                devolucion.Encf,
                devolucion.EsInterna,
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
            DescartarArchivo(emision);
            throw;
        }

        var aviso = await ImprimirAsync(sesion, datos, esCopia: false, cancelacion);
        var titulo = devolucion.EsInterna
            ? $"Nota de crédito interna {devolucion.Numero} por {devolucion.SimboloMoneda}{devolucion.Total:N2} registrada (sin comprobante fiscal, no se usa como pago)."
            : $"Nota de crédito {devolucion.Encf} por {devolucion.SimboloMoneda}{devolucion.Total:N2} emitida.";
        return new RespuestaDevolucion(CodigoResultadoDevolucion.Correcto, $"{titulo}{(aviso is null ? null : $" {aviso}")}", NotaCredito: datos);
    }

    /// <summary>Borra el XML emitido cuando la devolución no llegó a guardarse; la nota interna no emite nada.</summary>
    private void DescartarArchivo(EmisionEcf? emision)
    {
        if (emision is not null)
            emisorEcf.DescartarArchivo(emision);
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

        var diasVigencia = await DiasVigenciaAsync(sesion, cancelacion);
        var estado = nota.EstadoSaldo(Hoy, diasVigencia);
        var datos = new DatosSaldoNotaCredito(nota.Id, nota.Numero, nota.Encf, nota.ClienteNombre, nota.Total, nota.Saldo, nota.VenceEn(diasVigencia), estado,
            nota.EsInterna);
        if (nota.EsInterna)
            return new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.DevolucionInvalida,
                $"La nota de crédito {nota.Numero} es interna: solo ajusta la factura, no se usa como forma de pago.", datos);

        return estado switch
        {
            EstadoNotaCredito.Consumida => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.NotaCreditoConsumida,
                $"La nota de crédito {nota.Encf ?? nota.Numero} ya fue consumida.", datos),
            EstadoNotaCredito.Vencida => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.NotaCreditoVencida,
                $"La nota de crédito {nota.Encf ?? nota.Numero} venció el {nota.VenceEn(diasVigencia):dd/MM/yyyy}.", datos),
            _ => new RespuestaSaldoNotaCredito(CodigoResultadoDevolucion.Correcto, $"Saldo disponible: {nota.SimboloMoneda}{nota.Saldo:N2}.", datos),
        };
    }

    public async Task<RespuestaDevolucion> ReimprimirAsync(SesionUsuario sesion, int devolucionId, CancellationToken cancelacion = default)
    {
        var devolucion = await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas)
            .SingleOrDefaultAsync(d => d.Id == devolucionId && d.CajaId == sesion.CajaId, cancelacion);
        if (devolucion is null)
            return Rechazo(CodigoResultadoDevolucion.NotaCreditoNoEncontrada, "La nota de crédito no existe en esta caja.");

        var documento = await contexto.DocumentosElectronicos.AsNoTracking().SingleOrDefaultAsync(d => d.VentaId == devolucion.Id, cancelacion);
        var datos = devolucion.ADatos(documento, Hoy, await DiasVigenciaAsync(sesion, cancelacion));
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

    /// <summary>
    /// Entrega el dinero de la devolución según lo permita la configuración (RF-123): efectivo de la gaveta, devolución a la tarjeta
    /// o cheque que emite contabilidad. Devuelve el motivo del rechazo, o nulo si el reembolso quedó registrado.
    /// </summary>
    private async Task<string?> ReembolsarAsync(SesionUsuario sesion, SolicitudDevolucion solicitud, Devolucion devolucion, int? turnoId,
        ResultadoPermiso permiso, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var referencia = solicitud.ReembolsoReferencia?.Trim();
        var detalle = solicitud.ReembolsoDetalle?.Trim();

        switch (solicitud.Reembolso)
        {
            case TipoReembolso.Efectivo:
                if (!await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametros.ReembolsoEfectivo, sesion.CajaId, cancelacion))
                    return "La devolución en efectivo no está habilitada; el cliente queda con el saldo de la nota de crédito.";

                var maximo = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.MontoMaximoReembolsoEfectivo, sesion.CajaId, cancelacion);
                if (maximo is { } tope && devolucion.Total > tope)
                    return $"En efectivo solo se devuelven hasta {devolucion.SimboloMoneda}{tope:N2}; el resto queda en la nota de crédito.";

                if (turnoId is not { } turnoAbierto)
                    return "Para devolver efectivo tiene que haber un turno abierto en la caja.";

                var turno = await contexto.Turnos.SingleAsync(t => t.Id == turnoAbierto, cancelacion);
                var numero = await contexto.MovimientosCaja.Where(m => m.TurnoId == turno.Id).CountAsync(cancelacion) + 1;

                // Sale de la gaveta: baja lo esperado del cuadre igual que un retiro.
                contexto.MovimientosCaja.Add(MovimientoCaja.Reembolso(turno, numero, devolucion.Total, devolucion.Moneda,
                    $"Devolución {devolucion.Numero} de la factura {devolucion.VentaOrigenNumero}", sesion.UsuarioId, sesion.Nombre,
                    permiso.SupervisorId, permiso.SupervisorNombre, ahora));
                break;

            case TipoReembolso.Tarjeta:
                if (!await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametros.ReembolsoTarjeta, sesion.CajaId, cancelacion))
                    return "La devolución a la tarjeta no está habilitada.";
                if (string.IsNullOrWhiteSpace(referencia))
                    return "Indique la autorización con la que el terminal devolvió el monto a la tarjeta.";
                break;

            case TipoReembolso.Cheque:
                if (!await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametros.ReembolsoCheque, sesion.CajaId, cancelacion))
                    return "La devolución con cheque no está habilitada.";
                if (string.IsNullOrWhiteSpace(detalle))
                    return "Indique a nombre de quién y en qué banco se emite el cheque.";
                break;

            default:
                return "Tipo de reembolso no válido.";
        }

        devolucion.RegistrarReembolso(solicitud.Reembolso, referencia, detalle, ahora);
        return null;
    }

    private async Task<Venta?> BuscarVentaAsync(string codigo, bool seguimiento, CancellationToken cancelacion)
    {
        var consulta = contexto.Ventas.Include(v => v.Lineas).AsQueryable();
        if (!seguimiento)
            consulta = consulta.AsNoTracking();

        var venta = await consulta.FirstOrDefaultAsync(v => v.NumeroTransaccion == codigo, cancelacion);
        if (venta is not null || codigo.Length != DocumentoElectronico.LargoEncf)
            return venta;

        // También por el e-NCF impreso en la factura.
        var ventaId = await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.Encf == codigo).Select(d => (int?)d.VentaId).FirstOrDefaultAsync(cancelacion);
        return ventaId is null ? null : await consulta.FirstOrDefaultAsync(v => v.Id == ventaId, cancelacion);
    }

    /// <summary>Cantidad por línea de la factura que sigue pendiente de entrega en pendientes no anulados.</summary>
    private async Task<Dictionary<int, decimal>> PorEntregarAsync(int ventaId, CancellationToken cancelacion) =>
        (await contexto.PendientesEntrega.AsNoTracking().Include(p => p.Lineas)
            .Where(p => p.VentaId == ventaId && p.Estado != EstadoPendiente.Anulado)
            .ToListAsync(cancelacion))
        .SelectMany(p => p.Lineas)
        .GroupBy(l => l.NumeroLineaVenta)
        .Select(grupo => (Linea: grupo.Key, Pendiente: grupo.Sum(l => l.CantidadPendiente)))
        .Where(x => x.Pendiente > 0)
        .ToDictionary(x => x.Linea, x => x.Pendiente);

    private async Task<Dictionary<int, DevueltoLinea>> DevueltoAsync(int ventaId, CancellationToken cancelacion) =>
        (await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas).Where(d => d.VentaOrigenId == ventaId).ToListAsync(cancelacion)).Devuelto();

    private async Task<DatosFacturaDevolucion> ArmarFacturaAsync(SesionUsuario sesion, Venta venta, CancellationToken cancelacion)
    {
        var previas = await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas).Where(d => d.VentaOrigenId == venta.Id)
            .OrderBy(d => d.CreadaEn).ToListAsync(cancelacion);
        var devuelto = previas.Devuelto();
        var porEntregar = await PorEntregarAsync(venta.Id, cancelacion);
        var encf = await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == venta.Id).Select(d => d.Encf).FirstOrDefaultAsync(cancelacion);
        var diasRetencion = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasRetencionImpuestoDevolucion, sesion.CajaId, cancelacion);

        var cobradaEn = venta.CobradaEn!.Value;
        var dias = Hoy.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(cobradaEn, reloj.LocalTimeZone).DateTime).DayNumber;

        var lineas = venta.Lineas.Where(l => l.EstaActiva).OrderBy(l => l.NumeroLinea).Select(l =>
        {
            var previo = devuelto.GetValueOrDefault(l.NumeroLinea) ?? new DevueltoLinea(0m, 0m);
            var pendiente = porEntregar.GetValueOrDefault(l.NumeroLinea);
            var disponible = Math.Max(0m, l.Cantidad - previo.Cantidad - pendiente);
            var precio = decimal.Round(l.ImporteConImpuesto / l.Cantidad, 2, MidpointRounding.AwayFromZero);

            // Con mercancía pendiente de entrega lo disponible no es el resto de la línea: se estima por precio.
            var importeDisponible = pendiente > 0
                ? decimal.Round(l.ImporteConImpuesto / l.Cantidad * disponible, 2, MidpointRounding.AwayFromZero)
                : l.ImporteConImpuesto - previo.ImporteFactura;
            return new DatosLineaFacturaDevolucion(l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo,
                l.UnidadMedidaCodigo, l.DecimalesCantidad, l.PermiteDecimales, l.Cantidad, previo.Cantidad, disponible,
                precio, importeDisponible, l.PorcentajeImpuesto, l.TipoArticulo == TipoArticulo.Serializado);
        }).ToList();

        var cliente = venta.ClienteNombre is { } nombre
            ? new DatosClienteVenta(venta.ClienteId, venta.ClienteTipoDocumento, venta.ClienteDocumento, nombre)
            : null;

        return new DatosFacturaDevolucion(venta.Id, venta.NumeroTransaccion, encf, venta.TipoComprobante, cobradaEn, dias, dias > diasRetencion, diasRetencion,
            cliente, lineas, await ListarMotivosAsync(cancelacion),
            previas.Select(d => new DatosNotaCreditoResumen(d.Id, d.Numero, d.Encf, d.Total, d.CreadaEn)).ToList());
    }

    /// <summary>El cliente de la factura; si no tiene, el documento digitado con el nombre del cliente registrado o el digitado (RF-160).</summary>
    private async Task<ClienteDevolucion?> ClienteAsync(Venta venta, SolicitudDevolucion solicitud, CancellationToken cancelacion)
    {
        if (venta.ClienteDocumento is { } documentoFactura)
            return new ClienteDevolucion(venta.ClienteTipoDocumento, documentoFactura, venta.ClienteNombre ?? documentoFactura);

        var validacion = DocumentoIdentidad.Validar(solicitud.ClienteDocumento);
        if (!validacion.EsValido)
            return null;

        // El nombre sale del maestro de clientes; si el documento no está registrado, lo digita el cajero.
        var nombre = string.IsNullOrWhiteSpace(solicitud.ClienteNombre)
            ? (await consultaDocumentos.ConsultarAsync(validacion.Documento, cancelacion)).Cliente?.Nombre
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
