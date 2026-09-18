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

    public int Departamento(int codigo) => Id("Departamento", codigo.ToString());
    public int Categoria(int codigo) => Id("Categoria", codigo.ToString());
    public int Marca(int codigo) => Id("Marca", codigo.ToString());
    public int UnidadMedida(int codigo) => Id("UnidadMedida", codigo.ToString());
    public int Impuesto(string codigo) => Id("Impuesto", codigo);
    public int Articulo(string codigo) => Id("Articulo", codigo);
    public int Sucursal(string codigo) => Id("Sucursal", codigo);
    public int Caja(string sucursalCodigo, string cajaCodigo) => Id("Caja", $"{sucursalCodigo}:{cajaCodigo}");
    public int Banco(string codigo) => Id("Banco", codigo);
    public int NivelFidelidad(int codigo) => Id("NivelFidelidad", codigo.ToString());
    public int Promocion(string codigo) => Id("Promocion", codigo);

    /// <summary>Id derivado del tipo y el código (mismo código, mismo Id; siempre mayor que cero).</summary>
    public static int Id(string tipo, string? codigo) =>
        (int)(BitConverter.ToUInt32(MD5.HashData(Encoding.UTF8.GetBytes($"{tipo}:{codigo?.Trim().ToUpperInvariant()}"))) % int.MaxValue) + 1;
}
