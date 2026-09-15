using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;

namespace CgPos.Pos.Pruebas.Seguridad;

/// <summary>Ingreso a la caja contra SQL Server real (PIN, carné, huella, bloqueo y estado de caja/usuario).</summary>
public class AutenticacionPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private static readonly Guid Empresa = Guid.CreateVersion7();

    [SkippableFact]
    public async Task Pin_correcto_inicia_sesion_con_los_permisos_del_rol_y_queda_auditado()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;

        var resultado = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Pin(escenario.CodigoCajero, EscenarioSeguridad.PinCajero));

        Assert.True(resultado.Exitoso, resultado.Motivo?.ToString());
        Assert.Equal(escenario.Cajero, resultado.Sesion!.UsuarioId);
        Assert.Equal(escenario.CajaUno, resultado.Sesion.CajaId);
        Assert.Equal(1, resultado.Sesion.Nivel);
        Assert.True(resultado.Sesion.TienePermiso(CatalogoPermisos.RegistrarVenta));
        Assert.False(resultado.Sesion.TienePermiso(CatalogoPermisos.EliminarLinea));
        Assert.Equal(1, await EscenarioSeguridad.ContarAuditoriaAsync(proveedor, "Seguridad.IngresoExitoso", escenario.Cajero));
    }

    [SkippableFact]
    public async Task Tres_pines_incorrectos_bloquean_y_el_bloqueo_vence_con_el_tiempo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        var (proveedor, reloj) = escenario.CrearProveedor(escenario.CajaUno);
        await using var proveedorUsado = proveedor;
        var pinIncorrecto = new CredencialUsuario.Pin(escenario.CodigoCajero, "9999");
        var pinCorrecto = new CredencialUsuario.Pin(escenario.CodigoCajero, EscenarioSeguridad.PinCajero);

        Assert.Equal(MotivoRechazoIngreso.CredencialesInvalidas, (await EscenarioSeguridad.IngresarAsync(proveedor, pinIncorrecto)).Motivo);
        Assert.Equal(MotivoRechazoIngreso.CredencialesInvalidas, (await EscenarioSeguridad.IngresarAsync(proveedor, pinIncorrecto)).Motivo);

        var tercero = await EscenarioSeguridad.IngresarAsync(proveedor, pinIncorrecto);
        Assert.Equal(MotivoRechazoIngreso.UsuarioBloqueado, tercero.Motivo);
        Assert.Equal(EscenarioSeguridad.Inicio.AddMinutes(5), tercero.BloqueadoHasta);

        // Bloqueado: ni con el PIN correcto entra.
        Assert.Equal(MotivoRechazoIngreso.UsuarioBloqueado, (await EscenarioSeguridad.IngresarAsync(proveedor, pinCorrecto)).Motivo);

        reloj.Avanzar(TimeSpan.FromMinutes(5));
        Assert.True((await EscenarioSeguridad.IngresarAsync(proveedor, pinCorrecto)).Exitoso);
        Assert.True(await EscenarioSeguridad.ContarAuditoriaAsync(proveedor, "Seguridad.IngresoBloqueado", escenario.Cajero) >= 1);
    }

    [SkippableFact]
    public async Task Usuario_inexistente_recibe_el_mismo_mensaje_que_un_pin_incorrecto()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;

        var inexistente = new CredencialUsuario.Pin($"NOEXISTE{escenario.Sufijo}", EscenarioSeguridad.PinCajero);
        var incorrecto = new CredencialUsuario.Pin(escenario.CodigoCajero, "0000");

        var resultadoInexistente = await EscenarioSeguridad.IngresarAsync(proveedor, inexistente);
        var resultadoIncorrecto = await EscenarioSeguridad.IngresarAsync(proveedor, incorrecto);

        Assert.Equal(MotivoRechazoIngreso.CredencialesInvalidas, resultadoInexistente.Motivo);
        Assert.Equal(resultadoIncorrecto.Motivo, resultadoInexistente.Motivo);
        Assert.Equal(
            MensajesSeguridad.Para(resultadoIncorrecto.Motivo!.Value, incorrecto),
            MensajesSeguridad.Para(resultadoInexistente.Motivo!.Value, inexistente));
    }

    [SkippableFact]
    public async Task Carne_reconocido_inicia_sesion_y_desconocido_se_rechaza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;

        var conCarne = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Carne($"  {escenario.CarneCajero} "));
        var desconocido = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Carne("CARNE-QUE-NO-EXISTE"));

        Assert.True(conCarne.Exitoso, conCarne.Motivo?.ToString());
        Assert.Equal(escenario.Cajero, conCarne.Sesion!.UsuarioId);
        Assert.Equal(MotivoRechazoIngreso.CredencialesInvalidas, desconocido.Motivo);
    }

    [SkippableFact]
    public async Task Huella_identificada_por_el_lector_inicia_sesion()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var conHuella = escenario.CrearProveedor(escenario.CajaUno, usuarioHuella: escenario.Cajero).Proveedor;
        await using var sinHuella = escenario.CrearProveedor(escenario.CajaUno, usuarioHuella: null).Proveedor;

        Assert.True((await EscenarioSeguridad.IngresarAsync(conHuella, new CredencialUsuario.Huella())).Exitoso);
        Assert.Equal(MotivoRechazoIngreso.CredencialesInvalidas, (await EscenarioSeguridad.IngresarAsync(sinHuella, new CredencialUsuario.Huella())).Motivo);
    }

    [SkippableFact]
    public async Task Caja_deshabilitada_no_permite_ingresar()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaDos).Proveedor;

        var resultado = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Pin(escenario.CodigoCajero, EscenarioSeguridad.PinCajero));

        Assert.Equal(MotivoRechazoIngreso.CajaDeshabilitada, resultado.Motivo);
    }

    [SkippableFact]
    public async Task Usuario_no_asignado_a_la_caja_no_ingresa()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;

        var resultado = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Pin(escenario.CodigoSinCaja, EscenarioSeguridad.PinSinCaja));

        Assert.Equal(MotivoRechazoIngreso.CajaNoAsignada, resultado.Motivo);
    }

    [SkippableFact]
    public async Task Usuario_inactivo_solo_se_revela_con_credencial_correcta()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(escenario.CajaUno).Proveedor;

        var correcto = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Pin(escenario.CodigoInactivo, EscenarioSeguridad.PinInactivo));
        var incorrecto = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Pin(escenario.CodigoInactivo, "0000"));

        Assert.Equal(MotivoRechazoIngreso.UsuarioInactivo, correcto.Motivo);
        Assert.Equal(MotivoRechazoIngreso.CredencialesInvalidas, incorrecto.Motivo);
    }

    [SkippableFact]
    public async Task Sin_caja_configurada_no_se_puede_ingresar()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = await EscenarioSeguridad.CrearAsync(baseDatos, Empresa);
        await using var proveedor = escenario.CrearProveedor(cajaId: null).Proveedor;

        var resultado = await EscenarioSeguridad.IngresarAsync(proveedor, new CredencialUsuario.Pin(escenario.CodigoCajero, EscenarioSeguridad.PinCajero));

        Assert.Equal(MotivoRechazoIngreso.CajaNoConfigurada, resultado.Motivo);
    }
}
