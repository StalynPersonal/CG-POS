using CgPos.Contracts.Sincronizacion;
using CgPos.Pos.Application.Sincronizacion;

namespace CgPos.Pos.Tests.Sincronizacion;

public class MensajeOutboxTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(-4));

    private static MensajeOutbox CrearMensaje(string contenido = """{"numero":"E320000000001"}""") =>
        MensajeOutbox.Crear("Factura.Emitida", Guid.CreateVersion7(), contenido, Ahora);

    [Fact]
    public void Crear_deja_el_mensaje_pendiente_y_listo_para_enviar()
    {
        var mensaje = CrearMensaje();

        Assert.Equal(EstadoMensajeOutbox.Pendiente, mensaje.Estado);
        Assert.Equal(0, mensaje.Intentos);
        Assert.Equal(Ahora, mensaje.CreadoEn);
        Assert.Equal(Ahora, mensaje.ProximoIntentoEn);
        Assert.NotEqual(Guid.Empty, mensaje.Id);
    }

    [Fact]
    public void Hash_es_sha256_hex_y_depende_solo_del_contenido()
    {
        var a = CrearMensaje("""{"total":850.00}""");
        var b = CrearMensaje("""{"total":850.00}""");
        var c = CrearMensaje("""{"total":850.01}""");

        Assert.Equal(64, a.HashContenido.Length);
        Assert.Equal(a.HashContenido, b.HashContenido);
        Assert.NotEqual(a.HashContenido, c.HashContenido);
        Assert.NotEqual(a.Id, b.Id); // mismo contenido, mensajes distintos: cada uno con su clave de idempotencia
    }

    [Fact]
    public void Flujo_exitoso_termina_confirmado_sin_proximo_intento()
    {
        var mensaje = CrearMensaje();

        mensaje.MarcarEnProceso();
        mensaje.MarcarEnviado(Ahora.AddSeconds(1));
        mensaje.MarcarConfirmado(Ahora.AddSeconds(2));

        Assert.Equal(EstadoMensajeOutbox.Confirmado, mensaje.Estado);
        Assert.Equal(1, mensaje.Intentos);
        Assert.Equal(Ahora.AddSeconds(1), mensaje.EnviadoEn);
        Assert.Equal(Ahora.AddSeconds(2), mensaje.ConfirmadoEn);
        Assert.Null(mensaje.ProximoIntentoEn);
        Assert.Null(mensaje.UltimoError);
    }

    [Fact]
    public void Fallo_programa_reintento_y_cuenta_intentos()
    {
        var mensaje = CrearMensaje();

        mensaje.MarcarEnProceso();
        mensaje.RegistrarFallo("Central no disponible", Ahora.AddMinutes(1));

        Assert.Equal(EstadoMensajeOutbox.Error, mensaje.Estado);
        Assert.Equal("Central no disponible", mensaje.UltimoError);
        Assert.Equal(Ahora.AddMinutes(1), mensaje.ProximoIntentoEn);

        mensaje.MarcarEnProceso();
        mensaje.MarcarConfirmado(Ahora.AddMinutes(2));

        Assert.Equal(2, mensaje.Intentos);
        Assert.Equal(EstadoMensajeOutbox.Confirmado, mensaje.Estado);
        Assert.Null(mensaje.UltimoError);
    }

    [Fact]
    public void Error_largo_se_trunca()
    {
        var mensaje = CrearMensaje();
        mensaje.MarcarEnProceso();

        mensaje.RegistrarFallo(new string('x', MensajeOutbox.LargoMaximoError + 500), Ahora);

        Assert.Equal(MensajeOutbox.LargoMaximoError, mensaje.UltimoError!.Length);
    }

    [Fact]
    public void Mensaje_confirmado_no_se_vuelve_a_procesar()
    {
        var mensaje = CrearMensaje();
        mensaje.MarcarEnProceso();
        mensaje.MarcarConfirmado(Ahora);

        Assert.Throws<InvalidOperationException>(mensaje.MarcarEnProceso);
    }

    [Fact]
    public void No_se_puede_enviar_sin_tomarlo_primero()
    {
        var mensaje = CrearMensaje();

        Assert.Throws<InvalidOperationException>(() => mensaje.MarcarEnviado(Ahora));
    }

    [Fact]
    public void Crear_exige_documento_y_contenido()
    {
        Assert.Throws<ArgumentException>(() => MensajeOutbox.Crear("Factura.Emitida", Guid.Empty, "{}", Ahora));
        Assert.Throws<ArgumentException>(() => MensajeOutbox.Crear("Factura.Emitida", Guid.CreateVersion7(), " ", Ahora));
        Assert.Throws<ArgumentException>(() => MensajeOutbox.Crear(new string('T', MensajeOutbox.LargoMaximoTipo + 1), Guid.CreateVersion7(), "{}", Ahora));
    }
}
