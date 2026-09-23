using CgPos.Dominio.Organizacion;

namespace CgPos.Dominio.Pruebas.Organizacion;

public class CatalogoParametrosPruebas
{
    [Fact]
    public void Catalogo_tiene_claves_unicas_y_los_del_central_se_distinguen_por_prefijo()
    {
        var claves = CatalogoParametros.Todos.Select(d => d.Clave).ToList();

        Assert.Equal(claves.Count, claves.Distinct(StringComparer.Ordinal).Count());
        Assert.All(CatalogoParametros.Todos, d =>
            Assert.Equal(d.Alcance == AlcanceParametro.Central, d.Clave.StartsWith("Central.", StringComparison.Ordinal)));
        Assert.All(CatalogoParametros.Todos, d => Assert.True(d.Descripcion.Length <= Parametro.LargoMaximoDescripcion, d.Clave));
    }

    [Theory]
    [InlineData("Seguridad.IntentosMaximosClave", "3", null)]
    [InlineData("Seguridad.IntentosMaximosClave", "tres", "El valor debe ser un número entero.")]
    [InlineData("Seguridad.IntentosMaximosClave", "0", "El valor no puede ser menor que 1.")]
    [InlineData("Seguridad.IntentosMaximosClave", "", "El parámetro «Intentos de clave fallidos seguidos que bloquean al usuario de la caja» (Seguridad.IntentosMaximosClave) es obligatorio.")]
    [InlineData("Fiscal.TipoIngresos", "7", "El valor no puede ser mayor que 6.")]
    [InlineData("Fiscal.MontoIdentificacionConsumo", "250000.50", null)]
    [InlineData("Fiscal.MontoIdentificacionConsumo", "250 mil", "El valor debe ser un número (use punto decimal).")]
    [InlineData("Caja.FondoEnCuadre", "TRUE", null)]
    [InlineData("Caja.FondoEnCuadre", "sí", "El valor debe ser true o false.")]
    [InlineData("Caja.FondoPredeterminado", "", null)]
    [InlineData("Tickets.MensajePie", "¡Gracias por su compra!", null)]
    public void Valor_se_valida_segun_el_tipo_el_rango_y_si_es_obligatorio(string clave, string valor, string? problemaEsperado)
    {
        var definicion = CatalogoParametros.Buscar(clave);

        Assert.NotNull(definicion);
        Assert.Equal(problemaEsperado, definicion.ValidarValor(valor));
    }

    [Fact]
    public void Clave_fuera_del_catalogo_no_se_encuentra()
    {
        Assert.Null(CatalogoParametros.Buscar("Pruebas.NoExiste"));
        Assert.Null(CatalogoParametros.Buscar(null));
        Assert.NotNull(CatalogoParametros.Buscar("  Caja.FondoEnCuadre "));
    }
}
