using CgPos.Dominio.Seguridad;

namespace CgPos.Dominio.Pruebas.Seguridad;

public class UsuarioPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 8, 0, 0, TimeSpan.FromHours(-4));
    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(5);

    private static Usuario CrearUsuario() => Usuario.Crear("C001", "Cajera Prueba", Ids.Siguiente());

    [Fact]
    public void Crear_limpia_espacios_y_valida_datos_obligatorios()
    {
        var usuario = Usuario.Crear("  C001 ", "  Ana Pérez ", Ids.Siguiente());

        Assert.Equal("C001", usuario.Codigo);
        Assert.Equal("Ana Pérez", usuario.Nombre);
        Assert.True(usuario.Activo);

        Assert.Throws<ArgumentException>(() => Usuario.Crear("", "Ana", Ids.Siguiente()));
        Assert.Throws<ArgumentException>(() => Usuario.Crear("C002", " ", Ids.Siguiente()));
        Assert.Throws<ArgumentException>(() => Usuario.Crear("C003", "Ana", 0));
        Assert.Throws<ArgumentException>(() => Usuario.Crear(new string('X', Usuario.LargoMaximoCodigo + 1), "Ana", Ids.Siguiente()));
    }

    [Fact]
    public void Solo_opera_las_cajas_asignadas_y_estando_activo()
    {
        var usuario = CrearUsuario();
        var caja01 = Ids.Siguiente();
        var caja02 = Ids.Siguiente();

        usuario.AsignarCaja(caja01);
        usuario.AsignarCaja(caja01); // repetir no duplica

        Assert.Single(usuario.CajasAsignadas);
        Assert.True(usuario.PuedeOperarCaja(caja01));
        Assert.False(usuario.PuedeOperarCaja(caja02));

        usuario.Desactivar();
        Assert.False(usuario.PuedeOperarCaja(caja01));

        usuario.Activar();
        usuario.QuitarCaja(caja01);
        Assert.False(usuario.PuedeOperarCaja(caja01));
    }

    [Fact]
    public void Se_bloquea_al_alcanzar_los_intentos_maximos()
    {
        var usuario = CrearUsuario();

        Assert.False(usuario.RegistrarIngresoFallido(Ahora, intentosMaximos: 3, Bloqueo));
        Assert.False(usuario.RegistrarIngresoFallido(Ahora, intentosMaximos: 3, Bloqueo));
        Assert.False(usuario.EstaBloqueado(Ahora));

        Assert.True(usuario.RegistrarIngresoFallido(Ahora, intentosMaximos: 3, Bloqueo));

        Assert.True(usuario.EstaBloqueado(Ahora.AddMinutes(4)));
        Assert.False(usuario.EstaBloqueado(Ahora.AddMinutes(5))); // vence exactamente al cumplirse el tiempo
        Assert.Equal(0, usuario.IntentosFallidos);
    }

    [Fact]
    public void Ingreso_exitoso_reinicia_intentos_y_registra_la_fecha()
    {
        var usuario = CrearUsuario();
        usuario.RegistrarIngresoFallido(Ahora, 3, Bloqueo);
        usuario.RegistrarIngresoFallido(Ahora, 3, Bloqueo);

        usuario.RegistrarIngresoExitoso(Ahora.AddMinutes(1));

        Assert.Equal(0, usuario.IntentosFallidos);
        Assert.Null(usuario.BloqueadoHasta);
        Assert.Equal(Ahora.AddMinutes(1), usuario.UltimoIngresoEn);
    }

    [Fact]
    public void Hash_de_la_clave_es_obligatorio_y_con_largo_maximo()
    {
        var usuario = CrearUsuario();

        usuario.EstablecerClaveHash("PBKDF2-SHA256$100000$sal$hash");
        Assert.Equal("PBKDF2-SHA256$100000$sal$hash", usuario.ClaveHash);

        Assert.Throws<ArgumentException>(() => usuario.EstablecerClaveHash(" "));
        Assert.Throws<ArgumentException>(() => usuario.EstablecerClaveHash(new string('a', Usuario.LargoMaximoHashClave + 1)));
    }
}
