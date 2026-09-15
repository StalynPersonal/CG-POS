using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiClientesPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Un_cliente_se_busca_conserva_su_documento_y_baja_con_sus_direcciones()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var digitos = $"4{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        var documento = $"{digitos[..3]}-{digitos[3..10]}-{digitos[10..]}";
        var sufijo = digitos[^5..];

        var principal = new DireccionClienteCarga(Guid.CreateVersion7(), "Casa", "Calle Primera 1", Ciudad: "Santo Domingo");
        var oficina = new DireccionClienteCarga(Guid.CreateVersion7(), "Oficina", "Av. Churchill 100", EsPrincipal: true);
        var datos = new ClienteCarga(Guid.CreateVersion7(), TipoDocumentoIdentidad.Cedula, documento, $"José Núñez {sufijo}", TipoComprobante.FacturaCreditoFiscal,
            ListaPrecio: ListaPrecio.Mayor, Telefono: "809-555-0101", Direcciones: [principal, oficina]);
        var creado = await EnviarAsync(cliente, admin, $"/api/maestros/clientes/{datos.Id}", datos);
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);

        var porNombre = await ObtenerAsync<PaginaMaestros<ClienteCarga>>(cliente, admin, $"/api/maestros/clientes?buscar={Uri.EscapeDataString($"jose nunez {sufijo}")}");
        Assert.Equal(datos.Id, Assert.Single(porNombre.Elementos).Dato.Id);

        // Mismo documento sin guiones en otro cliente, cambiar el documento o un documento sin formato: se rechazan antes de publicar.
        var duplicado = datos with { Id = Guid.CreateVersion7(), Documento = digitos, Direcciones = [] };
        Assert.Contains("ya existe", (await EnviarAsync(cliente, admin, $"/api/maestros/clientes/{duplicado.Id}", duplicado)).Cuerpo!.Mensaje);
        Assert.Contains("No se puede cambiar el documento del cliente",
            (await EnviarAsync(cliente, admin, $"/api/maestros/clientes/{datos.Id}", datos with { Documento = $"5{digitos[1..]}" })).Cuerpo!.Mensaje);
        var sinFormato = datos with { Id = Guid.CreateVersion7(), Documento = "123" };
        var rechazado = await EnviarAsync(cliente, admin, $"/api/maestros/clientes/{sinFormato.Id}", sinFormato);
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.Estado);
        Assert.Contains("no tiene formato de cédula", rechazado.Cuerpo!.Mensaje);
        Assert.DoesNotContain("(Parameter", rechazado.Cuerpo.Mensaje);

        // El mismo documento con guiones o sin ellos es el mismo cliente; quitar una dirección también baja.
        Assert.True((await EnviarAsync(cliente, admin, $"/api/maestros/clientes/{datos.Id}", datos with { Documento = digitos, Direcciones = [oficina] })).Cuerpo!.Exitosa);

        var bajado = Assert.Single((await BajarAsync(cliente, tokenCaja, marca)).Maestros!.Clientes!, c => c.Id == datos.Id);
        Assert.Equal((ListaPrecio.Mayor, TipoComprobante.FacturaCreditoFiscal), (bajado.ListaPrecio, bajado.TipoComprobante));
        Assert.Equal(oficina.Id, Assert.Single(bajado.Direcciones!).Id);
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde) =>
        await ObtenerAsync<PaqueteBajadaMaestros>(cliente, token, $"/api/sincronizacion/maestros?desde={desde}");

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Put, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
