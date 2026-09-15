using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Fiscal;
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

namespace CgPos.Pos.Infraestructura.Ecf;

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

internal sealed record EmisionEcf(DocumentoElectronico Documento, DocumentoElectronicoParaCentral ParaCentral, DateOnly VenceSecuencia);

internal sealed class EmisionEcfExcepcion(CodigoResultadoVenta codigo, string mensaje) : Exception(mensaje)
{
    public CodigoResultadoVenta Codigo { get; } = codigo;
}

/// <summary>
/// Emite el e-CF de una venta cobrada dentro de la transacción del cobro (RF-218): toma la siguiente secuencia de la caja,
/// arma y valida el documento, lo firma con el certificado en memoria, obtiene el código de seguridad y el timbre, y deja el
/// XML firmado en la carpeta de pendientes (RF-219). Si la transacción no se confirma, la secuencia vuelve atrás.
/// </summary>
internal sealed class EmisionComprobantes(ContextoDatosPos contexto, ICertificadoCaja certificado, IConfiguration configuracion, TimeProvider reloj)
{
    private readonly FirmadorEcf _firmador = new();

    /// <remarks>Debe llamarse con una transacción abierta en el contexto.</remarks>
    public async Task<EmisionEcf> EmitirAsync(Venta venta, CancellationToken cancelacion)
    {
        var certificadoFirma = certificado.ObtenerParaFirmar()
            ?? throw new EmisionEcfExcepcion(CodigoResultadoVenta.CertificadoNoCargado,
                "El certificado digital de la caja no está cargado. Digite su PIN para poder facturar.");

        var ahora = reloj.GetUtcNow();
        if (certificado.VenceEn is { } vence && vence < ahora)
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.CertificadoNoCargado, "El certificado digital de la caja está vencido. Solicite uno nuevo.");

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var asignada = await SiguienteSecuenciaAsync(venta.CajaId, venta.TipoComprobante, hoy, cancelacion)
            ?? throw new EmisionEcfExcepcion(CodigoResultadoVenta.ComprobanteNoDisponible,
                $"No hay secuencia de e-CF disponible para {ReglasComprobante.Nombre(venta.TipoComprobante)} (E{(int)venta.TipoComprobante}) en esta caja: " +
                "está agotada, vencida o no asignada. Solicite un rango al Central o aplique el procedimiento de contingencia.");

        var encf = SecuenciaEcf.FormatearEncf(venta.TipoComprobante, asignada.Ultimo);
        var emisor = await EmisorAsync(venta.SucursalId, cancelacion);
        var documentoEcf = ConversionEcf.DesdeVenta(venta, encf, asignada.VenceEn, emisor, ahora);

        var xml = GeneradorXmlEcf.Generar(documentoEcf);
        var errores = ValidadorEcf.Validar(documentoEcf).Concat(ValidadorEcf.ValidarContraXsd(xml, configuracion[ClavesEcf.CarpetaXsd])).ToList();
        if (errores.Count > 0)
            throw new EmisionEcfExcepcion(CodigoResultadoVenta.EcfInvalido, $"El e-CF no pasó la validación: {string.Join(" ", errores.Take(3))}");

        var firmado = _firmador.Firmar(xml, certificadoFirma);
        var codigoSeguridad = CodigoSeguridadEcf.Obtener(firmado);
        var urlTimbre = TimbreEcf.Url(Ambiente(), documentoEcf, codigoSeguridad);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(firmado)));

        var ruta = RutaXml("Pendientes", reloj.GetLocalNow(), emisor.Rnc, encf);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        await File.WriteAllTextAsync(ruta, firmado, new UTF8Encoding(false), cancelacion);

        var documento = DocumentoElectronico.Emitir(venta.Id, venta.CajaId, venta.TipoComprobante, encf, venta.CobradaEn ?? ahora, documentoEcf.FechaHoraFirma,
            codigoSeguridad, documentoEcf.Totales.MontoTotal, hash, ruta, urlTimbre);
        contexto.DocumentosElectronicos.Add(documento);

        return new EmisionEcf(documento, new DocumentoElectronicoParaCentral(encf, venta.TipoComprobante, firmado, hash, documentoEcf.FechaHoraFirma), asignada.VenceEn);
    }

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

    private AmbienteEcf Ambiente() =>
        Enum.TryParse<AmbienteEcf>(configuracion[ClavesEcf.Ambiente], ignoreCase: true, out var ambiente) ? ambiente : AmbienteEcf.Pruebas;

    private string RutaXml(string estado, DateTimeOffset fecha, string rnc, string encf)
    {
        var baseXml = configuracion[ClavesEcf.CarpetaXml] is { Length: > 0 } carpeta ? carpeta : ClavesEcf.CarpetaXmlPredeterminada;
        return Path.GetFullPath(Path.Combine(baseXml, estado, fecha.ToString("yyyy"), fecha.ToString("MM"), fecha.ToString("dd"), $"{rnc}{encf}.xml"));
    }

    private sealed class SecuenciaAsignada
    {
        public Guid Id { get; set; }
        public long Ultimo { get; set; }
        public DateOnly VenceEn { get; set; }
    }

    /// <summary>Toma la siguiente secuencia disponible de forma atómica (bloqueo de fila) dentro de la transacción abierta.</summary>
    private async Task<SecuenciaAsignada?> SiguienteSecuenciaAsync(Guid cajaId, TipoComprobante tipo, DateOnly hoy, CancellationToken cancelacion)
    {
        var fecha = hoy.ToDateTime(TimeOnly.MinValue);
        var resultado = await contexto.Database.SqlQuery<SecuenciaAsignada>($"""
            WITH Candidata AS (
                SELECT TOP (1) * FROM SecuenciasEcf WITH (UPDLOCK, ROWLOCK)
                WHERE CajaId = {cajaId} AND TipoComprobante = {(int)tipo} AND Activa = 1 AND Ultimo < Hasta AND VenceEn >= {fecha}
                ORDER BY Desde)
            UPDATE Candidata SET Ultimo = Ultimo + 1
            OUTPUT inserted.Id AS Id, inserted.Ultimo AS Ultimo, inserted.VenceEn AS VenceEn;
            """).ToListAsync(cancelacion);

        return resultado.SingleOrDefault();
    }

    private async Task<EmisorEcf> EmisorAsync(Guid sucursalId, CancellationToken cancelacion)
    {
        var datos = await (
                from sucursal in contexto.Sucursales
                join empresa in contexto.Empresas on sucursal.EmpresaId equals empresa.Id
                where sucursal.Id == sucursalId
                select new { empresa.Rnc, empresa.RazonSocial, empresa.NombreComercial, EmpresaDireccion = empresa.Direccion, Sucursal = sucursal.Nombre, SucursalDireccion = sucursal.Direccion })
            .AsNoTracking()
            .SingleAsync(cancelacion);

        return new EmisorEcf(datos.Rnc, datos.RazonSocial, datos.NombreComercial, datos.Sucursal,
            datos.EmpresaDireccion ?? datos.SucursalDireccion ?? "Dirección no registrada");
    }
}

/// <summary>Convierte la venta cobrada al documento e-CF: montos sin ITBIS por tasa, descuentos por línea y formas de pago.</summary>
internal static class ConversionEcf
{
    public static DocumentoEcf DesdeVenta(Venta venta, string encf, DateOnly venceSecuencia, EmisorEcf emisor, DateTimeOffset fechaFirma)
    {
        var lineas = venta.Lineas.Where(l => l.EstaActiva).OrderBy(l => l.NumeroLinea).ToList();
        var items = new List<ItemEcf>(lineas.Count);
        decimal gravado1 = 0, gravado2 = 0, gravado3 = 0, exento = 0, itbis1 = 0, itbis2 = 0, itbis3 = 0;

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            var factor = 1m + linea.PorcentajeImpuesto / 100m;
            var neto = linea.ImporteConImpuesto;
            var baseNeta = Redondear(neto / factor);
            var baseBruta = Redondear(linea.ImporteBruto / factor);
            var itbis = neto - baseNeta;
            var precioUnitario = linea.Cantidad == 0 ? 0m : decimal.Round(linea.ImporteBruto / linea.Cantidad / factor, 4, MidpointRounding.AwayFromZero);
            var indicador = linea.IndicadorFacturacion is >= 1 and <= 4 ? linea.IndicadorFacturacion : linea.PorcentajeImpuesto == 0 ? 4 : 1;

            // La unidad de medida de la DGII usa una tabla de códigos propia; se omite hasta homologarla.
            items.Add(new ItemEcf(i + 1, indicador, linea.Descripcion, linea.Cantidad, null, precioUnitario, Math.Max(0m, baseBruta - baseNeta), baseNeta));

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
            new TotalesEcf(gravado1, gravado2, gravado3, exento, itbis1, itbis2, itbis3, total),
            venta.CobradaEn ?? fechaFirma,
            fechaFirma,
            FormasPago: FormasPago(venta));
    }

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

internal sealed class ServicioEcf(ContextoDatosPos contexto, ICertificadoCaja certificado, IParametros parametros, IAuditoria auditoria, TimeProvider reloj) : IServicioEcf
{
    private static readonly TipoComprobante[] TiposDeVenta =
        [TipoComprobante.FacturaConsumo, TipoComprobante.FacturaCreditoFiscal, TipoComprobante.RegimenesEspeciales, TipoComprobante.Gubernamental];

    public async Task<DatosEstadoEcf> ObtenerEstadoAsync(SesionUsuario sesion, CancellationToken cancelacion = default)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var umbral = await parametros.ObtenerDecimalAsync(ClavesParametros.PorcentajeAlertaSecuenciaEcf, sesion.CajaId, 10m, cancelacion);
        var diasAlerta = await parametros.ObtenerEnteroAsync(ClavesParametros.DiasAlertaCertificado, sesion.CajaId, 30, cancelacion);

        var secuencias = await contexto.SecuenciasEcf.AsNoTracking()
            .Where(s => s.CajaId == sesion.CajaId && s.Activa)
            .OrderBy(s => s.TipoComprobante).ThenBy(s => s.Desde)
            .ToListAsync(cancelacion);

        var alertas = new List<string>();
        var datos = secuencias.Select(s =>
        {
            var disponible = s.Disponible(hoy);
            var enAlerta = !disponible || s.PorcentajeRestante <= umbral;
            return new DatosSecuenciaEcf(s.TipoComprobante, s.Desde, s.Hasta, s.Ultimo, s.Restantes, s.PorcentajeRestante, s.VenceEn, disponible, enAlerta);
        }).ToList();

        foreach (var tipo in TiposDeVenta)
        {
            var delTipo = datos.Where(s => s.TipoComprobante == tipo).ToList();
            var nombre = $"E{(int)tipo} {ReglasComprobante.Nombre(tipo)}";
            if (!delTipo.Any(s => s.Disponible))
                alertas.Add($"No hay secuencia disponible para {nombre}: agotada, vencida o no asignada.");
            else if (delTipo.Where(s => s.Disponible).Sum(s => s.Restantes) is var restantes && delTipo.Where(s => s.Disponible).All(s => s.EnAlerta))
                alertas.Add($"Quedan {restantes:N0} comprobantes de {nombre}. Solicite un nuevo rango al Central.");
        }

        if (!certificado.Configurado)
            alertas.Add("La caja no tiene certificado digital instalado.");
        else if (!certificado.Cargado)
            alertas.Add("Digite el PIN del certificado digital para poder facturar.");

        int? diasParaVencer = certificado.VenceEn is { } vence ? (int)Math.Floor((vence - reloj.GetUtcNow()).TotalDays) : null;
        if (diasParaVencer is { } dias && dias <= diasAlerta)
            alertas.Add(dias < 0 ? "El certificado digital de la caja está vencido." : $"El certificado digital de la caja vence en {dias} días.");

        return new DatosEstadoEcf(certificado.Configurado, certificado.Cargado, certificado.Sujeto, certificado.VenceEn, diasParaVencer, datos, alertas);
    }

    public async Task<RespuestaCertificado> CargarCertificadoAsync(SesionUsuario sesion, string pin, CancellationToken cancelacion = default)
    {
        var error = certificado.Cargar(pin);

        // Nunca se audita el PIN; solo quién intentó cargar el certificado y el resultado.
        auditoria.Registrar(new EntradaAuditoria(error is null ? "Ecf.CertificadoCargado" : "Ecf.CertificadoRechazado", "Caja", sesion.CajaCodigo,
            Detalle: new { certificado.Sujeto, certificado.VenceEn, Error = error },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);

        return new RespuestaCertificado(error is null, error ?? "Certificado digital cargado. La caja puede facturar.", await ObtenerEstadoAsync(sesion, cancelacion));
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
