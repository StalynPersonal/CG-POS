using CgPos.Dominio.Comun;
using CgPos.Dominio.Entregas;

namespace CgPos.Dominio.Ventas;

/// <summary>
/// La venta que el cajero está armando en la pantalla. Vive en su propia tabla y todavía no es un documento: no tiene
/// número de factura, y si se descarta desaparece sin dejar un hueco en el libro de ventas.
/// </summary>
public sealed class VentaEnProceso : Venta
{
    private readonly List<LineaVentaEnProceso> _lineas = [];
    private readonly List<DestinoEntregaEnProceso> _destinos = [];

    /// <summary>Con qué nombrarla mientras no tiene número: es lo que se ve en pantalla y lo que queda en la auditoría.</summary>
    public override string Identificacion => $"B-{Id:000000}";

    public static VentaEnProceso Iniciar(int sucursalId, int cajaId, int turnoId, int usuarioId, string usuarioNombre, string moneda,
        string simboloMoneda, DateTimeOffset ahora)
    {
        var venta = new VentaEnProceso();
        venta.Abrir(sucursalId, cajaId, turnoId, usuarioId, usuarioNombre, moneda, simboloMoneda, ahora);
        return venta;
    }

    /// <summary>La venta que estaba en espera vuelve a la mesa de trabajo, con todo lo que tenía.</summary>
    public static VentaEnProceso Retomar(VentaGuardada guardada, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(guardada);

        var venta = new VentaEnProceso();
        venta.CopiarDe(guardada);
        venta.VolverACurso(ahora);
        return venta;
    }

    private protected override LineaVenta NuevaLinea() => new LineaVentaEnProceso();

    private protected override DestinoEntrega NuevoDestino() => new DestinoEntregaEnProceso();

    private protected override LineaDestinoEntrega NuevaLineaDestino() => new LineaDestinoEntregaEnProceso();

    private protected override IReadOnlyList<LineaVenta> LineasInternas => _lineas;

    private protected override void AgregarLinea(LineaVenta linea) => _lineas.Add((LineaVentaEnProceso)linea);

    private protected override IReadOnlyList<DestinoEntrega> DestinosInternos => _destinos;

    private protected override void AgregarDestino(DestinoEntrega destino) => _destinos.Add((DestinoEntregaEnProceso)destino);

    private protected override void QuitarDestino(DestinoEntrega destino) => _destinos.Remove((DestinoEntregaEnProceso)destino);

    /// <summary>La venta que se arma no tiene pagos: se cobra la copia que va a la tabla de ventas cobradas.</summary>
    private protected override IReadOnlyList<PagoVenta> PagosInternos => [];

    private protected override void AgregarPago(PagoVenta pago) =>
        throw new InvalidOperationException("La venta se cobra en su copia para la tabla de ventas, no en la mesa de trabajo.");
}

/// <summary>
/// La venta que el cajero dejó en espera para atender a otro cliente. Se la nombra con una referencia suya («Sra. María»,
/// «102»), que es con lo que la encuentra al volver: todavía no tiene número de factura.
/// </summary>
public sealed class VentaGuardada : Venta
{
    public const int LargoMaximoReferencia = 40;

    private readonly List<LineaVentaGuardada> _lineas = [];
    private readonly List<DestinoEntregaGuardada> _destinos = [];

    /// <summary>Cómo la llamó el cajero al guardarla.</summary>
    public string Referencia { get; private set; } = string.Empty;

    public override string Identificacion => Referencia;

    public static VentaGuardada Guardar(VentaEnProceso venta, string referencia, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(venta);

        var guardada = new VentaGuardada { Referencia = Validar.Texto(referencia, "Referencia", LargoMaximoReferencia) };
        guardada.CopiarDe(venta);
        guardada.PonerEnEspera(ahora);
        return guardada;
    }

    private protected override LineaVenta NuevaLinea() => new LineaVentaGuardada();

    private protected override DestinoEntrega NuevoDestino() => new DestinoEntregaGuardada();

    private protected override LineaDestinoEntrega NuevaLineaDestino() => new LineaDestinoEntregaGuardada();

    private protected override IReadOnlyList<LineaVenta> LineasInternas => _lineas;

    private protected override void AgregarLinea(LineaVenta linea) => _lineas.Add((LineaVentaGuardada)linea);

    private protected override IReadOnlyList<DestinoEntrega> DestinosInternos => _destinos;

    private protected override void AgregarDestino(DestinoEntrega destino) => _destinos.Add((DestinoEntregaGuardada)destino);

    private protected override void QuitarDestino(DestinoEntrega destino) => _destinos.Remove((DestinoEntregaGuardada)destino);

    /// <summary>En espera no se cobra: primero se retoma.</summary>
    private protected override IReadOnlyList<PagoVenta> PagosInternos => [];

    private protected override void AgregarPago(PagoVenta pago) =>
        throw new InvalidOperationException("Una venta en espera no se cobra: primero se retoma.");
}

/// <summary>
/// La venta cobrada: el documento. Aquí, y solo aquí, tiene número de factura y pagos, porque a esta tabla solo entra lo
/// que de verdad se vendió. Su Id sale de su propia secuencia, así que es correlativo.
/// </summary>
public sealed class VentaCobrada : Venta
{
    private readonly List<LineaVentaCobrada> _lineas = [];
    private readonly List<DestinoEntregaCobrada> _destinos = [];
    private readonly List<PagoVenta> _pagos = [];

    public override string Identificacion => NumeroTransaccion;

    /// <summary>
    /// Prepara el documento con todo lo del borrador, todavía sin número: el cobro se valida sobre esta copia, y solo si
    /// pasa se le pide el número a la secuencia. Así un cobro rechazado no deja un hueco en la numeración.
    /// </summary>
    public static VentaCobrada DesdeBorrador(VentaEnProceso venta)
    {
        ArgumentNullException.ThrowIfNull(venta);

        var cobrada = new VentaCobrada();
        cobrada.CopiarDe(venta);
        return cobrada;
    }

    /// <summary>
    /// Le da su número de factura, ya cobrada. Se llama después de validar el cobro para que un pago rechazado no deje un
    /// número sin usar.
    /// </summary>
    /// <param name="origen">Sucursal, caja y turno tal como están al cobrar: quedan en la venta como foto.</param>
    public void Numerar(OrigenVenta origen, long secuencia, int digitosSecuencia)
    {
        if (Estado != EstadoVenta.Cobrada)
            throw new InvalidOperationException("Solo se numera una venta cobrada.");

        AsignarNumero(origen, secuencia, digitosSecuencia);
    }

    private protected override LineaVenta NuevaLinea() => new LineaVentaCobrada();

    private protected override DestinoEntrega NuevoDestino() => new DestinoEntregaCobrada();

    private protected override LineaDestinoEntrega NuevaLineaDestino() => new LineaDestinoEntregaCobrada();

    private protected override IReadOnlyList<LineaVenta> LineasInternas => _lineas;

    private protected override void AgregarLinea(LineaVenta linea) => _lineas.Add((LineaVentaCobrada)linea);

    private protected override IReadOnlyList<DestinoEntrega> DestinosInternos => _destinos;

    private protected override void AgregarDestino(DestinoEntrega destino) => _destinos.Add((DestinoEntregaCobrada)destino);

    private protected override void QuitarDestino(DestinoEntrega destino) => _destinos.Remove((DestinoEntregaCobrada)destino);

    private protected override IReadOnlyList<PagoVenta> PagosInternos => _pagos;

    private protected override void AgregarPago(PagoVenta pago) => _pagos.Add(pago);
}

/// <summary>Línea de la venta que se está armando.</summary>
public sealed class LineaVentaEnProceso : LineaVenta;

/// <summary>Línea de la venta que quedó en espera.</summary>
public sealed class LineaVentaGuardada : LineaVenta;

/// <summary>Línea de la venta cobrada: es la que viaja al Central y la que se devuelve.</summary>
public sealed class LineaVentaCobrada : LineaVenta;

/// <summary>Dónde se cobró la venta: sucursal, caja y turno tal como estaban en ese momento.</summary>
public sealed record OrigenVenta(string SucursalCodigo, string SucursalNombre, string CajaCodigo, long TurnoNumero);
