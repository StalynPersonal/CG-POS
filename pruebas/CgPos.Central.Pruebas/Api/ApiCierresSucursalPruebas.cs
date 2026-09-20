using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiCierresSucursalPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task La_sucursal_se_cierra_con_todos_sus_turnos_cerrados_una_vez_por_dia_y_registra_los_depositos()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var dia = new DateOnly(2026, 6, 18);
        var turnoUno = Random.Shared.NextInt64(1_000_000, 4_000_000);
        var turnoDos = turnoUno + 1;

        // Dos turnos vendieron ese día, pero solo el primero informó su cierre.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, TiposMensaje.VentaCobrada, Venta(dia, turnoUno)));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, TiposMensaje.VentaCobrada, Venta(dia, turnoDos)));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, TiposMensaje.TurnoCerrado, Cierre(dia, turnoUno, esperado: 5000m, declarado: 4950m)));

        var ruta = $"/api/manager/cierres-sucursal/preparar?sucursalId={CentralEnPruebas.Sucursal}&fecha={dia:yyyy-MM-dd}";
        var incompleto = await ObtenerAsync<DatosPreparacionCierreSucursal>(cliente, admin, ruta);
        Assert.Contains(incompleto.Pendientes, p => p.Contains($"turno {turnoDos}", StringComparison.Ordinal));
        var rechazado = await CerrarAsync(cliente, admin, dia, [new SolicitudDepositoCierreSucursal("DOP", "BPD", "BOL-1", 4950m, dia.AddDays(1))]);
        Assert.False(rechazado.Exitosa);
        Assert.Contains("No se puede cerrar la sucursal todavía", rechazado.Mensaje);

        // Con el segundo cierre ya no hay pendientes: el efectivo a depositar es lo declarado en efectivo; la tarjeta no se deposita.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, TiposMensaje.TurnoCerrado, Cierre(dia, turnoDos, esperado: 1000m, declarado: 1000m,
            tarjeta: 2500m)));
        var preparacion = await ObtenerAsync<DatosPreparacionCierreSucursal>(cliente, admin, ruta);
        Assert.Empty(preparacion.Pendientes);
        Assert.Equal(2, preparacion.Cierres.Count);
        var efectivo = Assert.Single(preparacion.Efectivo);
        Assert.Equal(("DOP", 5950m), (efectivo.Moneda, efectivo.ADepositar));
        Assert.Contains(preparacion.FormasPago, f => f.Tipo == TipoFormaPago.Tarjeta && f.Declarado == 2500m);

        // El banco debe existir en el maestro; luego se cierra con lo depositado y la diferencia queda a la vista.
        Assert.Contains("no existe", (await CerrarAsync(cliente, admin, dia, [new SolicitudDepositoCierreSucursal("DOP", "NOEXISTE", "BOL-1", 5900m, dia)])).Mensaje);
        var cerrado = await CerrarAsync(cliente, admin, dia,
        [
            new SolicitudDepositoCierreSucursal("DOP", "BPD", "BOL-1", 5000m, dia.AddDays(1)),
            new SolicitudDepositoCierreSucursal("DOP", "BRD", "BOL-2", 900m, dia.AddDays(1)),
        ]);
        Assert.True(cerrado.Exitosa, cerrado.Mensaje);
        Assert.Contains("ya tiene su cierre", (await CerrarAsync(cliente, admin, dia, [])).Mensaje);

        // Un cierre de caja que llega después no cambia el consolidado, pero se avisa.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, TiposMensaje.TurnoCerrado, Cierre(dia, turnoDos + 1, esperado: 10m, declarado: 10m)));

        var cierres = await ObtenerAsync<List<DatosCierreSucursal>>(cliente, admin,
            $"/api/manager/cierres-sucursal?sucursalId={CentralEnPruebas.Sucursal}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}");
        var cierre = Assert.Single(cierres);
        Assert.Equal((2, 6000m, 5950m, -50m), (cierre.CantidadCierres, cierre.TotalEsperado, cierre.TotalDeclarado, cierre.Diferencia));
        Assert.Equal(("DOP", 5950m, 5900m, -50m), (cierre.Efectivo[0].Moneda, cierre.Efectivo[0].ADepositar, cierre.Efectivo[0].Depositado, cierre.Efectivo[0].Diferencia));
        Assert.Equal(["Banco Popular Dominicano", "Banreservas"], cierre.Depositos.Select(d => d.BancoNombre).Order().ToList());
        Assert.Equal(1, cierre.CierresPosteriores);
    }

    [SkippableFact]
    public async Task Sin_permiso_de_cierre_de_sucursal_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINCIE{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Cierre#2026", false, CatalogoPermisosCentral.ConsultarReportes);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Cierre#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/manager/cierres-sucursal?desde=2026-01-01&hasta=2026-01-31", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static DocumentoVentaCobrada Venta(DateOnly dia, long turno)
    {
        var cobrada = new DateTimeOffset(dia.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(-4));
        return new DocumentoVentaCobrada(CentralEnPruebas.NumeroDocumento(CentralEnPruebas.CajaUno, TipoDocumentoNumerado.Factura), turno, "C001",
            "Cajero Desarrollo", cobrada.AddMinutes(-5), cobrada, TipoComprobante.FacturaConsumo, null, "DOP", [],
            new DatosTotalesVenta(100m, 18m, 118m, 1, 1m, [new DatosDesgloseImpuesto(18m, 1, 100m, 18m, 118m)]), null,
            [new DocumentoPagoVenta(1, "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 118m, null, 118m, null, null, null, null, false)],
            118m, 0m, 0m, null, null, []);
    }

    private static DocumentoCierreTurno Cierre(DateOnly dia, long turno, decimal esperado, decimal declarado, decimal tarjeta = 0m)
    {
        List<DocumentoCierreFormaPago> formas = [new("EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 10, esperado, declarado, declarado - esperado)];
        if (tarjeta > 0)
            formas.Add(new DocumentoCierreFormaPago("TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 3, tarjeta, tarjeta, 0m));

        return new DocumentoCierreTurno(turno, 1, dia, true, 0m, false, "DOP", 10, esperado + tarjeta, 0m, esperado, declarado, declarado - esperado,
            "Cajero Desarrollo", new DateTimeOffset(dia.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(-4)),
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-4)), formas, [], []);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, string tipo, object documento)
    {
        var referencia = documento switch
        {
            DocumentoVentaCobrada venta => venta.Numero,
            DocumentoCierreTurno cierre => cierre.TurnoNumero.ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentException("Documento no esperado.", nameof(documento)),
        };
        var contenido = JsonSerializer.Serialize(documento, OpcionesJson.Predeterminadas);
        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), tipo, referencia, contenido, HashSincronizacion.Calcular(contenido), sucursal, caja,
            DateTimeOffset.UtcNow);
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<RespuestaAdministracion> CerrarAsync(HttpClient cliente, string token, DateOnly dia, IReadOnlyList<SolicitudDepositoCierreSucursal> depositos)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/manager/cierres-sucursal", token,
            new SolicitudCierreSucursal(CentralEnPruebas.Sucursal, dia, depositos, "Cierre de prueba")));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
