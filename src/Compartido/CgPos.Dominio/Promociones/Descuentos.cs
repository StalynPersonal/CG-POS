using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Promociones;

/// <summary>Motivo seleccionable para los descuentos manuales (RF-203).</summary>
public sealed class MotivoDescuento : Entidad
{
    public const int LargoMaximoNombre = 100;

    private MotivoDescuento()
    {
    }

    public int Codigo { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;

    public static MotivoDescuento Crear(int codigo, string nombre, Guid? id = null)
    {
        var motivo = new MotivoDescuento
        {
            Id = id ?? Guid.CreateVersion7(),
            Codigo = Validar.Codigo(codigo, "Código de motivo"),
        };
        motivo.CambiarNombre(nombre);
        return motivo;
    }

    public void CambiarNombre(string nombre) => Nombre = Validar.Texto(nombre, "Nombre de motivo", LargoMaximoNombre);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;
}

/// <summary>
/// Tope de descuento manual por nivel de usuario, general o para un departamento, una categoría, una marca o un artículo (RF-202, RN-10).
/// A mayor nivel del autorizador, mayor el descuento permitido.
/// </summary>
public sealed class TopeDescuento : Entidad
{
    private TopeDescuento()
    {
    }

    /// <summary>Código numérico con que se sincroniza entre el Central y las cajas.</summary>
    public int Codigo { get; private set; }

    public int Nivel { get; private set; }
    public Guid? DepartamentoId { get; private set; }
    public Guid? CategoriaId { get; private set; }
    public Guid? MarcaId { get; private set; }
    public Guid? ArticuloId { get; private set; }

    /// <summary>Sin departamento, categoría, marca ni artículo: aplica a todo.</summary>
    public bool EsGeneral => DepartamentoId is null && CategoriaId is null && MarcaId is null && ArticuloId is null;
    public decimal? PorcentajeMaximo { get; private set; }
    public decimal? MontoMaximo { get; private set; }

    public static TopeDescuento Crear(int codigo, int nivel, decimal? porcentajeMaximo, decimal? montoMaximo, Guid? departamentoId = null, Guid? articuloId = null,
        Guid? id = null, Guid? categoriaId = null, Guid? marcaId = null)
    {
        var tope = new TopeDescuento { Id = id ?? Guid.CreateVersion7(), Codigo = Validar.Codigo(codigo, "Código del tope") };
        tope.Actualizar(nivel, porcentajeMaximo, montoMaximo, departamentoId, articuloId, categoriaId, marcaId);
        return tope;
    }

    public void Actualizar(int nivel, decimal? porcentajeMaximo, decimal? montoMaximo, Guid? departamentoId, Guid? articuloId, Guid? categoriaId = null,
        Guid? marcaId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(nivel, 1);
        if (new[] { departamentoId, categoriaId, marcaId, articuloId }.Count(id => id is not null) > 1)
            throw new ArgumentException("Un tope es para un departamento, una categoría, una marca o un artículo, no para varios a la vez.");
        if (porcentajeMaximo is null && montoMaximo is null)
            throw new ArgumentException("Indique el porcentaje o el monto máximo del tope.");
        if (porcentajeMaximo is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(porcentajeMaximo), porcentajeMaximo, "El porcentaje máximo debe estar entre 0 y 100.");
        if (montoMaximo < 0)
            throw new ArgumentOutOfRangeException(nameof(montoMaximo), montoMaximo, "El monto máximo no puede ser negativo.");

        Nivel = nivel;
        DepartamentoId = departamentoId;
        CategoriaId = categoriaId;
        MarcaId = marcaId;
        ArticuloId = articuloId;
        PorcentajeMaximo = porcentajeMaximo;
        MontoMaximo = montoMaximo;
    }
}

/// <param name="NivelTope">Nivel del tope que se aplicó; nulo si no hay topes para ese alcance.</param>
/// <param name="SinConfiguracion">No hay ningún tope que aplique (ni del artículo, ni de su categoría, marca o departamento, ni general).</param>
public sealed record EvaluacionTope(bool Permitido, int? NivelTope, decimal? PorcentajeMaximo, decimal? MontoMaximo, bool SinConfiguracion = false);

public static class ReglasTopeDescuento
{
    /// <summary>
    /// Toma el alcance más específico que tenga topes (artículo, categoría, marca, departamento y por último general) y, dentro de él, el tope del
    /// nivel más alto que no supere el del autorizador. Si ese alcance solo tiene topes de niveles superiores, el descuento
    /// exige a alguien de mayor nivel. Sin ningún tope configurado el descuento manual no se permite: el límite lo define el negocio.
    /// </summary>
    /// <param name="articuloId">Nulo para el descuento a la factura, que solo usa topes generales.</param>
    public static EvaluacionTope Evaluar(IReadOnlyCollection<TopeDescuento> topes, int nivelAutorizador, Guid? articuloId, Guid? departamentoId,
        decimal porcentaje, decimal monto, Guid? categoriaId = null, Guid? marcaId = null)
    {
        ArgumentNullException.ThrowIfNull(topes);

        var alcance = new[]
            {
                articuloId is null ? [] : topes.Where(t => t.ArticuloId == articuloId).ToList(),
                categoriaId is null ? [] : topes.Where(t => t.CategoriaId == categoriaId).ToList(),
                marcaId is null ? [] : topes.Where(t => t.MarcaId == marcaId).ToList(),
                departamentoId is null ? [] : topes.Where(t => t.DepartamentoId == departamentoId).ToList(),
                topes.Where(t => t.EsGeneral).ToList(),
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
