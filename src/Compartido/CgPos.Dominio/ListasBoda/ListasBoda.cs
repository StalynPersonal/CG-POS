using CgPos.Dominio.Comun;

namespace CgPos.Dominio.ListasBoda;

public enum EstadoListaBoda
{
    Abierta,
    Cerrada,
}

/// <summary>
/// Lista de boda (o de regalos de cualquier evento). La crea el Central con los datos de los festejados, del evento y los artículos
/// que pidieron; las cajas la consultan por su número al vender. Que lo comprado se descuente de las cantidades pedidas es
/// parametrizable en el Central: con el descuento apagado, la lista solo guía al cliente y el historial de compras queda igual.
/// </summary>
public sealed class ListaBoda : Entidad
{
    public const int LargoMaximoNumero = 20;
    public const int LargoMaximoNombre = 150;
    public const int LargoMaximoDocumento = 20;
    public const int LargoMaximoContacto = 100;
    public const int LargoMaximoLugar = 200;
    public const int LargoMaximoObservacion = 500;

    private readonly List<ArticuloListaBoda> _articulos = [];
    private readonly List<CompraListaBoda> _compras = [];

    private ListaBoda()
    {
    }

    /// <summary>Número con el que el cliente y la caja identifican la lista; lo asigna el Central.</summary>
    public string Numero { get; private set; } = string.Empty;

    /// <summary>Nombre del evento tal como se le dice al cliente, ej. "Boda de Ana y Luis".</summary>
    public string Evento { get; private set; } = string.Empty;

    public DateOnly FechaEvento { get; private set; }
    public string? Lugar { get; private set; }

    public string ClienteDocumento { get; private set; } = string.Empty;
    public string ClienteNombre { get; private set; } = string.Empty;
    public string? ClienteTelefono { get; private set; }
    public string? ClienteCorreo { get; private set; }

    /// <summary>Sucursal que atiende la lista; nulo si sirve para todas.</summary>
    public int? SucursalId { get; private set; }

    public string? Observacion { get; private set; }
    public EstadoListaBoda Estado { get; private set; } = EstadoListaBoda.Abierta;
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    public IReadOnlyList<ArticuloListaBoda> Articulos => _articulos;
    public IReadOnlyList<CompraListaBoda> Compras => _compras;

    public static ListaBoda Crear(string numero, string evento, DateOnly fechaEvento, string? lugar, string clienteDocumento, string clienteNombre,
        string? telefono, string? correo, int? sucursalId, string? observacion, DateTimeOffset ahora)
    {
        var lista = new ListaBoda
        {
            Numero = Validar.Texto(numero, "Número de la lista", LargoMaximoNumero).ToUpperInvariant(),
            CreadaEn = ahora,
        };

        lista.Actualizar(evento, fechaEvento, lugar, clienteDocumento, clienteNombre, telefono, correo, sucursalId, observacion, ahora);
        return lista;
    }

    public void Actualizar(string evento, DateOnly fechaEvento, string? lugar, string clienteDocumento, string clienteNombre, string? telefono,
        string? correo, int? sucursalId, string? observacion, DateTimeOffset ahora)
    {
        Evento = Validar.Texto(evento, "Evento", LargoMaximoNombre);
        FechaEvento = fechaEvento;
        Lugar = Validar.TextoOpcional(lugar, "Lugar", LargoMaximoLugar);
        ClienteDocumento = Validar.Texto(clienteDocumento, "Documento del cliente", LargoMaximoDocumento);
        ClienteNombre = Validar.Texto(clienteNombre, "Cliente", LargoMaximoNombre);
        ClienteTelefono = Validar.TextoOpcional(telefono, "Teléfono", LargoMaximoContacto);
        ClienteCorreo = Validar.TextoOpcional(correo, "Correo", LargoMaximoContacto);
        SucursalId = sucursalId;
        Observacion = Validar.TextoOpcional(observacion, "Observación", LargoMaximoObservacion);
        ActualizadaEn = ahora;
    }

    /// <summary>Deja la lista con los artículos indicados; lo ya comprado de un artículo que sigue en la lista se conserva.</summary>
    public void ReemplazarArticulos(IEnumerable<(string Codigo, string Descripcion, decimal Cantidad)> articulos, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(articulos);
        var nuevos = articulos
            .Select(a => (Codigo: (a.Codigo ?? string.Empty).Trim().ToUpperInvariant(), a.Descripcion, a.Cantidad))
            .Where(a => a.Codigo.Length > 0)
            .ToList();

        if (nuevos.Count == 0)
            throw new ArgumentException("La lista debe tener al menos un artículo.", nameof(articulos));
        if (nuevos.GroupBy(a => a.Codigo, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Un artículo no puede repetirse en la lista.", nameof(articulos));

        foreach (var sobrante in _articulos.Where(a => nuevos.All(n => n.Codigo != a.ArticuloCodigo)).ToList())
            _articulos.Remove(sobrante);

        foreach (var (codigo, descripcion, cantidad) in nuevos)
        {
            if (_articulos.FirstOrDefault(a => a.ArticuloCodigo == codigo) is { } existente)
                existente.Cambiar(descripcion, cantidad);
            else
                _articulos.Add(ArticuloListaBoda.Crear(Id, codigo, descripcion, cantidad));
        }

        ActualizadaEn = ahora;
    }

    public void Cerrar(DateTimeOffset ahora)
    {
        Estado = EstadoListaBoda.Cerrada;
        ActualizadaEn = ahora;
    }

    public void Reabrir(DateTimeOffset ahora)
    {
        Estado = EstadoListaBoda.Abierta;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Registra la compra que una caja hizo contra la lista. Si el Central tiene activado el descuento, lo comprado baja de las
    /// cantidades pedidas; si no, la compra queda en el historial y la lista no cambia.
    /// </summary>
    /// <returns>Falso si esa factura ya se había registrado: los mensajes de la caja pueden repetirse.</returns>
    public bool RegistrarCompra(string ventaNumero, int cajaId, decimal monto, IEnumerable<(string Codigo, decimal Cantidad)> lineas,
        bool descontarDeLaLista, DateTimeOffset fecha, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        var numero = Validar.Texto(ventaNumero, "Número de la factura", LargoMaximoNumero).ToUpperInvariant();
        if (_compras.Any(c => c.VentaNumero == numero))
            return false;

        _compras.Add(CompraListaBoda.Crear(Id, numero, cajaId, monto, fecha, ahora));

        if (descontarDeLaLista)
        {
            foreach (var (codigo, cantidad) in lineas)
            {
                if (cantidad <= 0)
                    continue;
                if (_articulos.FirstOrDefault(a => a.ArticuloCodigo == (codigo ?? string.Empty).Trim().ToUpperInvariant()) is { } articulo)
                    articulo.Comprar(cantidad);
            }
        }

        ActualizadaEn = ahora;
        return true;
    }
}

/// <summary>Artículo pedido en la lista, con lo que ya se compró de él.</summary>
public sealed class ArticuloListaBoda : Entidad
{
    public const int LargoMaximoCodigo = 30;

    private ArticuloListaBoda()
    {
    }

    public int ListaBodaId { get; private set; }
    public string ArticuloCodigo { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public decimal Cantidad { get; private set; }
    public decimal Comprado { get; private set; }

    /// <summary>Lo que falta por comprar de este artículo.</summary>
    public decimal Pendiente => Math.Max(0m, Cantidad - Comprado);

    internal static ArticuloListaBoda Crear(int listaBodaId, string codigo, string descripcion, decimal cantidad)
    {
        var articulo = new ArticuloListaBoda
        {
            ListaBodaId = listaBodaId,
            ArticuloCodigo = Validar.Texto(codigo, "Código del artículo", LargoMaximoCodigo).ToUpperInvariant(),
        };

        articulo.Cambiar(descripcion, cantidad);
        return articulo;
    }

    internal void Cambiar(string descripcion, decimal cantidad)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), cantidad, "La cantidad pedida debe ser mayor que cero.");

        Descripcion = Validar.Texto(descripcion, "Descripción del artículo", ListaBoda.LargoMaximoNombre);
        Cantidad = decimal.Round(cantidad, 3, MidpointRounding.AwayFromZero);
    }

    internal void Comprar(decimal cantidad) => Comprado += decimal.Round(cantidad, 3, MidpointRounding.AwayFromZero);
}

/// <summary>Factura que una caja cobró contra la lista; la factura se registra una sola vez aunque el mensaje se repita.</summary>
public sealed class CompraListaBoda : Entidad
{
    private CompraListaBoda()
    {
    }

    public int ListaBodaId { get; private set; }
    public string VentaNumero { get; private set; } = string.Empty;
    public int CajaId { get; private set; }
    public decimal Monto { get; private set; }
    public DateTimeOffset Fecha { get; private set; }
    public DateTimeOffset RegistradaEn { get; private set; }

    internal static CompraListaBoda Crear(int listaBodaId, string ventaNumero, int cajaId, decimal monto, DateTimeOffset fecha, DateTimeOffset ahora) =>
        new()
        {
            ListaBodaId = listaBodaId,
            VentaNumero = ventaNumero,
            CajaId = Validar.Id(cajaId, "Caja"),
            Monto = decimal.Round(monto, 2, MidpointRounding.AwayFromZero),
            Fecha = fecha,
            RegistradaEn = ahora,
        };
}
