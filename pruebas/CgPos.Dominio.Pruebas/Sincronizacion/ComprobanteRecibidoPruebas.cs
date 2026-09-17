using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Dominio.Pruebas.Sincronizacion;

public class ComprobanteRecibidoPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(-4));

    private static ComprobanteRecibido Nuevo()
    {
        var documento = Ids.Asignar(DocumentoRecibido.Recibir(Guid.CreateVersion7(), Ids.Siguiente(), Ids.Siguiente(), "Venta.Cobrada", "010110000001", "{}",
            new string('A', DocumentoRecibido.LargoHash), Ahora, Ahora));
        return ComprobanteRecibido.Registrar(documento, "E320000000001", TipoComprobante.FacturaConsumo, "<ECF/>", new string('B', DocumentoRecibido.LargoHash), Ahora, Ahora);
    }

    [Fact]
    public void Un_envio_recibido_espera_su_consulta_y_no_se_envia_otra_vez()
    {
        var comprobante = Nuevo();

        comprobante.RegistrarEnvio("TRK-1", Ahora, Ahora.AddMinutes(1));

        Assert.Equal((EstadoEnvioDgii.Enviado, "TRK-1", 1, (DateTimeOffset?)Ahora.AddMinutes(1)),
            (comprobante.EstadoDgii, comprobante.TrackId, comprobante.IntentosEnvio, comprobante.ProximoIntentoEn));
        Assert.Throws<InvalidOperationException>(() => comprobante.RegistrarEnvio("TRK-2", Ahora, Ahora));
    }

    [Fact]
    public void Un_fallo_de_envio_sigue_pendiente_y_cuenta_el_intento()
    {
        var comprobante = Nuevo();

        comprobante.RegistrarFalloEnvio("Sin conexión", Ahora, Ahora.AddMinutes(5));
        comprobante.RegistrarFalloEnvio("Sin conexión", Ahora, Ahora.AddMinutes(10));

        Assert.Equal((EstadoEnvioDgii.Pendiente, 2, "Sin conexión"), (comprobante.EstadoDgii, comprobante.IntentosEnvio, comprobante.MensajeDgii));
    }

    [Fact]
    public void Un_rechazado_se_puede_reenviar_pero_un_aceptado_no()
    {
        var rechazado = Nuevo();
        rechazado.RegistrarEnvio("TRK-1", Ahora, Ahora);
        rechazado.RegistrarResultado(EstadoEnvioDgii.Rechazado, "Firma inválida", Ahora);

        rechazado.PrepararReenvio(Ahora.AddHours(1));

        Assert.Equal((EstadoEnvioDgii.Pendiente, (string?)null), (rechazado.EstadoDgii, rechazado.TrackId));

        var aceptado = Nuevo();
        aceptado.RegistrarResultado(EstadoEnvioDgii.Aceptado, null, Ahora, "TRK-9");
        Assert.Equal((1, "TRK-9"), (aceptado.IntentosEnvio, aceptado.TrackId));
        Assert.Throws<InvalidOperationException>(() => aceptado.PrepararReenvio(Ahora));
        Assert.Throws<InvalidOperationException>(() => aceptado.RegistrarResultado(EstadoEnvioDgii.Rechazado, null, Ahora));
    }

    [Fact]
    public void Un_resultado_de_la_dgii_no_puede_ser_pendiente_ni_enviado()
    {
        var comprobante = Nuevo();

        Assert.Throws<ArgumentOutOfRangeException>(() => comprobante.RegistrarResultado(EstadoEnvioDgii.Enviado, null, Ahora));
    }
}
