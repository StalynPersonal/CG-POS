namespace CgPos.Domain.Comun;

/// <summary>
/// Base de toda entidad persistida. El Id es un GUID v7 (ordenable por tiempo): se genera en la caja
/// sin coordinar con el Central y no colisiona entre cajas offline. Los maestros que bajan del Central
/// conservan el Id asignado allá.
/// </summary>
public abstract class Entidad
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
