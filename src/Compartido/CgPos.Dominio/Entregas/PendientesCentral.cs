using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Entregas;

/// <summary>
/// Copia en el Central de un pendiente de entrega o envío de una caja (RF-249, RF-252). La caja es la autoridad sobre sus pendientes:
/// aquí solo se refleja lo que informa, para verlos todos juntos, seguir los atrasos y despachar desde una sucursal distinta.
/// </summary>
public sealed class PendienteCentral : Entidad
{
    public const int LargoMaximoNumero = 30;
    public const int LargoMaximoTexto = 250;
    public const int LargoMaximoBusqueda = 600;

    private PendienteCentral()
    {
    }

    public string Numero { get; private set; } = string.Empty;
    public string VentaNumero { get; private set; } = string.Empty;
    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public MetodoEntrega Metodo { get; private set; }
    public EstadoPendiente Estado { get; private set; }
    public string? AlmacenNombre { get; private set; }
    public string? Ciudad { get; private set; }
    public string? ClienteDocumento { get; private set; }
    public string? ClienteNombre { get; private set; }
    public string? Telefono { get; private set; }
    public DateOnly? FechaComprometida { get; private set; }

    /// <summary>Unidades del pendiente y cuántas se han entregado, para ver de un vistazo lo que falta.</summary>
    public decimal Unidades { get; private set; }

    public decimal UnidadesEntregadas { get; private set; }

    public DateTimeOffset CreadoEn { get; private set; }

    /// <summary>Última actualización según la caja: un mensaje más viejo no pisa uno más nuevo.</summary>
    public DateTimeOffset ActualizadoEn { get; private set; }

    public DateTimeOffset RecibidoEn { get; private set; }

    /// <summary>Cuándo se le avisó al cliente que su pedido está listo; nulo si todavía no se le avisó (RF-256).</summary>
    public DateTimeOffset? AvisoEnviadoEn { get; private set; }

    /// <summary>Documento completo tal como lo envió la caja (líneas y entregas), para el detalle.</summary>
    public string Contenido { get; private set; } = string.Empty;

    /// <summary>Número, factura, cliente y destino en minúsculas y sin acentos, para buscar.</summary>
    public string TextoBusqueda { get; private set; } = string.Empty;

    public bool EstaAbierto => Estado is not (EstadoPendiente.Entregado or EstadoPendiente.Anulado);

    /// <summary>Se pasó de la fecha comprometida y todavía no se entregó (RF-252).</summary>
    public bool EstaAtrasado(DateOnly hoy) => EstaAbierto && FechaComprometida is { } fecha && fecha < hoy;

    public static PendienteCentral Registrar(DatosPendienteCentral datos, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var pendiente = new PendienteCentral
        {
            Numero = Validar.Texto(datos.Numero, "Número del pendiente", LargoMaximoNumero),
            VentaNumero = Validar.TextoOpcional(datos.VentaNumero, "Número de la venta", LargoMaximoNumero) ?? string.Empty,
            SucursalId = Validar.Id(datos.SucursalId, "Sucursal"),
            CajaId = Validar.Id(datos.CajaId, "Caja"),
            CreadoEn = datos.CreadoEn,
        };

        pendiente.Actualizar(datos, ahora);
        return pendiente;
    }

    /// <returns><c>false</c> si el mensaje es más viejo que lo que ya se tenía: no se pisa el estado más reciente.</returns>
    public bool Actualizar(DatosPendienteCentral datos, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (ActualizadoEn > datos.ActualizadoEn)
            return false;

        Metodo = datos.Metodo;
        Estado = datos.Estado;
        AlmacenNombre = Validar.TextoOpcional(datos.AlmacenNombre, "Almacén", LargoMaximoTexto);
        Ciudad = Validar.TextoOpcional(datos.Ciudad, "Ciudad", LargoMaximoTexto);
        ClienteDocumento = Validar.TextoOpcional(datos.ClienteDocumento, "Documento del cliente", LargoMaximoTexto);
        ClienteNombre = Validar.TextoOpcional(datos.ClienteNombre, "Nombre del cliente", LargoMaximoTexto);
        Telefono = Validar.TextoOpcional(datos.Telefono, "Teléfono", LargoMaximoTexto);
        FechaComprometida = datos.FechaComprometida;
        Unidades = datos.Unidades;
        UnidadesEntregadas = datos.UnidadesEntregadas;
        ActualizadoEn = datos.ActualizadoEn;
        RecibidoEn = ahora;
        Contenido = Validar.Texto(datos.Contenido, "Contenido del pendiente", int.MaxValue);
        TextoBusqueda = Texto(datos);
        return true;
    }

    /// <summary>El pedido está listo para que el cliente lo retire y todavía no se le ha avisado.</summary>
    public bool EsperaAviso => AvisoEnviadoEn is null && Estado == EstadoPendiente.Preparado;

    public void MarcarAvisado(DateTimeOffset ahora) => AvisoEnviadoEn = ahora;

    private static string Texto(DatosPendienteCentral datos)
    {
        var texto = string.Join(' ', new[] { datos.Numero, datos.VentaNumero, datos.ClienteNombre, datos.ClienteDocumento, datos.Telefono, datos.AlmacenNombre, datos.Ciudad }
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        var normalizado = Comun.TextoBusqueda.Normalizar(texto) ?? string.Empty;
        return normalizado.Length > LargoMaximoBusqueda ? normalizado[..LargoMaximoBusqueda] : normalizado;
    }
}

/// <summary>Lo que el Central guarda de un pendiente informado por una caja.</summary>
public sealed record DatosPendienteCentral(
    string Numero,
    string VentaNumero,
    int SucursalId,
    int CajaId,
    MetodoEntrega Metodo,
    EstadoPendiente Estado,
    string? AlmacenNombre,
    string? Ciudad,
    string? ClienteDocumento,
    string? ClienteNombre,
    string? Telefono,
    DateOnly? FechaComprometida,
    decimal Unidades,
    decimal UnidadesEntregadas,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string Contenido);
