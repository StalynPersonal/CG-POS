using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiNotasCreditoPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Una_nota_de_credito_de_otra_sucursal_se_consulta_reserva_y_consume_una_sola_vez()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var (mensaje, numeroNota, encf) = MensajeEmision(CentralEnPruebas.CajaUno, 1000m, DateOnly.FromDateTime(DateTime.Today));
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenUno, mensaje)).Estado);

        // Otra caja ve el saldo, por e-NCF o por número, sin Id del Central, y reserva parte para una factura suya.
        var nota = await ObtenerAsync<DatosNotaCreditoParaCaja>(cliente, tokenDos, $"/api/notas-credito/{encf}");
        Assert.Equal((numeroNota, 1000m, 1000m, EstadoNotaCreditoCentral.Vigente), (nota.Numero, nota.Total, nota.Disponible, nota.Estado));
        Assert.Equal(encf, (await ObtenerAsync<DatosNotaCreditoParaCaja>(cliente, tokenDos, $"/api/notas-credito/{numeroNota}")).Encf);

        var factura = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaDos, TipoDocumentoNumerado.Factura);
        var reserva = await ReservarAsync(cliente, tokenDos, numeroNota, factura, 400m);
        Assert.True(reserva.Exitosa, reserva.Mensaje);
        Assert.Equal(400m, reserva.Monto);
        Assert.Equal(600m, (await ObtenerAsync<DatosNotaCreditoParaCaja>(cliente, tokenDos, $"/api/notas-credito/{encf}")).Disponible);

        // Reintentar el cobro de la misma factura reemplaza su reserva; pedir más de lo disponible reserva lo que queda y lo avisa.
        var otraFactura = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaDos, TipoDocumentoNumerado.Factura);
        Assert.Equal(400m, (await ReservarAsync(cliente, tokenDos, numeroNota, factura, 400m)).Monto);
        var parcial = await ReservarAsync(cliente, tokenDos, numeroNota, otraFactura, 1000m);
        Assert.Equal((true, 600m), (parcial.Exitosa, parcial.Monto));
        Assert.Contains("Solo hay", parcial.Mensaje);

        // Liberar, con los números de la nota y de la factura, devuelve el saldo retenido.
        using (var liberada = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Delete, $"/api/notas-credito/{numeroNota}/reservas/{otraFactura}", tokenDos)))
            Assert.Equal(HttpStatusCode.NoContent, liberada.StatusCode);
        Assert.Equal(600m, (await ObtenerAsync<DatosNotaCreditoParaCaja>(cliente, tokenDos, $"/api/notas-credito/{encf}")).Disponible);

        // El consumo descuenta el saldo y cierra la reserva; repetirlo no lo descuenta dos veces.
        var consumo = MensajeConsumo(CentralEnPruebas.CajaDos, numeroNota, encf, factura, 400m);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenDos, consumo)).Estado);
        Assert.Equal(EstadoRecepcion.Duplicado, (await EnviarAsync(cliente, tokenDos, consumo)).Estado);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenDos, MensajeConsumo(CentralEnPruebas.CajaDos, numeroNota, encf, factura, 400m))).Estado);

        var despues = Assert.Single((await ObtenerAsync<PaginaNotasCreditoCentral>(cliente, await CentralEnPruebas.TokenAdministradorAsync(cliente),
            $"/api/manager/notas-credito?buscar={encf}")).Elementos);
        Assert.Equal((400m, 0m, 600m), (despues.Consumido, despues.Reservado, despues.Disponible));
        Assert.False(despues.Sobregirada);
    }

    [SkippableFact]
    public async Task Un_consumo_que_llega_antes_que_la_emision_se_descuenta_al_registrarla()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var (emision, numeroNota, encf) = MensajeEmision(CentralEnPruebas.CajaUno, 500m, DateOnly.FromDateTime(DateTime.Today));

        var consumo = MensajeConsumo(CentralEnPruebas.CajaDos, numeroNota, encf, CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaDos, TipoDocumentoNumerado.Factura),
            200m);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenDos, consumo)).Estado);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenUno, emision)).Estado);

        var nota = await ObtenerAsync<DatosNotaCreditoParaCaja>(cliente, tokenDos, $"/api/notas-credito/{encf}");
        Assert.Equal((500m, 300m), (nota.Total, nota.Disponible));
    }

    [SkippableFact]
    public async Task La_vigencia_se_cuenta_desde_la_emision_con_los_dias_configurados_hoy_y_subirlos_habilita_la_nota()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var emitida = DateOnly.FromDateTime(DateTime.Today).AddDays(-70);
        var (mensaje, numeroNota, encf) = MensajeEmision(CentralEnPruebas.CajaUno, 750m, emitida);
        Assert.Equal(EstadoRecepcion.Recibido, (await EnviarAsync(cliente, tokenUno, mensaje)).Estado);
        var factura = CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, TipoDocumentoNumerado.Factura);

        var anterior = await central.CambiarParametroAsync(CatalogoParametros.DiasVigenciaNotaCredito, "60");
        try
        {
            // Con 60 días de vigencia, una nota de 70 días venció.
            var vencida = await ReservarAsync(cliente, tokenUno, numeroNota, factura, 100m);
            Assert.False(vencida.Exitosa);
            Assert.Contains($"venció el {emitida.AddDays(60):dd/MM/yyyy}", vencida.Mensaje);
            Assert.Contains((await ObtenerAsync<PaginaNotasCreditoCentral>(cliente, admin, "/api/manager/notas-credito?estado=Vencida&tamano=100")).Elementos,
                n => n.Numero == numeroNota);

            // Si el negocio sube los días, la misma nota vuelve a poder usarse.
            await central.CambiarParametroAsync(CatalogoParametros.DiasVigenciaNotaCredito, "90");
            var habilitada = await ObtenerAsync<DatosNotaCreditoParaCaja>(cliente, tokenUno, $"/api/notas-credito/{encf}");
            Assert.Equal((EstadoNotaCreditoCentral.Vigente, emitida.AddDays(90)), (habilitada.Estado, habilitada.VenceEn));
            Assert.True((await ReservarAsync(cliente, tokenUno, numeroNota, factura, 100m)).Exitosa);
        }
        finally
        {
            await central.CambiarParametroAsync(CatalogoParametros.DiasVigenciaNotaCredito, anterior);
        }
    }

    [SkippableFact]
    public async Task Sin_permiso_de_notas_de_credito_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINNC{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Notas#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Notas#2026")).Cuerpo!.TokenAcceso!;

        using var manager = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/notas-credito", token));
        using var caja = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/notas-credito/E340000000001", token));

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (manager.StatusCode, caja.StatusCode));
    }

    private static (MensajeSincronizacion Mensaje, string Numero, string Encf) MensajeEmision(int cajaId, decimal total, DateOnly fechaEmision)
    {
        var numero = CentralEnPruebas.NumeroDocumento(cajaId, TipoDocumentoNumerado.NotaCredito);
        var encf = $"E34{Random.Shared.NextInt64(1, 9_999_999_999):D10}";
        var xml = $"<ECF><Encabezado><eNCF>{encf}</eNCF></Encabezado><Signature>firma</Signature></ECF>";
        var comprobante = new DatosComprobanteElectronico(encf, TipoComprobante.NotaCredito, "ABC123", DateTimeOffset.UtcNow, "https://ecf.dgii.gov.do/consulta",
            EstadoDocumentoElectronico.PendienteSincronizar);
        var ecf = new DocumentoElectronicoParaCentral(encf, TipoComprobante.NotaCredito, xml, HashSincronizacion.Calcular(xml), DateTimeOffset.UtcNow);
        var nota = new DocumentoNotaCreditoEmitida(numero, CentralEnPruebas.NumeroDocumento(cajaId, TipoDocumentoNumerado.Factura), "E320000000001",
            DateTimeOffset.UtcNow.AddDays(-1), null, TipoDocumentoIdentidad.Cedula, "00113918205", "Cliente de prueba", 1, "Devolucion", null, "Cajero Prueba", null,
            false, true, total / 1.18m, total - (total / 1.18m), 0m, total, "DOP", fechaEmision, DateTimeOffset.UtcNow, [], comprobante, 0, TipoReembolso.SaldoNotaCredito,
            null, null, ecf);
        var contenido = JsonSerializer.Serialize(nota, OpcionesJson.Predeterminadas);

        return (new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.NotaCreditoEmitida, numero, contenido, HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(cajaId).Sucursal, CentralEnPruebas.CodigosCaja(cajaId).Caja,
            DateTimeOffset.UtcNow), numero, encf);
    }

    private static MensajeSincronizacion MensajeConsumo(int cajaId, string numeroNota, string encf, string ventaNumero, decimal monto)
    {
        var contenido = JsonSerializer.Serialize(
            new DocumentoConsumoNotaCredito(numeroNota, encf, ventaNumero, monto, 0m, DateTimeOffset.UtcNow), OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.NotaCreditoConsumida, numeroNota, contenido, HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(cajaId).Sucursal, CentralEnPruebas.CodigosCaja(cajaId).Caja,
            DateTimeOffset.UtcNow);
    }

    private static async Task<RespuestaRecepcionCentral> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<RespuestaReservaNotaCredito> ReservarAsync(HttpClient cliente, string token, string numeroNota, string ventaNumero, decimal monto)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/notas-credito/{numeroNota}/reservas", token,
            new SolicitudReservaNotaCredito(ventaNumero, monto)));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaReservaNotaCredito>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
