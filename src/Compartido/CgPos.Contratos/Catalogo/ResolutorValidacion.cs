using System.Security.Cryptography;
using System.Text;

namespace CgPos.Contratos.Catalogo;

/// <summary>
/// Resolutor para validar un paquete sin base de datos: a cada código le da un Id fijo. Las referencias que no existen las valida quien conoce
/// lo ya publicado.
/// </summary>
public sealed class ResolutorValidacion : IResolutorCodigos
{
    public static ResolutorValidacion Instancia { get; } = new();

    public Guid Departamento(int codigo) => Id("Departamento", codigo.ToString());
    public Guid Categoria(int codigo) => Id("Categoria", codigo.ToString());
    public Guid Marca(int codigo) => Id("Marca", codigo.ToString());
    public Guid UnidadMedida(int codigo) => Id("UnidadMedida", codigo.ToString());
    public Guid Impuesto(string codigo) => Id("Impuesto", codigo);
    public Guid Articulo(string codigo) => Id("Articulo", codigo);
    public Guid Sucursal(int codigo) => Id("Sucursal", codigo.ToString());
    public Guid Caja(int sucursalCodigo, int cajaCodigo) => Id("Caja", $"{sucursalCodigo}:{cajaCodigo}");
    public Guid Banco(string codigo) => Id("Banco", codigo);
    public Guid NivelFidelidad(int codigo) => Id("NivelFidelidad", codigo.ToString());
    public Guid Promocion(string codigo) => Id("Promocion", codigo);

    /// <summary>Id derivado del tipo y el código (mismo código, mismo Id; nunca vacío).</summary>
    public static Guid Id(string tipo, string? codigo) =>
        new(MD5.HashData(Encoding.UTF8.GetBytes($"{tipo}:{codigo?.Trim().ToUpperInvariant()}")));
}
