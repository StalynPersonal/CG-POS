using System.Text.RegularExpressions;
using CgPos.Dominio.Seguridad;

namespace CgPos.Dominio.Pruebas.Seguridad;

public class RolYPermisosPruebas
{
    [Fact]
    public void Catalogo_tiene_codigos_unicos_con_formato_modulo_accion()
    {
        var codigos = CatalogoPermisos.Todos.Select(p => p.Codigo).ToList();

        Assert.NotEmpty(codigos);
        Assert.Equal(codigos.Count, codigos.Distinct(StringComparer.Ordinal).Count());
        Assert.All(CatalogoPermisos.Todos, p =>
        {
            Assert.Matches(new Regex(@"^[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+$"), p.Codigo);
            Assert.StartsWith(p.Modulo + ".", p.Codigo);
            Assert.False(string.IsNullOrWhiteSpace(p.Descripcion));
            Assert.True(p.Codigo.Length <= Permiso.LargoMaximoCodigo);
        });
    }

    [Fact]
    public void Rol_concede_solo_los_permisos_asignados()
    {
        var supervisor = Rol.Crear("SUPERVISOR", "Supervisor", nivel: 2);

        supervisor.AsignarPermiso(CatalogoPermisos.EliminarLinea);
        supervisor.AsignarPermiso(CatalogoPermisos.EliminarLinea); // repetir no duplica
        supervisor.AsignarPermiso(CatalogoPermisos.AutorizarOperaciones);

        Assert.Equal(2, supervisor.PermisosAsignados.Count);
        Assert.True(supervisor.TienePermiso(CatalogoPermisos.EliminarLinea));
        Assert.False(supervisor.TienePermiso(CatalogoPermisos.ReabrirCierre));

        supervisor.QuitarPermiso(CatalogoPermisos.EliminarLinea);
        Assert.False(supervisor.TienePermiso(CatalogoPermisos.EliminarLinea));
    }

    [Fact]
    public void Rol_inactivo_no_concede_permisos()
    {
        var rol = Rol.Crear("CAJERO", "Cajero", nivel: 1);
        rol.AsignarPermiso(CatalogoPermisos.RegistrarVenta);

        rol.Desactivar();

        Assert.False(rol.TienePermiso(CatalogoPermisos.RegistrarVenta));
    }

    [Fact]
    public void No_se_asignan_permisos_fuera_del_catalogo()
    {
        var rol = Rol.Crear("CAJERO", "Cajero", nivel: 1);

        Assert.Throws<ArgumentException>(() => rol.AsignarPermiso("Ventas.HacerMagia"));
        Assert.Throws<ArgumentException>(() => Permiso.Crear(new DefinicionPermiso("Ventas.HacerMagia", "Ventas", "No existe")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Nivel_debe_estar_en_rango(int nivel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rol.Crear("X", "X", nivel));
    }
}
