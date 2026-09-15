namespace CgPos.Pos.Aplicacion.Sincronizacion;

/// <summary>Hasta dónde la caja ya recibió y aplicó algo del Central (ej. la versión de los maestros). Se guarda solo después de aplicarlo.</summary>
public sealed class MarcaSincronizacion
{
    public const int LargoMaximoClave = 100;

    /// <summary>Versión de maestros del Central aplicada en la caja.</summary>
    public const string VersionMaestros = "Maestros.Version";

    private MarcaSincronizacion()
    {
    }

    public string Clave { get; private set; } = string.Empty;
    public long Valor { get; private set; }
    public DateTimeOffset ActualizadaEn { get; private set; }

    public static MarcaSincronizacion Crear(string clave, long valor, DateTimeOffset ahora)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        if (clave.Length > LargoMaximoClave)
            throw new ArgumentException($"La clave de la marca excede {LargoMaximoClave} caracteres.", nameof(clave));

        return new MarcaSincronizacion { Clave = clave, Valor = valor, ActualizadaEn = ahora };
    }

    public void Actualizar(long valor, DateTimeOffset ahora)
    {
        Valor = valor;
        ActualizadaEn = ahora;
    }
}
