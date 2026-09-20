using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;

namespace CgPos.Dominio.Cotizaciones;

public enum EstadoCotizacion
{
    Abierta,

    /// <summary>Una caja la convirtió en factura; no se vuelve a usar.</summary>
    Facturada,

    Anulada,
}

/// <summary>Totales de una cotización, con el ITBIS separado igual que en la factura.</summary>
public sealed record TotalesCotizacion(decimal Subtotal, decimal Impuesto, decimal Descuento, decimal Total);

/// <summary>
/// Presupuesto que se le da a un cliente: se arma en el Central, se imprime o se le envía, vale por unos días y después
/// cualquier caja la convierte en factura. Los precios quedan congelados: si el artículo sube, mientras la cotización esté
/// vigente se respeta lo cotizado, que es lo que el cliente entiende por presupuesto.
/// </summary>
public sealed class Cotizacion : Entidad
{
    public const int LargoMaximoNumero = 20;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoDocumento = 20;
    public const int LargoMaximoContacto = 100;
    public const int LargoMaximoObservacion = 500;
    public const int LargoMaximoUsuario = 150;

    private readonly List<LineaCotizacion> _lineas = [];

    private Cotizacion()
    {
    }

    /// <summary>Número con el que el cliente y la caja la identifican; lo asigna el Central (ej. COT000123).</summary>
    public string Numero { get; private set; } = string.Empty;

    public string ClienteNombre { get; private set; } = string.Empty;
    public string? ClienteDocumento { get; private set; }
    public string? ClienteTelefono { get; private set; }
    public string? ClienteCorreo { get; private set; }

    /// <summary>Sucursal que la atiende; nulo si vale en cualquiera.</summary>
    public int? SucursalId { get; private set; }

    public string? Observacion { get; private set; }

    /// <summary>Último día en que se puede facturar sin autorización.</summary>
    public DateOnly VenceEn { get; private set; }

    public EstadoCotizacion Estado { get; private set; } = EstadoCotizacion.Abierta;

    /// <summary>Factura que la consumió, cuando ya se facturó.</summary>
    public string? VentaNumero { get; private set; }

    public DateTimeOffset? FacturadaEn { get; private set; }
    public string? MotivoAnulacion { get; private set; }
    public string CreadaPor { get; private set; } = string.Empty;
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    public IReadOnlyList<LineaCotizacion> Lineas => _lineas;

    /// <summary>Pasó su fecha y sigue abierta: se puede facturar, pero con autorización de un supervisor.</summary>
    public bool EstaVencida(DateOnly hoy) => Estado == EstadoCotizacion.Abierta && hoy > VenceEn;

    /// <summary>Todavía se puede cambiar: una facturada o anulada queda como está.</summary>
    public bool EsEditable => Estado == EstadoCotizacion.Abierta;

    public static Cotizacion Crear(string numero, string clienteNombre, string? clienteDocumento, string? telefono, string? correo, int? sucursalId,
        DateOnly venceEn, string? observacion, string creadaPor, DateTimeOffset ahora)
    {
        var cotizacion = new Cotizacion
        {
            Numero = Validar.Texto(numero, "Número de la cotización", LargoMaximoNumero).ToUpperInvariant(),
            CreadaPor = Validar.Texto(creadaPor, "Usuario", LargoMaximoUsuario),
            CreadaEn = ahora,
        };

        cotizacion.Actualizar(clienteNombre, clienteDocumento, telefono, correo, sucursalId, venceEn, observacion, ahora);
        return cotizacion;
    }

    /// <exception cref="ArgumentException">La cotización ya no se puede cambiar.</exception>
    public void Actualizar(string clienteNombre, string? clienteDocumento, string? telefono, string? correo, int? sucursalId, DateOnly venceEn,
        string? observacion, DateTimeOffset ahora)
    {
        AsegurarEditable();

        ClienteNombre = Validar.Texto(clienteNombre, "Cliente", LargoMaximoNombre);
        ClienteDocumento = Validar.TextoOpcional(clienteDocumento, "Documento del cliente", LargoMaximoDocumento);
        ClienteTelefono = Validar.TextoOpcional(telefono, "Teléfono", LargoMaximoContacto);
        ClienteCorreo = Validar.TextoOpcional(correo, "Correo", LargoMaximoContacto);
        SucursalId = sucursalId;
        VenceEn = venceEn;
        Observacion = Validar.TextoOpcional(observacion, "Observación", LargoMaximoObservacion);
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Deja la cotización con estas líneas y estos precios. Se reemplazan todas: una cotización es una foto de un momento,
    /// no un documento que se va corrigiendo por partes.
    /// </summary>
    /// <exception cref="ArgumentException">No hay líneas, se repite un artículo o algún importe no es válido.</exception>
    public void ReemplazarLineas(IEnumerable<DatosLineaCotizacion> lineas, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        AsegurarEditable();

        var nuevas = lineas.ToList();
        if (nuevas.Count == 0)
            throw new ArgumentException("La cotización debe tener al menos una línea.", nameof(lineas));

        var codigos = nuevas.Select(l => (l.ArticuloCodigo ?? string.Empty).Trim().ToUpperInvariant()).ToList();
        if (codigos.Any(c => c.Length == 0))
            throw new ArgumentException("Cada línea tiene que decir de qué artículo es.", nameof(lineas));
        if (codigos.GroupBy(c => c, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Un artículo no puede repetirse en la cotización; sume la cantidad en una sola línea.", nameof(lineas));

        _lineas.Clear();
        var numero = 1;
        foreach (var linea in nuevas)
            _lineas.Add(LineaCotizacion.Crear(Id, numero++, linea));

        ActualizadaEn = ahora;
    }

    /// <summary>La cotización no se borra: se anula con su motivo y queda para consulta.</summary>
    public void Anular(string motivo, DateTimeOffset ahora)
    {
        AsegurarEditable();

        MotivoAnulacion = Validar.Texto(motivo, "Motivo", LargoMaximoObservacion);
        Estado = EstadoCotizacion.Anulada;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// La caja la facturó. Llega por sincronización, y los mensajes de la caja pueden repetirse.
    /// </summary>
    /// <returns>Falso si ya estaba facturada con esa misma factura.</returns>
    public bool RegistrarFactura(string ventaNumero, DateTimeOffset facturadaEn, DateTimeOffset ahora)
    {
        var numero = Validar.Texto(ventaNumero, "Número de la factura", LargoMaximoNumero).ToUpperInvariant();
        if (Estado == EstadoCotizacion.Facturada && VentaNumero == numero)
            return false;

        Estado = EstadoCotizacion.Facturada;
        VentaNumero = numero;
        FacturadaEn = facturadaEn;
        ActualizadaEn = ahora;
        return true;
    }

    /// <summary>Los totales, con el ITBIS separado con el mismo redondeo que usa la factura.</summary>
    public TotalesCotizacion Totales() =>
        new(_lineas.Sum(l => l.Base), _lineas.Sum(l => l.Impuesto), _lineas.Sum(l => l.Descuento), _lineas.Sum(l => l.Importe));

    private void AsegurarEditable()
    {
        if (!EsEditable)
            throw new ArgumentException($"La cotización {Numero} está {Estado.ToString().ToLowerInvariant()} y ya no se puede cambiar.", nameof(Estado));
    }
}

/// <summary>Una línea tal como se cotiza: el artículo, su cantidad y el precio con el que se le prometió al cliente.</summary>
/// <param name="PrecioUnitario">Con impuesto incluido, igual que en la caja.</param>
/// <param name="Descuento">Monto descontado de esa línea; 0 si no lleva.</param>
public sealed record DatosLineaCotizacion(
    string ArticuloCodigo,
    string Descripcion,
    string? UnidadMedida,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Descuento,
    decimal PorcentajeImpuesto,
    int IndicadorFacturacion);

public sealed class LineaCotizacion : Entidad
{
    public const int LargoMaximoCodigo = 30;
    public const int LargoMaximoDescripcion = 200;
    public const int LargoMaximoUnidad = 10;

    private LineaCotizacion()
    {
    }

    public int CotizacionId { get; internal set; }
    public int NumeroLinea { get; private set; }
    public string ArticuloCodigo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public string? UnidadMedida { get; private set; }
    public decimal Cantidad { get; private set; }

    /// <summary>Precio congelado al cotizar, con impuesto incluido.</summary>
    public decimal PrecioUnitario { get; private set; }

    public decimal Descuento { get; private set; }
    public decimal PorcentajeImpuesto { get; private set; }
    public int IndicadorFacturacion { get; private set; }

    /// <summary>Cantidad por precio, antes del descuento.</summary>
    public decimal ImporteBruto => decimal.Round(Cantidad * PrecioUnitario, 2, MidpointRounding.AwayFromZero);

    /// <summary>Lo que se cobra por esta línea, con impuesto.</summary>
    public decimal Importe => ImporteBruto - Descuento;

    public decimal Base => CalculoImpuestos.BaseDe(Importe, PorcentajeImpuesto);

    public decimal Impuesto => Importe - Base;

    internal static LineaCotizacion Crear(int cotizacionId, int numeroLinea, DatosLineaCotizacion datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var linea = new LineaCotizacion
        {
            CotizacionId = cotizacionId,
            NumeroLinea = numeroLinea,
            ArticuloCodigo = Validar.Texto(datos.ArticuloCodigo, "Código del artículo", LargoMaximoCodigo).ToUpperInvariant(),
            Descripcion = Validar.Texto(datos.Descripcion, "Descripción", LargoMaximoDescripcion),
            UnidadMedida = Validar.TextoOpcional(datos.UnidadMedida, "Unidad de medida", LargoMaximoUnidad),
            Cantidad = decimal.Round(datos.Cantidad, 3, MidpointRounding.AwayFromZero),
            PrecioUnitario = decimal.Round(datos.PrecioUnitario, 2, MidpointRounding.AwayFromZero),
            Descuento = decimal.Round(datos.Descuento, 2, MidpointRounding.AwayFromZero),
            PorcentajeImpuesto = datos.PorcentajeImpuesto,
            IndicadorFacturacion = datos.IndicadorFacturacion,
        };

        if (linea.Cantidad <= 0)
            throw new ArgumentException($"La cantidad de {linea.Descripcion} debe ser mayor que cero.", nameof(datos));
        if (linea.PrecioUnitario < 0)
            throw new ArgumentException($"El precio de {linea.Descripcion} no puede ser negativo.", nameof(datos));
        if (linea.PorcentajeImpuesto is < 0m or > 100m)
            throw new ArgumentException($"El impuesto de {linea.Descripcion} debe estar entre 0 y 100.", nameof(datos));
        if (linea.Descuento < 0 || linea.Descuento > linea.ImporteBruto)
            throw new ArgumentException($"El descuento de {linea.Descripcion} no puede ser negativo ni pasar del importe de la línea.", nameof(datos));

        return linea;
    }
}
