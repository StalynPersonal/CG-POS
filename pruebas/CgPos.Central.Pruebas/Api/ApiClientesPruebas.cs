using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

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

        var principal = new DireccionClienteCarga("Casa", "Calle Primera 1", Ciudad: "Santo Domingo");
        var oficina = new DireccionClienteCarga("Oficina", "Av. Churchill 100", EsPrincipal: true);
        var datos = new ClienteCarga($"CL{digitos}", TipoDocumentoIdentidad.Cedula, documento, $"José Núñez {sufijo}", TipoComprobante.FacturaCreditoFiscal,
            ListaPrecio: ListaPrecio.Mayor, Telefono: "809-555-0101", Direcciones: [principal, oficina]);
        var creado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/clientes", datos);
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);

        var porNombre = await ObtenerAsync<PaginaMaestros<ClienteCarga>>(cliente, admin, $"/api/maestros/clientes?buscar={Uri.EscapeDataString($"núñez {sufijo}")}");
        Assert.Equal(datos.Codigo, Assert.Single(porNombre.Elementos).Dato.Codigo);

        // Mismo documento sin guiones en otro cliente, cambiar el documento o un documento sin formato: se rechazan antes de publicar.
        var duplicado = datos with { Codigo = $"CX{digitos}", Documento = digitos, Direcciones = [] };
        Assert.Contains("ya existe", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/clientes", duplicado)).Cuerpo!.Mensaje);
        Assert.Contains("se cambia con «Corregir documento»",
            (await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/clientes", datos with { Documento = $"5{digitos[1..]}" })).Cuerpo!.Mensaje);
        var sinFormato = datos with { Codigo = $"CY{digitos}", Documento = "123" };
        var rechazado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/clientes", sinFormato);
        Assert.Equal(HttpStatusCode.BadRequest, rechazado.Estado);
        Assert.Contains("no tiene formato de cédula", rechazado.Cuerpo!.Mensaje);
        Assert.DoesNotContain("(Parameter", rechazado.Cuerpo.Mensaje);

        // El mismo documento con guiones o sin ellos es el mismo cliente; quitar una dirección también baja.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/clientes", datos with { Documento = digitos, Direcciones = [oficina] })).Cuerpo!.Exitosa);

        var bajado = Assert.Single((await BajarAsync(cliente, tokenCaja, marca)).Maestros!.Clientes!, c => c.Codigo == datos.Codigo);
        Assert.Equal((ListaPrecio.Mayor, TipoComprobante.FacturaCreditoFiscal), (bajado.ListaPrecio, bajado.TipoComprobante));
        Assert.Equal(oficina.Alias, Assert.Single(bajado.Direcciones!).Alias);
    }

    [SkippableFact]
    public async Task El_documento_mal_digitado_se_corrige_con_motivo_valido_y_unico_y_baja_a_las_cajas()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;

        var original = CedulaValida();
        var datos = new ClienteCarga($"CC{original}", TipoDocumentoIdentidad.Cedula, original, $"Cliente corrección {original[^4..]}");
        var otro = new ClienteCarga($"CO{original}", TipoDocumentoIdentidad.Cedula, CedulaValida(), $"Otro cliente {original[^4..]}");
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/clientes", datos)).Cuerpo!.Exitosa);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/clientes", otro)).Cuerpo!.Exitosa);

        var ruta = $"/api/maestros/clientes/{datos.Codigo}/documento";
        var correcta = CedulaValida();
        Assert.Equal("Indique el motivo de la corrección.",
            (await CorregirAsync(cliente, admin, ruta, new SolicitudCorreccionDocumentoCliente(TipoDocumentoIdentidad.Cedula, correcta, " "))).Cuerpo!.Mensaje);
        // Sin forma de cédula no se acepta: once dígitos son once dígitos.
        Assert.Contains("no tiene forma de una cédula",
            (await CorregirAsync(cliente, admin, ruta, new SolicitudCorreccionDocumentoCliente(TipoDocumentoIdentidad.Cedula, "12345", "Mal digitado"))).Cuerpo!.Mensaje);
        Assert.Contains("ya tiene ese documento",
            (await CorregirAsync(cliente, admin, ruta, new SolicitudCorreccionDocumentoCliente(TipoDocumentoIdentidad.Cedula, otro.Documento, "Mal digitado"))).Cuerpo!.Mensaje);

        var corregido = await CorregirAsync(cliente, admin, ruta, new SolicitudCorreccionDocumentoCliente(TipoDocumentoIdentidad.Cedula, correcta, "Se digitó mal la cédula"));
        Assert.True(corregido.Cuerpo!.Exitosa, corregido.Cuerpo.Mensaje);

        // Es el mismo cliente (mismo código) con el documento nuevo; la corrección queda auditada con el anterior y el motivo.
        var bajado = Assert.Single((await BajarAsync(cliente, tokenCaja, marca)).Maestros!.Clientes!, c => c.Codigo == datos.Codigo);
        Assert.Equal(correcta, bajado.Documento);
        var auditado = await central.UsarContextoAsync(contexto => contexto.Auditoria.AsNoTracking()
            .SingleAsync(a => a.Accion == "Maestros.ClienteDocumentoCorregido" && a.EntidadId == datos.Codigo));
        Assert.Equal("Se digitó mal la cédula", auditado.Motivo);
        Assert.Contains(original, auditado.Detalle);

        // Una cédula vieja, con el dígito verificador que no cuadra, se acepta avisando: hay cédulas legítimas así.
        var sinDigito = await CorregirAsync(cliente, admin, $"/api/maestros/clientes/{otro.Codigo}/documento",
            new SolicitudCorreccionDocumentoCliente(TipoDocumentoIdentidad.Cedula, CedulaInvalida(), "Cédula vieja"));
        Assert.True(sinDigito.Cuerpo!.Exitosa, sinDigito.Cuerpo.Mensaje);
        Assert.Contains("dígito verificador no cuadra", sinDigito.Cuerpo.Advertencia);

        // Sin el permiso de corrección no se puede, aunque administre maestros.
        var codigo = $"MAE{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Solo.Maestros#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Solo.Maestros#2026")).Cuerpo!.TokenAcceso!;
        Assert.Equal(HttpStatusCode.Forbidden,
            (await CorregirAsync(cliente, token, ruta, new SolicitudCorreccionDocumentoCliente(TipoDocumentoIdentidad.Cedula, CedulaValida(), "Sin permiso"))).Estado);
    }

    /// <summary>Cédula aleatoria con dígito verificador válido, para no chocar con otras pruebas.</summary>
    private static string CedulaValida()
    {
        while (true)
        {
            var base10 = $"4{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
            foreach (var digito in Enumerable.Range(0, 10))
                if (DocumentoIdentidad.Validar($"{base10}{digito}") is { EsValido: true } valida)
                    return valida.Documento;
        }
    }

    private static string CedulaInvalida()
    {
        var valida = CedulaValida();
        return valida[..10] + (char)('0' + (valida[10] - '0' + 1) % 10);
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CorregirAsync(HttpClient cliente, string token, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde) =>
        await ObtenerAsync<PaqueteBajadaMaestros>(cliente, token, $"/api/sincronizacion/maestros?desde={desde}");

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }
}
