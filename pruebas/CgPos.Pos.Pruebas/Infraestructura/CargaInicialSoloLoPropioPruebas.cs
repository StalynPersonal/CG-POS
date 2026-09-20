using CgPos.Contratos.CargaInicial;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Infraestructura;

/// <summary>
/// Una caja configurada solo guarda lo suyo: su sucursal, su terminal y las asignaciones de usuario que le tocan. Lo de las
/// demás sucursales y cajas no le hace falta; cuando lo necesita (una factura de otra caja, por ejemplo) se lo pide al Central.
/// Va en su propia clase porque configura la caja, y eso cambia lo que ve cualquier otra prueba de la misma base.
/// </summary>
public class CargaInicialSoloLoPropioPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private const string SucursalPropia = "01";
    private const string SucursalAjena = "09";
    private const string CajaPropia = "01";
    private const string CajaAjena = "02";

    [SkippableFact]
    public async Task Descarta_las_sucursales_y_cajas_que_no_son_de_este_equipo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            var configuracion = ambito.ServiceProvider.GetRequiredService<IConfiguracionCaja>();
            var error = await configuracion.GuardarAsync(new SolicitudConfigurarCaja(
                SucursalPropia, CajaPropia, "10.12.1.101", "http://central:5280", "credencial-de-prueba", "ADMIN", "Clave.Pruebas"));
            Assert.Null(error);
        }

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarAsync(Paquete());
        }

        await using var lectura = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = lectura.ServiceProvider.GetRequiredService<ContextoDatosPos>();

        Assert.Equal(SucursalPropia, (await contexto.Sucursales.SingleAsync()).Codigo);

        var caja = await contexto.Cajas.SingleAsync();
        Assert.Equal((CajaPropia, SucursalPropia), (caja.Codigo, caja.SucursalCodigo));

        // El cajero baja con las dos cajas asignadas; aquí solo queda la de este equipo.
        var cajero = await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleAsync(u => u.Codigo == "C001");
        Assert.True(cajero.PuedeOperarCaja(caja.Id));
        Assert.Equal(caja.Id, Assert.Single(cajero.CajasAsignadas).CajaId);

        // Los parámetros de otra caja tampoco se guardan.
        Assert.Equal(caja.Id, (await contexto.Parametros.SingleAsync(p => p.CajaId != null)).CajaId);
    }

    private static PaqueteCargaInicial Paquete() =>
        new(
            new EmpresaCarga("999000004", "Empresa de Pruebas SRL"),
            Sucursales:
            [
                new SucursalCarga(SucursalPropia, "Sucursal propia"),
                new SucursalCarga(SucursalAjena, "Sucursal ajena"),
            ],
            Cajas:
            [
                new CajaCarga(SucursalPropia, CajaPropia, "Caja 01", DireccionIp: "10.12.1.101"),
                new CajaCarga(SucursalPropia, CajaAjena, "Caja 02", DireccionIp: "10.12.1.102"),
                new CajaCarga(SucursalAjena, CajaPropia, "Caja 01 de la otra sucursal", DireccionIp: "10.12.9.101"),
            ],
            Roles: [new RolCarga("CAJERO", "Cajero", 1, [CatalogoPermisos.RegistrarVenta, CatalogoPermisos.AbrirTurno])],
            Usuarios:
            [
                new UsuarioCarga("C001", "Cajero Prueba", "CAJERO",
                    [new CajaReferencia(SucursalPropia, CajaPropia), new CajaReferencia(SucursalPropia, CajaAjena), new CajaReferencia(SucursalAjena, CajaPropia)],
                    Clave: "Cajero.1111"),
            ],
            Parametros:
            [
                new ParametroCarga("Prueba.SoloLoPropio", "1", SucursalCodigo: SucursalPropia, CajaCodigo: CajaPropia),
                new ParametroCarga("Prueba.SoloLoPropio", "2", SucursalCodigo: SucursalPropia, CajaCodigo: CajaAjena),
            ]);
}
