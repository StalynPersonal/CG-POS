using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;

namespace CgPos.Central.Web.Seguridad;

/// <summary>Llamadas a la API del Central con la sesión del usuario.</summary>
public sealed class ClienteCentral(IHttpClientFactory fabricaHttp)
{
    private HttpClient Http => fabricaHttp.CreateClient(ServicioSesionCentral.NombreHttp);

    // ---------- Seguridad del Central ----------

    public Task<IReadOnlyList<DatosPermisoCentral>?> ListarPermisosAsync() => ListarAsync<DatosPermisoCentral>("api/seguridad/permisos");

    public Task<IReadOnlyList<DatosRolCentral>?> ListarRolesAsync() => ListarAsync<DatosRolCentral>("api/seguridad/roles");

    public Task<RespuestaAdministracion> CrearRolAsync(SolicitudRolCentral solicitud) => EnviarAsync(HttpMethod.Post, "api/seguridad/roles", solicitud);

    public Task<RespuestaAdministracion> ActualizarRolAsync(Guid rolId, SolicitudRolCentral solicitud) => EnviarAsync(HttpMethod.Put, $"api/seguridad/roles/{rolId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoRolAsync(Guid rolId, bool activo) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/roles/{rolId}/{(activo ? "activar" : "desactivar")}");

    public Task<IReadOnlyList<DatosUsuarioCentral>?> ListarUsuariosAsync() => ListarAsync<DatosUsuarioCentral>("api/seguridad/usuarios");

    public Task<RespuestaAdministracion> CrearUsuarioAsync(SolicitudUsuarioCentral solicitud) => EnviarAsync(HttpMethod.Post, "api/seguridad/usuarios", solicitud);

    public Task<RespuestaAdministracion> ActualizarUsuarioAsync(Guid usuarioId, SolicitudActualizarUsuarioCentral solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/seguridad/usuarios/{usuarioId}", solicitud);

    public Task<RespuestaAdministracion> RestablecerContrasenaAsync(Guid usuarioId, string contrasenaTemporal) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/contrasena", new SolicitudContrasenaTemporal(contrasenaTemporal));

    public Task<RespuestaAdministracion> DesbloquearUsuarioAsync(Guid usuarioId) => EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/desbloquear");

    public Task<RespuestaAdministracion> CambiarEstadoUsuarioAsync(Guid usuarioId, bool activo) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/{(activo ? "activar" : "desactivar")}");

    // ---------- Organización ----------

    public async Task<DatosEmpresa?> ObtenerEmpresaAsync()
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosEmpresa>("api/organizacion/empresa", OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> ActualizarEmpresaAsync(SolicitudEmpresa solicitud) => EnviarAsync(HttpMethod.Put, "api/organizacion/empresa", solicitud);

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesAsync() => ListarAsync<DatosSucursal>("api/organizacion/sucursales");

    public Task<RespuestaAdministracion> CrearSucursalAsync(SolicitudSucursal solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/sucursales", solicitud);

    public Task<RespuestaAdministracion> ActualizarSucursalAsync(Guid sucursalId, SolicitudSucursal solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/sucursales/{sucursalId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoSucursalAsync(Guid sucursalId, bool activa) =>
        EnviarAsync(HttpMethod.Post, $"api/organizacion/sucursales/{sucursalId}/{(activa ? "activar" : "desactivar")}");

    public Task<IReadOnlyList<DatosCaja>?> ListarCajasAsync() => ListarAsync<DatosCaja>("api/organizacion/cajas");

    public Task<RespuestaAdministracion> CrearCajaAsync(SolicitudCaja solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/cajas", solicitud);

    public Task<RespuestaAdministracion> ActualizarCajaAsync(Guid cajaId, SolicitudActualizarCaja solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/cajas/{cajaId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoCajaAsync(Guid cajaId, bool habilitada) =>
        EnviarAsync(HttpMethod.Post, $"api/organizacion/cajas/{cajaId}/{(habilitada ? "habilitar" : "deshabilitar")}");

    public Task<IReadOnlyList<DefinicionParametro>?> ListarCatalogoParametrosAsync() => ListarAsync<DefinicionParametro>("api/organizacion/parametros/catalogo");

    public Task<IReadOnlyList<DatosParametro>?> ListarParametrosAsync() => ListarAsync<DatosParametro>("api/organizacion/parametros");

    public Task<RespuestaAdministracion> CrearParametroAsync(SolicitudParametro solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/parametros", solicitud);

    public Task<RespuestaAdministracion> CambiarValorParametroAsync(Guid parametroId, string valor) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/parametros/{parametroId}", new SolicitudValorParametro(valor));

    // ---------- Credenciales de las cajas ----------

    /// <returns>La credencial recién emitida (su secreto solo se ve aquí) o el motivo por el que no se emitió.</returns>
    public async Task<(DatosCredencialDispositivo? Credencial, string? Error)> EmitirCredencialAsync(Guid cajaId)
    {
        try
        {
            using var respuesta = await Http.PostAsync($"api/cajas/{cajaId}/credencial", null);
            if (respuesta.IsSuccessStatusCode)
                return (await respuesta.Content.ReadFromJsonAsync<DatosCredencialDispositivo>(OpcionesJson.Predeterminadas), null);

            return (null, respuesta.StatusCode switch
            {
                HttpStatusCode.Forbidden => "No tiene permiso para emitir credenciales de caja.",
                HttpStatusCode.NotFound => "La caja no existe.",
                HttpStatusCode.Unauthorized => "La sesión venció. Ingrese nuevamente.",
                _ => $"El Central respondió {(int)respuesta.StatusCode}.",
            });
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return (null, ServicioSesionCentral.SinComunicacion);
        }
    }

    public Task<RespuestaAdministracion> RevocarCredencialAsync(Guid cajaId, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"api/cajas/{cajaId}/credencial/revocar", new SolicitudRevocacionCredencial(motivo));

    // ---------- Rangos de e-CF ----------

    public Task<IReadOnlyList<DatosSecuenciaEcfCentral>?> ListarSecuenciasAsync() => ListarAsync<DatosSecuenciaEcfCentral>("api/fiscal/secuencias");

    public Task<RespuestaAdministracion> AsignarSecuenciaAsync(SolicitudSecuenciaEcf solicitud) => EnviarAsync(HttpMethod.Post, "api/fiscal/secuencias", solicitud);

    public Task<RespuestaAdministracion> ActualizarSecuenciaAsync(Guid secuenciaId, SolicitudActualizarSecuenciaEcf solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/fiscal/secuencias/{secuenciaId}", solicitud);

    // ---------- Usuarios y roles de caja ----------

    public Task<IReadOnlyList<CgPos.Dominio.Seguridad.DefinicionPermiso>?> ListarPermisosCajaAsync() =>
        ListarAsync<CgPos.Dominio.Seguridad.DefinicionPermiso>("api/usuarios-caja/permisos");

    public Task<IReadOnlyList<DatosRolCaja>?> ListarRolesCajaAsync() => ListarAsync<DatosRolCaja>("api/usuarios-caja/roles");

    public Task<RespuestaAdministracion> GuardarRolCajaAsync(Guid? rolId, SolicitudRolCaja solicitud) =>
        rolId is { } id ? EnviarAsync(HttpMethod.Put, $"api/usuarios-caja/roles/{id}", solicitud) : EnviarAsync(HttpMethod.Post, "api/usuarios-caja/roles", solicitud);

    public Task<IReadOnlyList<DatosUsuarioCaja>?> ListarUsuariosCajaAsync() => ListarAsync<DatosUsuarioCaja>("api/usuarios-caja/usuarios");

    public Task<RespuestaAdministracion> GuardarUsuarioCajaAsync(Guid? usuarioId, SolicitudUsuarioCaja solicitud) =>
        usuarioId is { } id
            ? EnviarAsync(HttpMethod.Put, $"api/usuarios-caja/usuarios/{id}", solicitud)
            : EnviarAsync(HttpMethod.Post, "api/usuarios-caja/usuarios", solicitud);

    // ---------- Catálogos de maestros ----------

    /// <param name="ruta">Ruta del catálogo (ej. "familias"); los registros llegan en su formato de carga.</param>
    public Task<IReadOnlyList<DatosMaestroCentral<System.Text.Json.Nodes.JsonObject>>?> ListarCatalogoAsync(string ruta) =>
        ListarAsync<DatosMaestroCentral<System.Text.Json.Nodes.JsonObject>>($"api/maestros/{ruta}");

    /// <summary>Crea o cambia el registro con ese Id.</summary>
    public Task<RespuestaAdministracion> GuardarCatalogoAsync(string ruta, Guid id, System.Text.Json.Nodes.JsonObject dato) =>
        EnviarAsync(HttpMethod.Put, $"api/maestros/{ruta}/{id}", dato);

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesMaestrosAsync() => ListarAsync<DatosSucursal>("api/maestros/sucursales");

    /// <summary>Catálogo tipado (familias, unidades…) para referencias.</summary>
    public Task<IReadOnlyList<DatosMaestroCentral<T>>?> ListarMaestroAsync<T>(string ruta) => ListarAsync<DatosMaestroCentral<T>>($"api/maestros/{ruta}");

    // ---------- Clientes, artículos y precios ----------

    public Task<PaginaMaestros<CgPos.Contratos.Catalogo.ClienteCarga>?> BuscarClientesAsync(string? texto, int pagina, int tamano, CancellationToken cancelacion = default) =>
        BuscarAsync<CgPos.Contratos.Catalogo.ClienteCarga>("api/maestros/clientes", texto, pagina, tamano, cancelacion);

    public Task<RespuestaAdministracion> GuardarClienteAsync(CgPos.Contratos.Catalogo.ClienteCarga cliente) =>
        EnviarAsync(HttpMethod.Put, $"api/maestros/clientes/{cliente.Id}", cliente);

    /// <param name="modulo">"maestros" o "precios", según el permiso con el que se consulta.</param>
    public Task<PaginaMaestros<CgPos.Contratos.Catalogo.ArticuloCarga>?> BuscarArticulosAsync(string modulo, string? texto, int pagina, int tamano,
        CancellationToken cancelacion = default) =>
        BuscarAsync<CgPos.Contratos.Catalogo.ArticuloCarga>($"api/{modulo}/articulos", texto, pagina, tamano, cancelacion);

    /// <returns>Nulo si no se pudo consultar.</returns>
    private async Task<PaginaMaestros<T>?> BuscarAsync<T>(string ruta, string? texto, int pagina, int tamano, CancellationToken cancelacion)
    {
        try
        {
            return await Http.GetFromJsonAsync<PaginaMaestros<T>>(
                $"{ruta}?buscar={Uri.EscapeDataString(texto ?? string.Empty)}&pagina={pagina}&tamano={tamano}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> GuardarArticuloAsync(CgPos.Contratos.Catalogo.ArticuloCarga articulo) =>
        EnviarAsync(HttpMethod.Put, $"api/maestros/articulos/{articulo.Id}", articulo);

    public Task<RespuestaAdministracion> CambiarPreciosAsync(Guid articuloId, SolicitudPreciosArticulo solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/precios/articulos/{articuloId}", solicitud);

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.FamiliaCarga>>?> ListarFamiliasPreciosAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.FamiliaCarga>>("api/precios/familias");

    public Task<IReadOnlyList<DatosTopeDescuentoCentral>?> ListarTopesAsync() => ListarAsync<DatosTopeDescuentoCentral>("api/precios/topes");

    public Task<RespuestaAdministracion> GuardarTopeAsync(CgPos.Contratos.Catalogo.TopeDescuentoCarga tope) =>
        EnviarAsync(HttpMethod.Put, $"api/precios/topes/{tope.Id}", tope);

    // ---------- Promociones ----------

    public Task<IReadOnlyList<DatosPromocionCentral>?> ListarPromocionesAsync() => ListarAsync<DatosPromocionCentral>("api/promociones");

    public Task<RespuestaAdministracion> GuardarPromocionAsync(CgPos.Contratos.Catalogo.PromocionCarga promocion) =>
        EnviarAsync(HttpMethod.Put, $"api/promociones/{promocion.Id}", promocion);

    public Task<(ResultadoImportacionPromociones? Datos, string? Error)> ImportarPromocionesAsync(SolicitudImportacionPromociones solicitud) =>
        PostearAsync<ResultadoImportacionPromociones>("api/promociones/importar", solicitud);

    public Task<(ResultadoSimulacionPromociones? Datos, string? Error)> SimularPromocionesAsync(SolicitudSimulacionPromociones solicitud) =>
        PostearAsync<ResultadoSimulacionPromociones>("api/promociones/simular", solicitud);

    public async Task<IReadOnlyList<CgPos.Contratos.Catalogo.ArticuloCarga>> ArticulosPromocionAsync(IReadOnlyList<Guid> ids) =>
        ids.Count == 0 ? [] : (await PostearAsync<List<CgPos.Contratos.Catalogo.ArticuloCarga>>("api/promociones/articulos/por-id", ids)).Datos ?? [];

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.FamiliaCarga>>?> ListarFamiliasPromocionesAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.FamiliaCarga>>("api/promociones/familias");

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesPromocionesAsync() => ListarAsync<DatosSucursal>("api/promociones/sucursales");

    // ---------- Monitor de sincronización ----------

    public async Task<DatosMonitorCentral?> ObtenerMonitorAsync()
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosMonitorCentral>("api/monitor", OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<PaginaComprobantesDgii?> BuscarComprobantesDgiiAsync(CgPos.Dominio.Sincronizacion.EstadoEnvioDgii? estado, Guid? cajaId, string? buscar,
        bool soloConFallo, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        var ruta = $"api/monitor/comprobantes?pagina={pagina}&tamano={tamano}&soloConFallo={(soloConFallo ? "true" : "false")}"
                   + (estado is { } e ? $"&estado={e}" : string.Empty)
                   + (cajaId is { } c ? $"&cajaId={c}" : string.Empty)
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<PaginaComprobantesDgii>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<string?> ObtenerXmlComprobanteAsync(Guid comprobanteId)
    {
        try
        {
            return await Http.GetStringAsync($"api/monitor/comprobantes/{comprobanteId}/xml");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> ReenviarComprobanteAsync(Guid comprobanteId) => EnviarAsync(HttpMethod.Post, $"api/monitor/comprobantes/{comprobanteId}/reenviar");

    public Task<IReadOnlyList<DatosConflictoSincronizacion>?> ListarConflictosAsync(bool abiertos) =>
        ListarAsync<DatosConflictoSincronizacion>($"api/monitor/conflictos?abiertos={(abiertos ? "true" : "false")}");

    public Task<RespuestaAdministracion> ResolverConflictoAsync(Guid conflictoId, string resolucion) =>
        EnviarAsync(HttpMethod.Post, $"api/monitor/conflictos/{conflictoId}/resolver", new SolicitudResolverConflicto(resolucion));

    // ---------- Notas de crédito ----------

    public async Task<PaginaNotasCreditoCentral?> BuscarNotasCreditoAsync(string? buscar, CgPos.Dominio.Devoluciones.EstadoNotaCreditoCentral? estado,
        bool soloSobregiradas, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        var ruta = $"api/manager/notas-credito?pagina={pagina}&tamano={tamano}&soloSobregiradas={(soloSobregiradas ? "true" : "false")}"
                   + (estado is { } filtro ? $"&estado={filtro}" : string.Empty)
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<PaginaNotasCreditoCentral>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<IReadOnlyList<DatosMovimientoNotaCredito>?> ListarMovimientosNotaCreditoAsync(Guid notaCreditoId) =>
        ListarAsync<DatosMovimientoNotaCredito>($"api/manager/notas-credito/{notaCreditoId}/movimientos");

    public Task<RespuestaAdministracion> ProrrogarNotaCreditoAsync(Guid notaCreditoId, DateOnly venceEn, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/notas-credito/{notaCreditoId}/prorrogar", new SolicitudProrrogaNotaCredito(venceEn, motivo));

    // ---------- Programa de fidelidad ----------

    public async Task<PaginaMiembrosFidelidadCentral?> BuscarMiembrosFidelidadAsync(string? buscar, bool soloConPuntos, int pagina, int tamano,
        CancellationToken cancelacion = default)
    {
        var ruta = $"api/manager/fidelidad/miembros?pagina={pagina}&tamano={tamano}&soloConPuntos={(soloConPuntos ? "true" : "false")}"
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<PaginaMiembrosFidelidadCentral>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<IReadOnlyList<DatosMovimientoPuntosCentral>?> ListarMovimientosPuntosAsync(Guid miembroId) =>
        ListarAsync<DatosMovimientoPuntosCentral>($"api/manager/fidelidad/miembros/{miembroId}/movimientos");

    // ---------- Actualización de las cajas ----------

    public Task<IReadOnlyList<DatosVersionCaja>?> ListarVersionesCajasAsync() =>
        ListarAsync<DatosVersionCaja>("api/manager/actualizaciones/cajas");

    public async Task<DatosActualizacionCaja?> VersionPublicadaAsync()
    {
        try
        {
            using var respuesta = await Http.GetAsync("api/manager/actualizaciones/publicada");
            return respuesta.StatusCode == System.Net.HttpStatusCode.NoContent || !respuesta.IsSuccessStatusCode
                ? null
                : await respuesta.Content.ReadFromJsonAsync<DatosActualizacionCaja>(OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    // ---------- Reportes ----------

    public async Task<TablaReporte?> ReporteAsync(TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, Guid? sucursalId, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<TablaReporte>(RutaReporte(tipo, desde, hasta, sucursalId, null), OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <returns>El archivo con su nombre y tipo, o nulo si el Central no lo generó.</returns>
    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> DescargarReporteAsync(TipoReporteCentral tipo, string formato, DateOnly desde,
        DateOnly hasta, Guid? sucursalId)
    {
        var ruta = formato == "607" ? RutaReporte(TipoReporteCentral.Formato607, desde, hasta, sucursalId, "archivo") : RutaReporte(tipo, desde, hasta, sucursalId, formato);
        try
        {
            using var respuesta = await Http.GetAsync(ruta);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName ?? $"reporte.{formato}";
            return (nombre.Trim('"'), respuesta.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", await respuesta.Content.ReadAsByteArrayAsync());
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static string RutaReporte(TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, Guid? sucursalId, string? formato) =>
        $"api/manager/reportes/{tipo}{(formato is null ? string.Empty : "/" + formato)}?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}"
        + (sucursalId is { } sucursal ? $"&sucursalId={sucursal}" : string.Empty);

    // ---------- Despacho de pendientes y envíos ----------

    public async Task<PaginaPendientesCentral?> BuscarPendientesAsync(string? buscar, CgPos.Dominio.Entregas.EstadoPendiente? estado,
        CgPos.Dominio.Entregas.MetodoEntrega? metodo, Guid? sucursalId, bool soloAtrasados, bool soloAbiertos, int pagina, int tamano,
        CancellationToken cancelacion = default)
    {
        var ruta = $"api/manager/despacho/pendientes?pagina={pagina}&tamano={tamano}"
                   + $"&soloAtrasados={(soloAtrasados ? "true" : "false")}&soloAbiertos={(soloAbiertos ? "true" : "false")}"
                   + (estado is { } filtroEstado ? $"&estado={filtroEstado}" : string.Empty)
                   + (metodo is { } filtroMetodo ? $"&metodo={filtroMetodo}" : string.Empty)
                   + (sucursalId is { } sucursal ? $"&sucursalId={sucursal}" : string.Empty)
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<PaginaPendientesCentral>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<DetallePendienteCentral?> ObtenerPendienteAsync(Guid pendienteId)
    {
        try
        {
            return await Http.GetFromJsonAsync<DetallePendienteCentral>($"api/manager/despacho/pendientes/{pendienteId}", OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<ResumenDespachoCentral?> ResumenDespachoAsync()
    {
        try
        {
            return await Http.GetFromJsonAsync<ResumenDespachoCentral>("api/manager/despacho/resumen", OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<RespuestaAjustePuntos> AjustarPuntosAsync(Guid miembroId, int puntos, string motivo)
    {
        var (datos, error) = await PostearAsync<RespuestaAjustePuntos>($"api/manager/fidelidad/miembros/{miembroId}/ajustes",
            new SolicitudAjustePuntos(puntos, motivo));
        return datos ?? new RespuestaAjustePuntos(false, error ?? "No se pudo ajustar el saldo de puntos.");
    }

    // ---------- Comunes ----------

    /// <returns>La respuesta tipada o el motivo por el que no se obtuvo.</returns>
    private async Task<(T? Datos, string? Error)> PostearAsync<T>(string ruta, object cuerpo)
    {
        try
        {
            using var respuesta = await Http.PostAsJsonAsync(ruta, cuerpo, OpcionesJson.Predeterminadas);
            if (respuesta.IsSuccessStatusCode)
                return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas), null);

            return (default, respuesta.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "La sesión venció. Ingrese nuevamente.",
                HttpStatusCode.Forbidden => "No tiene permiso para esta operación.",
                HttpStatusCode.NotFound => "No se encontró el registro.",
                _ => $"El Central respondió {(int)respuesta.StatusCode}.",
            });
        }
        catch (HttpRequestException)
        {
            return (default, ServicioSesionCentral.SinComunicacion);
        }
        catch (JsonException)
        {
            return (default, "La respuesta del Central no es válida.");
        }
    }

    /// <returns>Nulo si no se pudo consultar (sin comunicación, sesión vencida o sin permiso).</returns>
    private async Task<IReadOnlyList<T>?> ListarAsync<T>(string ruta)
    {
        try
        {
            return await Http.GetFromJsonAsync<List<T>>(ruta, OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private async Task<RespuestaAdministracion> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        try
        {
            using var solicitud = new HttpRequestMessage(metodo, ruta)
            {
                Content = cuerpo is null ? null : JsonContent.Create(cuerpo, cuerpo.GetType(), options: OpcionesJson.Predeterminadas),
            };
            using var respuesta = await Http.SendAsync(solicitud);

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                return new RespuestaAdministracion(false, "La sesión venció. Ingrese nuevamente.");
            if (respuesta.StatusCode == HttpStatusCode.Forbidden)
                return new RespuestaAdministracion(false, "No tiene permiso para esta operación.");

            if (respuesta.Content.Headers.ContentType?.MediaType == "application/json"
                && await respuesta.Content.ReadFromJsonAsync<RespuestaAdministracion>(OpcionesJson.Predeterminadas) is { } datos)
                return datos;

            // Sin cuerpo (204) es un éxito; reglas sin configurar (422) y otros errores llegan como texto.
            var texto = await respuesta.Content.ReadAsStringAsync();
            if (respuesta.IsSuccessStatusCode)
                return new RespuestaAdministracion(true, string.IsNullOrWhiteSpace(texto) ? null : texto);

            return new RespuestaAdministracion(false, string.IsNullOrWhiteSpace(texto) ? $"Respuesta inesperada del Central ({(int)respuesta.StatusCode})." : texto);
        }
        catch (HttpRequestException)
        {
            return new RespuestaAdministracion(false, ServicioSesionCentral.SinComunicacion);
        }
        catch (JsonException)
        {
            return new RespuestaAdministracion(false, "La respuesta del Central no es válida.");
        }
    }
}
