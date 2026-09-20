using System.Globalization;
using System.Text;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Globalizacion;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Perifericos;

namespace CgPos.Pos.Infraestructura.Tickets;

/// <summary>Datos del encabezado del ticket que no están en la venta.</summary>
internal sealed record EncabezadoTicket(
    string EmpresaNombre,
    string EmpresaRnc,
    string? EmpresaDireccion,
    string? EmpresaTelefono,
    string SucursalNombre,
    string? SucursalDireccion,
    string CajaCodigo,
    string? MensajePie,
    TimeZoneInfo ZonaHoraria,
    DatosMoneda Moneda);

/// <summary>
/// Ticket de venta para impresora térmica de 80 mm (42 columnas): texto plano y ESC/POS con la página de códigos PC858 para
/// acentos y ñ. Muestra descuentos por línea y totalizados (RF-84), pagos con su referencia (RF-32), tasa (RF-212) y devuelta.
/// El NCF y el código QR del e-CF se agregan en la Fase C7.
/// </summary>
internal static class GeneradorTicket
{
    public const int Ancho = 42;

    private enum Estilo
    {
        Normal,
        Negrita,
        Centrado,
        Titulo,

        /// <summary>El texto es el contenido de un código QR (URL del timbre del e-CF).</summary>
        Qr,

        /// <summary>El texto se imprime como código de barras CODE128 con el texto legible debajo.</summary>
        Barras,
    }

    static GeneradorTicket() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static DocumentoImpresion Generar(EncabezadoTicket encabezado, DatosVenta venta, bool esCopia)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));
        void Importe(string etiqueta, decimal monto, Estilo estilo = Estilo.Normal) => Agregar(Columnas(etiqueta, monto.ToString("N2", cultura)), estilo);

        Agregar(encabezado.EmpresaNombre.ToUpper(cultura), Estilo.Titulo);
        Agregar($"RNC {encabezado.EmpresaRnc}", Estilo.Centrado);
        if (encabezado.EmpresaDireccion is { } direccionEmpresa)
            foreach (var parte in Envolver(direccionEmpresa))
                Agregar(parte, Estilo.Centrado);
        if (encabezado.EmpresaTelefono is { } telefono)
            Agregar($"Tel. {telefono}", Estilo.Centrado);
        Agregar($"{encabezado.SucursalNombre} · Caja {encabezado.CajaCodigo}", Estilo.Centrado);
        if (esCopia)
            Agregar("*** COPIA ***", Estilo.Negrita);
        Separador();

        Agregar($"{ReglasComprobante.Nombre(venta.TipoComprobante).ToUpper(cultura)} (E{(int)venta.TipoComprobante})", Estilo.Negrita);
        if (venta.Comprobante is { } encabezadoEcf)
        {
            Agregar($"e-NCF: {encabezadoEcf.Encf}", Estilo.Negrita);
            if (encabezadoEcf.VenceSecuencia is { } vence)
                Agregar($"Válido hasta: {vence.ToString("dd/MM/yyyy", cultura)}");
        }
        else
        {
            Agregar("e-NCF: pendiente de emisión");
        }
        Agregar($"Transacción: {venta.NumeroTransaccion}");
        Agregar($"Fecha: {TimeZoneInfo.ConvertTime(venta.CobradaEn ?? venta.IniciadaEn, encabezado.ZonaHoraria).ToString("dd/MM/yyyy h:mm tt", cultura)}");
        Agregar($"Cajero: {venta.UsuarioNombre}");
        if (venta.Cliente is { } cliente)
        {
            foreach (var parte in Envolver($"Cliente: {cliente.Nombre}"))
                Agregar(parte);
            if (cliente.Documento is { } documento)
                Agregar($"{(cliente.TipoDocumento?.ToString() ?? "Documento").ToUpper(cultura)}: {documento}");
        }
        Separador();

        foreach (var linea in venta.Lineas.Where(l => !l.EsReverso && !l.Anulada))
        {
            foreach (var parte in Envolver(linea.Descripcion))
                Agregar(parte);

            var cantidad = linea.Cantidad.ToString("N" + linea.DecimalesCantidad, cultura);
            Agregar(Columnas($"  {cantidad} {linea.UnidadMedidaCodigo} x {linea.PrecioUnitario.ToString("N2", cultura)}", linea.ImporteBruto.ToString("N2", cultura)));
            if (linea.DescuentoPromocion > 0)
                Agregar(Columnas($"  Oferta {linea.PromocionDescripcion}", $"-{linea.DescuentoPromocion.ToString("N2", cultura)}"));
            if (linea.DescuentoManual > 0)
                Agregar(Columnas("  Descuento", $"-{linea.DescuentoManual.ToString("N2", cultura)}"));
            if (linea.DescuentoFactura > 0)
                Agregar(Columnas("  Desc. factura", $"-{linea.DescuentoFactura.ToString("N2", cultura)}"));
            if (linea.Serial is { } serial)
                Agregar($"  Serial: {serial}");
        }
        Separador();

        if (venta.ListaBoda is { } listaBoda)
        {
            foreach (var parte in Envolver($"Lista {listaBoda.Numero}: {listaBoda.Evento}"))
                Agregar(parte);
            Separador();
        }

        Importe("SUBTOTAL (sin ITBIS)", venta.Totales.Subtotal);
        foreach (var tasa in venta.Totales.Desglose.Where(d => d.Impuesto > 0))
            Importe($"ITBIS {tasa.Porcentaje.ToString("0.##", cultura)}%", tasa.Impuesto);

        // La factura va sin ITBIS y el ticket tiene que decirlo, con el motivo: el régimen especial o la certificación.
        if (venta.ExentaDeImpuesto)
        {
            var motivo = venta.CertificacionExencion is { Length: > 0 } certificacion
                ? $"EXENTA DE ITBIS - CERTIFICACION {certificacion}"
                : "EXENTA DE ITBIS - REGIMEN ESPECIAL";
            foreach (var parte in Envolver(motivo))
                Agregar(parte, Estilo.Negrita);
        }
        if (venta.Totales.Descuento > 0)
            Importe("DESCUENTOS", -venta.Totales.Descuento);
        Importe($"TOTAL {venta.SimboloMoneda}", venta.Totales.Total, Estilo.Titulo);

        // Gubernamental (E45): la retención de la Ley 32-23 se le descuenta al cliente de lo que paga.
        if (venta.Totales.Retencion > 0)
        {
            Importe("RETENCIÓN LEY 32-23", -venta.Totales.Retencion);
            Importe($"TOTAL A PAGAR {venta.SimboloMoneda}", venta.Totales.TotalAPagar, Estilo.Titulo);
        }

        if (venta.RedondeoEfectivo != 0)
            Importe("Redondeo efectivo", venta.RedondeoEfectivo);
        if (venta.TotalCobrado is { } cobrado && cobrado != venta.Totales.TotalAPagar)
            Importe("TOTAL COBRADO", cobrado, Estilo.Negrita);

        if (venta.Pagos is { Count: > 0 } pagos)
        {
            Separador();
            foreach (var pago in pagos)
            {
                var detalle = pago.Moneda == venta.Moneda
                    ? pago.FormaPagoNombre
                    : $"{pago.FormaPagoNombre} {pago.MontoRecibido.ToString("N2", cultura)} {pago.Moneda} @ {pago.TasaCambio?.ToString("N2", cultura)}";
                Importe(detalle, pago.MontoAplicado);

                var referencia = string.Join(" ", new[]
                {
                    pago.TipoTarjetaNombre,
                    pago.UltimosDigitos is { } digitos ? $"****{digitos}" : null,
                    pago.BancoNombre,
                    pago.Referencia is { } numero ? $"Ref. {numero}" : null,
                    pago.AprobacionManual ? "(manual)" : null,
                }.Where(texto => !string.IsNullOrWhiteSpace(texto)));
                if (referencia.Length > 0)
                    Agregar($"  {referencia}");
            }

            Importe("DEVUELTA", venta.Devuelta, Estilo.Titulo);
        }

        Separador();
        Agregar($"Artículos: {venta.Totales.CantidadArticulos.ToString("0.###", cultura)}");

        // Programa de fidelidad (RF-238, RF-239).
        if (venta.Fidelidad is { } fidelidad)
        {
            Separador();
            foreach (var parte in Envolver($"Fidelidad: {fidelidad.Nombre}{(fidelidad.Nivel is null ? null : $" ({fidelidad.Nivel})")}"))
                Agregar(parte);
            if (fidelidad.PuntosAcumulados > 0)
                Agregar(Columnas("Puntos ganados", fidelidad.PuntosAcumulados.ToString("N0", cultura)));
            if (fidelidad.PuntosCanjeados > 0)
                Agregar(Columnas("Puntos canjeados", fidelidad.PuntosCanjeados.ToString("N0", cultura)));
        }

        // Representación impresa del e-CF (RF-221): código de seguridad, fecha de firma y QR del timbre.
        if (venta.Comprobante is { } ecf)
        {
            Separador();
            Agregar($"Código de seguridad: {ecf.CodigoSeguridad}");
            Agregar($"Fecha de firma: {TimeZoneInfo.ConvertTime(ecf.FechaFirma, encabezado.ZonaHoraria).ToString("dd-MM-yyyy HH:mm:ss", cultura)}");
            Agregar(ecf.UrlTimbre, Estilo.Qr);
        }

        // Código de barras para llamar la factura en devoluciones (RF-56).
        Agregar(venta.NumeroTransaccion, Estilo.Barras);
        if (encabezado.MensajePie is { } pie)
            foreach (var parte in Envolver(pie))
                Agregar(parte, Estilo.Centrado);

        return Documento($"ticket-{venta.NumeroTransaccion}{(esCopia ? "-copia" : null)}", lineas);
    }

    /// <summary>
    /// Nota de crédito (RF-163): la copia del cliente lleva el código de barras para consumirla (RF-57) y la política de consumo (RF-83);
    /// la de contabilidad lleva su propio texto. Ambas muestran quién autorizó (RF-162).
    /// </summary>
    public static DocumentoImpresion GenerarNotaCredito(EncabezadoTicket encabezado, DatosNotaCredito nota, bool copiaContabilidad, string? politica, bool esCopia)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));
        void Importe(string etiqueta, decimal monto, Estilo estilo = Estilo.Normal) => Agregar(Columnas(etiqueta, monto.ToString("N2", cultura)), estilo);

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia);
        Agregar(nota.EsInterna ? "NOTA DE CRÉDITO INTERNA" : "NOTA DE CRÉDITO (E34)", Estilo.Titulo);
        Agregar(copiaContabilidad ? "COPIA CONTABILIDAD" : "ORIGINAL CLIENTE", Estilo.Centrado);
        if (nota.EsInterna)
            Agregar("SIN VALOR FISCAL - SOLO AJUSTE", Estilo.Negrita);
        if (nota.Comprobante is { } comprobante)
            Agregar($"e-NCF: {comprobante.Encf}", Estilo.Negrita);
        Agregar($"Número: {nota.Numero}");
        Agregar($"Fecha: {HoraLocal(encabezado, nota.CreadaEn, cultura)}");
        Agregar($"Modifica: {nota.EncfOrigen ?? "factura sin e-CF"}");
        Agregar($"Factura: {nota.VentaOrigenNumero} del {TimeZoneInfo.ConvertTime(nota.VentaOrigenCobradaEn, encabezado.ZonaHoraria).ToString("dd/MM/yyyy", cultura)}");
        foreach (var parte in Envolver($"Cliente: {nota.ClienteNombre}"))
            Agregar(parte);
        Agregar($"{(nota.ClienteTipoDocumento?.ToString() ?? "Documento").ToUpper(cultura)}: {nota.ClienteDocumento}");
        Agregar($"Cajero: {nota.UsuarioNombre}");
        if (nota.AutorizadoPorNombre is { } autorizo)
            Agregar($"Autorizó: {autorizo}");
        foreach (var parte in Envolver($"Motivo: {nota.MotivoNombre}{(nota.Observacion is null ? null : $" - {nota.Observacion}")}"))
            Agregar(parte);
        Separador();

        foreach (var linea in nota.Lineas)
        {
            foreach (var parte in Envolver(linea.Descripcion))
                Agregar(parte);
            var cantidad = linea.Cantidad.ToString("N" + linea.DecimalesCantidad, cultura);
            Agregar(Columnas($"  {cantidad} {linea.UnidadMedidaCodigo} x {linea.PrecioUnitario.ToString("N2", cultura)}", linea.Importe.ToString("N2", cultura)));
            if (linea.ImpuestoRetenido > 0)
                Agregar(Columnas("  ITBIS retenido", $"-{linea.ImpuestoRetenido.ToString("N2", cultura)}"));
            if (linea.Serial is { } serial)
                Agregar($"  Serial: {serial}");
        }
        Separador();

        Importe("SUBTOTAL (sin ITBIS)", nota.Subtotal);
        if (nota.Impuesto > 0)
            Importe("ITBIS", nota.Impuesto);
        if (nota.ImpuestoRetenido > 0)
            Importe("ITBIS retenido (fuera de plazo)", nota.ImpuestoRetenido);
        Importe($"TOTAL {Simbolo(encabezado, nota.Moneda)}", nota.Total, Estilo.Titulo);
        if (nota.PuntosReversados > 0)
            Agregar(Columnas("Puntos de fidelidad reversados", nota.PuntosReversados.ToString("N0", cultura)));

        if (nota.EsInterna)
        {
            Agregar("Ajuste interno: no devuelve dinero ni se usa como forma de pago.", Estilo.Negrita);
            Agregar(nota.Numero, Estilo.Barras);
        }
        else if (!copiaContabilidad)
        {
            Agregar($"Válida para consumo hasta el {nota.VenceEn.ToString("dd/MM/yyyy", cultura)}", Estilo.Negrita);
            Agregar(nota.Comprobante?.Encf ?? nota.Numero, Estilo.Barras);
        }

        if (nota.Comprobante is { } ecf)
        {
            Separador();
            Agregar($"Código de seguridad: {ecf.CodigoSeguridad}");
            Agregar($"Fecha de firma: {TimeZoneInfo.ConvertTime(ecf.FechaFirma, encabezado.ZonaHoraria).ToString("dd-MM-yyyy HH:mm:ss", cultura)}");
            Agregar(ecf.UrlTimbre, Estilo.Qr);
        }

        if (politica is not null)
        {
            Separador();
            foreach (var parte in Envolver(politica))
                Agregar(parte);
        }

        AgregarFirmas(lineas, "Cliente", "Autorizado");

        return Documento($"nc-{nota.Numero}{(copiaContabilidad ? "-contabilidad" : null)}{(esCopia ? "-copia" : null)}", lineas);
    }

    /// <summary>
    /// Voucher de pendiente de entrega o envío (RF-55, RF-249): código de barras para el despacho, artículos y cantidades, destino, fecha comprometida,
    /// cliente y políticas. Se imprime la copia del cliente y la del despacho (RF-88).
    /// </summary>
    public static DocumentoImpresion GenerarPendiente(EncabezadoTicket encabezado, DatosPendienteEntrega pendiente, bool copiaCliente, string? politica)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));
        void Envuelto(string texto)
        {
            foreach (var parte in Envolver(texto))
                Agregar(parte);
        }

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia: false);
        Agregar(pendiente.Metodo == MetodoEntrega.Envio ? "PENDIENTE DE ENVÍO" : "PENDIENTE DE RETIRO", Estilo.Titulo);
        Agregar(copiaCliente ? "COPIA CLIENTE" : "COPIA DESPACHO", Estilo.Centrado);
        Agregar($"Número: {pendiente.Numero}", Estilo.Negrita);
        Agregar($"Factura: {pendiente.VentaNumero}");
        Agregar($"Fecha: {HoraLocal(encabezado, pendiente.CreadoEn, cultura)}");
        if (pendiente.ClienteNombre is { } cliente)
            Envuelto($"Cliente: {cliente}{(pendiente.ClienteDocumento is { } documento ? $" ({documento})" : null)}");
        Agregar($"Vendió: {pendiente.VendidoPorNombre}");
        if (pendiente.AutorizadoPorNombre is { } autorizo)
            Agregar($"Autorizó: {autorizo}");
        Separador();

        if (pendiente.Metodo == MetodoEntrega.RetiroAlmacen)
        {
            Envuelto($"Retira en: {pendiente.AlmacenNombre}");
        }
        else
        {
            Envuelto($"Dirección: {pendiente.Direccion}");
            if (string.Join(", ", new[] { pendiente.Sector, pendiente.Ciudad }.Where(t => !string.IsNullOrWhiteSpace(t))) is { Length: > 0 } lugar)
                Envuelto(lugar);
            if (pendiente.Referencia is { } referencia)
                Envuelto($"Referencia: {referencia}");
            Agregar($"Teléfono: {pendiente.Telefono}");
            if (pendiente.Transportista is { } transportista)
                Envuelto($"Transportista: {transportista}");
            if (pendiente.CostoEnvio is { } costo)
                Agregar(Columnas("Costo de envío", costo.ToString("N2", cultura)));
        }

        if (pendiente.FechaComprometida is { } fecha)
            Agregar($"Fecha comprometida: {fecha.ToString("dd/MM/yyyy", cultura)}", Estilo.Negrita);
        if (pendiente.Comentario is { } comentario)
            Envuelto($"Comentario: {comentario}");
        Separador();

        foreach (var linea in pendiente.Lineas)
        {
            Envuelto(linea.Descripcion);
            Agregar(Columnas($"  {linea.Codigo}", $"{linea.Cantidad.ToString("N" + linea.DecimalesCantidad, cultura)} {linea.UnidadMedidaCodigo}"));
            if (linea.Serial is { } serial)
                Agregar($"  Serial: {serial}");
            else if (linea.Serializado)
                Agregar("  Serial: se registra al entregar");
        }

        // Código para llamar el pendiente en el despacho (RF-251).
        Agregar(pendiente.Numero, Estilo.Barras);

        if (politica is not null)
        {
            Separador();
            Envuelto(politica);
        }

        AgregarFirmas(lineas, "Cliente", "Despacho");
        return Documento($"pendiente-{pendiente.Numero}-{(copiaCliente ? "cliente" : "despacho")}", lineas);
    }

    /// <summary>Constancia de entrega de un pendiente (RF-254): qué se entregó, con serial, quién recibió con su cédula y lo que queda pendiente.</summary>
    public static DocumentoImpresion GenerarConstanciaEntrega(EncabezadoTicket encabezado, DatosPendienteEntrega pendiente, DatosEntregaPendiente entrega)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia: false);
        Agregar("CONSTANCIA DE ENTREGA", Estilo.Titulo);
        Agregar($"Pendiente: {pendiente.Numero} · entrega {entrega.Numero}", Estilo.Negrita);
        Agregar($"Factura: {pendiente.VentaNumero}");
        Agregar($"Fecha: {HoraLocal(encabezado, entrega.Fecha, cultura)}");
        foreach (var parte in Envolver($"Recibe: {entrega.RecibeNombre} · Cédula {entrega.RecibeCedula}"))
            Agregar(parte);
        Agregar($"Entregó: {entrega.UsuarioNombre}");
        Separador();

        foreach (var linea in entrega.Lineas)
        {
            foreach (var parte in Envolver(linea.Descripcion))
                Agregar(parte);
            Agregar(Columnas("  Entregado", linea.Cantidad.ToString("0.###", cultura)));
            if (linea.Serial is { } serial)
                Agregar($"  Serial: {serial}");
        }

        var restantes = pendiente.Lineas.Where(l => l.Cantidad - l.CantidadEntregada > 0).ToList();
        Separador();
        if (restantes.Count == 0)
        {
            Agregar("ENTREGA COMPLETA", Estilo.Negrita);
        }
        else
        {
            Agregar("QUEDA PENDIENTE", Estilo.Negrita);
            foreach (var linea in restantes)
                Agregar(Columnas($"  {linea.Codigo}", (linea.Cantidad - linea.CantidadEntregada).ToString("0.###", cultura)));
        }

        Agregar("Recibí conforme la mercancía descrita.");
        AgregarFirmas(lineas, "Recibe", "Entrega");
        return Documento($"constancia-{pendiente.Numero}-{entrega.Numero}", lineas);
    }

    /// <summary>Voucher con el saldo que queda de una nota de crédito usada parcialmente (RF-43).</summary>
    public static DocumentoImpresion GenerarSaldoNotaCredito(EncabezadoTicket encabezado, string codigo, string cliente, decimal saldo, string moneda, DateOnly venceEn,
        string ventaNumero)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia: false);
        Agregar("SALDO DE NOTA DE CRÉDITO", Estilo.Titulo);
        Agregar($"Nota: {codigo}");
        foreach (var parte in Envolver($"Cliente: {cliente}"))
            Agregar(parte);
        Agregar($"Usada en la factura {ventaNumero}");
        Agregar(new string('-', Ancho));
        Agregar(Columnas($"SALDO DISPONIBLE {Simbolo(encabezado, moneda)}", saldo.ToString("N2", cultura)), Estilo.Titulo);
        Agregar($"Válida hasta el {venceEn.ToString("dd/MM/yyyy", cultura)}", Estilo.Negrita);
        Agregar(codigo, Estilo.Barras);

        return Documento($"saldo-nc-{codigo}-{ventaNumero}", lineas);
    }

    /// <summary>Comprobante de retiro parcial de efectivo (RF-261), con firmas de quien entrega y quien recibe.</summary>
    public static DocumentoImpresion GenerarRetiro(EncabezadoTicket encabezado, DatosMovimientoCaja retiro, long turnoNumero)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia: false);
        Agregar($"RETIRO DE EFECTIVO Nº {retiro.Numero}", Estilo.Titulo);
        Agregar($"Turno: {turnoNumero}");
        Agregar($"Fecha: {HoraLocal(encabezado, retiro.Fecha, cultura)}");
        Agregar($"Cajero: {retiro.UsuarioNombre}");
        if (retiro.AutorizadoPorNombre is { } autorizo)
            Agregar($"Autorizó: {autorizo}");
        if (retiro.Motivo is { } motivo)
            foreach (var parte in Envolver($"Motivo: {motivo}"))
                Agregar(parte);
        Agregar(new string('-', Ancho));
        Agregar(Columnas($"MONTO {retiro.Moneda}", retiro.Monto.ToString("N2", cultura)), Estilo.Titulo);
        AgregarFirmas(lineas, "Entrega", "Recibe");

        return Documento($"retiro-{turnoNumero}-{retiro.Numero}", lineas);
    }

    /// <summary>Pre-cierre (RF-8): lo esperado por forma de pago sin cerrar el turno.</summary>
    public static DocumentoImpresion GenerarPreCierre(EncabezadoTicket encabezado, DatosResumenTurno resumen, DateTimeOffset emitido)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));
        void Importe(string etiqueta, decimal monto, Estilo estilo = Estilo.Normal) => Agregar(Columnas(etiqueta, monto.ToString("N2", cultura)), estilo);

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia: false);
        Agregar("PRE-CIERRE", Estilo.Titulo);
        Agregar("(no cierra el turno)", Estilo.Centrado);
        Agregar($"Turno {resumen.Turno.Numero} · {resumen.Turno.UsuarioActualNombre}");
        Agregar($"Apertura: {HoraLocal(encabezado, resumen.Turno.AbiertoEn, cultura)}");
        Agregar($"Emitido:  {HoraLocal(encabezado, emitido, cultura)}");
        Separador();
        Agregar(Columnas("Ventas cobradas", (resumen.CantidadVentas ?? 0).ToString("N0", cultura)));
        Importe("Total ventas", resumen.TotalVentas ?? 0m);
        Importe(resumen.FondoEnCuadre ? "Fondo inicial" : "Fondo inicial (fuera)", resumen.Turno.FondoInicial);
        Importe("Retiros", -resumen.TotalRetiros);
        Separador();
        Agregar("ESPERADO POR FORMA DE PAGO", Estilo.Negrita);
        foreach (var forma in resumen.FormasPago.Where(f => (f.Esperado ?? 0m) != 0m || (f.Transacciones ?? 0) > 0))
            Importe($"{forma.Nombre}{MonedaExtranjera(resumen.MonedaLocal, forma.Moneda)} ({forma.Transacciones ?? 0})", forma.Esperado ?? 0m);

        if (resumen.Bloqueos.Count > 0)
        {
            Separador();
            Agregar("PENDIENTE ANTES DE CERRAR", Estilo.Negrita);
            foreach (var bloqueo in resumen.Bloqueos)
                foreach (var parte in Envolver($"- {bloqueo}"))
                    Agregar(parte);
        }

        return Documento($"precierre-{resumen.Turno.Numero}-{emitido.ToUnixTimeSeconds()}", lineas);
    }

    /// <summary>
    /// Reporte de cierre de turno (RF-106, RF-263, RF-291): esperado, declarado y diferencia por forma de pago, conteo por denominaciones,
    /// retiros y relevos. La reimpresión reproduce el mismo reporte.
    /// </summary>
    public static DocumentoImpresion GenerarCierre(EncabezadoTicket encabezado, DatosCierre cierre, bool esCopia)
    {
        var cultura = CulturaRd.Crear();
        var lineas = new List<(string Texto, Estilo Estilo)>();
        void Agregar(string texto, Estilo estilo = Estilo.Normal) => lineas.Add((texto, estilo));
        void Separador() => Agregar(new string('-', Ancho));
        void Importe(string etiqueta, decimal monto, Estilo estilo = Estilo.Normal) => Agregar(Columnas(etiqueta, monto.ToString("N2", cultura)), estilo);

        AgregarEncabezadoCaja(lineas, encabezado, cultura, esCopia);
        Agregar($"CIERRE DE TURNO Nº {cierre.TurnoNumero}", Estilo.Titulo);
        Agregar($"Cierre {cierre.Numero}{(cierre.Ciego ? " · ciego" : string.Empty)} · Día {cierre.FechaOperacion.ToString("dd/MM/yyyy", cultura)}");
        Agregar($"Apertura: {HoraLocal(encabezado, cierre.AbiertoEn, cultura)}");
        Agregar($"Cierre:   {HoraLocal(encabezado, cierre.CerradoEn, cultura)}");
        Agregar($"Cajero: {cierre.UsuarioNombre}");
        Separador();
        Agregar(Columnas("Ventas cobradas", cierre.CantidadVentas.ToString("N0", cultura)));
        Importe("Total ventas", cierre.TotalVentas);
        Importe(cierre.FondoEnCuadre ? "Fondo inicial" : "Fondo inicial (fuera)", cierre.FondoInicial);
        Importe("Retiros", -cierre.TotalRetiros);
        Separador();

        Agregar("CUADRE POR FORMA DE PAGO", Estilo.Negrita);
        foreach (var forma in cierre.FormasPago)
        {
            Agregar($"{forma.Nombre}{MonedaExtranjera(cierre.Moneda, forma.Moneda)} · {forma.Transacciones.ToString("N0", cultura)} trx");
            Importe("  Esperado", forma.Esperado);
            Importe("  Declarado", forma.Declarado);
            Importe("  Diferencia", forma.Diferencia, forma.Diferencia == 0m ? Estilo.Normal : Estilo.Negrita);
        }
        Separador();
        var simbolo = Simbolo(encabezado, cierre.Moneda);
        var resultado = cierre.Diferencia switch { > 0m => "SOBRANTE", < 0m => "FALTANTE", _ => "CUADRADO" };
        Importe($"TOTAL ESPERADO {simbolo}", cierre.TotalEsperado);
        Importe($"TOTAL DECLARADO {simbolo}", cierre.TotalDeclarado);
        Importe($"{resultado} {simbolo}", cierre.Diferencia, Estilo.Titulo);

        if (cierre.Denominaciones.Count > 0)
        {
            Separador();
            Agregar("CONTEO POR DENOMINACIONES", Estilo.Negrita);
            foreach (var denominacion in cierre.Denominaciones)
                Importe($"  {denominacion.Moneda} {denominacion.Valor.ToString("N2", cultura)} x {denominacion.Cantidad.ToString("N0", cultura)}", denominacion.Importe);
        }

        var retiros = cierre.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Retiro).ToList();
        if (retiros.Count > 0)
        {
            Separador();
            Agregar("RETIROS", Estilo.Negrita);
            foreach (var retiro in retiros)
                Importe($"  Nº {retiro.Numero} {HoraCorta(encabezado, retiro.Fecha, cultura)} {retiro.AutorizadoPorNombre}", retiro.Monto);
        }

        var reembolsos = cierre.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Reembolso).ToList();
        if (reembolsos.Count > 0)
        {
            Separador();
            Agregar("REEMBOLSOS AL CLIENTE", Estilo.Negrita);
            foreach (var reembolso in reembolsos)
                Importe($"  Nº {reembolso.Numero} {HoraCorta(encabezado, reembolso.Fecha, cultura)} {reembolso.Motivo}", reembolso.Monto);
        }

        var relevos = cierre.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Relevo).ToList();
        if (relevos.Count > 0)
        {
            Separador();
            Agregar("RELEVOS", Estilo.Negrita);
            foreach (var relevo in relevos)
                foreach (var parte in Envolver($"  {HoraCorta(encabezado, relevo.Fecha, cultura)} {relevo.UsuarioAnteriorNombre} -> {relevo.UsuarioNombre}"))
                    Agregar(parte);
        }

        AgregarFirmas(lineas, "Cajero", "Supervisor");
        return Documento($"cierre-{cierre.TurnoNumero}-{cierre.Numero}{(esCopia ? "-copia" : null)}", lineas);
    }

    private static void AgregarEncabezadoCaja(List<(string Texto, Estilo Estilo)> lineas, EncabezadoTicket encabezado, CultureInfo cultura, bool esCopia)
    {
        lineas.Add((encabezado.EmpresaNombre.ToUpper(cultura), Estilo.Titulo));
        lineas.Add(($"RNC {encabezado.EmpresaRnc}", Estilo.Centrado));
        lineas.Add(($"{encabezado.SucursalNombre} · Caja {encabezado.CajaCodigo}", Estilo.Centrado));
        if (esCopia)
            lineas.Add(("*** COPIA ***", Estilo.Negrita));
        lineas.Add((new string('-', Ancho), Estilo.Normal));
    }

    private static void AgregarFirmas(List<(string Texto, Estilo Estilo)> lineas, string izquierda, string derecha)
    {
        lineas.Add((string.Empty, Estilo.Normal));
        lineas.Add((string.Empty, Estilo.Normal));
        lineas.Add(("___________________  ___________________", Estilo.Normal));
        lineas.Add((izquierda.PadRight(21) + derecha, Estilo.Normal));
    }

    private static DocumentoImpresion Documento(string nombre, List<(string Texto, Estilo Estilo)> lineas)
    {
        var texto = string.Join('\n', lineas.SelectMany(l => l.Estilo switch
        {
            Estilo.Centrado or Estilo.Titulo => [Centrar(l.Texto)],
            Estilo.Qr => ["[QR del timbre e-CF]", .. Trozos(l.Texto)],
            Estilo.Barras => [Centrar($"||| {l.Texto} |||")],
            _ => new[] { l.Texto },
        }));
        return new DocumentoImpresion(nombre, texto, EscPos(lineas));
    }

    /// <summary>Fecha y hora en la zona horaria configurada en el equipo de la caja.</summary>
    private static string HoraLocal(EncabezadoTicket encabezado, DateTimeOffset fecha, CultureInfo cultura) =>
        TimeZoneInfo.ConvertTime(fecha, encabezado.ZonaHoraria).ToString("dd/MM/yyyy h:mm tt", cultura);

    private static string HoraCorta(EncabezadoTicket encabezado, DateTimeOffset fecha, CultureInfo cultura) =>
        TimeZoneInfo.ConvertTime(fecha, encabezado.ZonaHoraria).ToString("h:mm tt", cultura);

    /// <summary>Marca las formas de pago en moneda extranjera; la moneda local no se indica.</summary>
    private static string MonedaExtranjera(string monedaLocal, string moneda) => moneda == monedaLocal ? string.Empty : $" ({moneda})";

    /// <summary>Símbolo de la moneda local configurada, o el código si el documento está en otra moneda.</summary>
    private static string Simbolo(EncabezadoTicket encabezado, string moneda) => moneda == encabezado.Moneda.Codigo ? encabezado.Moneda.Simbolo : moneda;

    private static byte[] EscPos(IEnumerable<(string Texto, Estilo Estilo)> lineas)
    {
        var codificacion = Encoding.GetEncoding(858);
        using var bytes = new MemoryStream();
        void Escribir(params byte[] datos) => bytes.Write(datos);

        Escribir(0x1B, 0x40);       // ESC @: iniciar
        Escribir(0x1B, 0x74, 19);   // ESC t 19: página de códigos PC858

        foreach (var (texto, estilo) in lineas)
        {
            if (estilo == Estilo.Qr)
            {
                // Código QR nativo de la impresora (GS ( k): modelo 2, módulo 5, corrección M.
                var datos = Encoding.ASCII.GetBytes(texto);
                var largo = datos.Length + 3;
                Escribir(0x1B, 0x61, 1);
                Escribir(0x1D, 0x28, 0x6B, 0x04, 0x00, 0x31, 0x41, 0x32, 0x00);
                Escribir(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x43, 0x05);
                Escribir(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x45, 0x31);
                Escribir(0x1D, 0x28, 0x6B, (byte)(largo & 0xFF), (byte)(largo >> 8), 0x31, 0x50, 0x30);
                bytes.Write(datos);
                Escribir(0x1D, 0x28, 0x6B, 0x03, 0x00, 0x31, 0x51, 0x30);
                Escribir(0x0A);
                continue;
            }

            if (estilo == Estilo.Barras)
            {
                // CODE128 subconjunto B (GS k 73): alto 60 puntos, módulo 2 y texto legible debajo.
                var datos = Encoding.ASCII.GetBytes("{B" + texto);
                Escribir(0x1B, 0x61, 1);
                Escribir(0x1D, 0x68, 60);
                Escribir(0x1D, 0x77, 2);
                Escribir(0x1D, 0x48, 2);
                Escribir(0x1D, 0x6B, 73, (byte)datos.Length);
                bytes.Write(datos);
                Escribir(0x0A);
                continue;
            }

            Escribir(0x1B, 0x61, (byte)(estilo is Estilo.Centrado or Estilo.Titulo ? 1 : 0)); // alineación
            Escribir(0x1B, 0x45, (byte)(estilo is Estilo.Negrita or Estilo.Titulo ? 1 : 0)); // negrita
            Escribir(0x1D, 0x21, (byte)(estilo == Estilo.Titulo ? 0x01 : 0x00));             // doble alto en títulos
            bytes.Write(codificacion.GetBytes(texto));
            Escribir(0x0A);
        }

        Escribir(0x1D, 0x21, 0x00);
        Escribir(0x1B, 0x64, 4);          // avanzar 4 líneas
        Escribir(0x1D, 0x56, 0x42, 0x00); // corte parcial
        return bytes.ToArray();
    }

    private static string Columnas(string izquierda, string derecha)
    {
        var espacio = Ancho - derecha.Length - 1;
        var recortada = izquierda.Length > espacio ? izquierda[..espacio] : izquierda;
        return recortada.PadRight(espacio) + " " + derecha;
    }

    private static IEnumerable<string> Trozos(string texto)
    {
        for (var inicio = 0; inicio < texto.Length; inicio += Ancho)
            yield return texto.Substring(inicio, Math.Min(Ancho, texto.Length - inicio));
    }

    private static string Centrar(string texto) =>
        texto.Length >= Ancho ? texto : new string(' ', (Ancho - texto.Length) / 2) + texto;

    private static IEnumerable<string> Envolver(string texto)
    {
        var palabras = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actual = new StringBuilder();
        foreach (var palabra in palabras)
        {
            if (actual.Length > 0 && actual.Length + 1 + palabra.Length > Ancho)
            {
                yield return actual.ToString();
                actual.Clear();
            }

            if (actual.Length > 0)
                actual.Append(' ');
            actual.Append(palabra.Length > Ancho ? palabra[..Ancho] : palabra);
        }

        if (actual.Length > 0)
            yield return actual.ToString();
    }
}
