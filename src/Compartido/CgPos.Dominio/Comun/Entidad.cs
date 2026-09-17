namespace CgPos.Dominio.Comun;

/// <summary>
/// Base de toda entidad persistida. El Id es un entero interno de cada base: lo asigna la base al agregar la entidad (HiLo), así que vale 0
/// hasta entonces, y nunca viaja entre la caja y el Central, que se entienden por códigos y números de documento.
/// </summary>
public abstract class Entidad
{
    public int Id { get; init; }
}
