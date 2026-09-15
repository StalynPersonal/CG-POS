using CgPos.Dominio.Seguridad;

namespace CgPos.Dominio.Pruebas.Seguridad;

public class UsuarioPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 8, 0, 0, TimeSpan.FromHours(-4));
    private static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(5);

    private static Usuario CrearUsuario() => Usuario.Crear("C001", "Cajera Prueba", Guid.CreateVersion7());

    [Fact]
    public void Crear_limpia_espacios_y_valida_datos_obligatorios()
    {
        var usuario = Usuario.Crear("  C001 ", "  Ana Pérez ", Guid.CreateVersion7());

        Assert.Equal("C001", usuario.Codigo);
        Assert.Equal("Ana Pérez", usuario.Nombre);
        Assert.True(usuario.Activo);

        Assert.Throws<ArgumentException>(() => Usuario.Crear("", "Ana", Guid.CreateVersion7()));
        Assert.Throws<ArgumentException>(() => Usuario.Crear("C002", " ", Guid.CreateVersion7()));
        Assert.Throws<ArgumentException>(() => Usuario.Crear("C003", "Ana", Guid.Empty));
        Assert.Throws<ArgumentException>(() => Usuario.Crear(new string('X', Usuario.LargoMaximoCodigo + 1), "Ana", Guid.CreateVersion7()));
    }

    [Fact]
    public void Solo_opera_las_cajas_asignadas_y_estando_activo()
    {
        var usuario = CrearUsuario();
        var caja01 = Guid.CreateVersion7();
        var caja02 = Guid.CreateVersion7();

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
    public void Credencial_de_barras_solo_acepta_hash_sha256_hex()
    {
        var usuario = CrearUsuario();
        var hash = new string('a', Usuario.LargoHashCredencialBarras);

        usuario.EstablecerCredencialBarrasHash(hash);
        Assert.Equal(hash.ToUpperInvariant(), usuario.CredencialBarrasHash);

        usuario.EstablecerCredencialBarrasHash(null);
        Assert.Null(usuario.CredencialBarrasHash);

        Assert.Throws<ArgumentException>(() => usuario.EstablecerCredencialBarrasHash("123456"));
        Assert.Throws<ArgumentException>(() => usuario.EstablecerCredencialBarrasHash(new string('Z', Usuario.LargoHashCredencialBarras)));
    }
}
