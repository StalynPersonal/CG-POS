namespace CgPos.Pos.Aplicacion.Perifericos;

/// <param name="Estable">La balanza reporta el peso detenido; solo así se factura.</param>
/// <param name="Unidad">Unidad en la que pesa la balanza (ej. "LB", "KG").</param>
public sealed record LecturaPeso(bool Estable, decimal Peso, string Unidad);

/// <summary>
/// Balanza conectada a la caja (RF-19): el peso de los productos no empacados se toma de aquí y no se digita.
/// El modelo y protocolo aún no están definidos; mientras tanto se usa una balanza simulada.
/// </summary>
public interface IBalanza
{
    /// <returns>La lectura actual, o nulo si no hay balanza o no respondió.</returns>
    Task<LecturaPeso?> LeerPesoAsync(CancellationToken cancelacion = default);
}

/// <param name="SinConexion">El terminal o la pasarela no respondió: habilita la aprobación manual por contingencia (RF-213).</param>
/// <param name="ReferenciaTerminal">Lo que el terminal necesita para anular la transacción (en CardNet, el host y el número de referencia).</param>
public sealed record ResultadoTerminal(bool Aprobada, bool SinConexion, string? Aprobacion, string? UltimosDigitos, string? Marca, string? Mensaje,
    string? ReferenciaTerminal = null);

/// <param name="Leida">El cliente pasó la tarjeta y el terminal entregó sus primeros dígitos.</param>
/// <param name="Bin">Primeros dígitos de la tarjeta (CardNet entrega 8), sin datos sensibles.</param>
public sealed record ResultadoConsultaTarjeta(bool Leida, bool SinConexion, string? Bin, string? Marca, string? Mensaje);

/// <summary>
/// Terminal de pago con tarjeta (CardNet con Ingenico 7000, RF-100). Cambiar de modelo es cambiar la configuración.
/// </summary>
public interface ITerminalPago
{
    /// <summary>
    /// El terminal lee la tarjeta antes de cobrar y entrega su BIN, para aplicar el descuento del banco (RF-98) y cobrar ya con el monto
    /// rebajado. Si no lo soporta, el cajero puede digitar los primeros dígitos.
    /// </summary>
    bool ConsultaTarjeta { get; }

    /// <summary>Pide al cliente pasar la tarjeta y devuelve su BIN; el terminal la guarda unos segundos para el cobro que sigue.</summary>
    Task<ResultadoConsultaTarjeta> ConsultarTarjetaAsync(CancellationToken cancelacion = default);

    /// <param name="impuesto">ITBIS incluido en el monto; el terminal lo informa al banco.</param>
    Task<ResultadoTerminal> CobrarAsync(decimal monto, decimal impuesto, string referenciaVenta, CancellationToken cancelacion = default);

    /// <summary>Anula una venta aprobada (RF-214).</summary>
    Task<ResultadoTerminal> AnularAsync(string aprobacion, string? referenciaTerminal, decimal monto, CancellationToken cancelacion = default);

    /// <summary>
    /// Cierra el lote del terminal al cerrar el turno y devuelve lo que el terminal contabilizó, para cuadrarlo con lo cobrado en la caja (RF-215).
    /// </summary>
    Task<ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default);
}

/// <param name="Aprobaciones">Autorizaciones que el terminal reporta en el lote; vacía si el modelo no las detalla.</param>
public sealed record ResultadoLoteTerminal(
    bool Correcto,
    string? Mensaje,
    string? NumeroLote = null,
    int Transacciones = 0,
    decimal Monto = 0m,
    IReadOnlyList<string>? Aprobaciones = null);

/// <param name="Nombre">Nombre corto del documento, ej. "ticket-01-01-00000002".</param>
/// <param name="Texto">Versión en texto plano (vista previa y copia en archivo).</param>
/// <param name="EscPos">Bytes listos para una impresora térmica ESC/POS.</param>
public sealed record DocumentoImpresion(string Nombre, string Texto, byte[] EscPos);

public sealed record ResultadoImpresion(bool Correcto, string? Mensaje);

/// <summary>
/// Impresora de tickets y gaveta de dinero (RF-112). El destino se elige por configuración sin cambiar el código (RF-285).
/// </summary>
public interface IImpresoraTicket
{
    Task<ResultadoImpresion> ImprimirAsync(DocumentoImpresion documento, CancellationToken cancelacion = default);

    Task<ResultadoImpresion> AbrirGavetaAsync(CancellationToken cancelacion = default);
}
