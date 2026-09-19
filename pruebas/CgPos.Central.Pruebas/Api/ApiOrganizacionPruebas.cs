using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

public class CatalogoParametrosCentralPruebas
{
    [Fact]
    public void Todas_las_claves_que_lee_el_central_estan_en_el_catalogo_como_parametros_del_central()
    {
        var claves = typeof(ClavesParametrosCentral).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(claves);
        Assert.All(claves, clave => Assert.Equal(AlcanceParametro.Central, CatalogoParametros.Buscar(clave)?.Alcance));
    }
}

[Collection(ColeccionCentral.Nombre)]
public class ApiOrganizacionPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Sucursal_y_caja_nuevas_bajan_a_las_cajas_y_sus_codigos_no_se_repiten()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var codigoSucursal = CodigoSucursal();

        var sucursal = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/sucursales",
            new SolicitudSucursal(codigoSucursal, "Sucursal de prueba", "Calle 1, Santiago", "809-555-0000"));
        Assert.True(sucursal.Cuerpo!.Exitosa, sucursal.Cuerpo.Mensaje);
        var sucursalId = sucursal.Cuerpo.Id!.Value;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/sucursales", new SolicitudSucursal(codigoSucursal, "Otra", null, null))).Estado);

        var caja = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/cajas", new SolicitudCaja(sucursalId, "01", "Caja 01 de prueba", DireccionUnica()));
        Assert.True(caja.Cuerpo!.Exitosa, caja.Cuerpo.Mensaje);
        var cajaId = caja.Cuerpo.Id!.Value;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/cajas", new SolicitudCaja(sucursalId, "01", "Repetida", DireccionUnica()))).Estado);

        // Todos los datos de la sucursal son obligatorios en el Central.
        var incompleta = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/organizacion/sucursales/{sucursalId}",
            new SolicitudSucursal(codigoSucursal, "Sucursal", " ", null));
        Assert.Equal("Complete los datos de la sucursal: dirección, teléfono.", incompleta.Cuerpo!.Mensaje);

        var cambioCodigo = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/organizacion/sucursales/{sucursalId}", new SolicitudSucursal(codigoSucursal + 1, "Sucursal", "Calle 1", "809-555-0000"));
        Assert.Equal("El código de la sucursal no se puede cambiar.", cambioCodigo.Cuerpo!.Mensaje);
        // La dirección es obligatoria también al editar: sin ella la caja no podría comunicarse.
        var sinDireccion = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/organizacion/cajas/{cajaId}", new SolicitudActualizarCaja("Caja renombrada"));
        Assert.Equal("Indique la dirección IP de la caja. (Parameter 'direccionIp')", sinDireccion.Cuerpo!.Mensaje);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/organizacion/cajas/{cajaId}",
            new SolicitudActualizarCaja("Caja renombrada", DireccionUnica()))).Cuerpo!.Exitosa);

        var listada = Assert.Single(await ListarAsync<DatosCaja>(cliente, admin, "/api/organizacion/cajas"), c => c.Id == cajaId);
        Assert.Equal((codigoSucursal, "Caja renombrada"), (listada.SucursalCodigo, listada.Nombre));
        Assert.Null(listada.CredencialEmitidaEn);
        Assert.Equal(1, Assert.Single(await ListarAsync<DatosSucursal>(cliente, admin, "/api/organizacion/sucursales"), s => s.Id == sucursalId).Cajas);

        // Los cambios de organización bajan a las cajas en su próxima descarga.
        var bajada = await BajarAsync(cliente, tokenCaja, marca);
        Assert.Contains(bajada.Organizacion!.Sucursales!, s => s.Codigo == codigoSucursal);
        Assert.Contains(bajada.Organizacion.Cajas!, c => c.SucursalCodigo == codigoSucursal && c.Codigo == "01" && c.Nombre == "Caja renombrada");
    }

    [SkippableFact]
    public async Task Parametros_se_validan_con_el_catalogo_antes_de_guardarse()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CrearAsync(SolicitudParametro solicitud) =>
            EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/parametros", solicitud);
        Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CambiarAsync(int id, string valor) =>
            EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/organizacion/parametros/{id}", new SolicitudValorParametro(valor));

        Assert.Equal("El parámetro «Pruebas.NoExiste» no existe en el catálogo.", (await CrearAsync(new("Pruebas.NoExiste", "1"))).Cuerpo!.Mensaje);
        Assert.Equal("El valor debe ser un número entero.", (await CrearAsync(new("Balanza.DigitosValor", "cinco", CajaId: CentralEnPruebas.CajaDos))).Cuerpo!.Mensaje);
        Assert.Equal("Los parámetros del Central solo pueden ser generales.",
            (await CrearAsync(new("Central.Seguridad.MinutosToken", "10", CajaId: CentralEnPruebas.CajaDos))).Cuerpo!.Mensaje);
        Assert.Equal("La moneda «XYZ» no está publicada en el maestro de monedas.",
            (await CrearAsync(new("General.MonedaLocal", "XYZ", SucursalId: CentralEnPruebas.Sucursal))).Cuerpo!.Mensaje);

        var creado = await CrearAsync(new("Balanza.DigitosValor", "6", CajaId: CentralEnPruebas.CajaDos));
        Assert.True(creado.Cuerpo!.Exitosa, creado.Cuerpo.Mensaje);
        Assert.Equal("Ese parámetro ya existe en ese ámbito; edite su valor.",
            (await CrearAsync(new("Balanza.DigitosValor", "7", CajaId: CentralEnPruebas.CajaDos))).Cuerpo!.Mensaje);

        var parametros = await ListarAsync<DatosParametro>(cliente, admin, "/api/organizacion/parametros");
        Assert.StartsWith("Caja 02", Assert.Single(parametros, p => p.Id == creado.Cuerpo.Id).Ambito);

        var cierreCiego = Assert.Single(parametros, p => p.Clave == "Caja.CierreCiego" && p.SucursalId is null && p.CajaId is null);
        var moneda = Assert.Single(parametros, p => p.Clave == "General.MonedaLocal" && p.SucursalId is null && p.CajaId is null);
        Assert.Equal("El valor debe ser true o false.", (await CambiarAsync(cierreCiego.Id, "quizás")).Cuerpo!.Mensaje);
        Assert.Equal($"El parámetro «{CatalogoParametros.Buscar("General.MonedaLocal")!.Descripcion}» (General.MonedaLocal) es obligatorio.",
            (await CambiarAsync(moneda.Id, "")).Cuerpo!.Mensaje);
        Assert.True((await CambiarAsync(cierreCiego.Id, "false")).Cuerpo!.Exitosa);
        Assert.True((await CambiarAsync(cierreCiego.Id, "true")).Cuerpo!.Exitosa);

        Assert.Contains(await ListarAsync<DefinicionParametro>(cliente, admin, "/api/organizacion/parametros/catalogo"),
            d => d.Clave == "Caja.CierreCiego" && d.Tipo == TipoValorParametro.Booleano);
    }

    [SkippableFact]
    public async Task Caja_deshabilitada_o_sucursal_inactiva_no_se_autentican_ante_el_central()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var sucursalId = (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/sucursales",
            new SolicitudSucursal(CodigoSucursal(), "Sucursal para deshabilitar", "Calle 2, Santiago", "809-555-0001"))).Cuerpo!.Id!.Value;
        var cajaId = (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/organizacion/cajas", new SolicitudCaja(sucursalId, "01", "Caja", DireccionUnica()))).Cuerpo!.Id!.Value;
        var secreto = await CentralEnPruebas.EmitirCredencialAsync(cliente, cajaId);

        Assert.Equal(HttpStatusCode.OK, await PedirTokenAsync(cliente, cajaId, secreto));

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/organizacion/cajas/{cajaId}/deshabilitar")).Cuerpo!.Exitosa);
        Assert.Equal(HttpStatusCode.Unauthorized, await PedirTokenAsync(cliente, cajaId, secreto));

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/organizacion/cajas/{cajaId}/habilitar")).Cuerpo!.Exitosa);
        Assert.Equal(HttpStatusCode.OK, await PedirTokenAsync(cliente, cajaId, secreto));

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, $"/api/organizacion/sucursales/{sucursalId}/desactivar")).Cuerpo!.Exitosa);
        Assert.Equal(HttpStatusCode.Unauthorized, await PedirTokenAsync(cliente, cajaId, secreto));

        var listada = Assert.Single(await ListarAsync<DatosCaja>(cliente, admin, "/api/organizacion/cajas"), c => c.Id == cajaId);
        Assert.NotNull(listada.CredencialEmitidaEn);
        Assert.NotNull(listada.CredencialUltimoUsoEn);
    }

    [SkippableFact]
    public async Task Empresa_se_actualiza_incluido_su_rnc_y_sin_permiso_no_se_administra()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var empresa = await ObtenerEmpresaAsync(cliente, admin);
        Assert.Equal("999000004", empresa.Rnc);

        var cambio = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, "Nombre comercial de prueba", empresa.Direccion, empresa.Telefono));
        Assert.True(cambio.Cuerpo!.Exitosa, cambio.Cuerpo.Mensaje);
        Assert.Equal("Nombre comercial de prueba", (await ObtenerEmpresaAsync(cliente, admin)).NombreComercial);
        await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono));

        // El RNC se corrige si se registró mal, pero solo con un RNC válido.
        var invalido = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono, "123456789"));
        Assert.False(invalido.Cuerpo!.Exitosa);
        Assert.Contains("dígito verificador", invalido.Cuerpo.Mensaje);
        Assert.Equal(empresa.Rnc, (await ObtenerEmpresaAsync(cliente, admin)).Rnc);

        // También se acepta una cédula, para cuando factura una persona física.
        var cedula = "00100000001";
        var cambioCedula = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono, cedula));
        Assert.True(cambioCedula.Cuerpo!.Exitosa, cambioCedula.Cuerpo.Mensaje);
        Assert.Equal(cedula, (await ObtenerEmpresaAsync(cliente, admin)).Rnc);

        var nuevoRnc = "401007551";
        var cambioRnc = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono, nuevoRnc));
        Assert.True(cambioRnc.Cuerpo!.Exitosa, cambioRnc.Cuerpo.Mensaje);
        Assert.Equal(nuevoRnc, (await ObtenerEmpresaAsync(cliente, admin)).Rnc);

        // Se devuelve el RNC original para no afectar a las demás pruebas.
        await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, empresa.NombreComercial, empresa.Direccion, empresa.Telefono, empresa.Rnc));
        Assert.Equal(empresa.Rnc, (await ObtenerEmpresaAsync(cliente, admin)).Rnc);

        // Todos los datos de la empresa son obligatorios en el Central.
        var incompleta = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/organizacion/empresa",
            new SolicitudEmpresa(empresa.RazonSocial, "", empresa.Direccion, empresa.Telefono));
        Assert.Equal("Complete el dato de la empresa: nombre comercial.", incompleta.Cuerpo!.Mensaje);

        var codigo = $"ORG{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Organizacion#2026", false, CatalogoPermisosCentral.ConsultarReportes);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Organizacion#2026")).Cuerpo!.TokenAcceso;
        using var sinPermiso = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/organizacion/empresa", token));
        Assert.Equal(HttpStatusCode.Forbidden, sinPermiso.StatusCode);
    }

    /// <summary>Una dirección distinta por caja creada: el Central no admite dos cajas con la misma.</summary>
    private static string DireccionUnica() => $"10.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}";

    private static int _sucursales = 10;

    /// <summary>Código de sucursal que ninguna otra prueba usa (la 01 es de los datos de desarrollo).</summary>
    private static string CodigoSucursal() =>
        Interlocked.Add(ref _sucursales, 2).ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<DatosEmpresa> ObtenerEmpresaAsync(HttpClient cliente, string token)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/organizacion/empresa", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<DatosEmpresa>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<HttpStatusCode> PedirTokenAsync(HttpClient cliente, int cajaId, string secreto)
    {
        var (sucursal, caja, direccionIp) = CentralEnPruebas.IdentidadCaja(cajaId);
        using var respuesta = await cliente.PostAsJsonAsync("/api/dispositivos/token",
            new SolicitudTokenDispositivo(sucursal, caja, secreto, direccionIp), OpcionesJson.Predeterminadas);
        return respuesta.StatusCode;
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/sincronizacion/maestros?desde={desde}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<PaqueteBajadaMaestros>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }

    private static async Task<List<T>> ListarAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<List<T>>(OpcionesJson.Predeterminadas))!;
    }
}
