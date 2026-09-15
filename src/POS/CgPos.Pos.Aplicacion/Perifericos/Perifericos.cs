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
public sealed record ResultadoTerminal(bool Aprobada, bool SinConexion, string? Aprobacion, string? UltimosDigitos, string? Marca, string? Mensaje);

/// <summary>
/// Terminal de pago con tarjeta (Verifone con pasarela Azul o CardNet, RF-100). El modelo y la pasarela aún no están
/// definidos; mientras tanto se usa un terminal simulado.
/// </summary>
public interface ITerminalPago
{
    Task<ResultadoTerminal> CobrarAsync(decimal monto, string referenciaVenta, CancellationToken cancelacion = default);

    /// <summary>Anula una venta aprobada (RF-214).</summary>
    Task<ResultadoTerminal> AnularAsync(string aprobacion, decimal monto, CancellationToken cancelacion = default);
}

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
