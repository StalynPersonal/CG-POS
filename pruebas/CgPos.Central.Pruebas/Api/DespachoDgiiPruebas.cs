using CgPos.Central.Aplicacion.Dgii;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class DespachoDgiiPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Un_e_cf_se_envia_y_toma_el_resultado_de_la_dgii_al_consultarlo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        var encf = await RegistrarComprobanteAsync();
        central.Dgii.ProgramarEnvio(encf, new RespuestaDgii(ResultadoRespuestaDgii.EnProceso, $"TRK-{encf}"));
        central.Dgii.ProgramarConsulta($"TRK-{encf}",
            new RespuestaDgii(ResultadoRespuestaDgii.EnProceso),
            new RespuestaDgii(ResultadoRespuestaDgii.AceptadoCondicional, Mensaje: "Diferencia menor en el total de ITBIS"));

        var ciclo = await central.ProcesarDgiiAsync();
        Assert.True(ciclo.Habilitado);
        var enviado = await LeerAsync(encf);
        Assert.Equal((EstadoEnvioDgii.Enviado, $"TRK-{encf}", 1), (enviado.EstadoDgii, enviado.TrackId, enviado.IntentosEnvio));
        Assert.True(enviado.ProximoIntentoEn > DateTimeOffset.UtcNow);

        // Antes de que toque la consulta no se consulta; la primera consulta sigue en proceso y se reprograma.
        await central.ProcesarDgiiAsync();
        await AdelantarAsync(encf);
        await central.ProcesarDgiiAsync();
        Assert.Equal(EstadoEnvioDgii.Enviado, (await LeerAsync(encf)).EstadoDgii);

        await AdelantarAsync(encf);
        await central.ProcesarDgiiAsync();
        var resultado = await LeerAsync(encf);
        Assert.Equal((EstadoEnvioDgii.AceptadoCondicional, "Diferencia menor en el total de ITBIS", (DateTimeOffset?)null),
            (resultado.EstadoDgii, resultado.MensajeDgii, resultado.ProximoIntentoEn));
    }

    [SkippableFact]
    public async Task Un_rechazo_inmediato_queda_registrado_con_su_motivo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        var encf = await RegistrarComprobanteAsync();
        central.Dgii.ProgramarEnvio(encf, new RespuestaDgii(ResultadoRespuestaDgii.Rechazado, Mensaje: "Estructura inválida: falta RNCComprador"));

        await central.ProcesarDgiiAsync();

        var rechazado = await LeerAsync(encf);
        Assert.Equal((EstadoEnvioDgii.Rechazado, 1), (rechazado.EstadoDgii, rechazado.IntentosEnvio));
        Assert.Contains("RNCComprador", rechazado.MensajeDgii);
        Assert.Null(rechazado.ProximoIntentoEn);
    }

    [SkippableFact]
    public async Task Un_fallo_de_comunicacion_se_reintenta_despues_de_la_espera_configurada()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        var encf = await RegistrarComprobanteAsync();
        central.Dgii.ProgramarEnvio(encf, RespuestaDgii.Fallo("Tiempo de espera agotado"));

        await central.ProcesarDgiiAsync();
        var fallido = await LeerAsync(encf);
        Assert.Equal((EstadoEnvioDgii.Pendiente, 1, "Tiempo de espera agotado"), (fallido.EstadoDgii, fallido.IntentosEnvio, fallido.MensajeDgii));
        Assert.True(fallido.ProximoIntentoEn > DateTimeOffset.UtcNow.AddMinutes(4), "Espera el reintento configurado (5 minutos en desarrollo).");

        await central.ProcesarDgiiAsync();
        Assert.Equal(1, (await LeerAsync(encf)).IntentosEnvio);

        await AdelantarAsync(encf);
        await central.ProcesarDgiiAsync();
        Assert.Equal((EstadoEnvioDgii.Enviado, 2), ((await LeerAsync(encf)).EstadoDgii, (await LeerAsync(encf)).IntentosEnvio));
    }

    [SkippableFact]
    public async Task Sin_el_envio_activado_no_se_envia_nada()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        var encf = await RegistrarComprobanteAsync();
        var anterior = await central.CambiarParametroAsync(ClavesParametrosCentral.DgiiHabilitado, "false");
        try
        {
            var ciclo = await central.ProcesarDgiiAsync();

            Assert.False(ciclo.Habilitado);
            Assert.Equal((EstadoEnvioDgii.Pendiente, 0), ((await LeerAsync(encf)).EstadoDgii, (await LeerAsync(encf)).IntentosEnvio));
            Assert.DoesNotContain(encf, central.Dgii.Enviados);
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.DgiiHabilitado, anterior);
        }
    }

    [Fact]
    public void La_espera_entre_reintentos_se_duplica_hasta_el_maximo()
    {
        var reintento = TimeSpan.FromMinutes(5);
        var maximo = TimeSpan.FromMinutes(60);

        Assert.Equal([5, 10, 20, 40, 60, 60],
            Enumerable.Range(1, 6).Select(intento => CgPos.Central.Infraestructura.Dgii.DespachadorDgii.Espera(intento, reintento, maximo).TotalMinutes));
    }

    private async Task<string> RegistrarComprobanteAsync()
    {
        var encf = $"E32{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        await central.UsarContextoAsync(async contexto =>
        {
            var ahora = DateTimeOffset.UtcNow;
            var documento = DocumentoRecibido.Recibir(Guid.CreateVersion7(), CentralEnPruebas.CajaUno, CentralEnPruebas.Sucursal, "Venta.Cobrada", Guid.CreateVersion7(),
                "{}", new string('A', DocumentoRecibido.LargoHash), ahora, ahora);
            contexto.DocumentosRecibidos.Add(documento);
            contexto.ComprobantesRecibidos.Add(ComprobanteRecibido.Registrar(documento, encf, TipoComprobante.FacturaConsumo,
                "<ECF><Encabezado><Emisor><RNCEmisor>131246796</RNCEmisor></Emisor></Encabezado></ECF>", new string('B', DocumentoRecibido.LargoHash), ahora, ahora));
            return await contexto.SaveChangesAsync();
        });
        return encf;
    }

    private Task<ComprobanteRecibido> LeerAsync(string encf) =>
        central.UsarContextoAsync(contexto => contexto.ComprobantesRecibidos.AsNoTracking().SingleAsync(c => c.Encf == encf));

    /// <summary>Hace que ya le toque el próximo intento, sin esperar la vigencia configurada.</summary>
    private Task AdelantarAsync(string encf) =>
        central.UsarContextoAsync(async contexto =>
        {
            var comprobante = await contexto.ComprobantesRecibidos.SingleAsync(c => c.Encf == encf);
            contexto.Entry(comprobante).Property(c => c.ProximoIntentoEn).CurrentValue = DateTimeOffset.UtcNow.AddSeconds(-1);
            return await contexto.SaveChangesAsync();
        });
}
