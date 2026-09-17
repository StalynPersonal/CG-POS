using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Seguridad;

/// <summary>Autorización de supervisor contra SQL Server real.</summary>
public class AutorizacionPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private static readonly Guid Empresa = Guid.CreateVersion7();

    [SkippableFact]
    public async Task Con_permiso_propio_no_se_requiere_supervisor()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);

        var resultado = await EscenarioSeguridad.AutorizarAsync(proveedor,
            new SolicitudAutorizacionSupervisor(cajero, CatalogoPermisos.RegistrarVenta, string.Empty, new CredencialUsuario(string.Empty, string.Empty)));

        Assert.True(resultado.Concedida);
        Assert.False(resultado.RequirioSupervisor);
    }

    [SkippableFact]
    public async Task Supervisor_autoriza_con_su_clave_y_queda_auditado_con_el_motivo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);

        var conPin = await EscenarioSeguridad.AutorizarAsync(proveedor, new SolicitudAutorizacionSupervisor(
            cajero, CatalogoPermisos.EliminarLinea, "Artículo mal escaneado",
            new CredencialUsuario(escenario.CodigoSupervisor, EscenarioSeguridad.ClaveSupervisor), "Factura", "FAC-0001"));

        Assert.True(conPin.Concedida, conPin.Motivo?.ToString());
        Assert.True(conPin.RequirioSupervisor);
        Assert.Equal(escenario.Supervisor, conPin.SupervisorId);

        await using var ambito = proveedor.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var registro = await contexto.Auditoria.SingleAsync(r => r.Accion == "Seguridad.AutorizacionConcedida" && r.EntidadId == "FAC-0001");
        Assert.Equal(escenario.Cajero, registro.UsuarioId);
        Assert.Equal(escenario.Supervisor, registro.AutorizadoPorId);
        Assert.Equal("Artículo mal escaneado", registro.Motivo);
        Assert.Equal("Factura", registro.TipoEntidad);
        Assert.Contains(CatalogoPermisos.EliminarLinea, registro.Detalle);
    }

    [SkippableFact]
    public async Task Un_cajero_no_puede_autorizar_a_otro()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);

        var resultado = await EscenarioSeguridad.AutorizarAsync(proveedor, new SolicitudAutorizacionSupervisor(
            cajero, CatalogoPermisos.EliminarLinea, "Prueba", new CredencialUsuario(escenario.CodigoCajeroDos, EscenarioSeguridad.ClaveCajeroDos)));

        Assert.False(resultado.Concedida);
        Assert.Equal(MotivoRechazoAutorizacion.SinPermisoParaAutorizar, resultado.Motivo);
    }

    [SkippableFact]
    public async Task Supervisor_sin_el_permiso_de_la_operacion_no_autoriza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);

        var resultado = await EscenarioSeguridad.AutorizarAsync(proveedor, new SolicitudAutorizacionSupervisor(
            cajero, CatalogoPermisos.VenderBajoPrecioMinimo, "Venta bajo el mínimo", new CredencialUsuario(escenario.CodigoSupervisor, EscenarioSeguridad.ClaveSupervisor)));

        Assert.Equal(MotivoRechazoAutorizacion.SinPermisoParaAutorizar, resultado.Motivo);
    }

    [SkippableFact]
    public async Task El_motivo_es_obligatorio()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);

        var resultado = await EscenarioSeguridad.AutorizarAsync(proveedor, new SolicitudAutorizacionSupervisor(
            cajero, CatalogoPermisos.EliminarLinea, "   ", new CredencialUsuario(escenario.CodigoSupervisor, EscenarioSeguridad.ClaveSupervisor)));

        Assert.Equal(MotivoRechazoAutorizacion.MotivoRequerido, resultado.Motivo);
    }

    [SkippableFact]
    public async Task Supervisor_de_nivel_inferior_al_solicitante_no_autoriza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var solicitanteNivelTres = (await SesionCajeroAsync(proveedor, escenario)) with { Nivel = 3 };

        var resultado = await EscenarioSeguridad.AutorizarAsync(proveedor, new SolicitudAutorizacionSupervisor(
            solicitanteNivelTres, CatalogoPermisos.EliminarLinea, "Prueba de nivel", new CredencialUsuario(escenario.CodigoSupervisor, EscenarioSeguridad.ClaveSupervisor)));

        Assert.Equal(MotivoRechazoAutorizacion.NivelInsuficiente, resultado.Motivo);
    }

    [SkippableFact]
    public async Task Clave_de_supervisor_incorrecta_cuenta_intentos_y_bloquea()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);
        var solicitud = new SolicitudAutorizacionSupervisor(
            cajero, CatalogoPermisos.EliminarLinea, "Prueba", new CredencialUsuario(escenario.CodigoSupervisor, "9999"));

        Assert.Equal(MotivoRechazoAutorizacion.CredencialesInvalidas, (await EscenarioSeguridad.AutorizarAsync(proveedor, solicitud)).Motivo);
        Assert.Equal(MotivoRechazoAutorizacion.CredencialesInvalidas, (await EscenarioSeguridad.AutorizarAsync(proveedor, solicitud)).Motivo);
        Assert.Equal(MotivoRechazoAutorizacion.SupervisorBloqueado, (await EscenarioSeguridad.AutorizarAsync(proveedor, solicitud)).Motivo);
    }

    [SkippableFact]
    public async Task Permiso_fuera_del_catalogo_se_rechaza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;
        var cajero = await SesionCajeroAsync(proveedor, escenario);

        var resultado = await EscenarioSeguridad.AutorizarAsync(proveedor, new SolicitudAutorizacionSupervisor(
            cajero, "Ventas.HacerMagia", "Prueba", new CredencialUsuario(escenario.CodigoSupervisor, EscenarioSeguridad.ClaveSupervisor)));

        Assert.Equal(MotivoRechazoAutorizacion.PermisoInexistente, resultado.Motivo);
    }

    private static async Task<SesionUsuario> SesionCajeroAsync(IServiceProvider proveedor, EscenarioSeguridad escenario)
    {
        var ingreso = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario(escenario.CodigoCajero, EscenarioSeguridad.ClaveCajero));
        Assert.True(ingreso.Exitoso, ingreso.Motivo?.ToString());
        return ingreso.Sesion!;
    }
}
