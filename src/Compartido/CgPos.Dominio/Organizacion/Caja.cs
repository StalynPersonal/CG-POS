using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Organizacion;

/// <summary>Terminal de venta. Solo una caja habilitada desde el Central puede abrir turno (RF-169).</summary>
public sealed class Caja : Entidad
{
    public const int LargoMaximoCodigo = 10;
    public const int LargoMaximoNombre = 100;

    private Caja()
    {
    }

    public Guid SucursalId { get; private set; }

    /// <summary>Código de la caja, único dentro de la sucursal (ej. "01").</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;
    public bool Habilitada { get; private set; } = true;

    /// <summary>Versión del Agente instalada en la caja, tal como la reporta al actualizarse (RF-292).</summary>
    public string? VersionAgente { get; private set; }

    public DateTimeOffset? VersionReportadaEn { get; private set; }

    public static Caja Crear(Guid sucursalId, string codigo, string nombre, Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            SucursalId = Validar.Id(sucursalId, "Sucursal"),
            Codigo = Validar.Texto(codigo, "Código de caja", LargoMaximoCodigo),
            Nombre = Validar.Texto(nombre, "Nombre de caja", LargoMaximoNombre),
        };

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de caja", LargoMaximoNombre);

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
