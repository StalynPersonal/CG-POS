using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>Terminal de venta. Solo una caja habilitada desde el Central puede abrir turno (RF-169).</summary>
public sealed class Caja : Entidad
{
    public const int LargoMaximoNombre = 100;
    public const int LargoMaximoIp = 45;

    private Caja()
    {
    }

    public int SucursalId { get; private set; }

    /// <summary>Código de la caja, de dos dígitos (01 a 99) y único dentro de su sucursal. Es el que va en los documentos.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

/// <summary>
    /// Dirección de red fija de esta caja, obligatoria y distinta a la de cualquier otra. Al comunicarse, la caja dice cuál
    /// es la suya y tiene que ser esta: así la misma credencial no sirve desde otro equipo.
    /// </summary>
    public string DireccionIp { get; private set; } = string.Empty;

    public bool Habilitada { get; private set; } = true;

    /// <summary>Versión del Agente instalada en la caja, tal como la reporta al actualizarse (RF-292).</summary>
    public string? VersionAgente { get; private set; }

    public DateTimeOffset? VersionReportadaEn { get; private set; }

    public static Caja Crear(int sucursalId, string codigo, string nombre, string direccionIp) =>
        new()
        {
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            Codigo = Validar.CodigoDosDigitos(codigo, "Código de caja"),
            Nombre = Validar.Texto(nombre, "Nombre de caja", LargoMaximoNombre),
            DireccionIp = NormalizarIp(direccionIp),
        };

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de caja", LargoMaximoNombre);

    /// <summary>Cambiar la dirección deja fuera a la caja hasta que se configure con la nueva: es lo que se espera.</summary>
    public void CambiarDireccionIp(string direccionIp) => DireccionIp = NormalizarIp(direccionIp);

    /// <summary>La dirección que dice la caja es la suya.</summary>
    public bool CoincideDireccionIp(string? direccionIp) =>
        string.Equals(DireccionIp, direccionIp?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <exception cref="ArgumentException">Falta la dirección o no es válida.</exception>
    private static string NormalizarIp(string direccionIp)
    {
        var texto = (direccionIp ?? string.Empty).Trim();
        if (texto.Length == 0)
            throw new ArgumentException("Indique la dirección IP de la caja.", nameof(direccionIp));

        if (!System.Net.IPAddress.TryParse(texto, out var direccion))
            throw new ArgumentException($"«{texto}» no es una dirección IP válida.", nameof(direccionIp));

        return direccion.ToString();
    }

    public void Habilitar() => Habilitada = true;

    public void Deshabilitar() => Habilitada = false;

    /// <summary>La caja informa qué versión del Agente quedó instalada, para seguir el despliegue desde el Central.</summary>
    public void ReportarVersion(string version, DateTimeOffset ahora)
    {
        VersionAgente = Validar.Texto(version, "Versión del Agente", LargoMaximoVersion);
        VersionReportadaEn = ahora;
    }

    public const int LargoMaximoVersion = 40;
}
