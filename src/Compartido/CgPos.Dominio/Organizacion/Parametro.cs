using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>
/// Parámetro de configuración clave/valor. Puede ser general (sin sucursal ni caja),
/// de una sucursal o de una caja. Al leerlo prevalece caja, luego sucursal, luego general.
/// </summary>
public sealed class Parametro : Entidad
{
    public const int LargoMaximoClave = 100;
    public const int LargoMaximoValor = 1000;
    public const int LargoMaximoDescripcion = 250;

    private Parametro()
    {
    }

    /// <summary>Clave con formato "Modulo.Nombre", ej. "Seguridad.IntentosMaximosPin".</summary>
    public string Clave { get; private set; } = string.Empty;

    public string Valor { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public Guid? SucursalId { get; private set; }
    public Guid? CajaId { get; private set; }

    public static Parametro Crear(string clave, string valor, string? descripcion = null,
        Guid? sucursalId = null, Guid? cajaId = null, Guid? id = null)
    {
        if (sucursalId is not null && cajaId is not null)
            throw new ArgumentException("Un parámetro aplica a una sucursal o a una caja, no a ambas.");
        if (sucursalId == Guid.Empty || cajaId == Guid.Empty)
            throw new ArgumentException("El ámbito del parámetro no puede ser un Id vacío.");

        var parametro = new Parametro
        {
            Id = id ?? Guid.CreateVersion7(),
            Clave = Validar.Texto(clave, "Clave", LargoMaximoClave),
            Descripcion = Validar.TextoOpcional(descripcion, "Descripción", LargoMaximoDescripcion),
            SucursalId = sucursalId,
            CajaId = cajaId,
        };
        parametro.CambiarValor(valor);
        return parametro;
    }

    public void CambiarValor(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        if (valor.Length > LargoMaximoValor)
            throw new ArgumentException($"El valor no puede exceder {LargoMaximoValor} caracteres.", nameof(valor));

        Valor = valor;
    }
}
