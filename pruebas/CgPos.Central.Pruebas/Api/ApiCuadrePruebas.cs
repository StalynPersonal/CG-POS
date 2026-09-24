using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Turnos;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

/// <summary>
/// Módulo de cuadre: el supervisor de la tienda entra con su usuario de caja, ve los cierres que le entregaron y declara
/// lo que contó. La caja cierra sin declarar nada, así que sin este paso el cierre queda pendiente.
/// </summary>
[Collection(ColeccionCentral.Nombre)]
public class ApiCuadrePruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_supervisor_entra_con_su_usuario_de_caja_y_cuadra_el_cierre_que_le_entregaron()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        // La caja entrega el turno: informa lo esperado y nada más.
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(200, 900));
        var turno = Random.Shared.NextInt64(1_000, 999_999);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarCierreAsync(cliente, tokenCaja, dia, turno, esperado: 3000m, tarjeta: 1500m));

        // Con una clave incorrecta no se entra.
        Assert.Null((await IngresarAsync(cliente, "S001", "esta-no-es")).TokenAcceso);

        var sesion = await IngresarAsync(cliente, "S001", "Supervisor.2026");
        Assert.True(sesion.Exitosa, sesion.Mensaje);
        var token = sesion.TokenAcceso!;
        Assert.Contains("Cuadre.Declarar", sesion.Sesion!.Permisos);
        var sucursal = Assert.Single(sesion.Sesion.Sucursales).Id;

        // Ve su cierre esperando el conteo.
        var pendiente = Assert.Single(await PendientesAsync(cliente, token, sucursal), c => c.TurnoNumero == turno);
        Assert.True(pendiente.PendienteDeCuadre);
        Assert.Equal(4500m, pendiente.TotalEsperado);

        var efectivo = pendiente.FormasPago.Single(f => f.Tipo == TipoFormaPago.Efectivo);
        var tarjeta = pendiente.FormasPago.Single(f => f.Tipo == TipoFormaPago.Tarjeta);
        var denominaciones = await DenominacionesAsync(cliente, token);
        var billete = denominaciones.First(d => d.Moneda == "DOP" && d.Valor == 1000m);

        // El efectivo declarado tiene que ser el que sale del conteo.
        var conteoMalo = new SolicitudCuadreCierre(
            [new SolicitudDeclaracionCuadre(efectivo.Id, 3000m), new SolicitudDeclaracionCuadre(tarjeta.Id, 1500m)],
            [new SolicitudConteoCuadre(billete.Id, 2)]);
        Assert.Contains("no coincide con el conteo", (await CuadrarAsync(cliente, token, pendiente.Id, conteoMalo)).Cuerpo!.Mensaje);

        // Faltan 1,000 pesos: se contaron dos billetes de mil y el esperado eran tres.
        var cuadre = new SolicitudCuadreCierre(
            [new SolicitudDeclaracionCuadre(efectivo.Id, 2000m), new SolicitudDeclaracionCuadre(tarjeta.Id, 1500m)],
            [new SolicitudConteoCuadre(billete.Id, 2)]);
        Assert.True((await CuadrarAsync(cliente, token, pendiente.Id, cuadre)).Cuerpo!.Exitosa);

        Assert.DoesNotContain(await PendientesAsync(cliente, token, sucursal), c => c.TurnoNumero == turno);

        var cuadrado = Assert.Single(await CierresAsync(cliente, token, sucursal, dia), c => c.TurnoNumero == turno);
        Assert.False(cuadrado.PendienteDeCuadre);
        Assert.Equal((3500m, -1000m), (cuadrado.TotalDeclarado, cuadrado.Diferencia));
        Assert.Equal("Supervisor Desarrollo", cuadrado.CuadradoPor);

        // Cuadrar dos veces no se puede: para arreglar un cuadre está la corrección.
        Assert.Contains("ya lo cuadró", (await CuadrarAsync(cliente, token, pendiente.Id, cuadre)).Cuerpo!.Mensaje);

        Assert.True(await central.UsarContextoAsync(contexto => contexto.Auditoria.AsNoTracking()
            .AnyAsync(a => a.Accion == "Cuadre.CierreCuadrado" && a.EntidadId == cuadrado.Id.ToString())));
    }

    [SkippableFact]
    public async Task Los_reportes_del_modulo_muestran_el_dia_las_diferencias_de_la_cajera_y_los_retiros()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(1_000, 2_000));
        var turno = Random.Shared.NextInt64(1_000, 999_999);
        var cajera = $"Cajera {turno}";
        var retiro = new DocumentoMovimientoTurno(TipoMovimientoCaja.Retiro, 1, 2000m, "DOP", "Se llevó el exceso a la bóveda", cajera, null,
            "Supervisor Desarrollo", new DateTimeOffset(dia.ToDateTime(new TimeOnly(14, 0)), TimeSpan.FromHours(-4)));
        // La caja cerró su lote de tarjetas: falta una aprobación de cada lado, que es lo que hay que investigar.
        var lote = new DocumentoLoteTarjetas("L-0001", 3, 1600m, 3, 1500m, 100m, true, cajera,
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(17, 30)), TimeSpan.FromHours(-4)), ["A-111"], ["A-999"]);
        Assert.Equal(EstadoRecepcion.Recibido,
            await EnviarCierreAsync(cliente, tokenCaja, dia, turno, esperado: 3000m, tarjeta: 1500m, cajera: cajera, movimientos: [retiro], lote: lote));

        var sesion = await IngresarAsync(cliente, "S001", "Supervisor.2026");
        var token = sesion.TokenAcceso!;
        var sucursal = sesion.Sesion!.Sucursales[0].Id;

        // Antes de cuadrar, el día ya se puede mirar: se ve lo esperado y que falta una caja por contar.
        var pendiente = Assert.Single(await PendientesAsync(cliente, token, sucursal), c => c.TurnoNumero == turno);
        var antes = await ResumenAsync(cliente, token, sucursal, dia);
        Assert.Equal((1, 4500m), (antes.CajasPendientes, antes.TotalEsperado));

        var efectivo = pendiente.FormasPago.Single(f => f.Tipo == TipoFormaPago.Efectivo);
        var tarjeta = pendiente.FormasPago.Single(f => f.Tipo == TipoFormaPago.Tarjeta);
        var billete = (await DenominacionesAsync(cliente, token)).First(d => d.Moneda == "DOP" && d.Valor == 1000m);
        Assert.True((await CuadrarAsync(cliente, token, pendiente.Id, new SolicitudCuadreCierre(
            [new SolicitudDeclaracionCuadre(efectivo.Id, 3000m), new SolicitudDeclaracionCuadre(tarjeta.Id, 1600m)],
            [new SolicitudConteoCuadre(billete.Id, 3)]))).Cuerpo!.Exitosa);

        // El resumen del día suma por forma de pago y ya no tiene cajas pendientes.
        var resumen = await ResumenAsync(cliente, token, sucursal, dia);
        Assert.Equal((0, 4600m, 100m), (resumen.CajasPendientes, resumen.TotalDeclarado, resumen.Diferencia));
        Assert.Equal(1500m, resumen.FormasPago.Single(f => f.Tipo == TipoFormaPago.Tarjeta).Esperado);

        // A la cajera le sobraron 100 pesos, y la diferencia es suya.
        var diferencia = Assert.Single(await DiferenciasAsync(cliente, token, sucursal, dia), d => d.UsuarioNombre == cajera);
        Assert.Equal((1, 1, 0m, 100m), (diferencia.Cierres, diferencia.CierresConDiferencia, diferencia.Faltantes, diferencia.Sobrantes));

        // El retiro del turno se guardó con su motivo y con quién lo autorizó.
        var movimiento = Assert.Single(await MovimientosAsync(cliente, token, sucursal, dia));
        Assert.Equal((TipoMovimientoCaja.Retiro, 2000m, "Supervisor Desarrollo"),
            (movimiento.Tipo, movimiento.Monto, movimiento.AutorizadoPorNombre));

        // El lote del terminal quedó junto al cierre, con las aprobaciones que aparecieron de un solo lado.
        var cerrado = Assert.Single(await CierresAsync(cliente, token, sucursal, dia), c => c.TurnoNumero == turno);
        Assert.Equal(("L-0001", 1600m, 1500m, 100m), (cerrado.Lote!.NumeroLote, cerrado.Lote.MontoCaja, cerrado.Lote.MontoTerminal, cerrado.Lote.Diferencia));
        Assert.Equal("A-111", Assert.Single(cerrado.Lote.SoloEnCaja));
        Assert.Equal("A-999", Assert.Single(cerrado.Lote.SoloEnTerminal));

        // Y el cuadre se vuelve a imprimir.
        var pdf = await DescargarCuadreAsync(cliente, token, pendiente.Id, sucursal);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf[..4]), StringComparison.Ordinal);

        // El supervisor no imprime ni consulta lo de otra sucursal.
        using var ajena = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/cuadre/resumen?sucursalId={sucursal + 1000}&dia={dia:yyyy-MM-dd}", token));
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
    }

    [SkippableFact]
    public async Task El_reporte_de_tiempos_separa_la_parada_prevista_de_la_imprevista()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(-Random.Shared.Next(2_000, 3_000));
        var turno = Random.Shared.NextInt64(1_000, 999_999);
        var cajera = $"Cajera {turno}";
        var numero = (int)(turno % 100_000);

        // Una hora de almuerzo (tiempo previsto) y diez minutos de baño (imprevisto).
        Assert.Equal(EstadoRecepcion.Recibido,
            await EnviarParadaAsync(cliente, tokenCaja, dia, turno, numero, cajera, 2, "Almuerzo", programado: true, minutos: 60, hora: 12));
        Assert.Equal(EstadoRecepcion.Recibido,
            await EnviarParadaAsync(cliente, tokenCaja, dia, turno, numero + 1, cajera, 1, "Baño", programado: false, minutos: 10, hora: 15));

        var sesion = await IngresarAsync(cliente, "S001", "Supervisor.2026");
        var token = sesion.TokenAcceso!;
        var sucursal = sesion.Sesion!.Sucursales[0].Id;

        var paradas = await ObtenerAsync<DatosTiemposParada>(cliente, token,
            $"/api/cuadre/tiempos-parada?sucursalId={sucursal}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}");

        Assert.Equal((2, 70, 60, 10), (paradas.Paradas, paradas.Minutos, paradas.MinutosProgramados, paradas.MinutosImprevistos));
        Assert.Equal(70, Assert.Single(paradas.PorCajero, t => t.Nombre == cajera).Minutos);
        Assert.Equal(60, Assert.Single(paradas.PorMotivo, t => t.Nombre == "Almuerzo").Minutos);
        Assert.Equal("Baño", paradas.Detalle[0].MotivoNombre);

        // El supervisor no ve los tiempos de otra tienda.
        using var ajena = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/cuadre/tiempos-parada?sucursalId={sucursal + 1000}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}", token));
        Assert.Equal(HttpStatusCode.Forbidden, ajena.StatusCode);
    }

    [SkippableFact]
    public async Task El_cajero_sin_permiso_de_cuadre_no_entra_al_modulo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();

        var rechazado = await IngresarAsync(cliente, "C001", "Cajero.2026");

        Assert.False(rechazado.Exitosa);
        Assert.Contains("no tiene acceso", rechazado.Mensaje);
    }

    private static async Task<RespuestaSesionCuadre> IngresarAsync(HttpClient cliente, string usuario, string clave)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/cuadre/ingreso", null,
            new SolicitudIngresoCuadre(usuario, clave)));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaSesionCuadre>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<IReadOnlyList<DatosCierreCaja>> PendientesAsync(HttpClient cliente, string token, int sucursalId) =>
        await ObtenerAsync<List<DatosCierreCaja>>(cliente, token, $"/api/cuadre/pendientes?sucursalId={sucursalId}");

    private static async Task<IReadOnlyList<DatosCierreCaja>> CierresAsync(HttpClient cliente, string token, int sucursalId, DateOnly dia) =>
        await ObtenerAsync<List<DatosCierreCaja>>(cliente, token, $"/api/cuadre/cierres?sucursalId={sucursalId}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}");

    private static async Task<DatosResumenCuadre> ResumenAsync(HttpClient cliente, string token, int sucursalId, DateOnly dia) =>
        await ObtenerAsync<DatosResumenCuadre>(cliente, token, $"/api/cuadre/resumen?sucursalId={sucursalId}&dia={dia:yyyy-MM-dd}");

    private static async Task<IReadOnlyList<DatosDiferenciaCajero>> DiferenciasAsync(HttpClient cliente, string token, int sucursalId, DateOnly dia) =>
        await ObtenerAsync<List<DatosDiferenciaCajero>>(cliente, token,
            $"/api/cuadre/diferencias?sucursalId={sucursalId}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}");

    private static async Task<IReadOnlyList<DatosMovimientoTurno>> MovimientosAsync(HttpClient cliente, string token, int sucursalId, DateOnly dia) =>
        await ObtenerAsync<List<DatosMovimientoTurno>>(cliente, token,
            $"/api/cuadre/movimientos?sucursalId={sucursalId}&desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}");

    private static async Task<byte[]> DescargarCuadreAsync(HttpClient cliente, string token, int cierreId, int sucursalId)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/cuadre/{cierreId}/pdf?sucursalId={sucursalId}", token));
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadAsByteArrayAsync();
    }

    private static async Task<IReadOnlyList<CgPos.Contratos.Catalogo.DatosDenominacion>> DenominacionesAsync(HttpClient cliente, string token) =>
        await ObtenerAsync<List<CgPos.Contratos.Catalogo.DatosDenominacion>>(cliente, token, "/api/cuadre/denominaciones");

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<(HttpStatusCode Estado, RespuestaAdministracion? Cuerpo)> CuadrarAsync(HttpClient cliente, string token, int cierreId,
        SolicitudCuadreCierre solicitud)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/cuadre/{cierreId}", token, solicitud));
        return (respuesta.StatusCode, await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas));
    }

    private static async Task<EstadoRecepcion?> EnviarCierreAsync(HttpClient cliente, string token, DateOnly dia, long turno, decimal esperado, decimal tarjeta,
        string cajera = "Cajero Desarrollo", IReadOnlyList<DocumentoMovimientoTurno>? movimientos = null, DocumentoLoteTarjetas? lote = null)
    {
        var cierre = new DocumentoCierreTurno(turno, 1, dia, 0m, false, "DOP", 8, esperado + tarjeta, 0m, esperado + tarjeta, cajera,
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(-4)),
            new DateTimeOffset(dia.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-4)),
            [
                new DocumentoCierreFormaPago("EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", 8, esperado),
                new DocumentoCierreFormaPago("TAR", "Tarjeta", TipoFormaPago.Tarjeta, "DOP", 2, tarjeta),
            ],
            movimientos ?? [],
            lote);

        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var contenido = JsonSerializer.Serialize(cierre, OpcionesJson.Predeterminadas);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.TurnoCerrado,
            turno.ToString(System.Globalization.CultureInfo.InvariantCulture), contenido, HashSincronizacion.Calcular(contenido), sucursal, caja,
            DateTimeOffset.UtcNow);

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<EstadoRecepcion?> EnviarParadaAsync(HttpClient cliente, string token, DateOnly dia, long turno, int numero, string cajera,
        int motivoCodigo, string motivoNombre, bool programado, int minutos, int hora)
    {
        var desde = new DateTimeOffset(dia.ToDateTime(new TimeOnly(hora, 0)), TimeSpan.FromHours(-4));
        var parada = new DocumentoSuspensionCaja(numero, turno, dia, cajera, motivoCodigo, motivoNombre, programado, null,
            desde, desde.AddMinutes(minutos), false);

        var (sucursal, caja) = CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno);
        var contenido = JsonSerializer.Serialize(parada, OpcionesJson.Predeterminadas);
        var mensaje = new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.SuspensionCaja,
            $"{turno}-{numero}", contenido, HashSincronizacion.Calcular(contenido), sucursal, caja, DateTimeOffset.UtcNow);

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }
}
