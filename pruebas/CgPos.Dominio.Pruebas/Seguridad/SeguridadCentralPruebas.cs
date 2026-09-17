using System.Security.Cryptography;
using System.Text;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;

namespace CgPos.Dominio.Pruebas.Seguridad;

public class SeguridadCentralPruebas
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 15, 8, 0, 0, TimeSpan.FromHours(-4));

    private static string HashDe(string texto) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    [Fact]
    public void Rotar_marca_el_token_usado_y_el_nuevo_conserva_la_sesion_sin_pasar_del_fin()
    {
        var usuarioId = Ids.Siguiente();
        var sesion = SesionCentral.Iniciar(usuarioId, HashDe("uno"), Inicio, TimeSpan.FromMinutes(60), TimeSpan.FromHours(2), "10.0.0.1", "Pruebas");

        Assert.Equal(sesion.Id, sesion.Familia);
        Assert.Equal(Inicio.AddMinutes(60), sesion.ExpiraEn);

        var segunda = sesion.Rotar(HashDe("dos"), Inicio.AddMinutes(50), TimeSpan.FromMinutes(90), null, null);

        Assert.True(sesion.FueUsada);
        Assert.Equal(segunda.Id, sesion.ReemplazadaPorId);
        Assert.False(sesion.EstaVigente(Inicio.AddMinutes(50)));
        Assert.Equal(sesion.Familia, segunda.Familia);
        Assert.Equal(Inicio.AddHours(2), segunda.FinSesion);
        // La inactividad llevaría a las 10:20, pero la sesión termina a las 10:00.
        Assert.Equal(Inicio.AddHours(2), segunda.ExpiraEn);
        Assert.True(segunda.EstaVigente(Inicio.AddMinutes(100)));
    }

    [Fact]
    public void Un_token_usado_vencido_o_revocado_no_se_puede_rotar()
    {
        var sesion = SesionCentral.Iniciar(Ids.Siguiente(), HashDe("uno"), Inicio, TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), null, null);

        Assert.Throws<InvalidOperationException>(() => sesion.Rotar(HashDe("dos"), Inicio.AddMinutes(31), TimeSpan.FromMinutes(30), null, null));

        var otra = SesionCentral.Iniciar(Ids.Siguiente(), HashDe("tres"), Inicio, TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), null, null);
        otra.Rotar(HashDe("cuatro"), Inicio.AddMinutes(5), TimeSpan.FromMinutes(30), null, null);
        Assert.Throws<InvalidOperationException>(() => otra.Rotar(HashDe("cinco"), Inicio.AddMinutes(6), TimeSpan.FromMinutes(30), null, null));

        var revocada = SesionCentral.Iniciar(Ids.Siguiente(), HashDe("seis"), Inicio, TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), null, null);
        revocada.Revocar(Inicio.AddMinutes(1), "Sesión cerrada");
        revocada.Revocar(Inicio.AddMinutes(2), "Otro motivo");
        Assert.Equal("Sesión cerrada", revocada.MotivoRevocacion);
        Assert.False(revocada.EstaVigente(Inicio.AddMinutes(3)));
    }

    [Fact]
    public void El_hash_del_token_debe_ser_sha256_hexadecimal()
    {
        Assert.Throws<ArgumentException>(() =>
            SesionCentral.Iniciar(Ids.Siguiente(), "token-en-claro", Inicio, TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), null, null));
        Assert.Throws<ArgumentException>(() =>
            CredencialDispositivo.Emitir(Ids.Siguiente(), "secreto", Inicio, "Administrador"));
    }

    [Fact]
    public void Usuario_del_central_se_bloquea_al_llegar_al_maximo_de_intentos()
    {
        var usuario = UsuarioCentral.Crear("ADMIN", "Administrador", "admin@empresa.do", Ids.Siguiente(), "PBKDF2-SHA256$600000$sal$hash", debeCambiarContrasena: true);

        Assert.False(usuario.RegistrarIngresoFallido(Inicio, 3, TimeSpan.FromMinutes(15)));
        Assert.False(usuario.RegistrarIngresoFallido(Inicio, 3, TimeSpan.FromMinutes(15)));
        Assert.True(usuario.RegistrarIngresoFallido(Inicio, 3, TimeSpan.FromMinutes(15)));
        Assert.True(usuario.EstaBloqueado(Inicio.AddMinutes(14)));
        Assert.False(usuario.EstaBloqueado(Inicio.AddMinutes(15)));

        usuario.CambiarContrasena("PBKDF2-SHA256$600000$sal$otro", debeCambiar: false, Inicio);
        Assert.False(usuario.DebeCambiarContrasena);
        Assert.Equal(Inicio, usuario.ContrasenaCambiadaEn);
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("@empresa.do")]
    [InlineData("usuario@")]
    [InlineData("con espacio@empresa.do")]
    public void Correo_invalido_se_rechaza(string correo) =>
        Assert.Throws<ArgumentException>(() => UsuarioCentral.Crear("U1", "Usuario", correo, Ids.Siguiente(), "hash", false));

    [Fact]
    public void Rol_del_central_solo_acepta_permisos_de_su_catalogo()
    {
        var rol = RolCentral.Crear("AUDITOR", "Auditor");
        rol.AsignarPermiso(CatalogoPermisosCentral.ConsultarReportes);
        rol.AsignarPermiso(CatalogoPermisosCentral.ConsultarReportes);

        Assert.Single(rol.PermisosAsignados);
        Assert.Throws<ArgumentException>(() => rol.AsignarPermiso(CatalogoPermisos.RegistrarVenta));

        rol.Desactivar();
        Assert.False(rol.TienePermiso(CatalogoPermisosCentral.ConsultarReportes));
    }

    [Fact]
    public void Catalogo_del_central_tiene_codigos_unicos_con_prefijo_central()
    {
        var codigos = CatalogoPermisosCentral.Todos.Select(p => p.Codigo).ToList();

        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codigos, codigo => Assert.StartsWith("Central.", codigo, StringComparison.Ordinal));
        Assert.DoesNotContain(codigos, CatalogoPermisos.Existe);
    }

    [Fact]
    public void Credencial_de_dispositivo_revocada_deja_de_estar_activa()
    {
        var credencial = CredencialDispositivo.Emitir(Ids.Siguiente(), HashDe("secreto"), Inicio, "Administrador");
        credencial.RegistrarUso(Inicio.AddMinutes(1), "192.168.1.20");

        Assert.True(credencial.Activa);
        Assert.Equal("192.168.1.20", credencial.UltimaIp);

        credencial.Revocar(Inicio.AddMinutes(2), "Equipo reemplazado");
        Assert.False(credencial.Activa);
        Assert.Throws<ArgumentException>(() => CredencialDispositivo.Emitir(Ids.Siguiente(), HashDe("x"), Inicio, " "));
    }
}
