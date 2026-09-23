using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Ventas;

namespace CgPos.Pos.Pruebas.Api;

/// <summary>
/// Una caja se dedica a ventas o a devoluciones según el rol del usuario que entra. El candado está en el Agente: bloquear
/// el botón en la pantalla no sirve de nada si escribiendo la dirección se pasa por encima.
/// </summary>
[Collection(ColeccionAgente.Nombre)]
public class ApiPermisosCajaPruebas(AgenteEnPruebas agente)
{
    private const string CodigoCincel = "7891114119695";

    [SkippableFact]
    public async Task El_cajero_sin_permiso_de_devoluciones_no_entra_al_modulo_ni_por_la_direccion()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        await IniciarSesionAsync(cliente, "C001", "Cajero.2026");

        // El rol CAJERO vende pero no emite notas de crédito: todo el módulo le responde 403.
        using (var factura = await cliente.GetAsync("/api/devoluciones/factura/010110000001"))
            Assert.Equal(HttpStatusCode.Forbidden, factura.StatusCode);

        using (var motivos = await cliente.GetAsync("/api/devoluciones/motivos"))
            Assert.Equal(HttpStatusCode.Forbidden, motivos.StatusCode);

        using (var registro = await cliente.PostAsJsonAsync("/api/devoluciones",
            new SolicitudDevolucion("010110000001", [], "401007551", "Cliente", 1, null, null), OpcionesJson.Predeterminadas))
        {
            Assert.Equal(HttpStatusCode.Forbidden, registro.StatusCode);
        }
    }

    [SkippableFact]
    public async Task El_usuario_de_una_caja_de_devoluciones_no_vende_pero_si_consulta_articulos_y_clientes()
    {
        Skip.If(agente.MotivoOmision is not null, agente.MotivoOmision);
        using var cliente = agente.Fabrica!.CreateClient();
        await IniciarSesionAsync(cliente, "D001", "Devolucion.2026");

        // Su rol solo devuelve: la venta le queda cerrada de punta a punta.
        using (var actual = await cliente.GetAsync("/api/ventas/actual"))
            Assert.Equal(HttpStatusCode.Forbidden, actual.StatusCode);

        using (var linea = await cliente.PostAsJsonAsync("/api/ventas/1/lineas", new SolicitudAgregarArticulo(CodigoCincel), OpcionesJson.Predeterminadas))
            Assert.Equal(HttpStatusCode.Forbidden, linea.StatusCode);

        // Pero sigue atendiendo: busca artículos, consulta precios y valida el documento del cliente.
        var articulo = await cliente.GetFromJsonAsync<DatosArticuloVenta>($"/api/articulos/codigo/{CodigoCincel}", OpcionesJson.Predeterminadas);
        Assert.NotNull(articulo);

        using (var clientes = await cliente.GetAsync("/api/documentos/401007551"))
            Assert.Equal(HttpStatusCode.OK, clientes.StatusCode);

        // Y su turno lo abre y lo cierra igual que cualquier cajero: la nota de crédito es del turno.
        var estado = await cliente.GetFromJsonAsync<DatosEstadoTurno>("/api/turnos/actual", OpcionesJson.Predeterminadas);
        Assert.NotNull(estado);

        // El módulo que sí le toca le responde.
        using var motivos = await cliente.GetAsync("/api/devoluciones/motivos");
        Assert.Equal(HttpStatusCode.OK, motivos.StatusCode);
    }

    private static async Task IniciarSesionAsync(HttpClient cliente, string codigo, string clave)
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/sesion/ingreso", new SolicitudIngreso(codigo, clave), OpcionesJson.Predeterminadas);
        var ingreso = await respuesta.Content.ReadFromJsonAsync<RespuestaIngreso>(OpcionesJson.Predeterminadas);
        Assert.True(ingreso!.Exitoso, ingreso.Mensaje);
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ingreso.Token);
    }
}
