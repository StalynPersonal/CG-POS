using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Infraestructura;

/// <summary>Verifica contra SQL Server real el mapeo de organización y seguridad (relaciones, colecciones e índices únicos).</summary>
public class OrganizacionSeguridadPersistenciaPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    [SkippableFact]
    public async Task Guarda_y_recupera_la_estructura_completa_con_permisos_y_cajas()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var sufijo = Guid.NewGuid().ToString("N")[..6];

        Empresa empresa;
        Sucursal sucursal;
        Caja caja;
        Rol rol;
        Usuario usuario;

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            await AsegurarCatalogoPermisosAsync(contexto);

            // Cada entidad recibe su Id al agregarla al contexto, antes de crear la que la referencia.
            empresa = Empresa.Crear(RncUnico(), "Contreras Group SRL");
            contexto.Add(empresa);
            sucursal = Sucursal.Crear(empresa.Id, 1, "Sucursal Principal");
            contexto.Add(sucursal);
            caja = Caja.Crear(sucursal.Id, 1, "Caja 01");
            contexto.Add(caja);
            rol = Rol.Crear($"SUP{sufijo}", "Supervisor", nivel: 2);
            rol.AsignarPermiso(CatalogoPermisos.AutorizarOperaciones);
            rol.AsignarPermiso(CatalogoPermisos.EliminarLinea);
            contexto.Add(rol);
            usuario = Usuario.Crear($"U{sufijo}", "Supervisor Prueba", rol.Id);
            usuario.AsignarCaja(caja.Id);
            usuario.EstablecerClaveHash($"PBKDF2-SHA256$100000$sal{sufijo}$hash");
            contexto.Add(usuario);
            contexto.Parametros.Add(Parametro.Crear("Seguridad.IntentosMaximosClave", "3", cajaId: caja.Id));
            await contexto.SaveChangesAsync();
        }

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

            var rolLeido = await contexto.Roles.Include(r => r.PermisosAsignados).SingleAsync(r => r.Id == rol.Id);
            Assert.True(rolLeido.TienePermiso(CatalogoPermisos.AutorizarOperaciones));
            Assert.True(rolLeido.TienePermiso(CatalogoPermisos.EliminarLinea));
            Assert.False(rolLeido.TienePermiso(CatalogoPermisos.ReabrirCierre));

            var usuarioLeido = await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleAsync(u => u.Id == usuario.Id);
            Assert.True(usuarioLeido.PuedeOperarCaja(caja.Id));
            Assert.Equal(usuario.ClaveHash, usuarioLeido.ClaveHash);

            var cajaLeida = await contexto.Cajas.SingleAsync(c => c.Id == caja.Id);
            Assert.True(cajaLeida.Habilitada);
            Assert.Equal("3", (await contexto.Parametros.SingleAsync(p => p.CajaId == caja.Id)).Valor);
        }
    }

    [SkippableFact]
    public async Task Codigo_de_usuario_repetido_se_rechaza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var sufijo = Guid.NewGuid().ToString("N")[..6];
        var rol = Rol.Crear($"CAJ{sufijo}", "Cajero", nivel: 1);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        contexto.Roles.Add(rol);
        contexto.Usuarios.Add(Usuario.Crear($"U{sufijo}", "Primero", rol.Id));
        contexto.Usuarios.Add(Usuario.Crear($"U{sufijo}", "Repetido", rol.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task Parametro_con_sucursal_y_caja_a_la_vez_lo_rechaza_la_base()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        // Sucursal y caja reales: así el único motivo posible de rechazo es la restricción de ámbito, no las llaves foráneas.
        // Cada entidad recibe su Id al agregarla al contexto, antes de crear la que la referencia.
        var empresa = Empresa.Crear(RncUnico(), "Empresa Ámbito");
        contexto.Add(empresa);
        var sucursal = Sucursal.Crear(empresa.Id, 1, "Sucursal Ámbito");
        contexto.Add(sucursal);
        var caja = Caja.Crear(sucursal.Id, 1, "Caja Ámbito");
        contexto.Add(caja);
        await contexto.SaveChangesAsync();

        // El dominio ya lo impide; se verifica que la base también lo garantice (defensa en profundidad).
        var insertar = () => contexto.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Parametros (Id, Clave, Valor, SucursalId, CajaId) VALUES (NEXT VALUE FOR SecuenciaParametros, 'Prueba.Ambito', '1', {sucursal.Id}, {caja.Id})");

        var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(insertar);
        Assert.Contains("CK_Parametros_UnSoloAmbito", error.Message);
    }

    private static async Task AsegurarCatalogoPermisosAsync(ContextoDatosPos contexto)
    {
        var existentes = await contexto.Permisos.Select(p => p.Codigo).ToListAsync();
        contexto.Permisos.AddRange(CatalogoPermisos.Todos.Where(d => !existentes.Contains(d.Codigo)).Select(Permiso.Crear));
    }

    private static string RncUnico() => Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();
}
