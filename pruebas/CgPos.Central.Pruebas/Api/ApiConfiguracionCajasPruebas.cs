using System.Net;
using System.Net.Http.Json;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Infraestructura.Seguridad;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiConfiguracionCajasPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Rangos_de_e_cf_no_se_solapan_solo_se_amplian_y_bajan_a_su_caja()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCajaDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var marca = (await BajarAsync(cliente, tokenCajaDos, 0)).Hasta;
        var desde = Random.Shared.NextInt64(1_000_000, 9_000_000_000);
        var vence = new DateOnly(2028, 6, 30);

        var asignado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/fiscal/secuencias",
            new SolicitudSecuenciaEcf(CentralEnPruebas.CajaDos, TipoComprobante.FacturaCreditoFiscal, desde, desde + 99, vence));
        Assert.True(asignado.Cuerpo!.Exitosa, asignado.Cuerpo.Mensaje);
        var secuenciaId = asignado.Cuerpo.Id!.Value;

        var solapado = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/fiscal/secuencias",
            new SolicitudSecuenciaEcf(CentralEnPruebas.CajaUno, TipoComprobante.FacturaCreditoFiscal, desde + 50, desde + 150, vence));
        Assert.Equal(HttpStatusCode.BadRequest, solapado.Estado);
        Assert.Contains("se solapan", solapado.Cuerpo!.Mensaje);

        var tipoNoPermitido = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/fiscal/secuencias",
            new SolicitudSecuenciaEcf(CentralEnPruebas.CajaDos, TipoComprobante.Compras, desde + 1000, desde + 1100, vence));
        Assert.Equal(HttpStatusCode.BadRequest, tipoNoPermitido.Estado);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/fiscal/secuencias/{secuenciaId}",
            new SolicitudActualizarSecuenciaEcf(desde + 199, vence.AddMonths(6), true))).Cuerpo!.Exitosa);
        var reducido = await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/fiscal/secuencias/{secuenciaId}", new SolicitudActualizarSecuenciaEcf(desde + 150, vence, true));
        Assert.Contains("no puede reducirse", reducido.Cuerpo!.Mensaje);

        var listado = Assert.Single(await ListarAsync<DatosSecuenciaEcfCentral>(cliente, admin, "/api/fiscal/secuencias"), s => s.Id == secuenciaId);
        Assert.Equal((desde + 199, "02", (long?)null), (listado.Hasta, listado.CajaCodigo, listado.UltimoRecibido));

        var bajada = await BajarAsync(cliente, tokenCajaDos, marca);
        Assert.Equal(desde + 199, Assert.Single(bajada.Maestros!.SecuenciasEcf!, s => s.Id == secuenciaId).Hasta);
    }

    [SkippableFact]
    public async Task Solo_se_anulan_en_la_dgii_numeros_sin_usar_de_un_rango_que_la_caja_ya_no_usa()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var desde = Random.Shared.NextInt64(1_000_000, 9_000_000_000);
        var vence = new DateOnly(2028, 6, 30);
        var secuenciaId = (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/fiscal/secuencias",
            new SolicitudSecuenciaEcf(CentralEnPruebas.CajaDos, TipoComprobante.Gubernamental, desde, desde + 99, vence))).Cuerpo!.Id!.Value;
        var ruta = $"/api/fiscal/secuencias/{secuenciaId}/anulaciones";

        // Con el rango activo la caja podría usar esos números sin conexión.
        Assert.Contains("está activo", (await EnviarAsync(cliente, admin, HttpMethod.Post, ruta, new SolicitudAnulacionEcf(desde, desde + 99, "Caja dañada"))).Cuerpo!.Mensaje);

        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/fiscal/secuencias/{secuenciaId}",
            new SolicitudActualizarSecuenciaEcf(desde + 99, vence, false))).Cuerpo!.Exitosa);

        // La caja ya envió el e-CF número desde + 10: no se puede anular.
        await RegistrarRecibidoAsync(SecuenciaEcf.FormatearEncf(TipoComprobante.Gubernamental, desde + 10));
        var conUsado = await EnviarAsync(cliente, admin, HttpMethod.Post, ruta, new SolicitudAnulacionEcf(desde, desde + 20, "Caja dañada"));
        Assert.Contains("ya se usó", conUsado.Cuerpo!.Mensaje);
        Assert.Contains($"{desde + 11:N0}", conUsado.Cuerpo.Mensaje);

        var sinMotivo = await EnviarAsync(cliente, admin, HttpMethod.Post, ruta, new SolicitudAnulacionEcf(desde + 11, desde + 99, " "));
        Assert.Equal("Indique el motivo de la anulación.", sinMotivo.Cuerpo!.Mensaje);

        var anulada = await EnviarAsync(cliente, admin, HttpMethod.Post, ruta, new SolicitudAnulacionEcf(desde + 11, desde + 99, "Caja dañada"));
        Assert.True(anulada.Cuerpo!.Exitosa, anulada.Cuerpo.Mensaje);
        Assert.Contains(central.Dgii.Anulaciones, xml => xml.Contains(SecuenciaEcf.FormatearEncf(TipoComprobante.Gubernamental, desde + 11)));

        Assert.Contains("ya se anuló", (await EnviarAsync(cliente, admin, HttpMethod.Post, ruta, new SolicitudAnulacionEcf(desde + 50, desde + 60, "Otra vez"))).Cuerpo!.Mensaje);

        // Un rechazo de la DGII también queda registrado.
        central.Dgii.ProgramarAnulacion(new CgPos.Central.Aplicacion.Dgii.RespuestaAnulacionDgii(false, false, "Secuencia utilizada", null));
        var rechazada = await EnviarAsync(cliente, admin, HttpMethod.Post, ruta, new SolicitudAnulacionEcf(desde, desde + 9, "Prueba de rechazo"));
        Assert.Contains("rechazó", rechazada.Cuerpo!.Mensaje);

        var anulaciones = (await ListarAsync<DatosAnulacionEcf>(cliente, admin, "/api/fiscal/anulaciones")).Where(a => a.SecuenciaId == secuenciaId).ToList();
        Assert.Equal([(EstadoAnulacionEcf.Rechazada, 10L), (EstadoAnulacionEcf.Aceptada, 89L)], anulaciones.Select(a => (a.Estado, a.Cantidad)));
    }

    private Task RegistrarRecibidoAsync(string encf) =>
        central.UsarContextoAsync(async contexto =>
        {
            var ahora = DateTimeOffset.UtcNow;
            var documento = CgPos.Dominio.Sincronizacion.DocumentoRecibido.Recibir(Guid.CreateVersion7(), CentralEnPruebas.CajaDos, CentralEnPruebas.Sucursal,
                "Venta.Cobrada", Guid.CreateVersion7(), "{}", new string('A', CgPos.Dominio.Sincronizacion.DocumentoRecibido.LargoHash), ahora, ahora);
            contexto.DocumentosRecibidos.Add(documento);
            contexto.ComprobantesRecibidos.Add(CgPos.Dominio.Sincronizacion.ComprobanteRecibido.Registrar(documento, encf, TipoComprobante.Gubernamental,
                "<ECF/>", new string('B', CgPos.Dominio.Sincronizacion.DocumentoRecibido.LargoHash), ahora, ahora));
            return await contexto.SaveChangesAsync();
        });

    [SkippableFact]
    public async Task Usuarios_y_roles_de_caja_bajan_con_el_pin_como_hash_y_se_validan_antes_de_publicar()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var rol = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/usuarios-caja/roles",
            new SolicitudRolCaja($"RCJ{sufijo}", "Cajero de prueba", 1, [CatalogoPermisos.RegistrarVenta, CatalogoPermisos.AbrirTurno]));
        Assert.True(rol.Cuerpo!.Exitosa, rol.Cuerpo.Mensaje);
        var rolId = rol.Cuerpo.Id!.Value;

        Assert.Contains("no existe en el catálogo",
            (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/usuarios-caja/roles", new SolicitudRolCaja($"MAL{sufijo}", "Mal", 1, ["Ventas.NoExiste"]))).Cuerpo!.Mensaje);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/usuarios-caja/roles", new SolicitudRolCaja($"NIV{sufijo}", "Nivel", 10, []))).Estado);

        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var codigo = $"U{sufijo}";
        var usuario = await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/usuarios-caja/usuarios",
            new SolicitudUsuarioCaja(codigo, "Cajero Prueba", rolId, [CentralEnPruebas.CajaUno], Pin: "4321", Carne: $"CARNE-{sufijo}"));
        Assert.True(usuario.Cuerpo!.Exitosa, usuario.Cuerpo.Mensaje);
        var usuarioId = usuario.Cuerpo.Id!.Value;

        var listado = Assert.Single(await ListarAsync<DatosUsuarioCaja>(cliente, admin, "/api/usuarios-caja/usuarios"), u => u.Id == usuarioId);
        Assert.Equal((true, true, "Cajero de prueba"), (listado.TienePin, listado.TieneCarne, listado.RolNombre));

        var bajada = await BajarAsync(cliente, tokenCaja, marca);
        var publicado = Assert.Single(bajada.Organizacion!.Usuarios!, u => u.Id == usuarioId);
        Assert.Null(publicado.Pin);
        Assert.True(new HashCredenciales().VerificarPin("4321", publicado.PinHash!));

        // Editar sin PIN nuevo conserva el hash; el código no cambia.
        Assert.True((await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/usuarios-caja/usuarios/{usuarioId}",
            new SolicitudUsuarioCaja(codigo, "Cajero Renombrado", rolId, [CentralEnPruebas.CajaUno]))).Cuerpo!.Exitosa);
        var editado = Assert.Single((await BajarAsync(cliente, tokenCaja, bajada.Hasta)).Organizacion!.Usuarios!, u => u.Id == usuarioId);
        Assert.Equal(("Cajero Renombrado", publicado.PinHash, publicado.CredencialBarrasHash), (editado.Nombre, editado.PinHash, editado.CredencialBarrasHash));

        Assert.Contains("no se puede cambiar", (await EnviarAsync(cliente, admin, HttpMethod.Put, $"/api/usuarios-caja/usuarios/{usuarioId}",
            new SolicitudUsuarioCaja($"X{sufijo}", "Otro", rolId, []))).Cuerpo!.Mensaje);
        Assert.Contains("no tiene PIN", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/usuarios-caja/usuarios",
            new SolicitudUsuarioCaja($"S{sufijo}", "Sin PIN", rolId, []))).Cuerpo!.Mensaje);
        Assert.Contains("carné", (await EnviarAsync(cliente, admin, HttpMethod.Post, "/api/usuarios-caja/usuarios",
            new SolicitudUsuarioCaja($"C{sufijo}", "Mismo carné", rolId, [], Pin: "1234", Carne: $"CARNE-{sufijo}"))).Cuerpo!.Mensaje);
    }

    [SkippableFact]
    public async Task Sin_permiso_fiscal_ni_de_usuarios_de_caja_se_responde_403()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var codigo = $"SINCAJ{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        await central.CrearUsuarioAsync(codigo, "Sin.Permisos#2026", false, CatalogoPermisosCentral.ConsultarReportes);
        var token = (await CentralEnPruebas.IngresarAsync(cliente, codigo, "Sin.Permisos#2026")).Cuerpo!.TokenAcceso;

        using var secuencias = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/fiscal/secuencias", token));
        using var usuarios = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/usuarios-caja/usuarios", token));

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (secuencias.StatusCode, usuarios.StatusCode));
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
