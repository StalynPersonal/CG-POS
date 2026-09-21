namespace CgPos.Dominio.Entregas;

/// <summary>Destino de entrega de la venta que se está armando.</summary>
public sealed class DestinoEntregaEnProceso : DestinoEntrega
{
    private readonly List<LineaDestinoEntregaEnProceso> _lineas = [];

    private protected override IReadOnlyList<LineaDestinoEntrega> LineasInternas => _lineas;

    private protected override void AgregarLinea(LineaDestinoEntrega linea) => _lineas.Add((LineaDestinoEntregaEnProceso)linea);
}

/// <summary>Destino de entrega de la venta que quedó en espera.</summary>
public sealed class DestinoEntregaGuardada : DestinoEntrega
{
    private readonly List<LineaDestinoEntregaGuardada> _lineas = [];

    private protected override IReadOnlyList<LineaDestinoEntrega> LineasInternas => _lineas;

    private protected override void AgregarLinea(LineaDestinoEntrega linea) => _lineas.Add((LineaDestinoEntregaGuardada)linea);
}

/// <summary>Destino de entrega de la venta cobrada: de él nace el pendiente de entrega.</summary>
public sealed class DestinoEntregaCobrada : DestinoEntrega
{
    private readonly List<LineaDestinoEntregaCobrada> _lineas = [];

    private protected override IReadOnlyList<LineaDestinoEntrega> LineasInternas => _lineas;

    private protected override void AgregarLinea(LineaDestinoEntrega linea) => _lineas.Add((LineaDestinoEntregaCobrada)linea);
}

/// <summary>Línea del destino de la venta que se está armando.</summary>
public sealed class LineaDestinoEntregaEnProceso : LineaDestinoEntrega;

/// <summary>Línea del destino de la venta que quedó en espera.</summary>
public sealed class LineaDestinoEntregaGuardada : LineaDestinoEntrega;

/// <summary>Línea del destino de la venta cobrada.</summary>
public sealed class LineaDestinoEntregaCobrada : LineaDestinoEntrega;
