namespace CgPos.Dominio.Pruebas;

/// <summary>Códigos numéricos únicos para los catálogos que crean las pruebas (no se repiten entre pruebas que comparten base de datos).</summary>
internal static class Codigos
{
    private static int _ultimo = Random.Shared.Next(100_000, 800_000);

    public static int Siguiente() => Interlocked.Increment(ref _ultimo);
}

/// <summary>Id enteros para entidades que las pruebas construyen sin base de datos (la base asigna los suyos al guardar).</summary>
internal static class Ids
{
    private static int _ultimo = Random.Shared.Next(1_000, 100_000);

    public static int Siguiente() => Interlocked.Increment(ref _ultimo);

    /// <summary>Da un Id a una entidad construida sin base, como si ya estuviera guardada.</summary>
    public static T Asignar<T>(T entidad) where T : CgPos.Dominio.Comun.Entidad
    {
        typeof(CgPos.Dominio.Comun.Entidad).GetProperty(nameof(CgPos.Dominio.Comun.Entidad.Id))!.SetValue(entidad, Siguiente());
        return entidad;
    }

    /// <summary>Lo mismo para una colección de hijos, que al guardarse también reciben el suyo.</summary>
    public static void AsignarHijos<T>(IEnumerable<T> entidades) where T : CgPos.Dominio.Comun.Entidad
    {
        foreach (var entidad in entidades)
            Asignar(entidad);
    }
}
