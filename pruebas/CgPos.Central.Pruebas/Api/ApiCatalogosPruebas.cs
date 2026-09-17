using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiCatalogosPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Un_catalogo_se_guarda_con_las_reglas_de_la_caja_y_baja_a_las_cajas()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var sufijo = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

        // El Central sugiere el siguiente código numérico libre.
        var sugerido = await LeerAsync<int>(cliente, admin, "/api/maestros/departamentos/siguiente-codigo");
        Assert.True(sugerido > 0);

        var departamento = new DepartamentoCarga(Codigos.Siguiente(), "Departamento de prueba", PermiteDescuentoManual: false);
        var guardada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/departamentos", departamento);
        Assert.True(guardada.Cuerpo!.Exitosa, guardada.Cuerpo.Mensaje);

        var listado = Assert.Single(await ListarAsync<DatosMaestroCentral<DepartamentoCarga>>(cliente, admin, "/api/maestros/departamentos"), f => f.Dato.Codigo == departamento.Codigo);
        Assert.Equal(departamento, listado.Dato);
        Assert.False(string.IsNullOrWhiteSpace(listado.ModificadoPor));

        // Crear con un código que ya existe no pisa el registro; cambiar uno que no existe tampoco lo crea.
        Assert.Contains("Ya existe", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/departamentos", departamento)).Cuerpo!.Mensaje);
        Assert.Equal(HttpStatusCode.NotFound,
            (await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/departamentos", departamento with { Codigo = Codigos.Siguiente() })).Estado);

        // Las reglas del dominio se validan antes de publicar.
        var sinNombre = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/departamentos", departamento with { Nombre = " " });
        Assert.Equal(HttpStatusCode.BadRequest, sinNombre.Estado);
        Assert.Contains($"Departamento {departamento.Codigo}", sinNombre.Cuerpo!.Mensaje);

        // Forma de pago: el tipo no cambia y su moneda debe estar publicada.
        var codigoMoneda = $"X{(char)('A' + Random.Shared.Next(26))}{(char)('A' + Random.Shared.Next(26))}";
        var moneda = new MonedaCarga(codigoMoneda, "Moneda de prueba", "¤");
        var monedaGuardada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/monedas", moneda);
        if (!monedaGuardada.Cuerpo!.Exitosa)
            Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/monedas", moneda)).Cuerpo!.Exitosa);

        var forma = new FormaPagoCarga($"P{sufijo}", "Pago de prueba", TipoFormaPago.Transferencia, 90, codigoMoneda);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/formas-pago", forma)).Cuerpo!.Exitosa);
        Assert.Contains("No se puede cambiar el tipo",
            (await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/formas-pago", forma with { Tipo = TipoFormaPago.Cheque })).Cuerpo!.Mensaje);
        var otraForma = forma with { Codigo = $"Q{sufijo}", Moneda = "ZZZ" };
        Assert.Contains("no está publicada", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/formas-pago", otraForma)).Cuerpo!.Mensaje);

        var bajada = await BajarAsync(cliente, tokenCaja, marca);
        Assert.Contains(bajada.Maestros!.Departamentos!, f => f.Codigo == departamento.Codigo && !f.PermiteDescuentoManual);
        Assert.Contains(bajada.Maestros.FormasPago!, f => f.Codigo == forma.Codigo && f.Moneda == codigoMoneda);
        Assert.DoesNotContain(bajada.Maestros.FormasPago!, f => f.Codigo == otraForma.Codigo);
    }

    [SkippableFact]
    public async Task Los_catalogos_de_fidelidad_y_de_descuentos_por_tarjeta_se_administran_y_bajan_a_las_cajas()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var sufijo = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var ahora = DateTimeOffset.UtcNow;

        var nivel = new NivelFidelidadCarga(Codigos.Siguiente(), "Nivel oro", 2, 1.5m);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/niveles-fidelidad", nivel)).Cuerpo!.Exitosa);

        var regla = new ReglaAcumulacionCarga(Codigos.Siguiente(), "Un punto por cada 100", CgPos.Dominio.Fidelidad.TipoReglaAcumulacion.Monto, 100m, 1m);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/reglas-acumulacion", regla)).Cuerpo!.Exitosa);

        var descuento = new DescuentoTarjetaCarga($"T{sufijo}", "10 % con tarjetas del banco", "455123,401288",
            CgPos.Dominio.Promociones.TipoDescuentoTarjeta.Porcentaje, 10m, ahora.AddDays(-1), ahora.AddMonths(1), MontoMinimo: 500m, MontoMaximo: 2000m);
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/descuentos-tarjeta", descuento)).Cuerpo!.Exitosa);

        // Las reglas del dominio se validan antes de publicar: un BIN muy corto no pasa.
        var invalido = await EnviarAsync(cliente, admin, HttpMethod.Put, "/api/maestros/descuentos-tarjeta", descuento with { Bines = "40" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.Estado);

        Assert.Single(await ListarAsync<DatosMaestroCentral<DescuentoTarjetaCarga>>(cliente, admin, "/api/maestros/descuentos-tarjeta"),
            d => d.Dato.Codigo == descuento.Codigo);

        var bajada = await BajarAsync(cliente, tokenCaja, marca);
        Assert.Contains(bajada.Maestros!.NivelesFidelidad!, n => n.Codigo == nivel.Codigo && n.FactorAcumulacion == 1.5m);
        Assert.Contains(bajada.Maestros.ReglasAcumulacion!, r => r.Codigo == regla.Codigo && r.Puntos == 1m);
        Assert.Contains(bajada.Maestros.DescuentosTarjeta!, d => d.Codigo == descuento.Codigo && d.Bines == "455123,401288");
    }

    [SkippableFact]
    public async Task Sin_permiso_de_maestros_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINMAE{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Permisos#2026", false, CatalogoPermisosCentral.AdministrarPrecios);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Permisos#2026")).Cuerpo!.TokenAcceso;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/maestros/departamentos", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/sincronizacion/maestros?desde={desde}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<PaqueteBajadaMaestros>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        var datos = respuesta.Content.Headers.ContentType?.MediaType == "application/json"
            ? await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas)
            : null;
        return (respuesta.StatusCode, datos);
    }

    private static Task<List<T>> ListarAsync<T>(HttpClient cliente, string token, string ruta) => LeerAsync<List<T>>(cliente, token, ruta);

    private static async Task<T> LeerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
