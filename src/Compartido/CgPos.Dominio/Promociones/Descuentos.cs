using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Promociones;

/// <summary>Motivo seleccionable para los descuentos manuales (RF-203).</summary>
public sealed class MotivoDescuento : Entidad
{
    public const int LargoMaximoCodigo = 20;
    public const int LargoMaximoNombre = 100;

    private MotivoDescuento()
    {
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;

    public static MotivoDescuento Crear(string codigo, string nombre, Guid? id = null)
    {
        var motivo = new MotivoDescuento
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Texto(codigo, "Código de motivo", LargoMaximoCodigo).ToUpperInvariant(),
        };
        motivo.CambiarNombre(nombre);
        return motivo;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de motivo", LargoMaximoNombre);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

/// <summary>
/// Tope de descuento manual por nivel de usuario, general o para una familia o un artículo (RF-202, RN-10).
/// A mayor nivel del autorizador, mayor el descuento permitido.
/// </summary>
public sealed class TopeDescuento : Entidad
{
    private TopeDescuento()
    {
    }

    public int Nivel { get; private set; }
    public Guid? FamiliaId { get; private set; }
    public Guid? ArticuloId { get; private set; }
    public decimal? PorcentajeMaximo { get; private set; }
    public decimal? MontoMaximo { get; private set; }

    public static TopeDescuento Crear(int nivel, decimal? porcentajeMaximo, decimal? montoMaximo, Guid? familiaId = null, Guid? articuloId = null, Guid? id = null)
    {
        var tope = new TopeDescuento { Id = id ?? Guid.CreateVersion7() };
        tope.Actualizar(nivel, porcentajeMaximo, montoMaximo, familiaId, articuloId);
        return tope;
    }

    public void Actualizar(int nivel, decimal? porcentajeMaximo, decimal? montoMaximo, Guid? familiaId, Guid? articuloId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nivel, 1);
        if (familiaId is not null && articuloId is not null)
            throw new ArgumentException("Un tope es para una familia o para un artículo, no para ambos.");
        if (porcentajeMaximo is null && montoMaximo is null)
            throw new ArgumentException("Indique el porcentaje o el monto máximo del tope.");
        if (porcentajeMaximo is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(porcentajeMaximo), porcentajeMaximo, "El porcentaje máximo debe estar entre 0 y 100.");
        if (montoMaximo < 0)
            throw new ArgumentOutOfRangeException(nameof(montoMaximo), montoMaximo, "El monto máximo no puede ser negativo.");

        Nivel = nivel;
        FamiliaId = familiaId;
        ArticuloId = articuloId;
        PorcentajeMaximo = porcentajeMaximo;
        MontoMaximo = montoMaximo;
    }
}

/// <param name="NivelTope">Nivel del tope que se aplicó; nulo si no hay topes para ese alcance.</param>
/// <param name="SinConfiguracion">No hay ningún tope que aplique (ni del artículo, ni de su familia, ni general).</param>
public sealed record EvaluacionTope(bool Permitido, int? NivelTope, decimal? PorcentajeMaximo, decimal? MontoMaximo, bool SinConfiguracion = false);

public static class ReglasTopeDescuento
{
    /// <summary>
    /// Toma el alcance más específico que tenga topes (artículo, luego familia, luego general) y, dentro de él, el tope del
    /// nivel más alto que no supere el del autorizador. Si ese alcance solo tiene topes de niveles superiores, el descuento
    /// exige a alguien de mayor nivel. Sin ningún tope configurado el descuento manual no se permite: el límite lo define el negocio.
    /// </summary>
    /// <param name="articuloId">Nulo para el descuento a la factura, que solo usa topes generales.</param>
    public static EvaluacionTope Evaluar(IReadOnlyCollection<TopeDescuento> topes, int nivelAutorizador, Guid? articuloId, Guid? familiaId,
        decimal porcentaje, decimal monto)
    {
        ArgumentNullException.ThrowIfNull(topes);

        var alcance = new[]
            {
                articuloId is null ? [] : topes.Where(t => t.ArticuloId == articuloId).ToList(),
                familiaId is null ? [] : topes.Where(t => t.ArticuloId is null && t.FamiliaId == familiaId).ToList(),
                topes.Where(t => t.ArticuloId is null && t.FamiliaId is null).ToList(),
            }
            .FirstOrDefault(grupo => grupo.Count > 0);

        if (alcance is null)
            return new EvaluacionTope(false, null, null, null, SinConfiguracion: true);

        var tope = alcance.Where(t => t.Nivel <= nivelAutorizador).MaxBy(t => t.Nivel);
        if (tope is null)
            return new EvaluacionTope(false, null, 0m, 0m);

        var permitido = (tope.PorcentajeMaximo is not { } maximo || porcentaje <= maximo)
                        && (tope.MontoMaximo is not { } montoMaximo || monto <= montoMaximo);
        return new EvaluacionTope(permitido, tope.Nivel, tope.PorcentajeMaximo, tope.MontoMaximo);
    }
}
