using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Fidelidad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiFidelidadPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task El_central_suma_los_puntos_de_todas_las_cajas_y_publica_el_saldo_en_el_maestro_del_miembro()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var cedula = CedulaValida();
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenUno, Inscripcion(cedula)));
        var miembroId = (await BuscarMiembroAsync(cliente, admin, cedula)).Id;

        var vencePronto = DateOnly.FromDateTime(DateTime.Today).AddDays(20);
        var venceDespues = DateOnly.FromDateTime(DateTime.Today).AddMonths(8);
        var acumulacion = Movimiento(CentralEnPruebas.CajaUno, cedula, TipoMovimientoPuntos.Acumulacion, 120, vencePronto);
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenUno, acumulacion));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenDos,
            Movimiento(CentralEnPruebas.CajaDos, cedula, TipoMovimientoPuntos.Acumulacion, 80, venceDespues)));

        // Un reenvío del mismo movimiento no acumula dos veces, ni aunque llegue en otro mensaje: lo identifican el documento y el tipo.
        Assert.Equal(EstadoRecepcion.Duplicado, await EnviarAsync(cliente, tokenUno, acumulacion));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenUno, acumulacion with { Id = Guid.CreateVersion7() }));

        // El canje gasta primero los puntos que vencen antes, para que el cliente no los pierda.
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, tokenDos,
            Movimiento(CentralEnPruebas.CajaDos, cedula, TipoMovimientoPuntos.Canje, -50, null)));

        var miembro = await BuscarMiembroAsync(cliente, admin, cedula);
        Assert.Equal((150, 70, vencePronto), (miembro.Puntos, miembro.PuntosPorVencer, miembro.ProximoVencimiento));

        // El saldo baja a las cajas dentro del maestro del miembro (RF-240).
        var maestro = Assert.Single((await BajarAsync(cliente, tokenUno, 0)).Maestros!.MiembrosFidelidad!, m => m.Cedula == cedula);
        Assert.Equal((150, 70, vencePronto), (maestro.SaldoPuntos, maestro.PuntosPorVencer, maestro.ProximoVencimiento));
        Assert.NotNull(maestro.SaldoAl);

        var movimientos = await ObtenerAsync<List<DatosMovimientoPuntosCentral>>(cliente, admin, $"/api/manager/fidelidad/miembros/{miembroId}/movimientos");
        Assert.Equal(3, movimientos.Count);
        Assert.All(movimientos, m => Assert.Equal(OrigenMovimientoPuntos.Caja, m.Origen));
    }

    [SkippableFact]
    public async Task Un_movimiento_que_llega_antes_que_la_inscripcion_se_suma_al_publicar_el_miembro()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var cedula = CedulaValida();

        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token,
            Movimiento(CentralEnPruebas.CajaUno, cedula, TipoMovimientoPuntos.Acumulacion, 45, null)));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Inscripcion(cedula)));

        var maestro = Assert.Single((await BajarAsync(cliente, token, 0)).Maestros!.MiembrosFidelidad!, m => m.Cedula == cedula);
        Assert.Equal(45, maestro.SaldoPuntos);
        Assert.Equal(45, (await BuscarMiembroAsync(cliente, admin, cedula)).Puntos);
    }

    [SkippableFact]
    public async Task El_central_ajusta_puntos_con_motivo_y_no_deja_el_saldo_en_negativo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        var cedula = CedulaValida();
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, Inscripcion(cedula)));
        var miembroId = (await BuscarMiembroAsync(cliente, admin, cedula)).Id;

        var sinMotivo = await AjustarAsync(cliente, admin, miembroId, 100, "  ");
        Assert.False(sinMotivo.Exitosa);
        Assert.Contains("motivo", sinMotivo.Mensaje);

        var aFavor = await AjustarAsync(cliente, admin, miembroId, 100, "Reclamo de puntos no acumulados");
        Assert.True(aFavor.Exitosa, aFavor.Mensaje);
        Assert.Equal(100, aFavor.Miembro!.Puntos);

        var excesivo = await AjustarAsync(cliente, admin, miembroId, -150, "Corrección");
        Assert.False(excesivo.Exitosa);
        Assert.Contains("solo tiene 100", excesivo.Mensaje);

        Assert.True((await AjustarAsync(cliente, admin, miembroId, -40, "Corrección de la acumulación")).Exitosa);
        Assert.Equal(60, (await BuscarMiembroAsync(cliente, admin, cedula)).Puntos);

        var movimientos = await ObtenerAsync<List<DatosMovimientoPuntosCentral>>(cliente, admin, $"/api/manager/fidelidad/miembros/{miembroId}/movimientos");
        Assert.Equal(2, movimientos.Count);
        Assert.All(movimientos, m => Assert.Equal(OrigenMovimientoPuntos.Central, m.Origen));
        Assert.Contains(movimientos, m => m.Motivo == "Corrección de la acumulación");

        var identificador = miembroId.ToString();
        var auditoria = await central.UsarContextoAsync(contexto => contexto.Auditoria
            .Where(r => r.Accion == "Fidelidad.PuntosAjustados" && r.EntidadId == identificador)
            .CountAsync());
        Assert.Equal(2, auditoria);
    }

    [SkippableFact]
    public async Task Sin_permiso_de_fidelidad_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINFID{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Fidelidad#2026", false, CatalogoPermisosCentral.AdministrarMaestros);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Fidelidad#2026")).Cuerpo!.TokenAcceso!;

        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/manager/fidelidad/miembros", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static MensajeSincronizacion Inscripcion(string cedula)
    {
        var contenido = JsonSerializer.Serialize(new DocumentoInscripcionFidelidad(cedula, "Miembro de Fidelidad", null, null, "Cajero Desarrollo", DateTimeOffset.UtcNow),
            OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.InscripcionFidelidad, cedula, contenido,
            HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno).Sucursal, CentralEnPruebas.CodigosCaja(CentralEnPruebas.CajaUno).Caja, DateTimeOffset.UtcNow);
    }

    /// <summary>Movimiento de una factura nueva de esa caja.</summary>
    private static MensajeSincronizacion Movimiento(int cajaId, string cedula, TipoMovimientoPuntos tipo, int puntos, DateOnly? venceEn)
    {
        var factura = CentralEnPruebas.NumeroDocumento(cajaId, CgPos.Dominio.Comun.TipoDocumentoNumerado.Factura);
        var contenido = JsonSerializer.Serialize(new DocumentoMovimientoPuntos(cedula, tipo, puntos, factura, DateTimeOffset.UtcNow, venceEn),
            OpcionesJson.Predeterminadas);
        return new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.MovimientoPuntos, $"{factura}-{tipo}", contenido, HashSincronizacion.Calcular(contenido), CentralEnPruebas.CodigosCaja(cajaId).Sucursal, CentralEnPruebas.CodigosCaja(cajaId).Caja, DateTimeOffset.UtcNow);
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static async Task<DatosMiembroFidelidadCentral> BuscarMiembroAsync(HttpClient cliente, string token, string cedula)
    {
        var pagina = await ObtenerAsync<PaginaMiembrosFidelidadCentral>(cliente, token, $"/api/manager/fidelidad/miembros?buscar={cedula}");
        return Assert.Single(pagina.Elementos);
    }

    private static async Task<RespuestaAjustePuntos> AjustarAsync(HttpClient cliente, string token, int miembroId, int puntos, string motivo)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, $"/api/manager/fidelidad/miembros/{miembroId}/ajustes", token,
            new SolicitudAjustePuntos(puntos, motivo)));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaAjustePuntos>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/sincronizacion/maestros?desde={desde}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<PaqueteBajadaMaestros>(OpcionesJson.Predeterminadas))!;
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }

    /// <summary>Una cédula nueva que pasa la validación del dominio.</summary>
    private static string CedulaValida()
    {
        for (var intento = 0; intento < 1000; intento++)
        {
            var candidata = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString("D11");
            try
            {
                return MiembroFidelidad.ValidarCedula(candidata);
            }
            catch (ArgumentException)
            {
            }
        }

        throw new InvalidOperationException("No se pudo generar una cédula válida.");
    }
}
