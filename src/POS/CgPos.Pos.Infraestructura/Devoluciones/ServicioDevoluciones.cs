using CgPos.Contratos.Central;
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
using CgPos.Pos.Aplicacion.Sincronizacion;
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
    IClienteCentral central,
    TimeProvider reloj) : IServicioDevoluciones
{
    private const string TipoEntidadDevolucion = "Devolucion";

    private DateOnly Hoy => reloj.Ahora().Dia();

    /// <summary>Días de vigencia de las notas de crédito configurados hoy: se aplican al usarlas, no al emitirlas.</summary>
    private Task<int> DiasVigenciaAsync(SesionUsuario sesion, CancellationToken cancelacion) =>
        parametros.ObtenerEnteroAsync(ClavesParametros.DiasVigenciaNotaCredito, sesion.CajaId, cancelacion);

    /// <summary>
    /// La factura siempre se busca en el Central, aunque la haya vendido esta misma caja: es el único que sabe cuánto se devolvió
    /// ya de cada línea en toda la empresa, y una nota de crédito nunca se emite contra una factura que el Central no tenga
    /// registrada. Lo que llega se guarda como copia temporal y se borra al emitir la nota.
    /// </summary>
    public async Task<RespuestaFacturaDevolucion> BuscarFacturaAsync(SesionUsuario sesion, string numero, CancellationToken cancelacion = default)
    {
        var codigo = (numero ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length == 0)
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.FacturaNoEncontrada, "Escanee o digite el número de la factura.", null);

        var consulta = await central.ConsultarFacturaAsync(codigo, cancelacion);
        if (!consulta.CentralRespondio)
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.SinConexionCentral,
                $"No se pudo consultar la factura {codigo} en el Central. Las notas de crédito se emiten contra la factura registrada en el Central, así que sin conexión no se pueden emitir. ({consulta.Error})",
                null);
        if (consulta.Factura is not { } remota)
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.FacturaNoEncontrada,
                $"La factura {codigo} no está registrada en el Central. Verifique el número; si se acaba de facturar, espere a que la caja termine de sincronizar.",
                null);

        var copia = await GuardarCopiaAsync(remota, cancelacion);
        var factura = await ArmarFacturaAsync(sesion, copia, cancelacion);
        if (factura.Lineas.All(l => l.CantidadDisponible <= 0))
            return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.TodoDevuelto,
                $"Todos los artículos de la factura {remota.Numero} ya fueron devueltos.", factura);

        return new RespuestaFacturaDevolucion(CodigoResultadoDevolucion.Correcto, null, factura);
    }

    /// <summary>Guarda la factura del Central en la tabla temporal, reemplazando cualquier copia anterior de ese mismo número.</summary>
    private async Task<FacturaConsultada> GuardarCopiaAsync(DatosFacturaParaCaja remota, CancellationToken cancelacion)
    {
        await BorrarCopiaAsync(remota.Numero, cancelacion);

        // Los artículos se resuelven contra el maestro local, que baja completo a todas las cajas. El indicador de facturación del
        // e-CF sale del impuesto del artículo: la caja no lo recibe del Central porque es un dato de su propio maestro.
        var codigos = remota.Lineas.Select(l => l.Codigo).Distinct().ToList();
        var articulos = await (from a in contexto.Articulos.AsNoTracking()
                               where codigos.Contains(a.Codigo)
                               join i in contexto.Impuestos.AsNoTracking() on a.ImpuestoId equals i.Id into impuestos
                               from impuesto in impuestos.DefaultIfEmpty()
                               select new { a.Id, a.Codigo, a.EsServicio, Indicador = impuesto != null ? impuesto.IndicadorFacturacion : 1 })
            .ToDictionaryAsync(a => a.Codigo, cancelacion);
        var simbolo = await contexto.Monedas.AsNoTracking().Where(m => m.Codigo == remota.Moneda).Select(m => m.Simbolo)
            .FirstOrDefaultAsync(cancelacion) ?? remota.Moneda;

        var copia = FacturaConsultada.Crear(remota.Numero, remota.Encf, remota.SucursalCodigo, remota.CajaCodigo, remota.TipoComprobante,
            remota.CobradaEn, DocumentoIdentidad.Validar(remota.ClienteDocumento ?? string.Empty) is { EsAceptable: true } validacion ? validacion.Tipo : null,
            remota.ClienteDocumento, remota.ClienteNombre, remota.Moneda, simbolo, remota.Total, reloj.Ahora());
        contexto.FacturasConsultadas.Add(copia);

        foreach (var linea in remota.Lineas.OrderBy(l => l.NumeroLinea))
        {
            var articulo = articulos.GetValueOrDefault(linea.Codigo);
            copia.AgregarLinea(linea.NumeroLinea, articulo?.Id ?? 0, linea.Codigo, linea.CodigoLeido, linea.Descripcion, linea.TipoArticulo,
                linea.UnidadMedida ?? string.Empty, linea.DecimalesCantidad, linea.Cantidad, linea.Importe, linea.PorcentajeImpuesto,
                articulo?.Indicador ?? 1, articulo?.EsServicio ?? false, linea.Serial, linea.Devuelta);
        }

        // Si la vendió esta caja, se enlaza su venta: de ella salen los puntos acumulados y la mercancía pendiente de entregar,
        // que el Central no le puede decir a la caja.
        copia.EnlazarVentaLocal(await contexto.Ventas.AsNoTracking()
            .Where(v => v.NumeroTransaccion == remota.Numero && v.Estado == EstadoVenta.Cobrada)
            .Select(v => (int?)v.Id)
            .FirstOrDefaultAsync(cancelacion));

        await contexto.SaveChangesAsync(cancelacion);
        return copia;
    }

    /// <summary>
    /// Borra la copia temporal de esa factura, con sus líneas. Se llama al consultar de nuevo y al emitir la nota. De paso se lleva
    /// las copias viejas de otras facturas: cada consulta que el cajero abandona deja una, y nadie más las va a limpiar.
    /// </summary>
    private async Task BorrarCopiaAsync(string numero, CancellationToken cancelacion)
    {
        var caducan = reloj.Ahora().AddDays(-1);
        var previas = await contexto.FacturasConsultadas.Include(f => f.Lineas)
            .Where(f => f.Numero == numero || f.ConsultadaEn < caducan)
            .ToListAsync(cancelacion);
        if (previas.Count == 0)
            return;

        contexto.FacturasConsultadas.RemoveRange(previas);
        await contexto.SaveChangesAsync(cancelacion);
    }

    public async Task<RespuestaDevolucion> RegistrarAsync(SesionUsuario sesion, SolicitudDevolucion solicitud, CancellationToken cancelacion = default)
    {
        // La factura siempre viene del Central: lo que se devuelve es la copia que dejó la consulta, nunca la venta local.
        var numero = (solicitud.FacturaNumero ?? string.Empty).Trim().ToUpperInvariant();
        var copia = numero.Length == 0
            ? null
            : await contexto.FacturasConsultadas.AsNoTracking().Include(f => f.Lineas)
                .FirstOrDefaultAsync(f => f.Numero == numero, cancelacion);

        if (copia is null)
            return Rechazo(CodigoResultadoDevolucion.FacturaNoEncontrada,
                "La factura ya no está disponible. Vuelva a llamarla para devolverla.");

        // La venta de esta caja, si fue ella quien la vendió: de ahí salen los puntos que acumuló y lo pendiente de entregar.
        var venta = copia.VentaLocalId is { } ventaLocalId
            ? await contexto.Ventas.AsNoTracking().SingleOrDefaultAsync(v => v.Id == ventaLocalId, cancelacion)
            : null;

        var factura = copia.ParaDevolver(sesion.SucursalId, sesion.CajaId);
        var numeroFactura = factura.NumeroTransaccion;

        var motivo = await contexto.MotivosDevolucion.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Codigo == solicitud.MotivoCodigo && m.Activo, cancelacion);
        var cliente = venta is not null
            ? await ClienteAsync(venta, solicitud, cancelacion)
            : await ClienteRemotoAsync(copia, solicitud, cancelacion);

        var diasRetencion = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasRetencionImpuestoDevolucion, sesion.CajaId, cancelacion);
        var diasVigencia = await DiasVigenciaAsync(sesion, cancelacion);
        var encfOrigen = copia.Encf;
        var lineas = solicitud.Lineas.Select(l => new LineaSolicitadaDevolucion(l.NumeroLinea, l.Cantidad, l.Serial)).ToList();
        var ahora = reloj.Ahora();

        Devolucion Armar(IReadOnlyDictionary<int, DevueltoLinea> devuelto, string numero, int? turnoId, ResultadoPermiso? permiso) =>
            Devolucion.Registrar(factura, sesion.SucursalId, sesion.CajaId, encfOrigen, lineas, devuelto, cliente, motivo?.Codigo, motivo?.Nombre, solicitud.Observacion, numero, turnoId,
                sesion.UsuarioId, sesion.Nombre, permiso?.SupervisorId ?? sesion.UsuarioId, permiso?.SupervisorNombre ?? sesion.Nombre,
                diasRetencion, Hoy, ahora, reloj.LocalTimeZone, solicitud.Interna);

        // La nota interna no devuelve dinero: solo ajusta la factura.
        if (solicitud.Interna && solicitud.Reembolso != TipoReembolso.SaldoNotaCredito)
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, "Una nota de crédito interna no devuelve dinero: solo ajusta la factura.");

        // Se validan las reglas antes de pedir la clave del encargado.
        try
        {
            Armar(await DevueltoDeLaFacturaAsync(venta, copia, cancelacion), "VALIDACION", null, null);
        }
        catch (ReglaDevolucionExcepcion excepcion)
        {
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, excepcion.Message);
        }

        var permisoRequerido = solicitud.Interna ? CatalogoPermisos.AutorizarNotaCreditoInterna : CatalogoPermisos.AutorizarDevolucion;
        var permiso = await autorizaciones.VerificarAsync(sesion, permisoRequerido, solicitud.AutorizacionId, "Venta", numeroFactura, cancelacion);
        if (!permiso.Permitido)
            return permiso.AutorizacionRechazada
                ? Rechazo(CodigoResultadoDevolucion.AutorizacionInvalida, "La autorización no es válida, ya se usó o venció.", permisoRequerido)
                : Rechazo(CodigoResultadoDevolucion.RequiereAutorizacion, solicitud.Interna
                    ? "La nota de crédito interna requiere la autorización de un supervisor."
                    : "La devolución requiere la autorización del encargado.", permisoRequerido);

        // Se le piden al Central las líneas antes de emitir, para que dos cajas no devuelvan la misma mercancía. La reserva no se
        // libera al terminar: vence sola, y hasta entonces cubre el tiempo que la nota tarda en subir.
        var reserva = await central.ReservarFacturaAsync(numeroFactura, lineas.ToDictionary(l => l.NumeroLinea, l => l.Cantidad), cancelacion);
        if (!reserva.Exitosa)
            return Rechazo(reserva.CentralRespondio ? CodigoResultadoDevolucion.DevolucionInvalida : CodigoResultadoDevolucion.SinConexionCentral,
                reserva.CentralRespondio
                    ? reserva.Error!
                    : $"No se pudo confirmar la factura {numeroFactura} con el Central. Sin conexión no se emiten notas de crédito. ({reserva.Error})");

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
            devolucion = Armar(await DevueltoDeLaFacturaAsync(venta, copia, cancelacion), NumeroDocumento.Formatear(codigoSucursal, sesion.CajaCodigo, TipoDocumentoNumerado.NotaCredito, secuencia, digitos), turnoId, permiso);
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
                    await LiberarReservaAsync(numeroFactura, cancelacion);
                    return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, problema);
                }
            }

            // La devolución reversa los puntos que acumuló la compra, en proporción a lo devuelto (RF-244, RN-21).
            if (venta is { TieneFidelidad: true, PuntosAcumulados: > 0 })
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
            await LiberarReservaAsync(numeroFactura, cancelacion);
            return Rechazo(CodigoResultadoDevolucion.DevolucionInvalida, excepcion.Message);
        }
        catch (EmisionEcfExcepcion excepcion)
        {
            await transaccion.RollbackAsync(cancelacion);
            contexto.ChangeTracker.Clear();
            await LiberarReservaAsync(numeroFactura, cancelacion);
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
                Factura = numeroFactura,
                FacturaPropia = venta is not null,
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
            await LiberarReservaAsync(numeroFactura, cancelacion);
            throw;
        }

        // La nota ya está emitida: la copia que trajo el Central se borra. La caja no guarda facturas que no sean suyas.
        await BorrarCopiaAsync(numeroFactura, cancelacion);

        var aviso = await ImprimirAsync(sesion, datos, esCopia: false, cancelacion);
        var titulo = devolucion.EsInterna
            ? $"Nota de crédito interna {devolucion.Numero} por {devolucion.SimboloMoneda}{devolucion.Total:N2} registrada (sin comprobante fiscal, no se usa como pago)."
            : $"Nota de crédito {devolucion.Encf} por {devolucion.SimboloMoneda}{devolucion.Total:N2} emitida.";
        return new RespuestaDevolucion(CodigoResultadoDevolucion.Correcto, $"{titulo}{(aviso is null ? null : $" {aviso}")}", NotaCredito: datos);
    }

    /// <summary>Suelta en el Central las líneas retenidas cuando la nota no llegó a emitirse; sin red, la reserva vence sola.</summary>
    private async Task LiberarReservaAsync(string numeroFactura, CancellationToken cancelacion)
    {
        try
        {
            await central.LiberarReservaFacturaAsync(numeroFactura, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // Que no se pueda avisar no cambia nada: la reserva vence sola y la mercancía vuelve a estar disponible.
        }
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

        var documento = await contexto.DocumentosElectronicos.AsNoTracking().SingleOrDefaultAsync(d => d.VentaId == devolucion.Id && d.TipoOrigen == OrigenComprobante.Devolucion, cancelacion);
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

    /// <summary>
    /// Lo que no se puede devolver de la factura, según el Central: lo ya devuelto en cualquier tienda y la mercancía que todavía
    /// no se ha entregado (RF-233). Si la vendió esta caja se toma además lo de sus propias notas y se queda la mayor de las dos:
    /// una nota recién emitida que todavía no ha subido no la ve el Central, y sin esto se devolvería dos veces lo mismo.
    /// </summary>
    private async Task<Dictionary<int, DevueltoLinea>> DevueltoDeLaFacturaAsync(Venta? venta, FacturaConsultada copia, CancellationToken cancelacion) =>
        Devuelto(copia, venta is null
            ? []
            : await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas).Where(d => d.VentaOrigenId == venta.Id).ToListAsync(cancelacion));

    /// <summary>Combina lo devuelto que informó el Central con las notas que esta caja ya emitió, quedándose con lo mayor por línea.</summary>
    private static Dictionary<int, DevueltoLinea> Devuelto(FacturaConsultada copia, IReadOnlyCollection<Devolucion> locales)
    {
        var devuelto = copia.Devuelto();
        foreach (var (numeroLinea, local) in locales.Devuelto())
        {
            var central = devuelto.GetValueOrDefault(numeroLinea);
            if (central is null || local.Cantidad > central.Cantidad)
                devuelto[numeroLinea] = local;
        }

        return devuelto;
    }

    /// <summary>
    /// La factura como la ve la pantalla. Los datos y lo ya devuelto salen de la copia que entregó el Central, que es el único que
    /// ve las devoluciones de toda la empresa. Si además la vendió esta caja se le suman dos cosas que solo ella sabe: la mercancía
    /// que todavía no ha entregado y las notas de crédito que ya le hizo.
    /// </summary>
    private async Task<DatosFacturaDevolucion> ArmarFacturaAsync(SesionUsuario sesion, FacturaConsultada copia, CancellationToken cancelacion)
    {
        var diasRetencion = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasRetencionImpuestoDevolucion, sesion.CajaId, cancelacion);
        var dias = Hoy.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(copia.CobradaEn, reloj.LocalTimeZone).DateTime).DayNumber;

        var previas = copia.VentaLocalId is { } ventaLocalId
            ? await contexto.Devoluciones.AsNoTracking().Include(d => d.Lineas).Where(d => d.VentaOrigenId == ventaLocalId)
                .OrderBy(d => d.CreadaEn).ToListAsync(cancelacion)
            : [];
        var devuelto = Devuelto(copia, previas);

        var lineas = copia.Lineas.OrderBy(l => l.NumeroLinea).Select(l =>
        {
            var previo = devuelto.GetValueOrDefault(l.NumeroLinea) ?? new DevueltoLinea(0m, 0m);
            var disponible = Math.Max(0m, l.Cantidad - previo.Cantidad);
            var precio = decimal.Round(l.ImporteConImpuesto / l.Cantidad, 2, MidpointRounding.AwayFromZero);

            // Lo que el Central ya descontó —devoluciones de otras tiendas y mercancía sin entregar— no cuadra con el resto
            // exacto de la línea, así que la parte disponible se estima por precio.
            var importeDisponible = previo.Cantidad > 0
                ? decimal.Round(l.ImporteConImpuesto / l.Cantidad * disponible, 2, MidpointRounding.AwayFromZero)
                : l.ImporteConImpuesto;

            return new DatosLineaFacturaDevolucion(l.NumeroLinea, l.ArticuloId, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo,
                l.UnidadMedidaCodigo, l.DecimalesCantidad, l.DecimalesCantidad > 0, l.Cantidad, previo.Cantidad, disponible,
                precio, importeDisponible, l.PorcentajeImpuesto, l.TipoArticulo == TipoArticulo.Serializado);
        }).ToList();

        var cliente = copia.ClienteNombre is { } nombre && copia.ClienteDocumento is { } documento
            ? new DatosClienteVenta(null, copia.ClienteTipoDocumento, documento, nombre)
            : null;

        return new DatosFacturaDevolucion(copia.VentaLocalId, copia.Numero, copia.Encf, copia.TipoComprobante, copia.CobradaEn, dias,
            dias > diasRetencion, diasRetencion, cliente, lineas, await ListarMotivosAsync(cancelacion),
            previas.Select(d => new DatosNotaCreditoResumen(d.Id, d.Numero, d.Encf, d.Total, d.CreadaEn)).ToList(),
            new DatosOrigenFactura(copia.VentaLocalId is null, copia.SucursalCodigo, copia.CajaCodigo));
    }

    /// <summary>El cliente de una factura que no vendió esta caja; si venía sin identificar, el que digite el cajero (RF-160).</summary>
    private async Task<ClienteDevolucion?> ClienteRemotoAsync(FacturaConsultada copia, SolicitudDevolucion solicitud, CancellationToken cancelacion)
    {
        if (copia.ClienteDocumento is { } documentoFactura && DocumentoIdentidad.Validar(documentoFactura).EsAceptable)
            return new ClienteDevolucion(copia.ClienteTipoDocumento, documentoFactura, copia.ClienteNombre ?? documentoFactura);

        var validacion = DocumentoIdentidad.Validar(solicitud.ClienteDocumento);
        if (!validacion.EsAceptable)
            return null;

        var nombre = string.IsNullOrWhiteSpace(solicitud.ClienteNombre)
            ? (await consultaDocumentos.ConsultarAsync(validacion.Documento, cancelacion)).Cliente?.Nombre
            : solicitud.ClienteNombre.Trim();

        return nombre is null ? null : new ClienteDevolucion(validacion.Tipo, validacion.Documento, nombre);
    }

    /// <summary>El cliente de la factura; si no tiene, el documento digitado con el nombre del cliente registrado o el digitado (RF-160).</summary>
    private async Task<ClienteDevolucion?> ClienteAsync(Venta venta, SolicitudDevolucion solicitud, CancellationToken cancelacion)
    {
        if (venta.ClienteDocumento is { } documentoFactura)
            return new ClienteDevolucion(venta.ClienteTipoDocumento, documentoFactura, venta.ClienteNombre ?? documentoFactura);

        var validacion = DocumentoIdentidad.Validar(solicitud.ClienteDocumento);
        if (!validacion.EsAceptable)
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
