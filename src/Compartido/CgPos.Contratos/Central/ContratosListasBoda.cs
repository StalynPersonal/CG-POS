using CgPos.Dominio.ListasBoda;

namespace CgPos.Contratos.Central;

/// <param name="Comprado">Cantidad ya comprada; solo baja si el Central tiene activado el descuento de las compras.</param>
public sealed record DatosArticuloListaBoda(string ArticuloCodigo, string Descripcion, decimal Cantidad, decimal Comprado, decimal Pendiente);

public sealed record DatosCompraListaBoda(string VentaNumero, string CajaCodigo, decimal Monto, DateTimeOffset Fecha);

/// <summary>Lista de boda tal como se administra en el Central.</summary>
public sealed record DatosListaBoda(
    int Id,
    string Numero,
    string Evento,
    DateOnly FechaEvento,
    string? Lugar,
    string ClienteDocumento,
    string ClienteNombre,
    string? ClienteTelefono,
    string? ClienteCorreo,
    int? SucursalId,
    string? SucursalNombre,
    string? Observacion,
    EstadoListaBoda Estado,
    decimal TotalComprado,
    DateTimeOffset CreadaEn,
    IReadOnlyList<DatosArticuloListaBoda> Articulos,
    IReadOnlyList<DatosCompraListaBoda> Compras);

public sealed record SolicitudArticuloListaBoda(string ArticuloCodigo, string Descripcion, decimal Cantidad);

/// <param name="Numero">Solo al crearla; si viene vacío, el Central asigna el próximo número.</param>
public sealed record SolicitudListaBoda(
    string? Numero,
    string Evento,
    DateOnly FechaEvento,
    string? Lugar,
    string ClienteDocumento,
    string ClienteNombre,
    string? ClienteTelefono,
    string? ClienteCorreo,
    int? SucursalId,
    string? Observacion,
    IReadOnlyList<SolicitudArticuloListaBoda> Articulos);

/// <param name="DescuentaCompras">Si lo comprado baja de las cantidades pedidas; lo configura el negocio en el Central.</param>
public sealed record DatosConfiguracionListasBoda(bool DescuentaCompras);

/// <summary>Lista de boda tal como la consulta una caja al vender: sin Ids del Central y con lo que falta por comprar.</summary>
public sealed record DatosListaBodaParaCaja(
    string Numero,
    string Evento,
    DateOnly FechaEvento,
    string ClienteNombre,
    EstadoListaBoda Estado,
    bool DescuentaCompras,
    IReadOnlyList<DatosArticuloListaBoda> Articulos);
