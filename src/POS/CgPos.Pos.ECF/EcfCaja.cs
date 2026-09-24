using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Ventas;
using CgPos.ECF.Documentos;
using CgPos.ECF.Firma;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.ECF;

/// <summary>Certificado de la caja en memoria del Agente (RF-217). Se carga con el PIN y no se guarda el PIN.</summary>
internal sealed class CertificadoCaja(IConfiguration configuracion, ILogger<CertificadoCaja> registro) : ICertificadoCaja, IDisposable
{
    private readonly Lock _bloqueo = new();
    private X509Certificate2? _certificado;

    private string? Ruta => configuracion[ClavesEcf.RutaCertificado] is { Length: > 0 } ruta ? Path.GetFullPath(ruta) : null;

    public bool Configurado => Ruta is { } ruta && File.Exists(ruta);

    public bool Cargado => _certificado is not null;

    public DateTimeOffset? VenceEn => _certificado is { } certificado ? new DateTimeOffset(certificado.NotAfter) : null;

    public string? Sujeto => _certificado?.Subject;

    public string? Cargar(string pin)
    {
        if (string.IsNullOrEmpty(pin))
            return "Digite el PIN del certificado digital.";
        if (!Configurado)
            return "La caja no tiene certificado digital instalado (Ecf:Certificado:Ruta).";

        try
        {
            var nuevo = CertificadoFirma.CargarPkcs12DesdeArchivo(Ruta!, pin);
            lock (_bloqueo)
            {
                var anterior = _certificado;
                _certificado = nuevo;
                anterior?.Dispose();
            }

            registro.LogInformation("Certificado digital cargado: {Sujeto}, vence {Vence:yyyy-MM-dd}", nuevo.Subject, nuevo.NotAfter);
            return null;
        }
        catch (CryptographicException)
        {
            return "PIN incorrecto o certificado dañado.";
        }
        catch (InvalidOperationException excepcion)
        {
            return excepcion.Message;
        }
    }

    public X509Certificate2? ObtenerParaFirmar() => _certificado;

    public void Dispose() => _certificado?.Dispose();
}

/// <summary>
/// Emite el e-CF de una venta cobrada dentro de la transacción del cobro (RF-218): toma la siguiente secuencia de la caja,
/// arma y valida el documento, lo firma con el certificado en memoria, obtiene el código de seguridad y el timbre, y deja el
/// XML firmado en la carpeta de pendientes (RF-219). Si la transacción no se confirma, la secuencia vuelve atrás.
/// </summary>
internal sealed class EmisionComprobantes(ContextoDatosPos contexto, ICertificadoCaja certificado, IParametros parametros, IConfiguration configuracion,
    IBandejaSalida bandejaSalida, TimeProvider reloj) : IEmisorComprobantes
{
    private readonly FirmadorEcf _firmador = new();

    /// <summary>Moneda en que la DGII recibe los montos del e-CF (norma fiscal, no configurable).</summary>
    private const string MonedaEcf = "DOP";

    /// <remarks>Debe llamarse con una transacción abierta en el contexto.</remarks>
    public Task<EmisionEcf> EmitirAsync(Venta venta, CancellationToken cancelacion)
    {
        ValidarMoneda(venta.Moneda);
        return EmitirDocumentoAsync(venta.Id, venta.NumeroTransaccion, OrigenComprobante.Venta, venta.CajaId, venta.SucursalId, venta.TipoComprobante, venta.CobradaEn,
            (encf, vence, emisor, ahora, tipoIngresos) => ConversionEcf.DesdeVenta(venta, encf, vence, emisor, ahora, tipoIngresos), cancelacion);
    }

    /// <summary>
    /// El e-CF se expresa en pesos dominicanos. Una caja con otra moneda local no puede emitirlo hasta definir la conversión
    /// (sección de otra moneda del e-CF con su tasa); se rechaza en lugar de enviar montos en una moneda que la DGII no espera.
    /// </summary>
    private static void ValidarMoneda(string moneda)
    {
        if (!string.Equals(moneda, MonedaEcf, StringComparison.OrdinalIgnoreCase))
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.EcfInvalido,
                $"La moneda local de la caja es {moneda}: el e-CF de la DGII se emite en {MonedaEcf} y la conversión aún no está definida.");
    }

    /// <summary>Nota de crédito E34 de una devolución, referenciando el e-CF de la factura (RF-227).</summary>
    /// <remarks>Debe llamarse con una transacción abierta en el contexto.</remarks>
    public Task<EmisionEcf> EmitirNotaCreditoAsync(Devolucion devolucion, CancellationToken cancelacion)
    {
        ValidarMoneda(devolucion.Moneda);
        return EmitirDocumentoAsync(devolucion.Id, devolucion.Numero, OrigenComprobante.Devolucion, devolucion.CajaId, devolucion.SucursalId, TipoComprobante.NotaCredito, devolucion.CreadaEn,
            (encf, vence, emisor, ahora, tipoIngresos) => ConversionEcf.DesdeNotaCredito(devolucion, encf, vence, emisor, ahora, tipoIngresos), cancelacion);
    }

    /// <param name="documentoId">Venta o devolución que origina el comprobante.</param>
    /// <param name="numeroDocumento">Número de esa venta o devolución, para encontrar el comprobante por él.</param>
    private async Task<EmisionEcf> EmitirDocumentoAsync(int documentoId, string numeroDocumento, OrigenComprobante origen, int cajaId, int sucursalId,
        TipoComprobante tipo, DateTimeOffset? fechaEmision,
        Func<string, DateOnly, EmisorEcf, DateTimeOffset, int, DocumentoEcf> armar, CancellationToken cancelacion)
    {
        var certificadoFirma = certificado.ObtenerParaFirmar()
            ?? throw new EmisionEcfExcepcion(CodigoResultadoVenta.CertificadoNoCargado,
                "El certificado digital de la caja no está cargado. Digite su PIN para poder emitir comprobantes.");

        var ahora = reloj.Ahora();
        if (certificado.VenceEn is { } vence && vence < ahora)
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.CertificadoNoCargado, "El certificado digital de la caja está vencido. Solicite uno nuevo.");

        var hoy = reloj.Ahora().Dia();
        var asignada = await SiguienteSecuenciaAsync(cajaId, tipo, hoy, cancelacion)
            ?? throw new EmisionEcfExcepcion(CodigoResultadoVenta.ComprobanteNoDisponible,
                $"No hay secuencia de e-CF disponible para {ReglasComprobante.Nombre(tipo)} (E{(int)tipo}) en esta caja: " +
                "está agotada, vencida o no asignada. Solicite un rango al Central: sin e-NCF disponible no se puede facturar.");

        var encf = SecuenciaEcf.FormatearEncf(tipo, asignada.Ultimo, asignada.Serie);
        await InformarConsumoAsync(cajaId, tipo, asignada, cancelacion);
        var emisor = await EmisorAsync(sucursalId, cancelacion);
        var tipoIngresos = await parametros.ObtenerEnteroAsync(ClavesParametros.TipoIngresos, cajaId, cancelacion);
        // Las fechas del e-CF van en la hora local configurada en el equipo de la caja.
        var documentoEcf = armar(encf, asignada.VenceEn, emisor, reloj.Ahora(), tipoIngresos);

        var montoIdentificacion = await parametros.ObtenerDecimalAsync(ClavesParametros.MontoIdentificacionConsumo, cajaId, cancelacion);
        var xml = GeneradorXmlEcf.Generar(documentoEcf);
        var errores = ValidadorEcf.Validar(documentoEcf, montoIdentificacion).ToList();
        if (errores.Count > 0)
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.EcfInvalido, $"El e-CF no pasó la validación: {string.Join(" ", errores.Take(3))}");

        var firmado = _firmador.Firmar(xml, certificadoFirma);

        // El esquema de la DGII exige la firma, así que se valida el XML ya firmado: si no cumple, no se envía nada.
        var erroresXsd = ValidadorEcf.ValidarContraXsd(firmado, configuracion[ClavesEcf.CarpetaXsd], documentoEcf.TipoEcf);
        if (erroresXsd.Count > 0)
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.EcfInvalido, $"El e-CF no cumple el esquema de la DGII: {string.Join(" ", erroresXsd.Take(3))}");

        var codigoSeguridad = CodigoSeguridadEcf.Obtener(firmado);
        // Las direcciones del timbre definen el ambiente de la DGII: son parámetros obligatorios, nunca se asume uno.
        var urlTimbre = TimbreEcf.Url(await parametros.ObtenerRequeridoAsync(ClavesParametros.UrlConsultaTimbre, cajaId, cancelacion),
            await parametros.ObtenerRequeridoAsync(ClavesParametros.UrlConsultaTimbreConsumo, cajaId, cancelacion), documentoEcf, codigoSeguridad, montoIdentificacion);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(firmado)));

        var ruta = RutaXml(RutasXmlEcf.Pendientes, reloj.Ahora(), emisor.Rnc, encf);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        await File.WriteAllTextAsync(ruta, firmado, new UTF8Encoding(false), cancelacion);

        var documento = DocumentoElectronico.Emitir(documentoId, numeroDocumento, origen, cajaId, tipo, encf, fechaEmision ?? ahora, documentoEcf.FechaHoraFirma,
            codigoSeguridad, documentoEcf.Totales.MontoTotal, hash, ruta, urlTimbre);
        contexto.DocumentosElectronicos.Add(documento);

        // Una factura de consumo que no llega al monto de identificación se le informa a la DGII como resumen (RFCE): el e-CF
        // completo queda en la caja y es el que recibe el cliente; al Central viaja el resumen firmado.
        var paraCentral = documentoEcf.TipoEcf == GeneradorXmlRfce.TipoResumible && documentoEcf.Totales.MontoTotal < montoIdentificacion
            ? ResumenConsumo(documentoEcf, codigoSeguridad, certificadoFirma, configuracion[ClavesEcf.CarpetaXsd])
            : new DocumentoElectronicoParaCentral(encf, tipo, firmado, hash, documentoEcf.FechaHoraFirma);

        return new EmisionEcf(documento, paraCentral, asignada.VenceEn);
    }

    /// <summary>Resumen de consumo firmado, listo para que el Central lo envíe al servicio de facturas de consumo de la DGII.</summary>
    private DocumentoElectronicoParaCentral ResumenConsumo(DocumentoEcf documentoEcf, string codigoSeguridad, X509Certificate2 certificado, string? carpetaXsd)
    {
        var resumen = _firmador.Firmar(GeneradorXmlRfce.Generar(documentoEcf, codigoSeguridad), certificado);
        var errores = ValidadorEcf.ValidarContraXsd(resumen, carpetaXsd, nombreEsquema: "RFCE");
        if (errores.Count > 0)
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.EcfInvalido, $"El resumen de consumo no cumple el esquema de la DGII: {string.Join(" ", errores.Take(3))}");

        return new DocumentoElectronicoParaCentral(documentoEcf.Encf, (TipoComprobante)documentoEcf.TipoEcf, resumen,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resumen))), documentoEcf.FechaHoraFirma, EsResumenConsumo: true);
    }

    void IEmisorComprobantes.DescartarArchivo(EmisionEcf emision) => DescartarArchivo(emision);

    /// <summary>Si el cobro no llegó a guardarse, su XML no debe quedar como pendiente.</summary>
    public static void DescartarArchivo(EmisionEcf emision)
    {
        try
        {
            File.Delete(emision.Documento.RutaXml);
        }
        catch (IOException)
        {
        }
    }

    private string RutaXml(string estado, DateTimeOffset fecha, string rnc, string encf)
    {
        var baseXml = configuracion[ClavesEcf.CarpetaXml] is { Length: > 0 } carpeta ? carpeta : ClavesEcf.CarpetaXmlPredeterminada;
        return Path.GetFullPath(Path.Combine(baseXml, estado, fecha.ToString("yyyy"), fecha.ToString("MM"), fecha.ToString("dd"), $"{rnc}{encf}.xml"));
    }

    private sealed class SecuenciaAsignada
    {
        public int Id { get; set; }
        public long Ultimo { get; set; }
        public long Desde { get; set; }
        public long Hasta { get; set; }
        public DateOnly VenceEn { get; set; }

        public long Restantes => Hasta - Ultimo;

        public bool Agotada => Ultimo >= Hasta;

        /// <summary>Serie del rango: la lleva el e-NCF, y cada rango conserva la suya.</summary>
        public string Serie { get; set; } = SecuenciaEcf.SeriePredeterminada;
    }

    /// <summary>Toma la siguiente secuencia disponible de forma atómica (bloqueo de fila) dentro de la transacción abierta.</summary>
    private async Task<SecuenciaAsignada?> SiguienteSecuenciaAsync(int cajaId, TipoComprobante tipo, DateOnly hoy, CancellationToken cancelacion)
    {
        var fecha = hoy.ToDateTime(TimeOnly.MinValue);
        var resultado = await contexto.Database.SqlQuery<SecuenciaAsignada>($"""
            WITH Candidata AS (
                SELECT TOP (1) * FROM SecuenciasEcf WITH (UPDLOCK, ROWLOCK)
                WHERE CajaId = {cajaId} AND TipoComprobante = {(int)tipo} AND Activa = 1 AND Ultimo < Hasta AND VenceEn >= {fecha}
                ORDER BY Desde)
            UPDATE Candidata SET Ultimo = Ultimo + 1
            OUTPUT inserted.Id AS Id, inserted.Ultimo AS Ultimo, inserted.Desde AS Desde, inserted.Hasta AS Hasta,
                   inserted.VenceEn AS VenceEn, inserted.Serie AS Serie;
            """).ToListAsync(cancelacion);

        return resultado.SingleOrDefault();
    }

    /// <summary>
    /// Le dice al Central por dónde va el rango, pero solo cuando importa: al quedar poco o al agotarse. En cada factura
    /// sería un mensaje por venta para un dato que nadie mira. El rango agotado se borra aquí mismo: en la caja no sirve
    /// para nada y el Central deja de bajárselo, que es lo que evita que vuelva a aparecer con la cuenta en cero.
    /// </summary>
    private async Task InformarConsumoAsync(int cajaId, TipoComprobante tipo, SecuenciaAsignada asignada, CancellationToken cancelacion)
    {
        var aviso = await parametros.ObtenerEnteroOpcionalAsync(ClavesParametros.ComprobantesAlertaSecuenciaEcf, cajaId, cancelacion);
        if (!asignada.Agotada && !(aviso is { } limite && asignada.Restantes <= limite))
            return;

        bandejaSalida.Encolar(TiposMensaje.ConsumoSecuenciaEcf, $"{cajaId}-{(int)tipo}-{asignada.Desde}",
            new DocumentoConsumoSecuenciaEcf(tipo, asignada.Serie, asignada.Desde, asignada.Hasta, asignada.Ultimo, asignada.Agotada, reloj.Ahora()));

        if (!asignada.Agotada)
            return;

        // Va en la misma transacción del cobro: si el cobro se deshace, el rango sigue ahí con su número sin usar.
        await contexto.SecuenciasEcf.Where(s => s.Id == asignada.Id).ExecuteDeleteAsync(cancelacion);
    }

    private async Task<EmisorEcf> EmisorAsync(int sucursalId, CancellationToken cancelacion)
    {
        var datos = await (
                from sucursal in contexto.Sucursales
                join empresa in contexto.Empresas on sucursal.EmpresaId equals empresa.Id
                where sucursal.Id == sucursalId
                select new { empresa.Rnc, empresa.RazonSocial, empresa.NombreComercial, EmpresaDireccion = empresa.Direccion, Sucursal = sucursal.Nombre, SucursalDireccion = sucursal.Direccion })
            .AsNoTracking()
            .SingleAsync(cancelacion);

        var direccion = !string.IsNullOrWhiteSpace(datos.EmpresaDireccion) ? datos.EmpresaDireccion
            : !string.IsNullOrWhiteSpace(datos.SucursalDireccion) ? datos.SucursalDireccion
            : throw new EmisionEcfExcepcion(CodigoResultadoVenta.EcfInvalido,
                "La empresa y la sucursal no tienen dirección registrada: configúrela en el Central para emitir comprobantes electrónicos.");

        return new EmisorEcf(datos.Rnc, datos.RazonSocial, datos.NombreComercial, datos.Sucursal, direccion);
    }
}

/// <summary>Convierte la venta cobrada al documento e-CF: montos sin ITBIS por tasa, descuentos por línea y formas de pago.</summary>
internal static class ConversionEcf
{
    /// <summary>La caja solo vende al contado: las ventas a crédito no pasan por el POS.</summary>
    private const int TipoPagoContado = 1;

    public static DocumentoEcf DesdeVenta(Venta venta, string encf, DateOnly venceSecuencia, EmisorEcf emisor, DateTimeOffset fechaFirma, int tipoIngresos)
    {
        var lineas = venta.Lineas.Where(l => l.EstaActiva).OrderBy(l => l.NumeroLinea).ToList();
        var items = new List<ItemEcf>(lineas.Count);
        decimal gravado1 = 0, gravado2 = 0, gravado3 = 0, exento = 0, itbis1 = 0, itbis2 = 0, itbis3 = 0;

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            // Los precios van sin ITBIS: la base es el importe de la línea y su ITBIS ya viene calculado.
            var baseNeta = linea.Importe;
            var baseBruta = linea.ImporteBruto;
            var itbis = linea.Impuesto;
            var precioUnitario = linea.Cantidad == 0 ? 0m : decimal.Round(linea.ImporteBruto / linea.Cantidad, 4, MidpointRounding.AwayFromZero);
            var indicador = linea.IndicadorFacturacion is >= 1 and <= 4 ? linea.IndicadorFacturacion : linea.PorcentajeImpuesto == 0 ? 4 : 1;

            // La unidad de medida de la DGII usa una tabla de códigos propia; se omite hasta homologarla.
            items.Add(new ItemEcf(i + 1, indicador, linea.Descripcion, linea.Cantidad, null, precioUnitario, Math.Max(0m, baseBruta - baseNeta), baseNeta,
                linea.EsServicio ? 2 : 1));

            switch (indicador)
            {
                case 1:
                    gravado1 += baseNeta;
                    itbis1 += itbis;
                    break;
                case 2:
                    gravado2 += baseNeta;
                    itbis2 += itbis;
                    break;
                case 3:
                    gravado3 += baseNeta;
                    itbis3 += itbis;
                    break;
                default:
                    exento += baseNeta;
                    break;
            }
        }

        var total = lineas.Sum(l => l.ImporteConImpuesto);
        var comprador = venta.ClienteDocumento is { } documento ? new CompradorEcf(documento, venta.ClienteNombre) : null;

        return new DocumentoEcf(
            (int)venta.TipoComprobante,
            encf,
            venceSecuencia,
            emisor,
            comprador,
            items,
            new TotalesEcf(gravado1, gravado2, gravado3, exento, itbis1, itbis2, itbis3, total,
                Tasa(items, lineas.Select(l => l.PorcentajeImpuesto).ToList(), 1),
                Tasa(items, lineas.Select(l => l.PorcentajeImpuesto).ToList(), 2),
                Tasa(items, lineas.Select(l => l.PorcentajeImpuesto).ToList(), 3),
                // Régimen especial con retención de la Ley 32-23: el comprobante lleva el total y lo que el cliente paga.
                venta.CalcularTotales() is { Retencion: > 0m } conRetencion ? conRetencion.TotalAPagar : null),
            (venta.CobradaEn ?? fechaFirma).ToOffset(fechaFirma.Offset),
            fechaFirma,
            TipoIngresos: tipoIngresos,
            TipoPago: TipoPagoContado,
            FormasPago: FormasPago(venta));
    }

    /// <summary>
    /// Nota de crédito E34: las líneas devueltas sin ITBIS por tasa y la referencia al e-CF modificado (código 1 si la devolución completa la
    /// factura, 3 si corrige montos). Con el ITBIS retenido (fuera de plazo) la nota solo acredita la base, como exenta.
    /// </summary>
    public static DocumentoEcf DesdeNotaCredito(Devolucion devolucion, string encf, DateOnly venceSecuencia, EmisorEcf emisor, DateTimeOffset fechaFirma,
        int tipoIngresos)
    {
        var items = new List<ItemEcf>(devolucion.Lineas.Count);
        decimal gravado1 = 0, gravado2 = 0, gravado3 = 0, exento = 0, itbis1 = 0, itbis2 = 0, itbis3 = 0;

        var lineasNota = devolucion.Lineas.OrderBy(l => l.NumeroLineaOrigen).ToList();
        foreach (var linea in lineasNota)
        {
            var indicador = devolucion.RetieneImpuesto ? 4
                : linea.IndicadorFacturacion is >= 1 and <= 4 ? linea.IndicadorFacturacion
                : linea.PorcentajeImpuesto == 0 ? 4 : 1;
            var precioUnitario = linea.Cantidad == 0 ? 0m : decimal.Round(linea.Base / linea.Cantidad, 4, MidpointRounding.AwayFromZero);
            items.Add(new ItemEcf(items.Count + 1, indicador, linea.Descripcion, linea.Cantidad, null, precioUnitario, 0m, linea.Base, linea.EsServicio ? 2 : 1));

            switch (indicador)
            {
                case 1:
                    gravado1 += linea.Base;
                    itbis1 += linea.Impuesto;
                    break;
                case 2:
                    gravado2 += linea.Base;
                    itbis2 += linea.Impuesto;
                    break;
                case 3:
                    gravado3 += linea.Base;
                    itbis3 += linea.Impuesto;
                    break;
                default:
                    exento += linea.Base;
                    break;
            }
        }

        var fechaFactura = DateOnly.FromDateTime(devolucion.VentaOrigenCobradaEn.ToOffset(fechaFirma.Offset).DateTime);
        return new DocumentoEcf(
            (int)TipoComprobante.NotaCredito,
            encf,
            venceSecuencia,
            emisor,
            new CompradorEcf(devolucion.ClienteDocumento, devolucion.ClienteNombre),
            items,
            new TotalesEcf(gravado1, gravado2, gravado3, exento, itbis1, itbis2, itbis3, devolucion.Total,
                Tasa(items, lineasNota.Select(l => l.PorcentajeImpuesto).ToList(), 1),
                Tasa(items, lineasNota.Select(l => l.PorcentajeImpuesto).ToList(), 2),
                Tasa(items, lineasNota.Select(l => l.PorcentajeImpuesto).ToList(), 3)),
            devolucion.CreadaEn.ToOffset(fechaFirma.Offset),
            fechaFirma,
            TipoIngresos: tipoIngresos,
            TipoPago: TipoPagoContado,
            Referencia: new ReferenciaEcf(devolucion.EncfOrigen ?? devolucion.VentaOrigenNumero, fechaFactura, devolucion.EsTotal ? 1 : 3));
    }

    /// <summary>Tasa de ITBIS del maestro de impuestos para las líneas de un indicador; nula si no hay líneas con ese indicador.</summary>
    /// <param name="porcentajes">Porcentaje de cada línea, en el mismo orden que los ítems.</param>
    private static decimal? Tasa(IReadOnlyList<ItemEcf> items, IReadOnlyList<decimal> porcentajes, int indicador) =>
        items.Select((item, indice) => (item.IndicadorFacturacion, Porcentaje: porcentajes[indice]))
            .Where(par => par.IndicadorFacturacion == indicador)
            .Select(par => (decimal?)par.Porcentaje)
            .FirstOrDefault();

    /// <summary>Pagos agrupados por la tabla de formas de pago de la DGII; la devuelta se descuenta del efectivo.</summary>
    private static List<FormaPagoEcf> FormasPago(Venta venta)
    {
        var porForma = new SortedDictionary<int, decimal>();
        var devueltaPendiente = venta.Devuelta;

        foreach (var pago in venta.Pagos.OrderByDescending(p => p.PermiteDevuelta).ThenBy(p => p.Numero))
        {
            var monto = pago.MontoAplicado;
            if (pago.PermiteDevuelta && devueltaPendiente > 0)
            {
                var descontar = Math.Min(monto, devueltaPendiente);
                monto -= descontar;
                devueltaPendiente -= descontar;
            }

            var codigo = pago.Tipo switch
            {
                TipoFormaPago.Efectivo or TipoFormaPago.MonedaExtranjera => 1,
                TipoFormaPago.Cheque or TipoFormaPago.Transferencia or TipoFormaPago.PrestamoBancario => 2,
                TipoFormaPago.Tarjeta => 3,
                TipoFormaPago.BonoRegalo or TipoFormaPago.TarjetaRegalo => 5,
                TipoFormaPago.NotaCredito => 7,
                _ => 8,
            };
            porForma[codigo] = porForma.GetValueOrDefault(codigo) + monto;
        }

        return porForma.Where(par => par.Value > 0).Select(par => new FormaPagoEcf(par.Key, par.Value)).ToList();
    }

    private static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
}

internal sealed class ServicioEcf(
    ContextoDatosPos contexto,
    ICertificadoCaja certificado,
    IParametros parametros,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioEcf
{
    private static readonly TipoComprobante[] TiposDeVenta =
        [TipoComprobante.FacturaConsumo, TipoComprobante.FacturaCreditoFiscal, TipoComprobante.RegimenesEspeciales, TipoComprobante.Gubernamental];

    public async Task<DatosEstadoEcf> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var hoy = reloj.Ahora().Dia();

        // Sin umbrales configurados no se inventan: el estado informa qué falta configurar y sigue mostrando lo demás.
        var faltantes = new List<string>();
        decimal? umbral = null;
        int? diasAlerta = null;
        // Los dos umbrales conviven y alerta el que se cumpla primero: en un rango de 50,000 el 5 % son 2,500 comprobantes
        // y no alarma a nadie; en uno de 100 son 5, y para entonces ya es tarde.
        var porCantidad = await parametros.ObtenerEnteroOpcionalAsync(ClavesParametros.ComprobantesAlertaSecuenciaEcf, sesion.CajaId, cancelacion);
        try
        {
            umbral = await parametros.ObtenerDecimalAsync(ClavesParametros.PorcentajeAlertaSecuenciaEcf, sesion.CajaId, cancelacion);
        }
        catch (ParametroNoConfiguradoExcepcion excepcion)
        {
            faltantes.Add(excepcion.Message);
        }

        try
        {
            diasAlerta = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasAlertaCertificado, sesion.CajaId, cancelacion);
        }
        catch (ParametroNoConfiguradoExcepcion excepcion)
        {
            faltantes.Add(excepcion.Message);
        }

        var secuencias = await contexto.SecuenciasEcf.AsNoTracking()
            .Where(s => s.CajaId == sesion.CajaId && s.Activa)
            .OrderBy(s => s.TipoComprobante).ThenBy(s => s.Desde)
            .ToListAsync(cancelacion);

        var alertas = new List<string>(faltantes);
        var datos = secuencias.Select(s =>
        {
            var disponible = s.Disponible(hoy);
            var enAlerta = !disponible
                           || (umbral is { } limite && s.PorcentajeRestante <= limite)
                           || (porCantidad is { } restantes && s.Restantes <= restantes);
            return new DatosSecuenciaEcf(s.TipoComprobante, s.Desde, s.Hasta, s.Ultimo, s.Restantes, s.PorcentajeRestante, s.VenceEn, disponible, enAlerta);
        }).ToList();

        foreach (var tipo in TiposDeVenta)
        {
            var delTipo = datos.Where(s => s.TipoComprobante == tipo).ToList();
            var nombre = $"E{(int)tipo} {ReglasComprobante.Nombre(tipo)}";
            if (!delTipo.Any(s => s.Disponible))
                alertas.Add($"No hay secuencia disponible para {nombre}: agotada, vencida o no asignada.");
            else if (delTipo.Where(s => s.Disponible).Sum(s => s.Restantes) is var restantes && delTipo.Where(s => s.Disponible).All(s => s.EnAlerta))
                alertas.Add($"Quedan {restantes:N0} comprobantes de {nombre}. Avise a soporte técnico para que soliciten un nuevo rango.");
        }

        // Resultado que la DGII dio a los e-CF de esta caja, que el Central devuelve en la bajada de maestros (RF-223).
        var resultadosDgii = await contexto.DocumentosElectronicos.AsNoTracking()
            .Where(d => d.CajaId == sesion.CajaId
                && (d.Estado == EstadoDocumentoElectronico.Rechazado || d.Estado == EstadoDocumentoElectronico.Aceptado
                    || d.Estado == EstadoDocumentoElectronico.AceptadoCondicional))
            .GroupBy(d => d.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancelacion);
        var rechazadosDgii = resultadosDgii.Where(r => r.Estado == EstadoDocumentoElectronico.Rechazado).Sum(r => r.Cantidad);
        var aceptadosDgii = resultadosDgii.Where(r => r.Estado != EstadoDocumentoElectronico.Rechazado).Sum(r => r.Cantidad);
        if (rechazadosDgii > 0)
            alertas.Add($"{rechazadosDgii} e-CF rechazados por la DGII. Consulte el detalle con el Central.");

        if (!certificado.Configurado)
            alertas.Add("La caja no tiene certificado digital instalado.");
        else if (!certificado.Cargado)
            alertas.Add("Digite el PIN del certificado digital para poder facturar.");

        int? diasParaVencer = certificado.VenceEn is { } vence ? (int)Math.Floor((vence - reloj.Ahora()).TotalDays) : null;
        if (diasParaVencer is { } dias && diasAlerta is { } diasLimite && dias <= diasLimite)
            alertas.Add(dias < 0 ? "El certificado digital de la caja está vencido." : $"El certificado digital de la caja vence en {dias} días.");

        return new DatosEstadoEcf(certificado.Configurado, certificado.Cargado, certificado.Sujeto, certificado.VenceEn, diasParaVencer, datos, alertas,
            rechazadosDgii, aceptadosDgii);
    }

    public async Task<RespuestaCertificado> CargarCertificadoAsync(SesionUsuario sesion, string pin, CancellationToken cancelacion = default)
    {
        var error = certificado.Cargar(pin);

        // Nunca se audita el PIN; solo quién intentó cargar el certificado y el resultado.
        auditoria.Registrar(new EntradaAuditoria(error is null ? "Ecf.CertificadoCargado" : "Ecf.CertificadoRechazado", "Caja", sesion.CajaCodigo,
            Detalle: new { certificado.Sujeto, certificado.VenceEn, Error = error },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        var mensaje = error ?? "Certificado digital cargado. La caja puede facturar.";

        return new RespuestaCertificado(error is null, mensaje, await ObtenerEstadoAsync(sesion, cancelacion));
    }

    public async Task<IReadOnlyList<DatosDocumentoElectronico>> ListarDocumentosAsync(SesionUsuario sesion, EstadoDocumentoElectronico? estado, int maximo = 100,
        CancellationToken cancelacion = default) =>
        await contexto.DocumentosElectronicos.AsNoTracking()
            .Where(d => d.CajaId == sesion.CajaId && (estado == null || d.Estado == estado))
            .OrderByDescending(d => d.FechaFirma)
            .Take(Math.Clamp(maximo, 1, 500))
            .Select(d => new DatosDocumentoElectronico(d.Id, d.VentaId, d.Encf, d.TipoComprobante, d.MontoTotal, d.FechaFirma, d.Estado, d.EstadoActualizadoEn,
                d.MensajeEstado, d.RutaXml))
            .ToListAsync(cancelacion);
}

public static class ExtensionesEcf
{
    /// <summary>
    /// Solo desarrollo: si no existe el certificado configurado crea uno autofirmado y lo carga con el PIN indicado.
    /// En producción el PIN lo digita un usuario al iniciar la jornada.
    /// </summary>
    public static void PrepararCertificadoDesarrollo(this IServiceProvider servicios, IConfiguration configuracion, string pin)
    {
        var registro = servicios.GetRequiredService<ILoggerFactory>().CreateLogger("CgPos.Ecf");
        if (configuracion[ClavesEcf.RutaCertificado] is not { Length: > 0 } rutaConfigurada)
        {
            registro.LogWarning("No hay ruta de certificado configurada ({Clave})", ClavesEcf.RutaCertificado);
            return;
        }

        var ruta = Path.GetFullPath(rutaConfigurada);
        if (!File.Exists(ruta))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            var ahora = DateTimeOffset.UtcNow;
            File.WriteAllBytes(ruta, CertificadoFirma.CrearAutofirmadoDesarrollo("CN=CAJA DESARROLLO, O=Contreras Group, C=DO", pin, ahora.AddDays(-1), ahora.AddYears(2)));
            registro.LogInformation("Certificado de desarrollo creado en {Ruta}", ruta);
        }

        if (servicios.GetRequiredService<ICertificadoCaja>().Cargar(pin) is { } error)
            registro.LogWarning("No se pudo cargar el certificado de desarrollo: {Error}", error);
    }
}
