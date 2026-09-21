using System.Net.Http.Json;
using CgPos.Central.Aplicacion.Notificaciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class AvisosDespachoPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Al_cliente_con_correo_se_le_avisa_una_vez_y_al_que_no_tiene_se_le_deja_para_llamarlo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var conCorreo = RncValido();
        var sinCorreo = RncValido();
        await PublicarClienteAsync(conCorreo, "Cliente Con Correo", "cliente@contrerasgroup.com.do");
        await PublicarClienteAsync(sinCorreo, "Cliente Sin Correo", null);

        var preparadoUno = Pendiente(conCorreo, "Cliente Con Correo", EstadoPendiente.Preparado);
        var preparadoDos = Pendiente(sinCorreo, "Cliente Sin Correo", EstadoPendiente.Preparado);
        var enProceso = Pendiente(conCorreo, "Cliente Con Correo", EstadoPendiente.EnPreparacion);
        foreach (var pendiente in new[] { preparadoUno, preparadoDos, enProceso })
            Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, pendiente));

        await central.CambiarParametroAsync(ClavesParametrosCentral.DespachoAvisarPreparado, "true");
        try
        {
            var correo = new CorreoDePrueba();
            var enviados = await AvisarAsync(correo);

            // Solo el preparado con correo recibe el aviso; el que no tiene correo se marca para llamarlo y no se reintenta.
            Assert.Equal(1, enviados);
            var mensaje = Assert.Single(correo.Enviados);
            Assert.Equal("cliente@contrerasgroup.com.do", mensaje.Destinatario);
            Assert.Contains(preparadoUno.Numero, mensaje.Asunto);
            Assert.Contains("Cliente Con Correo", mensaje.Cuerpo);

            Assert.Equal(0, await AvisarAsync(correo));
            Assert.Single(correo.Enviados);

            // El que aún se prepara no se avisa.
            var sinAviso = await central.UsarContextoAsync(contexto => contexto.PendientesEntrega.AsNoTracking()
                .Where(p => p.Numero == enProceso.Numero).Select(p => p.AvisoEnviadoEn).SingleAsync());
            Assert.Null(sinAviso);
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.DespachoAvisarPreparado, null);
        }
    }

    [SkippableFact]
    public async Task Si_el_correo_falla_el_aviso_se_reintenta_en_el_proximo_ciclo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var documento = RncValido();
        await PublicarClienteAsync(documento, "Cliente Del Reintento", "reintento@contrerasgroup.com.do");
        var pendiente = Pendiente(documento, "Cliente Del Reintento", EstadoPendiente.Preparado);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, pendiente));

        await central.CambiarParametroAsync(ClavesParametrosCentral.DespachoAvisarPreparado, "true");
        try
        {
            var caido = new CorreoDePrueba { Falla = "El servidor de correo no responde." };
            Assert.Equal(0, await AvisarAsync(caido));

            var restablecido = new CorreoDePrueba();
            Assert.Equal(1, await AvisarAsync(restablecido));
            Assert.Contains(restablecido.Enviados, m => m.Destinatario == "reintento@contrerasgroup.com.do");
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.DespachoAvisarPreparado, null);
        }
    }

    private Task<int> AvisarAsync(IServicioCorreo correo) =>
        central.UsarContextoAsync(async contexto =>
        {
            await using var ambito = central.Fabrica!.Services.CreateAsyncScope();
            var avisos = ActivatorUtilities.CreateInstance<CgPos.Central.Infraestructura.Notificaciones.AvisosDespacho>(ambito.ServiceProvider, correo);
            return await avisos.AvisarPreparadosAsync(50);
        });

    private async Task PublicarClienteAsync(string documento, string nombre, string? correo)
    {
        await using var ambito = central.Fabrica!.Services.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarAsync(
            new PaqueteMaestros(Clientes: [new ClienteCarga($"CL{Codigos.Siguiente()}", TipoDocumentoIdentidad.Rnc, documento, nombre, Correo: correo)]), "Pruebas");
    }

    private static DocumentoPendienteEntrega Pendiente(string documento, string nombre, EstadoPendiente estado)
    {
        var creado = DateTimeOffset.UtcNow.AddHours(-1);
        return new DocumentoPendienteEntrega(CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, CgPos.Dominio.Comun.TipoDocumentoNumerado.PendienteEntrega),
            CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, CgPos.Dominio.Comun.TipoDocumentoNumerado.Factura),
            MetodoEntrega.RetiroSucursal, estado, "01", "Sucursal Kennedy", null, null, null, null, "8095551234",
            null, null, DateOnly.FromDateTime(DateTime.Today), null, documento, nombre, "Cajero Desarrollo", null, creado, creado, "Cajero Desarrollo", null,
            [new DatosLineaPendiente(1, "CINCEL", "Cincel", "UND", 0, false, 1m, 0m, null)],
            []);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, DocumentoPendienteEntrega pendiente)
    {
        var contenido = JsonSerializer.Serialize(pendiente, OpcionesJson.Predeterminadas);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.PendienteCreado, pendiente.Numero, contenido,
            HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno).Sucursal, CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno).Caja, DateTimeOffset.UtcNow);
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    /// <summary>Un RNC nuevo que pasa la validación del dígito verificador de la DGII.</summary>
    private static string RncValido()
    {
        for (var intento = 0; intento < 1000; intento++)
        {
            var candidato = Random.Shared.Next(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (DocumentoIdentidad.RncValido(candidato))
                return candidato;
        }

        throw new InvalidOperationException("No se pudo generar un RNC válido.");
    }

    /// <summary>Servidor de correo de prueba: guarda lo enviado y puede simular que está caído.</summary>
    private sealed class CorreoDePrueba : IServicioCorreo
    {
        public List<MensajeCorreo> Enviados { get; } = [];

        public string? Falla { get; init; }

        public Task<bool> ConfiguradoAsync(CancellationToken cancelacion = default) => Task.FromResult(true);

        public Task<ResultadoCorreo> EnviarAsync(MensajeCorreo mensaje, CancellationToken cancelacion = default)
        {
            if (Falla is { } error)
                return Task.FromResult(ResultadoCorreo.Fallo(error));

            Enviados.Add(mensaje);
            return Task.FromResult(ResultadoCorreo.Correcto());
        }
    }
}
