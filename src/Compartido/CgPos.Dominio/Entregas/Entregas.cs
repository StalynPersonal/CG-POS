using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Entregas;

/// <summary>Almacén o sucursal donde el cliente retira mercancía pendiente (RF-140). Lo define el Central.</summary>
public sealed class Almacen : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 100;
    public const int LargoMaximoDireccion = 250;

    private Almacen()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public int SucursalId { get; private set; }
    public string? Direccion { get; private set; }
    public bool Activo { get; private set; } = true;

    public static Almacen Crear(string codigo, string nombre, int sucursalId, string? direccion)
    {
        var almacen = new Almacen
        {
            Codigo = Validar.Texto(codigo, "Código del almacén", LargoMaximoCodigo).ToUpperInvariant(),
        };
        almacen.Actualizar(nombre, sucursalId, direccion);
        return almacen;
    }

    public void Actualizar(string nombre, int sucursalId, string? direccion)
    {
        Nombre = Validar.Texto(nombre, "Nombre del almacén", LargoMaximoNombre);
        SucursalId = Validar.Id(sucursalId, "Sucursal");
        Direccion = Validar.TextoOpcional(direccion, "Dirección", LargoMaximoDireccion);
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

/// <summary>Cómo recibe el cliente lo que no se lleva en caja (RF-246). El despacho en caja no genera pendiente.</summary>
public enum MetodoEntrega
{
    RetiroAlmacen,
    Envio,
}

/// <summary>Datos del envío a dirección (RF-250).</summary>
public sealed record DatosEnvio(string Direccion, string? Sector, string? Ciudad, string? Referencia, string Telefono, string? Transportista, decimal? CostoEnvio);

/// <summary>Cantidad de una línea de la venta que va a un destino de entrega (RF-247).</summary>
public sealed record CantidadEntrega(int NumeroLinea, decimal Cantidad);

/// <summary>
/// Destino de entrega marcado en la venta en curso: retiro en un almacén o envío a dirección, con las líneas y cantidades que
/// incluye (RF-246 a RF-248). Al cobrar se convierte en un <see cref="PendienteEntrega"/>.
/// </summary>
public sealed class DestinoEntrega : Entidad
{
    public const int LargoMaximoTexto = 100;
    public const int LargoMaximoDireccion = 250;
    public const int LargoMaximoTelefono = 20;
    public const int LargoMaximoComentario = 250;
    public const int LargoMaximoNombre = 150;

    private readonly List<LineaDestinoEntrega> _lineas = [];

    private DestinoEntrega()
    {
    }

    public int VentaId { get; private set; }
    public int Numero { get; private set; }
    public MetodoEntrega Metodo { get; private set; }
    public int? AlmacenId { get; private set; }
    public string? AlmacenNombre { get; private set; }
    public string? Direccion { get; private set; }
    public string? Sector { get; private set; }
    public string? Ciudad { get; private set; }
    public string? Referencia { get; private set; }
    public string? Telefono { get; private set; }
    public string? Transportista { get; private set; }
    public decimal? CostoEnvio { get; private set; }
    public DateOnly? FechaComprometida { get; private set; }
    public string? Comentario { get; private set; }

    /// <summary>Supervisor que autorizó marcar la mercancía como pendiente (RF-53, RN-15).</summary>
    public int? AutorizadoPorId { get; private set; }

    public string? AutorizadoPorNombre { get; private set; }

    public IReadOnlyList<LineaDestinoEntrega> Lineas => _lineas;

    internal static DestinoEntrega Crear(int ventaId, int numero, MetodoEntrega metodo, int? almacenId, string? almacenNombre, DatosEnvio? envio,
        DateOnly? fechaComprometida, string? comentario, int? autorizadoPorId, string? autorizadoPorNombre, IEnumerable<CantidadEntrega> cantidades)
    {
        var destino = new DestinoEntrega
        {
            VentaId = ventaId,
            Numero = numero,
            Metodo = metodo,
            FechaComprometida = fechaComprometida,
            Comentario = Validar.TextoOpcional(comentario, "Comentario", LargoMaximoComentario),
            AutorizadoPorId = autorizadoPorId,
            AutorizadoPorNombre = Validar.TextoOpcional(autorizadoPorNombre, "Autorizado por", LargoMaximoNombre),
        };

        if (metodo == MetodoEntrega.RetiroAlmacen)
        {
            destino.AlmacenId = almacenId;
            destino.AlmacenNombre = Validar.Texto(almacenNombre, "Almacén", Almacen.LargoMaximoNombre);
        }
        else if (envio is not null)
        {
            destino.Direccion = Validar.Texto(envio.Direccion, "Dirección del envío", LargoMaximoDireccion);
            destino.Sector = Validar.TextoOpcional(envio.Sector, "Sector", LargoMaximoTexto);
            destino.Ciudad = Validar.TextoOpcional(envio.Ciudad, "Ciudad o provincia", LargoMaximoTexto);
            destino.Referencia = Validar.TextoOpcional(envio.Referencia, "Referencia", LargoMaximoDireccion);
            destino.Telefono = Validar.Texto(envio.Telefono, "Teléfono de contacto", LargoMaximoTelefono);
            destino.Transportista = Validar.TextoOpcional(envio.Transportista, "Transportista", LargoMaximoTexto);
            destino.CostoEnvio = envio.CostoEnvio is { } costo ? decimal.Round(costo, 2, MidpointRounding.AwayFromZero) : null;
        }

        foreach (var cantidad in cantidades)
            destino._lineas.Add(new LineaDestinoEntrega { DestinoEntregaId = destino.Id, NumeroLinea = cantidad.NumeroLinea, Cantidad = cantidad.Cantidad });

        return destino;
    }
}

public sealed class LineaDestinoEntrega : Entidad
{
    internal LineaDestinoEntrega()
    {
    }

    public int DestinoEntregaId { get; internal set; }
    public int NumeroLinea { get; internal set; }
    public decimal Cantidad { get; internal set; }
}

/// <summary>Estados del pendiente de entrega (RF-252).</summary>
public enum EstadoPendiente
{
    Pendiente,
    EnPreparacion,
    Preparado,

    /// <summary>El envío salió con el transportista.</summary>
    Despachado,

    /// <summary>Se entregó una parte; el resto sigue pendiente (RF-253).</summary>
    Parcial,

    Entregado,
    Anulado,
}

public enum CodigoErrorPendiente
{
    EstadoInvalido,
    CantidadInvalida,
    SerialRequerido,
    RecibeRequerido,
    MotivoRequerido,
    YaEntregado,
}

public sealed class ReglaPendienteExcepcion(CodigoErrorPendiente codigo, string mensaje) : Exception(mensaje)
{
    public CodigoErrorPendiente Codigo { get; } = codigo;
}

/// <summary>Cantidad que se entrega de una línea del pendiente; el serial se captura en el despacho para los serializados (RF-54, RN-16).</summary>
public sealed record CantidadEntregada(int NumeroLineaVenta, decimal Cantidad, string? Serial = null);

/// <summary>
/// Documento de pendiente de entrega o envío de una factura cobrada (RF-249): artículos, destino, fecha comprometida y cliente. Pasa por
/// preparación y despacho, admite entregas parciales con quien retira (RF-253, RF-254) y se anula con motivo si nada se entregó (RF-255).
/// </summary>
public sealed class PendienteEntrega : Entidad
{
    public const int LargoMaximoNumero = 30;
    public const int LargoMaximoMotivo = 250;

    private readonly List<LineaPendienteEntrega> _lineas = [];
    private readonly List<EntregaPendiente> _entregas = [];

    private PendienteEntrega()
    {
    }

    public string Numero { get; private set; } = string.Empty;
    public int VentaId { get; private set; }
    public string VentaNumero { get; private set; } = string.Empty;
    public int SucursalId { get; private set; }
    public int CajaId { get; private set; }
    public MetodoEntrega Metodo { get; private set; }
    public int? AlmacenId { get; private set; }
    public string? AlmacenNombre { get; private set; }
    public string? Direccion { get; private set; }
    public string? Sector { get; private set; }
    public string? Ciudad { get; private set; }
    public string? Referencia { get; private set; }
    public string? Telefono { get; private set; }
    public string? Transportista { get; private set; }
    public decimal? CostoEnvio { get; private set; }
    public DateOnly? FechaComprometida { get; private set; }
    public string? Comentario { get; private set; }
    public string? ClienteDocumento { get; private set; }
    public string? ClienteNombre { get; private set; }
    public string VendidoPorNombre { get; private set; } = string.Empty;
    public string? AutorizadoPorNombre { get; private set; }
    public EstadoPendiente Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }
    public string ActualizadoPorNombre { get; private set; } = string.Empty;
    public string? MotivoAnulacion { get; private set; }

    /// <summary>Cuándo se le avisó al cliente que su pedido está listo; nulo si todavía no se le avisó (RF-256).</summary>
    public DateTimeOffset? AvisoEnviadoEn { get; private set; }

    public IReadOnlyList<LineaPendienteEntrega> Lineas => _lineas;
    public IReadOnlyList<EntregaPendiente> Entregas => _entregas;

    /// <summary>El pedido está listo para que el cliente lo retire y todavía no se le ha avisado.</summary>
    public bool EsperaAviso => AvisoEnviadoEn is null && Estado == EstadoPendiente.Preparado;

    public void MarcarAvisado(DateTimeOffset ahora) => AvisoEnviadoEn = ahora;

    public bool EstaAbierto => Estado is not (EstadoPendiente.Entregado or EstadoPendiente.Anulado);

    public static PendienteEntrega Crear(Venta venta, DestinoEntrega destino, string numero, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(venta);
        ArgumentNullException.ThrowIfNull(destino);
        if (venta.Estado != EstadoVenta.Cobrada)
            throw new InvalidOperationException("El pendiente de entrega se genera al cobrar la factura.");

        var pendiente = new PendienteEntrega
        {
            Numero = Validar.Texto(numero, "Número del pendiente", LargoMaximoNumero),
            VentaId = venta.Id,
            VentaNumero = venta.NumeroTransaccion,
            SucursalId = venta.SucursalId,
            CajaId = venta.CajaId,
            Metodo = destino.Metodo,
            AlmacenId = destino.AlmacenId,
            AlmacenNombre = destino.AlmacenNombre,
            Direccion = destino.Direccion,
            Sector = destino.Sector,
            Ciudad = destino.Ciudad,
            Referencia = destino.Referencia,
            Telefono = destino.Telefono,
            Transportista = destino.Transportista,
            CostoEnvio = destino.CostoEnvio,
            FechaComprometida = destino.FechaComprometida,
            Comentario = destino.Comentario,
            ClienteDocumento = venta.ClienteDocumento,
            ClienteNombre = venta.ClienteNombre,
            VendidoPorNombre = venta.CobradaPorNombre ?? venta.UsuarioNombre,
            AutorizadoPorNombre = destino.AutorizadoPorNombre,
            Estado = EstadoPendiente.Pendiente,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
            ActualizadoPorNombre = venta.CobradaPorNombre ?? venta.UsuarioNombre,
        };

        foreach (var cantidad in destino.Lineas.OrderBy(l => l.NumeroLinea))
        {
            var linea = venta.Lineas.Single(l => l.NumeroLinea == cantidad.NumeroLinea && l.EstaActiva);
            pendiente._lineas.Add(LineaPendienteEntrega.Crear(pendiente.Id, linea, cantidad.Cantidad));
        }

        return pendiente;
    }

    /// <summary>
    /// Reconstruye en el Central el pendiente que informó una caja, con el estado y las entregas que ya tuviera. A partir de aquí
    /// quien lo despacha es el Central, que aplica las mismas reglas de este agregado: la caja solo lo creó al cobrar.
    /// </summary>
    /// <param name="almacenId">Almacén del Central que corresponde al código informado; nulo si el pendiente es un envío.</param>
    public static PendienteEntrega Reconstruir(DatosPendienteReconstruido datos, int sucursalId, int cajaId, int? almacenId)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var pendiente = new PendienteEntrega
        {
            Numero = Validar.Texto(datos.Numero, "Número del pendiente", LargoMaximoNumero),
            VentaNumero = Validar.TextoOpcional(datos.VentaNumero, "Número de la venta", LargoMaximoNumero) ?? string.Empty,
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            CajaId = Validar.Id(cajaId, "Caja"),
            Metodo = datos.Metodo,
            AlmacenId = almacenId,
            AlmacenNombre = Validar.TextoOpcional(datos.AlmacenNombre, "Almacén", Almacen.LargoMaximoNombre),
            Direccion = Validar.TextoOpcional(datos.Direccion, "Dirección del envío", DestinoEntrega.LargoMaximoDireccion),
            Sector = Validar.TextoOpcional(datos.Sector, "Sector", DestinoEntrega.LargoMaximoTexto),
            Ciudad = Validar.TextoOpcional(datos.Ciudad, "Ciudad o provincia", DestinoEntrega.LargoMaximoTexto),
            Referencia = Validar.TextoOpcional(datos.Referencia, "Referencia", DestinoEntrega.LargoMaximoDireccion),
            Telefono = Validar.TextoOpcional(datos.Telefono, "Teléfono de contacto", DestinoEntrega.LargoMaximoTelefono),
            Transportista = Validar.TextoOpcional(datos.Transportista, "Transportista", DestinoEntrega.LargoMaximoTexto),
            CostoEnvio = datos.CostoEnvio,
            FechaComprometida = datos.FechaComprometida,
            Comentario = Validar.TextoOpcional(datos.Comentario, "Comentario", DestinoEntrega.LargoMaximoComentario),
            ClienteDocumento = Validar.TextoOpcional(datos.ClienteDocumento, "Documento del cliente", DestinoEntrega.LargoMaximoTexto),
            ClienteNombre = Validar.TextoOpcional(datos.ClienteNombre, "Nombre del cliente", DestinoEntrega.LargoMaximoNombre),
            VendidoPorNombre = Validar.TextoOpcional(datos.VendidoPorNombre, "Vendedor", DestinoEntrega.LargoMaximoNombre) ?? string.Empty,
            AutorizadoPorNombre = Validar.TextoOpcional(datos.AutorizadoPorNombre, "Autorizado por", DestinoEntrega.LargoMaximoNombre),
            Estado = datos.Estado,
            CreadoEn = datos.CreadoEn,
            ActualizadoEn = datos.ActualizadoEn,
            ActualizadoPorNombre = Validar.TextoOpcional(datos.ActualizadoPorNombre, "Usuario", DestinoEntrega.LargoMaximoNombre) ?? string.Empty,
            MotivoAnulacion = Validar.TextoOpcional(datos.MotivoAnulacion, "Motivo", LargoMaximoMotivo),
        };

        foreach (var linea in datos.Lineas.OrderBy(l => l.NumeroLineaVenta))
            pendiente._lineas.Add(LineaPendienteEntrega.Reconstruir(pendiente.Id, linea));

        foreach (var entrega in datos.Entregas.OrderBy(e => e.Numero))
            pendiente._entregas.Add(EntregaPendiente.Reconstruir(pendiente.Id, entrega, pendiente._lineas));

        return pendiente;
    }

    /// <summary>Unidades del pendiente y cuántas se han entregado, para verlo de un vistazo en el listado.</summary>
    public decimal Unidades => _lineas.Sum(l => l.Cantidad);

    public decimal UnidadesEntregadas => _lineas.Sum(l => l.CantidadEntregada);

    /// <summary>Se pasó de la fecha comprometida y todavía no se entregó (RF-252).</summary>
    public bool EstaAtrasado(DateOnly hoy) => EstaAbierto && FechaComprometida is { } fecha && fecha < hoy;

    /// <summary>Cantidad de la línea de la factura que aún no se entrega (0 si el pendiente se anuló).</summary>
    public decimal CantidadPorEntregar(int numeroLineaVenta) =>
        Estado == EstadoPendiente.Anulado ? 0m : _lineas.Where(l => l.NumeroLineaVenta == numeroLineaVenta).Sum(l => l.CantidadPendiente);

    /// <summary>Avanza la preparación: en preparación, preparado y, para envíos, despachado con el transportista (RF-252).</summary>
    public void CambiarEstado(EstadoPendiente nuevo, string usuarioNombre, DateTimeOffset ahora)
    {
        var permitido = (Estado, nuevo) switch
        {
            (EstadoPendiente.Pendiente or EstadoPendiente.Parcial, EstadoPendiente.EnPreparacion) => true,
            (EstadoPendiente.Pendiente or EstadoPendiente.EnPreparacion or EstadoPendiente.Parcial, EstadoPendiente.Preparado) => true,
            (EstadoPendiente.Preparado or EstadoPendiente.Parcial, EstadoPendiente.Despachado) => Metodo == MetodoEntrega.Envio,
            _ => false,
        };
        if (!permitido)
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.EstadoInvalido, $"El pendiente {Numero} no puede pasar de {Estado} a {nuevo}.");

        Estado = nuevo;
        Actualizar(usuarioNombre, ahora);
    }

    /// <summary>
    /// Registra la entrega de todo o parte de lo pendiente (RF-253) con quien retira o recibe (RF-254). Los serializados exigen su serial.
    /// Queda <see cref="EstadoPendiente.Entregado"/> al completarse o <see cref="EstadoPendiente.Parcial"/> con saldo.
    /// </summary>
    public EntregaPendiente Entregar(IReadOnlyCollection<CantidadEntregada> cantidades, string? recibeNombre, string? recibeCedula, string usuarioNombre,
        DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(cantidades);
        if (!EstaAbierto)
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.YaEntregado, $"El pendiente {Numero} está {Estado}.");
        if (string.IsNullOrWhiteSpace(recibeNombre) || string.IsNullOrWhiteSpace(recibeCedula))
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.RecibeRequerido, "Indique el nombre y la cédula de quien recibe la mercancía.");

        var pedidas = cantidades.Where(c => c.Cantidad != 0m).ToList();
        if (pedidas.Count == 0)
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.CantidadInvalida, "Indique qué artículos se entregan.");
        if (pedidas.GroupBy(p => p.NumeroLineaVenta).Any(g => g.Count() > 1))
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.CantidadInvalida, "Cada artículo se indica una sola vez.");

        foreach (var pedida in pedidas)
        {
            var linea = _lineas.FirstOrDefault(l => l.NumeroLineaVenta == pedida.NumeroLineaVenta)
                ?? throw new ReglaPendienteExcepcion(CodigoErrorPendiente.CantidadInvalida, $"La línea {pedida.NumeroLineaVenta} no está en el pendiente.");
            if (pedida.Cantidad < 0 || pedida.Cantidad > linea.CantidadPendiente)
                throw new ReglaPendienteExcepcion(CodigoErrorPendiente.CantidadInvalida,
                    $"{linea.Descripcion}: quedan {linea.CantidadPendiente:0.###} por entregar.");
            if (linea.Serializado && string.IsNullOrWhiteSpace(pedida.Serial))
                throw new ReglaPendienteExcepcion(CodigoErrorPendiente.SerialRequerido, $"{linea.Descripcion}: escanee el serial del artículo que se entrega.");
        }

        var entrega = EntregaPendiente.Crear(Id, _entregas.Count + 1, recibeNombre, recibeCedula, usuarioNombre, ahora);
        foreach (var pedida in pedidas)
        {
            var linea = _lineas.First(l => l.NumeroLineaVenta == pedida.NumeroLineaVenta);
            var serial = linea.Serializado ? pedida.Serial!.Trim().ToUpperInvariant() : null;
            linea.RegistrarEntrega(pedida.Cantidad, serial);
            entrega.AgregarLinea(linea, pedida.Cantidad, serial);
        }

        _entregas.Add(entrega);
        Estado = _lineas.All(l => l.CantidadPendiente <= 0m) ? EstadoPendiente.Entregado : EstadoPendiente.Parcial;
        Actualizar(usuarioNombre, ahora);
        return entrega;
    }

    /// <summary>Anula el pendiente con motivo si aún no se entregó nada, liberando la mercancía (RF-255).</summary>
    public void Anular(string? motivo, string usuarioNombre, DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.EstadoInvalido, $"El pendiente {Numero} ya está {Estado}.");
        if (_lineas.Any(l => l.CantidadEntregada > 0))
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.YaEntregado, "El pendiente ya tiene entregas registradas y no se puede anular.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaPendienteExcepcion(CodigoErrorPendiente.MotivoRequerido, "Indique el motivo de la anulación del pendiente.");

        MotivoAnulacion = Validar.Texto(motivo, "Motivo", LargoMaximoMotivo);
        Estado = EstadoPendiente.Anulado;
        Actualizar(usuarioNombre, ahora);
    }

    private void Actualizar(string usuarioNombre, DateTimeOffset ahora)
    {
        ActualizadoPorNombre = Validar.Texto(usuarioNombre, "Usuario", DestinoEntrega.LargoMaximoNombre);
        ActualizadoEn = ahora;
    }
}

public sealed class LineaPendienteEntrega : Entidad
{
    private LineaPendienteEntrega()
    {
    }

    public int PendienteEntregaId { get; private set; }
    public int NumeroLineaVenta { get; private set; }
    public int ArticuloId { get; private set; }
    public string CodigoInterno { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public string UnidadMedidaCodigo { get; private set; } = string.Empty;
    public int DecimalesCantidad { get; private set; }
    public bool Serializado { get; private set; }
    public decimal Cantidad { get; private set; }
    public decimal CantidadEntregada { get; private set; }

    /// <summary>Serial vendido en caja o capturado al entregar.</summary>
    public string? Serial { get; private set; }

    public decimal CantidadPendiente => Cantidad - CantidadEntregada;

    internal static LineaPendienteEntrega Crear(int pendienteId, LineaVenta linea, decimal cantidad) =>
        new()
        {
            PendienteEntregaId = pendienteId,
            NumeroLineaVenta = linea.NumeroLinea,
            ArticuloId = linea.ArticuloId,
            CodigoInterno = linea.CodigoInterno,
            Descripcion = linea.Descripcion,
            UnidadMedidaCodigo = linea.UnidadMedidaCodigo,
            DecimalesCantidad = linea.DecimalesCantidad,
            Serializado = linea.TipoArticulo == TipoArticulo.Serializado,
            Cantidad = cantidad,
            Serial = linea.Serial,
        };

    internal static LineaPendienteEntrega Reconstruir(int pendienteId, DatosLineaPendienteReconstruida linea) =>
        new()
        {
            PendienteEntregaId = pendienteId,
            NumeroLineaVenta = linea.NumeroLineaVenta,
            ArticuloId = linea.ArticuloId,
            CodigoInterno = linea.CodigoInterno,
            Descripcion = linea.Descripcion,
            UnidadMedidaCodigo = linea.UnidadMedidaCodigo,
            DecimalesCantidad = linea.DecimalesCantidad,
            Serializado = linea.Serializado,
            Cantidad = linea.Cantidad,
            CantidadEntregada = linea.CantidadEntregada,
            Serial = linea.Serial,
        };

    internal void RegistrarEntrega(decimal cantidad, string? serial)
    {
        CantidadEntregada += cantidad;
        Serial = serial ?? Serial;
    }
}

/// <summary>Entrega registrada de un pendiente: quién recibió, cuándo y qué (constancia, RF-254).</summary>
public sealed class EntregaPendiente : Entidad
{
    private readonly List<LineaEntregaPendiente> _lineas = [];

    private EntregaPendiente()
    {
    }

    public int PendienteEntregaId { get; private set; }
    public int Numero { get; private set; }
    public string RecibeNombre { get; private set; } = string.Empty;
    public string RecibeCedula { get; private set; } = string.Empty;
    public string UsuarioNombre { get; private set; } = string.Empty;
    public DateTimeOffset Fecha { get; private set; }

    public IReadOnlyList<LineaEntregaPendiente> Lineas => _lineas;

    internal static EntregaPendiente Crear(int pendienteId, int numero, string? recibeNombre, string? recibeCedula, string usuarioNombre, DateTimeOffset ahora) =>
        new()
        {
            PendienteEntregaId = pendienteId,
            Numero = numero,
            RecibeNombre = Validar.Texto(recibeNombre, "Nombre de quien recibe", DestinoEntrega.LargoMaximoNombre),
            RecibeCedula = Validar.Texto(recibeCedula, "Cédula de quien recibe", 20),
            UsuarioNombre = Validar.Texto(usuarioNombre, "Usuario", DestinoEntrega.LargoMaximoNombre),
            Fecha = ahora,
        };

    internal static EntregaPendiente Reconstruir(int pendienteId, DatosEntregaReconstruida datos, IReadOnlyList<LineaPendienteEntrega> lineas)
    {
        var entrega = new EntregaPendiente
        {
            PendienteEntregaId = pendienteId,
            Numero = datos.Numero,
            RecibeNombre = Validar.Texto(datos.RecibeNombre, "Nombre de quien recibe", DestinoEntrega.LargoMaximoNombre),
            RecibeCedula = Validar.Texto(datos.RecibeCedula, "Cédula de quien recibe", 20),
            UsuarioNombre = Validar.Texto(datos.UsuarioNombre, "Usuario", DestinoEntrega.LargoMaximoNombre),
            Fecha = datos.Fecha,
        };

        foreach (var linea in datos.Lineas)
            entrega._lineas.Add(new LineaEntregaPendiente
            {
                EntregaPendienteId = entrega.Id,
                NumeroLineaVenta = linea.NumeroLineaVenta,
                Descripcion = linea.Descripcion,
                Cantidad = linea.Cantidad,
                Serial = linea.Serial,
            });

        return entrega;
    }

    internal void AgregarLinea(LineaPendienteEntrega linea, decimal cantidad, string? serial) =>
        _lineas.Add(new LineaEntregaPendiente
        {
            EntregaPendienteId = Id,
            NumeroLineaVenta = linea.NumeroLineaVenta,
            Descripcion = linea.Descripcion,
            Cantidad = cantidad,
            Serial = serial,
        });
}

public sealed class LineaEntregaPendiente : Entidad
{
    internal LineaEntregaPendiente()
    {
    }

    public int EntregaPendienteId { get; internal set; }
    public int NumeroLineaVenta { get; internal set; }
    public string Descripcion { get; internal set; } = string.Empty;
    public decimal Cantidad { get; internal set; }
    public string? Serial { get; internal set; }
}

/// <summary>
/// Lo que hace falta para reconstruir un pendiente en el Central a partir de lo que informó la caja. Es el documento de
/// sincronización visto por el dominio, sin depender de los contratos.
/// </summary>
public sealed record DatosPendienteReconstruido(
    string Numero,
    string VentaNumero,
    MetodoEntrega Metodo,
    EstadoPendiente Estado,
    string? AlmacenNombre,
    string? Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    string? Transportista,
    decimal? CostoEnvio,
    DateOnly? FechaComprometida,
    string? Comentario,
    string? ClienteDocumento,
    string? ClienteNombre,
    string VendidoPorNombre,
    string? AutorizadoPorNombre,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    string ActualizadoPorNombre,
    string? MotivoAnulacion,
    IReadOnlyList<DatosLineaPendienteReconstruida> Lineas,
    IReadOnlyList<DatosEntregaReconstruida> Entregas);

public sealed record DatosLineaPendienteReconstruida(
    int NumeroLineaVenta,
    int ArticuloId,
    string CodigoInterno,
    string Descripcion,
    string UnidadMedidaCodigo,
    int DecimalesCantidad,
    bool Serializado,
    decimal Cantidad,
    decimal CantidadEntregada,
    string? Serial);

public sealed record DatosEntregaReconstruida(
    int Numero,
    string RecibeNombre,
    string RecibeCedula,
    string UsuarioNombre,
    DateTimeOffset Fecha,
    IReadOnlyList<DatosLineaEntregaReconstruida> Lineas);

public sealed record DatosLineaEntregaReconstruida(int NumeroLineaVenta, string Descripcion, decimal Cantidad, string? Serial);
