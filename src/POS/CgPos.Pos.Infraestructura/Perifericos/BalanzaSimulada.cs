using System.Globalization;
using CgPos.Pos.Aplicacion.Perifericos;
using Microsoft.Extensions.Configuration;

namespace CgPos.Pos.Infraestructura.Perifericos;

/// <summary>
/// Balanza de desarrollo: devuelve siempre un peso estable configurable en <c>Perifericos:BalanzaSimulada</c>
/// (<c>Peso</c>, <c>Unidad</c>, <c>Habilitada</c>). Se reemplaza por el controlador real cuando se defina el modelo.
/// </summary>
internal sealed class BalanzaSimulada(IConfiguration configuracion) : IBalanza
{
    public const decimal PesoPredeterminado = 1.250m;

    public Task<LecturaPeso?> LeerPesoAsync(CancellationToken cancelacion = default)
    {
        var seccion = configuracion.GetSection("Perifericos:BalanzaSimulada");
        if (string.Equals(seccion["Habilitada"], "false", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<LecturaPeso?>(null);

        var peso = decimal.TryParse(seccion["Peso"], NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : PesoPredeterminado;
        return Task.FromResult<LecturaPeso?>(new LecturaPeso(true, peso, seccion["Unidad"] ?? "LB"));
    }
}
