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
