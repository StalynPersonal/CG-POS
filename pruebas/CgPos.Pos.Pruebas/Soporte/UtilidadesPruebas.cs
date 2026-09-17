using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;

namespace CgPos.Pos.Pruebas.Soporte;

/// <summary>Reloj controlable para probar bloqueos y vencimientos.</summary>
public sealed class RelojPrueba(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = inicio;

    public override DateTimeOffset GetUtcNow() => Ahora;

    public void Avanzar(TimeSpan tiempo) => Ahora += tiempo;
}

public sealed class ContextoCajaFijo(int? cajaId, int? sucursalCodigo = null, int? cajaCodigo = null) : IContextoCaja
{
    public int? SucursalCodigo { get; } = sucursalCodigo;
    public int? CajaCodigo { get; } = cajaCodigo;
    public int? CajaId { get; } = cajaId;
}

public static class RutasPrueba
{
    public static string RaizRepositorio()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "CgPos.slnx")))
                return carpeta.FullName;
        }

        throw new InvalidOperationException("No se encontró la raíz del repositorio (CgPos.slnx).");
    }
}
