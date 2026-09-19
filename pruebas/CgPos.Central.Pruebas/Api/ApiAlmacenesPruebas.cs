using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// Almacenes (M12): es el único catálogo que referencia a una sucursal, y lo hace por su código de dos dígitos. La pantalla
/// del Central los crea con esta misma llamada, así que aquí se comprueba que el código viaje como texto («01») y no como número.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiAlmacenesPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_almacen_se_crea_con_el_codigo_de_su_sucursal_y_se_rechaza_si_la_sucursal_no_existe()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var sucursales = await ObtenerAsync<List<DatosSucursal>>(cliente, admin, "/api/maestros/sucursales");
        var sucursal = sucursales.First(s => s.Activa);
        var codigo = $"ALM{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var almacen = new AlmacenCarga(codigo, "Depósito de prueba", sucursal.Codigo, "Calle 1, Boca Chica");
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/almacenes", almacen)).Exitosa);

        var guardados = await ObtenerAsync<List<DatosMaestroCentral<AlmacenCarga>>>(cliente, admin, "/api/maestros/almacenes");
        var guardado = Assert.Single(guardados, a => a.Dato.Codigo == codigo).Dato;
        Assert.Equal((sucursal.Codigo, "Depósito de prueba"), (guardado.SucursalCodigo, guardado.Nombre));

        // Una sucursal que no existe se rechaza con un mensaje, no con un error del servidor.
        var inventada = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/maestros/almacenes",
            almacen with { Codigo = codigo + "X", SucursalCodigo = "99" });
        Assert.False(inventada.Exitosa);
        Assert.Contains("sucursal", inventada.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<RespuestaAdministracion> EnviarAsync(HttpClient cliente, string token, HttpMethod metodo, string ruta, object cuerpo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(metodo, ruta, token, cuerpo));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
