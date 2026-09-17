namespace CgPos.Pos.Pruebas;

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
}
