using CgPos.Central.Aplicacion.Seguridad;

namespace CgPos.Central.Pruebas.Seguridad;

public class ReglasContrasenaPruebas
{
    [Theory]
    [InlineData("12345", 6, null, "La clave debe tener al menos 6 caracteres.")]
    [InlineData("123456", 6, null, null)]
    [InlineData("1234567890123", 6, null, null)] // sin máximo no hay tope
    [InlineData("12345678", 6, 8, null)]
    [InlineData("123456789", 6, 8, "La clave no puede tener más de 8 caracteres.")]
    [InlineData("12345", 4, 4, "La clave debe tener 4 caracteres.")] // mínimo y máximo iguales: largo exacto
    [InlineData("123", 4, 4, "La clave debe tener 4 caracteres.")]
    [InlineData(null, 4, null, "La clave debe tener al menos 4 caracteres.")]
    public void El_largo_se_valida_entre_el_minimo_y_el_maximo_configurados(string? clave, int minimo, int? maximo, string? esperado) =>
        Assert.Equal(esperado, ReglasContrasena.ValidarLargo(clave, minimo, maximo, "La clave"));

    [Fact]
    public void Un_maximo_menor_que_el_minimo_se_informa_como_error_de_configuracion() =>
        Assert.Contains("corríjalo en Parámetros", ReglasContrasena.ValidarLargo("123456", 8, 6, "La clave"));

    [Fact]
    public void La_contrasena_del_central_respeta_el_largo_y_la_complejidad()
    {
        Assert.Equal("La contraseña no puede tener más de 12 caracteres.", ReglasContrasena.Validar("Clave.Segura#2026", 10, 12, true, "ADMIN"));
        Assert.Null(ReglasContrasena.Validar("Segura#2026", 10, 12, true, "ADMIN"));
        Assert.Equal("La contraseña debe combinar mayúsculas, minúsculas, números y símbolos.", ReglasContrasena.Validar("segura20260", 10, null, true, "ADMIN"));
    }
}
