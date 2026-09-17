using CgPos.Dominio.Promociones;

namespace CgPos.Dominio.Pruebas.Ventas;

public class TopesDescuentoPruebas
{
    private static readonly Guid Ferreteria = Guid.CreateVersion7();
    private static readonly Guid Taladro = Guid.CreateVersion7();

    private static readonly TopeDescuento[] Topes =
    [
        TopeDescuento.Crear(2, 10m, null),                        // supervisor: 10 % general
        TopeDescuento.Crear(3, 30m, 5000m),                       // gerente: 30 % y hasta RD$5,000
        TopeDescuento.Crear(2, 5m, null, departamentoId: Ferreteria),  // ferretería más estricta para supervisor
        TopeDescuento.Crear(3, 3m, null, articuloId: Taladro),    // el taladro solo lo descuenta un gerente, hasta 3 %
    ];

    [Fact]
    public void Mayor_nivel_permite_mayor_descuento()
    {
        Assert.True(ReglasTopeDescuento.Evaluar(Topes, 2, null, null, 10m, 100m).Permitido);
        Assert.False(ReglasTopeDescuento.Evaluar(Topes, 2, null, null, 12m, 100m).Permitido);
        Assert.True(ReglasTopeDescuento.Evaluar(Topes, 3, null, null, 12m, 100m).Permitido);
        Assert.False(ReglasTopeDescuento.Evaluar(Topes, 3, null, null, 20m, 6000m).Permitido); // supera el monto máximo
    }

    [Fact]
    public void Se_usa_el_alcance_mas_especifico_con_topes()
    {
        var departamento = ReglasTopeDescuento.Evaluar(Topes, 2, Guid.CreateVersion7(), Ferreteria, 8m, 50m);
        Assert.False(departamento.Permitido);
        Assert.Equal(5m, departamento.PorcentajeMaximo);

        // El departamento solo tiene tope de nivel 2: un gerente usa ese mismo tope de departamento (el más alto que no supera su nivel).
        Assert.False(ReglasTopeDescuento.Evaluar(Topes, 3, Guid.CreateVersion7(), Ferreteria, 8m, 50m).Permitido);

        // El taladro solo tiene tope de nivel 3: un supervisor no puede descontarlo.
        var supervisor = ReglasTopeDescuento.Evaluar(Topes, 2, Taladro, Ferreteria, 1m, 10m);
        Assert.False(supervisor.Permitido);
        Assert.True(ReglasTopeDescuento.Evaluar(Topes, 3, Taladro, Ferreteria, 3m, 200m).Permitido);
    }

    [Fact]
    public void Sin_topes_configurados_no_se_permite_el_descuento_manual()
    {
        var evaluacion = ReglasTopeDescuento.Evaluar([], 9, Taladro, Ferreteria, 1m, 1m);

        Assert.False(evaluacion.Permitido);
        Assert.True(evaluacion.SinConfiguracion);
    }
}
