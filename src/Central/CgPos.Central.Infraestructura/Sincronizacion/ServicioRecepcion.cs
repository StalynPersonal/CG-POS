using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Fidelidad;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Sincronizacion;

internal sealed class ServicioRecepcion(
    ContextoDatosCentral contexto,
    IAuditoriaCentral auditoria,
    Fidelidad.RecalculadorPuntos recalculadorPuntos,
    Reportes.RegistroVentasCentral registroVentas,
    IParametrosCentral parametros,
    INumeracionCentral numeracion,
    TimeProvider reloj,
    ILogger<ServicioRecepcion> registro) : IServicioRecepcion
{
    public async Task<RespuestaRecepcionCentral> RecibirAsync(MensajeSincronizacion mensaje, CajaRemitente remitente, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        ArgumentNullException.ThrowIfNull(remitente);

        var ahora = reloj.Ahora();
        var estado = await EstadoCajaAsync(remitente.CajaId, cancelacion);

        if (mensaje.Id == Guid.Empty || string.IsNullOrWhiteSpace(mensaje.Referencia) || mensaje.Referencia.Trim().Length > DocumentoRecibido.LargoMaximoReferencia
            || string.IsNullOrWhiteSpace(mensaje.TipoMensaje)
            || mensaje.TipoMensaje.Length > DocumentoRecibido.LargoMaximoTipo || string.IsNullOrEmpty(mensaje.Contenido))
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.DocumentoInvalido, "El mensaje está incompleto.", ahora, cancelacion);

        if (mensaje.SucursalCodigo != remitente.SucursalCodigo || mensaje.CajaCodigo != remitente.CajaCodigo)
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.CajaNoCoincide,
                $"El mensaje indica la caja {mensaje.SucursalCodigo}-{mensaje.CajaCodigo}, pero lo envió la caja autenticada {remitente.SucursalCodigo}-{remitente.CajaCodigo}.",
                ahora, cancelacion);

        if (!HashSincronizacion.Coincide(mensaje.Contenido, mensaje.HashContenido))
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.HashInvalido,
                "El hash del contenido no coincide: el mensaje se alteró o llegó incompleto.", ahora, cancelacion);

        var existente = await contexto.DocumentosRecibidos.SingleOrDefaultAsync(d => d.MensajeId == mensaje.Id, cancelacion);
        if (existente is not null)
            return await ResponderExistenteAsync(existente, mensaje, remitente, estado, ahora, cancelacion);

        DocumentoElectronicoParaCentral? ecf = null;
        if (TiposMensaje.ConEcf.Contains(mensaje.TipoMensaje))
        {
            if (!TryLeerEcf(mensaje.Contenido, out ecf, out var problema))
                return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.DocumentoInvalido, problema!, ahora, cancelacion);

            if (ecf is not null && !HashSincronizacion.Coincide(ecf.XmlFirmado, ecf.HashXml))
                return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.XmlAlterado,
                    $"El hash del XML del e-CF {ecf.Encf} no coincide con el XML recibido.", ahora, cancelacion);
        }

        var documento = DocumentoRecibido.Recibir(mensaje.Id, remitente.CajaId, remitente.SucursalId, mensaje.TipoMensaje, mensaje.Referencia, mensaje.Contenido,
            mensaje.HashContenido, mensaje.CreadoEn, ahora);
        contexto.DocumentosRecibidos.Add(documento);

        if (ecf is not null)
            await RegistrarComprobanteAsync(documento, ecf, ahora, cancelacion);

        if (mensaje.TipoMensaje == TiposMensaje.VentaCobrada)
            await RegistrarVentaAsync(documento, remitente, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.TurnoCerrado)
            await RegistrarCierreAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.InscripcionFidelidad)
            await PublicarInscripcionAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.NotaCreditoEmitida)
            await RegistrarNotaCreditoAsync(documento, remitente, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.NotaCreditoConsumida)
            await RegistrarConsumoNotaCreditoAsync(documento, remitente, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.MovimientoPuntos)
            await RegistrarMovimientoPuntosAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje is TiposMensaje.PendienteCreado or TiposMensaje.PendienteActualizado)
            await RegistrarPendienteAsync(documento, remitente, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.IngresoUsuario)
            await RegistrarIngresoUsuarioAsync(documento, cancelacion);

        estado.RegistrarRecepcion(ahora);

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // Dos envíos simultáneos del mismo mensaje: el que llegó primero lo guardó.
            contexto.ChangeTracker.Clear();
            var ganador = await contexto.DocumentosRecibidos.AsNoTracking().SingleOrDefaultAsync(d => d.MensajeId == mensaje.Id, cancelacion);
            if (ganador is null)
                throw;

            return ganador.MismoContenido(mensaje.HashContenido)
                ? new RespuestaRecepcionCentral(EstadoRecepcion.Duplicado)
                : new RespuestaRecepcionCentral(EstadoRecepcion.Rechazado, "El Id del mensaje ya se recibió con otro contenido.");
        }

        return new RespuestaRecepcionCentral(EstadoRecepcion.Recibido);
    }

    private async Task<RespuestaRecepcionCentral> ResponderExistenteAsync(DocumentoRecibido existente, MensajeSincronizacion mensaje, CajaRemitente remitente,
        EstadoSincronizacionCaja estado, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        if (existente.CajaId != remitente.CajaId)
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.CajaNoCoincide,
                $"El Id del mensaje ya se recibió de la caja {existente.CajaId}.", ahora, cancelacion);

        if (!existente.MismoContenido(mensaje.HashContenido))
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.ContenidoDistinto,
                $"El Id del mensaje ya se recibió el {existente.RecibidoEn:yyyy-MM-dd HH:mm} con otro contenido.", ahora, cancelacion);

        existente.RegistrarReenvio(ahora);
        estado.RegistrarDuplicado(ahora);
        await contexto.SaveChangesAsync(cancelacion);
        return new RespuestaRecepcionCentral(EstadoRecepcion.Duplicado);
    }

    /// <summary>
    /// Registra el e-CF para enviarlo a la DGII. Si el e-NCF ya llegó en otro documento, la transacción se guarda igual (la caja es autoridad sobre sus
    /// transacciones) pero el comprobante no se registra dos veces y el conflicto queda abierto para revisarlo.
    /// </summary>
    private async Task RegistrarComprobanteAsync(DocumentoRecibido documento, DocumentoElectronicoParaCentral ecf, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var encf = ecf.Encf.Trim();
        var anterior = await contexto.ComprobantesRecibidos.AsNoTracking()
            .Where(c => c.Encf == encf)
            .Select(c => new { c.DocumentoId, c.CajaId })
            .FirstOrDefaultAsync(cancelacion);

        if (anterior is null)
        {
            contexto.ComprobantesRecibidos.Add(ComprobanteRecibido.Registrar(documento, encf, ecf.TipoComprobante, ecf.XmlFirmado, ecf.HashXml, ecf.FechaFirma, ahora,
                ecf.EsResumenConsumo));
            return;
        }

        await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.EncfDuplicado,
            $"El e-NCF {encf} ya se recibió en el documento {anterior.DocumentoId} de la caja {anterior.CajaId}. La transacción se guardó; el comprobante no se registró de nuevo para la DGII.",
            ahora, cancelacion);
    }

    /// <summary>
    /// Una inscripción de fidelidad hecha en caja (RF-237) se publica como miembro para todas las cajas. El miembro se identifica por la cédula:
    /// si ya está inscrita en el Central (dos cajas sin conexión), se conserva la del Central y el conflicto queda registrado (RN-24).
    /// </summary>
    private async Task PublicarInscripcionAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoInscripcionFidelidad? inscripcion = null;
        string? cedula = null;
        try
        {
            inscripcion = JsonSerializer.Deserialize<DocumentoInscripcionFidelidad>(documento.Contenido, OpcionesJson.Predeterminadas);
            if (inscripcion is not null)
                cedula = MiembroFidelidad.ValidarCedula(inscripcion.Cedula);
        }
        catch (Exception excepcion) when (excepcion is JsonException or ArgumentException)
        {
        }

        if (inscripcion is null || cedula is null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "La inscripción de fidelidad no se pudo leer (cédula inválida); se guardó sin publicar el miembro.", ahora, cancelacion);
            return;
        }

        if (await contexto.MiembrosFidelidad.AnyAsync(m => m.Cedula == cedula, cancelacion))
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.MiembroDuplicado,
                $"La cédula {cedula} ya está inscrita en el Central; se conserva esa inscripción y la caja actualiza su registro con ella.", ahora, cancelacion);
            return;
        }

        var miembro = MiembroFidelidad.DesdeCentral(cedula, inscripcion.Nombre, inscripcion.InscritoEn);
        miembro.ActualizarContacto(inscripcion.Nombre, inscripcion.Telefono, inscripcion.Correo);
        contexto.MiembrosFidelidad.Add(miembro);
        Persistencia.Configuraciones.ColumnasMaestro.Marcar(contexto, miembro, ahora, $"Inscripción en caja {await CodigoCajaAsync(documento, cancelacion)}");

        // Sus movimientos pueden haber llegado antes que la inscripción: el maestro sale ya con el saldo que corresponde.
        await recalculadorPuntos.RecalcularAsync(cedula, "Inscripción en caja", forzarPublicacion: true, cancelacion: cancelacion);
    }

    /// <summary>
    /// Suma al saldo oficial los puntos que acumuló, canjeó o reversó una caja (RF-240). El documento que los originó y el tipo identifican el
    /// <summary>
    /// Anota en el usuario cuándo entró por última vez y en qué caja. Un aviso viejo que llega tarde (la caja estuvo sin red)
    /// no retrocede la fecha, y un usuario que ya no existe en el Central se ignora sin dar el mensaje por malo.
    /// </summary>
    private async Task RegistrarIngresoUsuarioAsync(DocumentoRecibido documento, CancellationToken cancelacion)
    {
        DocumentoIngresoUsuario? ingreso = null;
        try
        {
            ingreso = JsonSerializer.Deserialize<DocumentoIngresoUsuario>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (ingreso is null || string.IsNullOrWhiteSpace(ingreso.UsuarioCodigo))
            return;

        var codigo = ingreso.UsuarioCodigo.Trim();
        var usuarioId = await contexto.UsuariosCaja.AsNoTracking().Where(u => u.Codigo == codigo).Select(u => (int?)u.Id).SingleOrDefaultAsync(cancelacion);
        if (usuarioId is not { } id)
            return;

        var acceso = await contexto.AccesosUsuarioCaja.SingleOrDefaultAsync(a => a.UsuarioId == id, cancelacion);
        if (acceso is null)
            contexto.AccesosUsuarioCaja.Add(AccesoUsuarioCaja.Registrar(id, documento.CajaId, ingreso.IngresoEn));
        else
            acceso.Actualizar(documento.CajaId, ingreso.IngresoEn);
    }

    /// movimiento, así que un reenvío no acumula dos veces, y el saldo recalculado se publica en el maestro del miembro para todas las cajas.
    /// </summary>
    private async Task RegistrarMovimientoPuntosAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoMovimientoPuntos? movimiento = null;
        try
        {
            movimiento = JsonSerializer.Deserialize<DocumentoMovimientoPuntos>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (movimiento is null || string.IsNullOrWhiteSpace(movimiento.Documento) || movimiento.Puntos == 0)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El movimiento de puntos no se pudo leer; el mensaje se guardó sin tocar el saldo del miembro.", ahora, cancelacion);
            return;
        }

        var numero = movimiento.Documento.Trim();
        if (await contexto.MovimientosPuntos.AnyAsync(m => m.Origen == OrigenMovimientoPuntos.Caja && m.Documento == numero && m.Tipo == movimiento.Tipo, cancelacion))
            return;

        try
        {
            contexto.MovimientosPuntos.Add(MovimientoPuntosCentral.DesdeCaja(movimiento.Cedula, movimiento.Tipo, movimiento.Puntos, numero, documento.CajaId,
                documento.SucursalId, movimiento.Fecha, movimiento.VenceEn, ahora));

            await recalculadorPuntos.RecalcularAsync(movimiento.Cedula, $"Caja {await CodigoCajaAsync(documento, cancelacion)}", cancelacion: cancelacion);
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or ArgumentOutOfRangeException)
        {
            contexto.ChangeTracker.Clear();
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"El movimiento de puntos no se pudo registrar: {ValidacionMaestros.MensajeError(excepcion)}", ahora, cancelacion);
        }
    }

    /// <summary>La venta cobrada alimenta el modelo de lectura de los reportes (ventas, ITBIS y 607); si no se puede leer, el documento se guarda igual.</summary>
    private async Task RegistrarVentaAsync(DocumentoRecibido documento, CajaRemitente remitente, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoVentaCobrada? venta = null;
        try
        {
            venta = JsonSerializer.Deserialize<DocumentoVentaCobrada>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        // Un documento sin totales ni pagos no se puede reflejar en los reportes.
        if (venta is { Totales: null } or { Pagos: null } or { Lineas: null })
            venta = null;

        var problema = venta is null
            ? "La venta no se pudo leer; el mensaje se guardó sin incluirla en los reportes."
            : NumeroAjeno(venta.Numero, TipoDocumentoNumerado.Factura, remitente);
        if (venta is null || problema is not null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                problema!, ahora, cancelacion);
            return;
        }

        await registroVentas.RegistrarVentaAsync(venta, documento.SucursalId, documento.CajaId, cancelacion);
        await RegistrarCompraListaBodaAsync(venta, documento, ahora, cancelacion);
        await MarcarCotizacionFacturadaAsync(venta, documento, ahora, cancelacion);
    }

    /// <summary>
    /// La factura salió de una cotización: queda cerrada con el número de la factura, para que no se facture dos veces. Es
    /// idempotente porque los mensajes de la caja pueden repetirse.
    /// </summary>
    private async Task MarcarCotizacionFacturadaAsync(DocumentoVentaCobrada venta, DocumentoRecibido documento, DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        if (venta.CotizacionNumero is not { Length: > 0 } numeroCotizacion)
            return;

        var numero = numeroCotizacion.Trim().ToUpperInvariant();
        var cotizacion = await contexto.Cotizaciones.FirstOrDefaultAsync(c => c.Numero == numero, cancelacion);
        if (cotizacion is null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje,
                TipoConflictoSincronizacion.DocumentoInvalido,
                $"La factura {venta.Numero} se cobró contra la cotización {numero}, que no existe en el Central.", ahora, cancelacion);
            return;
        }

        cotizacion.RegistrarFactura(venta.Numero, venta.CobradaEn, ahora);
    }

    /// <summary>
    /// La factura se compró contra una lista de boda (RF-73): queda en su historial y, si el Central tiene activado el descuento,
    /// baja las cantidades pedidas. Una factura se registra una sola vez aunque la caja reenvíe el mensaje.
    /// </summary>
    private async Task RegistrarCompraListaBodaAsync(DocumentoVentaCobrada venta, DocumentoRecibido documento, DateTimeOffset ahora,
        CancellationToken cancelacion)
    {
        if (venta.ListaBodaNumero is not { Length: > 0 } numeroLista)
            return;

        var numero = numeroLista.Trim().ToUpperInvariant();
        var lista = await contexto.ListasBoda.Include(l => l.Articulos).Include(l => l.Compras).FirstOrDefaultAsync(l => l.Numero == numero, cancelacion);
        if (lista is null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"La factura {venta.Numero} se cobró contra la lista {numero}, que no existe en el Central.", ahora, cancelacion);
            return;
        }

        var descontar = await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.ListasBodaDescontarCompras, cancelacion);
        lista.RegistrarCompra(venta.Numero, documento.CajaId, venta.TotalCobrado,
            venta.Lineas.Where(l => !l.EsReverso).Select(l => (l.CodigoInterno, l.Cantidad)), descontar, venta.CobradaEn, ahora);
    }

    /// <summary>El cierre de turno alimenta el reporte de cuadres (RF-267).</summary>
    private async Task RegistrarCierreAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoCierreTurno? cierre = null;
        try
        {
            cierre = JsonSerializer.Deserialize<DocumentoCierreTurno>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (cierre is null || cierre.TurnoNumero < 1)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El cierre de turno no se pudo leer; el mensaje se guardó sin incluirlo en los cuadres.", ahora, cancelacion);
            return;
        }

        await registroVentas.RegistrarCierreAsync(cierre, documento.SucursalId, documento.CajaId, cancelacion);
    }

    /// <summary>
    /// Refleja en el Central el pendiente de entrega o envío que informa una caja (RF-249, RF-252), para verlos todos juntos y seguir los atrasos.
    /// Se identifica por su número. La caja es la autoridad sobre sus pendientes: un mensaje más viejo que lo ya registrado no pisa el estado más reciente.
    /// </summary>
    private async Task RegistrarPendienteAsync(DocumentoRecibido documento, CajaRemitente remitente, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoPendienteEntrega? pendiente = null;
        try
        {
            pendiente = JsonSerializer.Deserialize<DocumentoPendienteEntrega>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        var problema = pendiente is null
            ? "El pendiente de entrega no se pudo leer; el mensaje se guardó sin reflejarlo en el Central."
            : NumeroAjeno(pendiente.Numero, TipoDocumentoNumerado.PendienteEntrega, remitente);
        if (pendiente is null || problema is not null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                problema!, ahora, cancelacion);
            return;
        }

        var numero = pendiente.Numero.Trim();

        // El Central es quien despacha: la caja solo crea el pendiente al cobrar. Si el mensaje se reenvía y el pendiente ya
        // está aquí, no se toca — pisarlo borraría las entregas que el Central ya registró.
        if (await contexto.PendientesEntrega.AnyAsync(p => p.Numero == numero, cancelacion))
            return;

        var sucursalRetiroId = pendiente.SucursalRetiroCodigo is { Length: > 0 } codigoRetiro
            ? await contexto.Sucursales.AsNoTracking().Where(s => s.Codigo == codigoRetiro).Select(s => (int?)s.Id).FirstOrDefaultAsync(cancelacion)
            : null;

        var datos = new DatosPendienteReconstruido(numero, pendiente.VentaNumero, pendiente.Metodo, pendiente.Estado, pendiente.SucursalRetiroNombre,
            pendiente.Direccion, pendiente.Sector, pendiente.Ciudad, pendiente.Referencia, pendiente.Telefono, pendiente.Transportista,
            pendiente.CostoEnvio, pendiente.FechaComprometida, pendiente.Comentario, pendiente.ClienteDocumento, pendiente.ClienteNombre,
            pendiente.VendidoPorNombre, pendiente.AutorizadoPorNombre, pendiente.CreadoEn, pendiente.ActualizadoEn, pendiente.ActualizadoPorNombre,
            pendiente.MotivoAnulacion,
            pendiente.Lineas.Select(l => new DatosLineaPendienteReconstruida(l.NumeroLineaVenta, 0, l.Codigo, l.Descripcion, l.UnidadMedidaCodigo,
                l.DecimalesCantidad, l.Serializado, l.Cantidad, l.CantidadEntregada, l.Serial)).ToList(),
            pendiente.Entregas.Select(e => new DatosEntregaReconstruida(e.Numero, e.RecibeNombre, e.RecibeCedula, e.UsuarioNombre, e.Fecha,
                e.Lineas.Select(l => new DatosLineaEntregaReconstruida(l.NumeroLineaVenta, l.Descripcion, l.Cantidad, l.Serial)).ToList())).ToList());

        try
        {
            var pendiente_ = PendienteEntrega.Reconstruir(datos, documento.SucursalId, documento.CajaId, sucursalRetiroId);

            // El Central le pone su propio número. Si falta su secuencia el pendiente entra igual: la caja ya lo creó al
            // cobrar, y perderlo dejaría mercancía sin despachar por una configuración.
            try
            {
                pendiente_.AsignarNumeroCentral(await numeracion.SiguienteAsync(DocumentosNumerados.Despacho, cancelacion));
            }
            catch (SecuenciaCentralNoConfiguradaExcepcion)
            {
            }

            contexto.PendientesEntrega.Add(pendiente_);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"El pendiente {numero} no se pudo registrar: {ValidacionMaestros.MensajeError(excepcion)}", ahora, cancelacion);
        }
    }

    /// <summary>Registra la nota de crédito en el Central, por su número, para poder consumirla en cualquier sucursal (RF-43).</summary>
    private async Task RegistrarNotaCreditoAsync(DocumentoRecibido documento, CajaRemitente remitente, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoNotaCreditoEmitida? emitida = null;
        try
        {
            emitida = JsonSerializer.Deserialize<DocumentoNotaCreditoEmitida>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        var problema = emitida is null
            ? "La nota de crédito no se pudo leer; el mensaje se guardó sin registrarla para otras sucursales."
            : NumeroAjeno(emitida.Numero, TipoDocumentoNumerado.NotaCredito, remitente);
        if (emitida is null || problema is not null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                problema!, ahora, cancelacion);
            return;
        }

        var numero = emitida.Numero.Trim();
        if (await contexto.NotasCredito.AnyAsync(n => n.Numero == numero, cancelacion))
            return;

        try
        {
            var nota = NotaCreditoCentral.Registrar(numero, emitida.Comprobante?.Encf, documento.CajaId, documento.SucursalId, emitida.ClienteDocumento,
                emitida.ClienteNombre, emitida.Moneda, emitida.Total, emitida.FechaEmision, emitida.CreadaEn, ahora, emitida.EsInterna);

            // Un consumo puede llegar antes que la emisión: los mensajes de una caja llegan en orden, pero los de dos cajas no.
            var consumido = await contexto.ConsumosNotaCredito.Where(c => c.NotaCreditoNumero == numero).SumAsync(c => (decimal?)c.Monto, cancelacion) ?? 0m;
            if (consumido > 0)
                nota.AplicarConsumo(consumido);

            contexto.NotasCredito.Add(nota);
            await registroVentas.RegistrarNotaCreditoAsync(emitida, documento.SucursalId, documento.CajaId, cancelacion);
        }
        catch (ArgumentException excepcion)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"La nota de crédito {numero} no se pudo registrar: {ValidacionMaestros.MensajeError(excepcion)}", ahora, cancelacion);
        }
    }

    /// <summary>Descuenta del saldo central lo que una caja consumió y cierra la reserva que tenía para esa factura (RF-38).</summary>
    private async Task RegistrarConsumoNotaCreditoAsync(DocumentoRecibido documento, CajaRemitente remitente, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoConsumoNotaCredito? consumo = null;
        try
        {
            consumo = JsonSerializer.Deserialize<DocumentoConsumoNotaCredito>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (consumo is null || string.IsNullOrWhiteSpace(consumo.NotaCreditoNumero) || consumo.Monto <= 0
            || NumeroAjeno(consumo.VentaNumero, TipoDocumentoNumerado.Factura, remitente) is not null)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.MensajeId, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El consumo de la nota de crédito no se pudo leer; el mensaje se guardó sin descontar el saldo.", ahora, cancelacion);
            return;
        }

        var notaNumero = consumo.NotaCreditoNumero.Trim();
        var ventaNumero = consumo.VentaNumero.Trim();
        if (await contexto.ConsumosNotaCredito.AnyAsync(c => c.NotaCreditoNumero == notaNumero && c.VentaNumero == ventaNumero, cancelacion))
            return;

        contexto.ConsumosNotaCredito.Add(ConsumoNotaCreditoCentral.Registrar(notaNumero, ventaNumero, documento.CajaId, consumo.Monto, consumo.Fecha, ahora));

        // Si la emisión todavía no llegó, el saldo se ajusta al registrarla.
        if (await contexto.NotasCredito.SingleOrDefaultAsync(n => n.Numero == notaNumero, cancelacion) is not { } nota)
            return;

        nota.AplicarConsumo(consumo.Monto);
        var reserva = await contexto.ReservasNotaCredito
            .Where(r => r.NotaCreditoId == nota.Id && r.CajaId == documento.CajaId && r.VentaNumero == ventaNumero && r.CerradaEn == null)
            .FirstOrDefaultAsync(cancelacion);
        reserva?.Cerrar("Consumida", ahora);
    }

    /// <summary>
    /// Motivo por el que un número no es de un documento de ese tipo de la caja que lo envía; nulo si lo es. El número lleva la sucursal y la caja:
    /// una caja no puede informar documentos de otra.
    /// </summary>
    private static string? NumeroAjeno(string? numero, TipoDocumentoNumerado tipo, CajaRemitente remitente) =>
        !NumeroDocumento.TryLeer(numero, out var sucursal, out var caja, out var leido) || leido != tipo
            ? $"El número '{numero}' no es de un documento de tipo {tipo}; el mensaje se guardó sin procesarlo."
            : sucursal != remitente.SucursalCodigo || caja != remitente.CajaCodigo
                ? $"El documento {numero} es de la caja {sucursal}-{caja}, pero lo envió la caja {remitente.SucursalCodigo}-{remitente.CajaCodigo}; el mensaje se guardó sin procesarlo."
                : null;

    private async Task<string> CodigoCajaAsync(DocumentoRecibido documento, CancellationToken cancelacion)
    {
        var codigos = await contexto.Cajas.AsNoTracking().Where(c => c.Id == documento.CajaId)
            .Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, Caja = c.Codigo })
            .SingleOrDefaultAsync(cancelacion);
        return codigos is null ? documento.CajaId.ToString() : $"{codigos.Sucursal}-{codigos.Caja}";
    }

    private async Task<RespuestaRecepcionCentral> RechazarAsync(MensajeSincronizacion mensaje, CajaRemitente remitente, EstadoSincronizacionCaja estado,
        TipoConflictoSincronizacion tipo, string detalle, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        await RegistrarConflictoAsync(remitente.CajaId, remitente.SucursalId, mensaje.Id, mensaje.TipoMensaje, tipo, detalle, ahora, cancelacion);
        estado.RegistrarRechazo(ahora, detalle);
        await contexto.SaveChangesAsync(cancelacion);
        return new RespuestaRecepcionCentral(EstadoRecepcion.Rechazado, detalle);
    }

    /// <summary>Un conflicto abierto del mismo mensaje y tipo solo suma la repetición: la caja reintenta un rechazo hasta que se corrige.</summary>
    private async Task RegistrarConflictoAsync(int cajaId, int sucursalId, Guid mensajeId, string? tipoMensaje, TipoConflictoSincronizacion tipo, string detalle,
        DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var abierto = await contexto.ConflictosSincronizacion
            .SingleOrDefaultAsync(c => c.MensajeId == mensajeId && c.CajaId == cajaId && c.Tipo == tipo && c.ResueltoEn == null, cancelacion);

        if (abierto is not null)
        {
            abierto.RegistrarOcurrencia(ahora);
            return;
        }

        var conflicto = ConflictoSincronizacion.Registrar(cajaId, sucursalId, mensajeId, tipoMensaje, tipo, detalle, ahora);
        contexto.ConflictosSincronizacion.Add(conflicto);
        auditoria.Registrar(new EntradaAuditoria("Sincronizacion.Conflicto", "Caja", cajaId.ToString(),
            new { Conflicto = conflicto.Id, Tipo = tipo.ToString(), Mensaje = mensajeId, TipoMensaje = tipoMensaje, Detalle = detalle }));
        registro.LogWarning("Conflicto de sincronización {Tipo} de la caja {Caja} en el mensaje {Mensaje}: {Detalle}", tipo, cajaId, mensajeId, detalle);
    }

    private async Task<EstadoSincronizacionCaja> EstadoCajaAsync(int cajaId, CancellationToken cancelacion)
    {
        var estado = await contexto.EstadosSincronizacionCaja.SingleOrDefaultAsync(e => e.CajaId == cajaId, cancelacion);
        if (estado is not null)
            return estado;

        estado = EstadoSincronizacionCaja.Crear(cajaId);
        contexto.EstadosSincronizacionCaja.Add(estado);
        return estado;
    }

    private static bool TryLeerEcf(string contenido, out DocumentoElectronicoParaCentral? ecf, out string? problema)
    {
        ecf = null;
        problema = null;

        try
        {
            using var json = JsonDocument.Parse(contenido);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                problema = "El contenido del documento no es un objeto JSON.";
                return false;
            }

            // Un documento sin e-CF (por ejemplo, un cierre) se guarda sin comprobante.
            if (!json.RootElement.TryGetProperty("ecf", out var elemento) || elemento.ValueKind == JsonValueKind.Null)
                return true;

            ecf = elemento.Deserialize<DocumentoElectronicoParaCentral>(OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
            problema = "El contenido del documento o su e-CF no es JSON válido.";
            return false;
        }

        if (ecf is { XmlFirmado.Length: > 0, Encf.Length: DocumentoElectronico.LargoEncf, HashXml.Length: DocumentoRecibido.LargoHash })
            return true;

        problema = "El e-CF del documento está incompleto (e-NCF, XML firmado o hash).";
        ecf = null;
        return false;
    }
}
