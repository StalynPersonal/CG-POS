using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Fidelidad;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Fidelidad;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
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
    TimeProvider reloj,
    ILogger<ServicioRecepcion> registro) : IServicioRecepcion
{
    public async Task<RespuestaRecepcionCentral> RecibirAsync(MensajeSincronizacion mensaje, CajaRemitente remitente, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        ArgumentNullException.ThrowIfNull(remitente);

        var ahora = reloj.GetUtcNow();
        var estado = await EstadoCajaAsync(remitente.CajaId, cancelacion);

        if (mensaje.Id == Guid.Empty || mensaje.AgregadoId == Guid.Empty || string.IsNullOrWhiteSpace(mensaje.TipoMensaje)
            || mensaje.TipoMensaje.Length > DocumentoRecibido.LargoMaximoTipo || string.IsNullOrEmpty(mensaje.Contenido))
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.DocumentoInvalido, "El mensaje está incompleto.", ahora, cancelacion);

        if (mensaje.CajaId != remitente.CajaId)
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.CajaNoCoincide,
                $"El mensaje indica la caja {mensaje.CajaId}, pero lo envió la caja autenticada {remitente.CajaId}.", ahora, cancelacion);

        if (!HashSincronizacion.Coincide(mensaje.Contenido, mensaje.HashContenido))
            return await RechazarAsync(mensaje, remitente, estado, TipoConflictoSincronizacion.HashInvalido,
                "El hash del contenido no coincide: el mensaje se alteró o llegó incompleto.", ahora, cancelacion);

        var existente = await contexto.DocumentosRecibidos.SingleOrDefaultAsync(d => d.Id == mensaje.Id, cancelacion);
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

        var documento = DocumentoRecibido.Recibir(mensaje.Id, remitente.CajaId, remitente.SucursalId, mensaje.TipoMensaje, mensaje.AgregadoId, mensaje.Contenido,
            mensaje.HashContenido, mensaje.CreadoEn, ahora);
        contexto.DocumentosRecibidos.Add(documento);

        if (ecf is not null)
            await RegistrarComprobanteAsync(documento, ecf, ahora, cancelacion);

        if (mensaje.TipoMensaje == TiposMensaje.VentaCobrada)
            await RegistrarVentaAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.TurnoCerrado)
            await RegistrarCierreAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.InscripcionFidelidad)
            await PublicarInscripcionAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.NotaCreditoEmitida)
            await RegistrarNotaCreditoAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.NotaCreditoConsumida)
            await RegistrarConsumoNotaCreditoAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje == TiposMensaje.MovimientoPuntos)
            await RegistrarMovimientoPuntosAsync(documento, ahora, cancelacion);
        else if (mensaje.TipoMensaje is TiposMensaje.PendienteCreado or TiposMensaje.PendienteActualizado)
            await RegistrarPendienteAsync(documento, ahora, cancelacion);

        estado.RegistrarRecepcion(ahora);

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // Dos envíos simultáneos del mismo mensaje: el que llegó primero lo guardó.
            contexto.ChangeTracker.Clear();
            var ganador = await contexto.DocumentosRecibidos.AsNoTracking().SingleOrDefaultAsync(d => d.Id == mensaje.Id, cancelacion);
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

        await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.EncfDuplicado,
            $"El e-NCF {encf} ya se recibió en el documento {anterior.DocumentoId} de la caja {anterior.CajaId}. La transacción se guardó; el comprobante no se registró de nuevo para la DGII.",
            ahora, cancelacion);
    }

    /// <summary>
    /// Una inscripción de fidelidad hecha en caja (RF-237) se publica como miembro para todas las cajas con el Id de la caja. Si la cédula ya está en el
    /// Central con otro Id (dos cajas sin conexión), se conserva la del Central y el conflicto queda registrado (RN-24).
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

        if (inscripcion is null || cedula is null || inscripcion.MiembroId == Guid.Empty)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "La inscripción de fidelidad no se pudo leer (cédula o miembro inválido); se guardó sin publicar el miembro.", ahora, cancelacion);
            return;
        }

        if (await contexto.MaestrosCentral.AnyAsync(m => m.Tipo == TipoMaestro.MiembroFidelidad && m.Id == inscripcion.MiembroId, cancelacion))
            return;

        var existente = await contexto.MaestrosCentral
            .Where(m => m.Tipo == TipoMaestro.MiembroFidelidad && m.Codigo == cedula)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(cancelacion);

        if (existente is { } idCentral)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.MiembroDuplicado,
                $"La cédula {cedula} ya está inscrita en el Central (miembro {idCentral}); se conserva esa inscripción y la caja actualiza su registro con ella. La caja la había inscrito como {inscripcion.MiembroId}.",
                ahora, cancelacion);
            return;
        }

        var miembro = new MiembroFidelidadCarga(inscripcion.MiembroId, cedula, inscripcion.Nombre, inscripcion.Telefono, inscripcion.Correo, InscritoEn: inscripcion.InscritoEn);
        contexto.MaestrosCentral.Add(MaestroCentral.Publicar(TipoMaestro.MiembroFidelidad, miembro.Id, cedula, null,
            JsonSerializer.Serialize(miembro, OpcionesJson.Predeterminadas), ahora, $"Inscripción en caja {documento.CajaId}",
            new FilaMaestro(TipoMaestro.MiembroFidelidad, miembro.Id, cedula, null, miembro).TextoBusqueda()));

        // Sus movimientos pueden haber llegado antes que la inscripción: el maestro sale ya con el saldo que corresponde.
        await recalculadorPuntos.RecalcularAsync(miembro.Id, cedula, "Inscripción en caja", forzarPublicacion: true, cancelacion: cancelacion);
    }

    /// <summary>
    /// Suma al saldo oficial los puntos que acumuló, canjeó o reversó una caja (RF-240). El Id del movimiento es el de la caja, así que un
    /// reenvío no acumula dos veces, y el saldo recalculado se publica en el maestro del miembro para todas las cajas.
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

        if (movimiento is null || movimiento.Id == Guid.Empty || movimiento.MiembroId == Guid.Empty || movimiento.Puntos == 0)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El movimiento de puntos no se pudo leer; el mensaje se guardó sin tocar el saldo del miembro.", ahora, cancelacion);
            return;
        }

        if (await contexto.MovimientosPuntos.AnyAsync(m => m.Id == movimiento.Id, cancelacion))
            return;

        try
        {
            contexto.MovimientosPuntos.Add(MovimientoPuntosCentral.DesdeCaja(movimiento.Id, movimiento.MiembroId, movimiento.Cedula, movimiento.Tipo,
                movimiento.Puntos, movimiento.VentaId, movimiento.DevolucionId, movimiento.Documento, documento.CajaId, documento.SucursalId, movimiento.Fecha,
                movimiento.VenceEn, ahora));

            await recalculadorPuntos.RecalcularAsync(movimiento.MiembroId, movimiento.Cedula, $"Caja {documento.CajaId}", cancelacion: cancelacion);
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or ArgumentOutOfRangeException)
        {
            contexto.ChangeTracker.Clear();
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"El movimiento de puntos no se pudo registrar: {ValidacionMaestros.MensajeError(excepcion)}", ahora, cancelacion);
        }
    }

    /// <summary>La venta cobrada alimenta el modelo de lectura de los reportes (ventas, ITBIS y 607); si no se puede leer, el documento se guarda igual.</summary>
    private async Task RegistrarVentaAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoVentaCobrada? venta = null;
        try
        {
            venta = JsonSerializer.Deserialize<DocumentoVentaCobrada>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (venta?.Venta is { } datos && datos.Id != Guid.Empty
            && await registroVentas.RegistrarVentaAsync(venta, documento.SucursalId, documento.CajaId, cancelacion) is { } duplicado)
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.NumeroDuplicado,
                duplicado, ahora, cancelacion);
    }

    /// <summary>El cierre de turno alimenta el reporte de cuadres (RF-267).</summary>
    private async Task RegistrarCierreAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DatosCierre? cierre = null;
        try
        {
            cierre = JsonSerializer.Deserialize<DatosCierre>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (cierre is null || cierre.Id == Guid.Empty || cierre.TurnoId == Guid.Empty)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El cierre de turno no se pudo leer; el mensaje se guardó sin incluirlo en los cuadres.", ahora, cancelacion);
            return;
        }

        await registroVentas.RegistrarCierreAsync(cierre, documento.SucursalId, documento.CajaId, cancelacion);
    }

    /// <summary>
    /// Refleja en el Central el pendiente de entrega o envío que informa una caja (RF-249, RF-252), para verlos todos juntos y seguir los atrasos.
    /// La caja es la autoridad sobre sus pendientes: un mensaje más viejo que lo ya registrado no pisa el estado más reciente.
    /// </summary>
    private async Task RegistrarPendienteAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DatosPendienteEntrega? pendiente = null;
        try
        {
            pendiente = JsonSerializer.Deserialize<DatosPendienteEntrega>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (pendiente is null || pendiente.Id == Guid.Empty || string.IsNullOrWhiteSpace(pendiente.Numero))
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El pendiente de entrega no se pudo leer; el mensaje se guardó sin reflejarlo en el Central.", ahora, cancelacion);
            return;
        }

        var datos = new DatosPendienteCentral(pendiente.Numero, pendiente.VentaId, pendiente.VentaNumero, documento.SucursalId, documento.CajaId, pendiente.Metodo,
            pendiente.Estado, pendiente.AlmacenNombre, pendiente.Ciudad, pendiente.ClienteDocumento, pendiente.ClienteNombre, pendiente.Telefono,
            pendiente.FechaComprometida, pendiente.Lineas.Sum(l => l.Cantidad), pendiente.Lineas.Sum(l => l.CantidadEntregada), pendiente.CreadoEn,
            pendiente.ActualizadoEn, documento.Contenido);

        try
        {
            if (await contexto.PendientesEntrega.SingleOrDefaultAsync(p => p.Id == pendiente.Id, cancelacion) is { } existente)
                existente.Actualizar(datos, ahora);
            else
                contexto.PendientesEntrega.Add(PendienteCentral.Registrar(pendiente.Id, datos, ahora));
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"El pendiente {pendiente.Numero} no se pudo registrar: {ValidacionMaestros.MensajeError(excepcion)}", ahora, cancelacion);
        }
    }

    /// <summary>Registra la nota de crédito en el Central para poder consumirla en cualquier sucursal (RF-43).</summary>
    private async Task RegistrarNotaCreditoAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoNotaCreditoEmitida? emitida = null;
        try
        {
            emitida = JsonSerializer.Deserialize<DocumentoNotaCreditoEmitida>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (emitida?.NotaCredito is not { } datos || datos.Id == Guid.Empty)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "La nota de crédito no se pudo leer; el mensaje se guardó sin registrarla para otras sucursales.", ahora, cancelacion);
            return;
        }

        if (await contexto.NotasCredito.AnyAsync(n => n.Id == datos.Id, cancelacion))
            return;

        try
        {
            var nota = NotaCreditoCentral.Registrar(datos.Id, datos.Numero, datos.Comprobante?.Encf, documento.CajaId, documento.SucursalId, datos.ClienteDocumento,
                datos.ClienteNombre, datos.Moneda, datos.Total, datos.VenceEn, datos.CreadaEn, ahora);

            // Un consumo puede llegar antes que la emisión: los mensajes de una caja llegan en orden, pero los de dos cajas no.
            var consumido = await contexto.ConsumosNotaCredito.Where(c => c.NotaCreditoId == nota.Id).SumAsync(c => (decimal?)c.Monto, cancelacion) ?? 0m;
            if (consumido > 0)
                nota.AplicarConsumo(consumido);

            contexto.NotasCredito.Add(nota);
            if (await registroVentas.RegistrarNotaCreditoAsync(emitida, documento.SucursalId, documento.CajaId, cancelacion) is { } duplicado)
                await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.NumeroDuplicado,
                    duplicado, ahora, cancelacion);
        }
        catch (ArgumentException excepcion)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                $"La nota de crédito {datos.Numero} no se pudo registrar: {ValidacionMaestros.MensajeError(excepcion)}", ahora, cancelacion);
        }
    }

    /// <summary>Descuenta del saldo central lo que una caja consumió y cierra la reserva que tenía (RF-38).</summary>
    private async Task RegistrarConsumoNotaCreditoAsync(DocumentoRecibido documento, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        DocumentoConsumoNotaCredito? consumo = null;
        try
        {
            consumo = JsonSerializer.Deserialize<DocumentoConsumoNotaCredito>(documento.Contenido, OpcionesJson.Predeterminadas);
        }
        catch (JsonException)
        {
        }

        if (consumo is null || consumo.NotaCreditoId == Guid.Empty || consumo.VentaId == Guid.Empty || consumo.Monto <= 0)
        {
            await RegistrarConflictoAsync(documento.CajaId, documento.SucursalId, documento.Id, documento.TipoMensaje, TipoConflictoSincronizacion.DocumentoInvalido,
                "El consumo de la nota de crédito no se pudo leer; el mensaje se guardó sin descontar el saldo.", ahora, cancelacion);
            return;
        }

        if (await contexto.ConsumosNotaCredito.AnyAsync(c => c.NotaCreditoId == consumo.NotaCreditoId && c.VentaId == consumo.VentaId, cancelacion))
            return;

        contexto.ConsumosNotaCredito.Add(ConsumoNotaCreditoCentral.Registrar(consumo.NotaCreditoId, consumo.VentaId, consumo.VentaNumero, documento.CajaId,
            consumo.Monto, consumo.Fecha, ahora));

        // Si la emisión todavía no llegó, el saldo se ajusta al registrarla.
        if (await contexto.NotasCredito.SingleOrDefaultAsync(n => n.Id == consumo.NotaCreditoId, cancelacion) is { } nota)
            nota.AplicarConsumo(consumo.Monto);

        var reserva = await contexto.ReservasNotaCredito
            .Where(r => r.NotaCreditoId == consumo.NotaCreditoId && r.CajaId == documento.CajaId && r.CerradaEn == null)
            .OrderBy(r => r.CreadaEn)
            .FirstOrDefaultAsync(cancelacion);
        reserva?.Cerrar("Consumida", ahora);
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
    private async Task RegistrarConflictoAsync(Guid cajaId, Guid sucursalId, Guid mensajeId, string? tipoMensaje, TipoConflictoSincronizacion tipo, string detalle,
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

    private async Task<EstadoSincronizacionCaja> EstadoCajaAsync(Guid cajaId, CancellationToken cancelacion)
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

            // Una venta sin e-CF (por ejemplo, sin certificado en contingencia) se guarda sin comprobante.
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
