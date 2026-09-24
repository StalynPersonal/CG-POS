using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;
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

    public Task<RespuestaAdministracion> ActualizarRolAsync(int rolId, SolicitudRolCentral solicitud) => EnviarAsync(HttpMethod.Put, $"api/seguridad/roles/{rolId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoRolAsync(int rolId, bool activo) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/roles/{rolId}/{(activo ? "activar" : "desactivar")}");

    public Task<IReadOnlyList<DatosUsuarioCentral>?> ListarUsuariosAsync() => ListarAsync<DatosUsuarioCentral>("api/seguridad/usuarios");

    public Task<RespuestaAdministracion> CrearUsuarioAsync(SolicitudUsuarioCentral solicitud) => EnviarAsync(HttpMethod.Post, "api/seguridad/usuarios", solicitud);

    public Task<RespuestaAdministracion> ActualizarUsuarioAsync(int usuarioId, SolicitudActualizarUsuarioCentral solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/seguridad/usuarios/{usuarioId}", solicitud);

    public Task<RespuestaAdministracion> RestablecerContrasenaAsync(int usuarioId, string contrasenaTemporal) =>
        EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/contrasena", new SolicitudContrasenaTemporal(contrasenaTemporal));

    public Task<RespuestaAdministracion> DesbloquearUsuarioAsync(int usuarioId) => EnviarAsync(HttpMethod.Post, $"api/seguridad/usuarios/{usuarioId}/desbloquear");

    public Task<RespuestaAdministracion> CambiarEstadoUsuarioAsync(int usuarioId, bool activo) =>
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

    public Task<RespuestaAdministracion> ActualizarSucursalAsync(int sucursalId, SolicitudSucursal solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/sucursales/{sucursalId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoSucursalAsync(int sucursalId, bool activa) =>
        EnviarAsync(HttpMethod.Post, $"api/organizacion/sucursales/{sucursalId}/{(activa ? "activar" : "desactivar")}");

    public Task<IReadOnlyList<DatosCaja>?> ListarCajasAsync() => ListarAsync<DatosCaja>("api/organizacion/cajas");

    public Task<RespuestaAdministracion> CrearCajaAsync(SolicitudCaja solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/cajas", solicitud);

    public Task<RespuestaAdministracion> ActualizarCajaAsync(int cajaId, SolicitudActualizarCaja solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/cajas/{cajaId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoCajaAsync(int cajaId, bool habilitada) =>
        EnviarAsync(HttpMethod.Post, $"api/organizacion/cajas/{cajaId}/{(habilitada ? "habilitar" : "deshabilitar")}");

    public Task<IReadOnlyList<DefinicionParametro>?> ListarCatalogoParametrosAsync() => ListarAsync<DefinicionParametro>("api/organizacion/parametros/catalogo");

    public Task<IReadOnlyList<DatosParametro>?> ListarParametrosAsync() => ListarAsync<DatosParametro>("api/organizacion/parametros");

    public Task<RespuestaAdministracion> CrearParametroAsync(SolicitudParametro solicitud) => EnviarAsync(HttpMethod.Post, "api/organizacion/parametros", solicitud);

    public Task<RespuestaAdministracion> CambiarValorParametroAsync(int parametroId, string valor) =>
        EnviarAsync(HttpMethod.Put, $"api/organizacion/parametros/{parametroId}", new SolicitudValorParametro(valor));

    // ---------- Credenciales de las cajas ----------

    /// <returns>La credencial recién emitida (su secreto solo se ve aquí) o el motivo por el que no se emitió.</returns>
    public async Task<(DatosCredencialDispositivo? Credencial, string? Error)> EmitirCredencialAsync(int cajaId)
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
                // El Central explica por qué no se emitió (por ejemplo, que ya tiene una vigente).
                HttpStatusCode.Conflict => await respuesta.Content.ReadAsStringAsync(),
                _ => $"El Central respondió {(int)respuesta.StatusCode}.",
            });
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return (null, ServicioSesionCentral.SinComunicacion);
        }
    }

    public Task<RespuestaAdministracion> RevocarCredencialAsync(int cajaId, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"api/cajas/{cajaId}/credencial/revocar", new SolicitudRevocacionCredencial(motivo));

    // ---------- Rangos de e-CF ----------

    public Task<IReadOnlyList<DatosSecuenciaEcfCentral>?> ListarSecuenciasAsync() => ListarAsync<DatosSecuenciaEcfCentral>("api/fiscal/secuencias");

    public Task<RespuestaAdministracion> AsignarSecuenciaAsync(SolicitudSecuenciaEcf solicitud) => EnviarAsync(HttpMethod.Post, "api/fiscal/secuencias", solicitud);

    public Task<RespuestaAdministracion> ActualizarSecuenciaAsync(int secuenciaId, SolicitudActualizarSecuenciaEcf solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/fiscal/secuencias/{secuenciaId}", solicitud);

    public Task<IReadOnlyList<DatosAnulacionEcf>?> ListarAnulacionesEcfAsync() => ListarAsync<DatosAnulacionEcf>("api/fiscal/anulaciones");

    public Task<RespuestaAdministracion> AnularSecuenciasAsync(int secuenciaId, SolicitudAnulacionEcf solicitud) =>
        EnviarAsync(HttpMethod.Post, $"api/fiscal/secuencias/{secuenciaId}/anulaciones", solicitud);

    // ---------- Usuarios y roles de caja ----------

    public Task<IReadOnlyList<CgPos.Dominio.Seguridad.DefinicionPermiso>?> ListarPermisosCajaAsync() =>
        ListarAsync<CgPos.Dominio.Seguridad.DefinicionPermiso>("api/usuarios-caja/permisos");

    public Task<IReadOnlyList<DatosRolCaja>?> ListarRolesCajaAsync() => ListarAsync<DatosRolCaja>("api/usuarios-caja/roles");

    public Task<RespuestaAdministracion> GuardarRolCajaAsync(int? rolId, SolicitudRolCaja solicitud) =>
        rolId is { } id ? EnviarAsync(HttpMethod.Put, $"api/usuarios-caja/roles/{id}", solicitud) : EnviarAsync(HttpMethod.Post, "api/usuarios-caja/roles", solicitud);

    public Task<IReadOnlyList<DatosUsuarioCaja>?> ListarUsuariosCajaAsync() => ListarAsync<DatosUsuarioCaja>("api/usuarios-caja/usuarios");

    public Task<RespuestaAdministracion> GuardarUsuarioCajaAsync(int? usuarioId, SolicitudUsuarioCaja solicitud) =>
        usuarioId is { } id
            ? EnviarAsync(HttpMethod.Put, $"api/usuarios-caja/usuarios/{id}", solicitud)
            : EnviarAsync(HttpMethod.Post, "api/usuarios-caja/usuarios", solicitud);

    // ---------- Catálogos de maestros ----------

    /// <param name="ruta">Ruta del catálogo (ej. "departamentos"); los registros llegan en su formato de carga.</param>
    public Task<IReadOnlyList<DatosMaestroCentral<System.Text.Json.Nodes.JsonObject>>?> ListarCatalogoAsync(string ruta) =>
        ListarAsync<DatosMaestroCentral<System.Text.Json.Nodes.JsonObject>>($"api/maestros/{ruta}");

    /// <param name="nuevo">Crea el registro (su código no puede existir); falso cambia el que tiene ese código.</param>
    public Task<RespuestaAdministracion> GuardarCatalogoAsync(string ruta, bool nuevo, System.Text.Json.Nodes.JsonObject dato) =>
        EnviarAsync(nuevo ? HttpMethod.Post : HttpMethod.Put, $"api/maestros/{ruta}", dato);

    /// <summary>Código sugerido para un registro nuevo de un catálogo con código numérico.</summary>
    public async Task<int?> SiguienteCodigoAsync(string modulo, string ruta)
    {
        try
        {
            return await Http.GetFromJsonAsync<int>($"api/{modulo}/{ruta}/siguiente-codigo", OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesMaestrosAsync() => ListarAsync<DatosSucursal>("api/maestros/sucursales");

    /// <summary>Catálogo tipado (departamentos, unidades…) para referencias.</summary>
    public Task<IReadOnlyList<DatosMaestroCentral<T>>?> ListarMaestroAsync<T>(string ruta) => ListarAsync<DatosMaestroCentral<T>>($"api/maestros/{ruta}");

    // ---------- Clientes, artículos y precios ----------

    public Task<PaginaMaestros<CgPos.Contratos.Catalogo.ClienteCarga>?> BuscarClientesAsync(string? texto, int pagina, int tamano, CancellationToken cancelacion = default) =>
        BuscarAsync<CgPos.Contratos.Catalogo.ClienteCarga>("api/maestros/clientes", texto, pagina, tamano, cancelacion);

    public Task<RespuestaAdministracion> GuardarClienteAsync(CgPos.Contratos.Catalogo.ClienteCarga cliente, bool nuevo) =>
        EnviarAsync(nuevo ? HttpMethod.Post : HttpMethod.Put, "api/maestros/clientes", cliente);

    public Task<RespuestaAdministracion> CorregirDocumentoClienteAsync(string codigoCliente, SolicitudCorreccionDocumentoCliente solicitud) =>
        EnviarAsync(HttpMethod.Post, $"api/maestros/clientes/{Uri.EscapeDataString(codigoCliente)}/documento", solicitud);

    /// <param name="modulo">"maestros" o "precios", según el permiso con el que se consulta.</param>
    /// <param name="campo">Dónde buscar (ver <see cref="CamposBusquedaArticulo"/>); nulo para buscar en todos.</param>
    public Task<PaginaMaestros<CgPos.Contratos.Catalogo.ArticuloCarga>?> BuscarArticulosAsync(string modulo, string? texto, int pagina, int tamano,
        CancellationToken cancelacion = default, string? campo = null, string? tipo = null) =>
        BuscarAsync<CgPos.Contratos.Catalogo.ArticuloCarga>($"api/{modulo}/articulos", texto, pagina, tamano, cancelacion,
            (campo is null ? null : $"&campo={campo}") + (tipo is null ? null : $"&tipo={tipo}"));

    /// <returns>Nulo si no se pudo consultar.</returns>
    private async Task<PaginaMaestros<T>?> BuscarAsync<T>(string ruta, string? texto, int pagina, int tamano, CancellationToken cancelacion,
        string? extra = null)
    {
        try
        {
            return await Http.GetFromJsonAsync<PaginaMaestros<T>>(
                $"{ruta}?buscar={Uri.EscapeDataString(texto ?? string.Empty)}&pagina={pagina}&tamano={tamano}{extra}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> GuardarArticuloAsync(CgPos.Contratos.Catalogo.ArticuloCarga articulo, bool nuevo) =>
        EnviarAsync(nuevo ? HttpMethod.Post : HttpMethod.Put, "api/maestros/articulos", articulo);

    public Task<RespuestaAdministracion> CambiarPreciosAsync(string codigoArticulo, SolicitudPreciosArticulo solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/precios/articulos/{Uri.EscapeDataString(codigoArticulo)}", solicitud);

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.DepartamentoCarga>>?> ListarDepartamentosPreciosAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.DepartamentoCarga>>("api/precios/departamentos");

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.CategoriaCarga>>?> ListarCategoriasPreciosAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.CategoriaCarga>>("api/precios/categorias");

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.MarcaCarga>>?> ListarMarcasPreciosAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.MarcaCarga>>("api/precios/marcas");

    public Task<IReadOnlyList<DatosTopeDescuentoCentral>?> ListarTopesAsync() => ListarAsync<DatosTopeDescuentoCentral>("api/precios/topes");

    public Task<RespuestaAdministracion> GuardarTopeAsync(CgPos.Contratos.Catalogo.TopeDescuentoCarga tope, bool nuevo) =>
        EnviarAsync(nuevo ? HttpMethod.Post : HttpMethod.Put, "api/precios/topes", tope);

    // ---------- Promociones ----------

    public Task<IReadOnlyList<DatosPromocionCentral>?> ListarPromocionesAsync() => ListarAsync<DatosPromocionCentral>("api/promociones");

    public Task<RespuestaAdministracion> GuardarPromocionAsync(CgPos.Contratos.Catalogo.PromocionCarga promocion, bool nueva) =>
        EnviarAsync(nueva ? HttpMethod.Post : HttpMethod.Put, "api/promociones", promocion);

    public Task<(ResultadoImportacionPromociones? Datos, string? Error)> ImportarPromocionesAsync(SolicitudImportacionPromociones solicitud) =>
        PostearAsync<ResultadoImportacionPromociones>("api/promociones/importar", solicitud);

    public Task<(ResultadoSimulacionPromociones? Datos, string? Error)> SimularPromocionesAsync(SolicitudSimulacionPromociones solicitud) =>
        PostearAsync<ResultadoSimulacionPromociones>("api/promociones/simular", solicitud);

    public async Task<IReadOnlyList<CgPos.Contratos.Catalogo.ArticuloCarga>> ArticulosPromocionAsync(IReadOnlyList<string> codigos) =>
        codigos.Count == 0 ? [] : (await PostearAsync<List<CgPos.Contratos.Catalogo.ArticuloCarga>>("api/promociones/articulos/por-codigo", codigos)).Datos ?? [];

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.DepartamentoCarga>>?> ListarDepartamentosPromocionesAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.DepartamentoCarga>>("api/promociones/departamentos");

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.CategoriaCarga>>?> ListarCategoriasPromocionesAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.CategoriaCarga>>("api/promociones/categorias");

    public Task<IReadOnlyList<DatosMaestroCentral<CgPos.Contratos.Catalogo.MarcaCarga>>?> ListarMarcasPromocionesAsync() =>
        ListarAsync<DatosMaestroCentral<CgPos.Contratos.Catalogo.MarcaCarga>>("api/promociones/marcas");

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

    public async Task<PaginaComprobantesDgii?> BuscarComprobantesDgiiAsync(CgPos.Dominio.Sincronizacion.EstadoEnvioDgii? estado, int? sucursalId, int? cajaId, string? buscar,
        bool soloConFallo, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        var ruta = $"api/monitor/comprobantes?pagina={pagina}&tamano={tamano}&soloConFallo={(soloConFallo ? "true" : "false")}"
                   + (estado is { } e ? $"&estado={e}" : string.Empty)
                   + (sucursalId is { } s ? $"&sucursalId={s}" : string.Empty)
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

    public async Task<string?> ObtenerXmlComprobanteAsync(int comprobanteId)
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

    public Task<RespuestaAdministracion> ReenviarComprobanteAsync(int comprobanteId) => EnviarAsync(HttpMethod.Post, $"api/monitor/comprobantes/{comprobanteId}/reenviar");

    /// <summary>Deja pedido que esa caja vuelva a bajar todos los maestros desde cero.</summary>
    public Task<RespuestaAdministracion> ResincronizarCajaAsync(int cajaId) => EnviarAsync(HttpMethod.Post, $"api/monitor/cajas/{cajaId}/resincronizar");

    public Task<IReadOnlyList<DatosConflictoSincronizacion>?> ListarConflictosAsync(bool abiertos) =>
        ListarAsync<DatosConflictoSincronizacion>($"api/monitor/conflictos?abiertos={(abiertos ? "true" : "false")}");

    public Task<RespuestaAdministracion> ResolverConflictoAsync(int conflictoId, string resolucion) =>
        EnviarAsync(HttpMethod.Post, $"api/monitor/conflictos/{conflictoId}/resolver", new SolicitudResolverConflicto(resolucion));

    // ---------- Notas de crédito ----------

    public async Task<PaginaNotasCreditoCentral?> BuscarNotasCreditoAsync(string? buscar, CgPos.Dominio.Devoluciones.EstadoNotaCreditoCentral? estado,
        bool soloSobregiradas, int pagina, int tamano, DateOnly? desde = null, DateOnly? hasta = null, CancellationToken cancelacion = default)
    {
        var ruta = $"api/manager/notas-credito?pagina={pagina}&tamano={tamano}&soloSobregiradas={(soloSobregiradas ? "true" : "false")}"
                   + (desde is { } inicio ? $"&desde={inicio:yyyy-MM-dd}" : string.Empty)
                   + (hasta is { } fin ? $"&hasta={fin:yyyy-MM-dd}" : string.Empty)
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

    // ---------- Facturas recibidas de las cajas (M16) ----------

    public async Task<PaginaComprobantesRecibidos?> BuscarComprobantesRecibidosAsync(DateOnly desde, DateOnly hasta, int? sucursalId, int? cajaId,
        CgPos.Dominio.Reportes.TipoComprobanteVenta? tipo, string? buscar, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        var ruta = $"api/manager/facturas?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}&pagina={pagina}&tamano={tamano}"
                   + (sucursalId is { } sucursal ? $"&sucursalId={sucursal}" : string.Empty)
                   + (cajaId is { } caja ? $"&cajaId={caja}" : string.Empty)
                   + (tipo is { } filtro ? $"&tipo={filtro}" : string.Empty)
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<PaginaComprobantesRecibidos>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<DatosComprobanteRecibidoDetalle?> ObtenerComprobanteRecibidoAsync(int comprobanteId, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosComprobanteRecibidoDetalle>($"api/manager/facturas/{comprobanteId}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    // ---------- Auditoría (M02) ----------

    public async Task<PaginaAuditoria?> BuscarAuditoriaAsync(DateOnly? desde, DateOnly? hasta, string? accion, string? tipoEntidad,
        string? usuario, string? buscar, bool soloConCambios, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        var ruta = $"api/manager/auditoria?pagina={pagina}&tamano={tamano}&soloConCambios={soloConCambios.ToString().ToLowerInvariant()}"
                   + (desde is { } inicio ? $"&desde={inicio:yyyy-MM-dd}" : string.Empty)
                   + (hasta is { } fin ? $"&hasta={fin:yyyy-MM-dd}" : string.Empty)
                   + (string.IsNullOrWhiteSpace(accion) ? string.Empty : $"&accion={Uri.EscapeDataString(accion)}")
                   + (string.IsNullOrWhiteSpace(tipoEntidad) ? string.Empty : $"&tipoEntidad={Uri.EscapeDataString(tipoEntidad)}")
                   + (string.IsNullOrWhiteSpace(usuario) ? string.Empty : $"&usuario={Uri.EscapeDataString(usuario)}")
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<PaginaAuditoria>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<DatosAuditoriaDetalle?> ObtenerAuditoriaAsync(int registroId, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosAuditoriaDetalle>($"api/manager/auditoria/{registroId}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<OpcionesAuditoria?> ObtenerOpcionesAuditoriaAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<OpcionesAuditoria>("api/manager/auditoria/opciones", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    // ---------- Cotizaciones ----------

    public async Task<IReadOnlyList<DatosCotizacion>?> ListarCotizacionesAsync(string? buscar, CgPos.Dominio.Cotizaciones.EstadoCotizacion? estado,
        CancellationToken cancelacion = default)
    {
        var ruta = "api/manager/cotizaciones?"
                   + (estado is { } filtro ? $"estado={filtro}&" : string.Empty)
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<IReadOnlyList<DatosCotizacion>>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Una sola cotización con sus líneas, para la pantalla que la edita.</summary>
    public async Task<DatosCotizacion?> ObtenerCotizacionAsync(int cotizacionId, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosCotizacion>($"api/manager/cotizaciones/{cotizacionId}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Hasta cuándo vale una cotización hecha hoy; nulo si no hay comunicación con el Central.</summary>
    public async Task<DateOnly?> VencimientoCotizacionAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DateOnly>("api/manager/cotizaciones/vencimiento", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Clientes del maestro que coinciden con el documento, buscados con el permiso de cotizaciones.</summary>
    public Task<PaginaMaestros<CgPos.Contratos.Catalogo.ClienteCarga>?> BuscarClientesCotizacionAsync(string documento, CancellationToken cancelacion = default) =>
        BuscarAsync<CgPos.Contratos.Catalogo.ClienteCarga>("api/manager/cotizaciones/clientes", documento, 0, 20, cancelacion);

    public Task<RespuestaAdministracion> CrearCotizacionAsync(SolicitudCotizacion solicitud) =>
        EnviarAsync(HttpMethod.Post, "api/manager/cotizaciones", solicitud);

    public Task<RespuestaAdministracion> ActualizarCotizacionAsync(int cotizacionId, SolicitudCotizacion solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/manager/cotizaciones/{cotizacionId}", solicitud);

    public Task<RespuestaAdministracion> AnularCotizacionAsync(int cotizacionId, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/cotizaciones/{cotizacionId}/anular", new SolicitudAnularCotizacion(motivo));

    /// <summary>El PDF en carta que se le entrega o se le envía al cliente.</summary>
    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> DescargarCotizacionAsync(int cotizacionId,
        CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.GetAsync($"api/manager/cotizaciones/{cotizacionId}/pdf", cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName ?? "cotizacion.pdf";
            return (nombre.Trim('"'), respuesta.Content.Headers.ContentType?.ToString() ?? "application/pdf",
                await respuesta.Content.ReadAsByteArrayAsync(cancelacion));
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    // ---------- Listas de boda (RF-73) ----------

    public async Task<IReadOnlyList<DatosListaBoda>?> ListarListasBodaAsync(string? buscar, CgPos.Dominio.ListasBoda.EstadoListaBoda? estado,
        CancellationToken cancelacion = default)
    {
        var ruta = "api/manager/listas-boda?"
                   + (estado is { } filtro ? $"estado={filtro}&" : string.Empty)
                   + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"buscar={Uri.EscapeDataString(buscar.Trim())}");
        try
        {
            return await Http.GetFromJsonAsync<IReadOnlyList<DatosListaBoda>>(ruta, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public async Task<DatosConfiguracionListasBoda?> ObtenerConfiguracionListasBodaAsync(CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosConfiguracionListasBoda>("api/manager/listas-boda/configuracion", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Una sola lista con sus artículos y sus compras, para la pantalla que la edita.</summary>
    public async Task<DatosListaBoda?> ObtenerListaBodaAsync(int listaBodaId, CancellationToken cancelacion = default)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosListaBoda>($"api/manager/listas-boda/{listaBodaId}", OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> CrearListaBodaAsync(SolicitudListaBoda solicitud) =>
        EnviarAsync(HttpMethod.Post, "api/manager/listas-boda", solicitud);

    public Task<RespuestaAdministracion> ActualizarListaBodaAsync(int listaBodaId, SolicitudListaBoda solicitud) =>
        EnviarAsync(HttpMethod.Put, $"api/manager/listas-boda/{listaBodaId}", solicitud);

    public Task<RespuestaAdministracion> CambiarEstadoListaBodaAsync(int listaBodaId, bool cerrar) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/listas-boda/{listaBodaId}/estado?cerrar={(cerrar ? "true" : "false")}");

    public Task<IReadOnlyList<DatosMovimientoNotaCredito>?> ListarMovimientosNotaCreditoAsync(int notaCreditoId) =>
        ListarAsync<DatosMovimientoNotaCredito>($"api/manager/notas-credito/{notaCreditoId}/movimientos");



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

    public Task<IReadOnlyList<DatosMovimientoPuntosCentral>?> ListarMovimientosPuntosAsync(int miembroId) =>
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

    // ---------- Cierre de sucursal ----------

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesCierreAsync() => ListarAsync<DatosSucursal>("api/manager/cierres-sucursal/sucursales");

    public Task<IReadOnlyList<CgPos.Contratos.Catalogo.BancoCarga>?> ListarBancosCierreAsync() =>
        ListarAsync<CgPos.Contratos.Catalogo.BancoCarga>("api/manager/cierres-sucursal/bancos");

    public Task<IReadOnlyList<DatosCierreSucursal>?> ListarCierresSucursalAsync(int? sucursalId, DateOnly desde, DateOnly hasta) =>
        ListarAsync<DatosCierreSucursal>(
            $"api/manager/cierres-sucursal?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}{(sucursalId is { } sucursal ? $"&sucursalId={sucursal}" : string.Empty)}");

    public async Task<DatosPreparacionCierreSucursal?> PrepararCierreSucursalAsync(int sucursalId, DateOnly fecha)
    {
        try
        {
            return await Http.GetFromJsonAsync<DatosPreparacionCierreSucursal>($"api/manager/cierres-sucursal/preparar?sucursalId={sucursalId}&fecha={fecha:yyyy-MM-dd}",
                OpcionesJson.Predeterminadas);
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    public Task<RespuestaAdministracion> CerrarSucursalAsync(SolicitudCierreSucursal solicitud) =>
        EnviarAsync(HttpMethod.Post, "api/manager/cierres-sucursal", solicitud);

    // ---------- Cierres de caja ----------

    public Task<IReadOnlyList<DatosSucursal>?> ListarSucursalesCierreCajaAsync() => ListarAsync<DatosSucursal>("api/manager/cierres-caja/sucursales");

    public Task<IReadOnlyList<DatosCaja>?> ListarCajasCierreCajaAsync() => ListarAsync<DatosCaja>("api/manager/cierres-caja/cajas");

    public Task<IReadOnlyList<DatosCierreCaja>?> ListarCierresCajaAsync(int? sucursalId, int? cajaId, DateOnly desde, DateOnly hasta) =>
        ListarAsync<DatosCierreCaja>(
            $"api/manager/cierres-caja?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}"
            + $"{(sucursalId is { } sucursal ? $"&sucursalId={sucursal}" : string.Empty)}{(cajaId is { } caja ? $"&cajaId={caja}" : string.Empty)}");

    public Task<RespuestaAdministracion> AjustarCierreCajaAsync(int cierreId, SolicitudAjusteCierre solicitud) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/cierres-caja/{cierreId}/ajustes", solicitud);

    // ---------- Reportes ----------

    public async Task<TablaReporte?> ReporteAsync(TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, int? sucursalId, CancellationToken cancelacion = default)
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
        DateOnly hasta, int? sucursalId)
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

    private static string RutaReporte(TipoReporteCentral tipo, DateOnly desde, DateOnly hasta, int? sucursalId, string? formato) =>
        $"api/manager/reportes/{tipo}{(formato is null ? string.Empty : "/" + formato)}?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}"
        + (sucursalId is { } sucursal ? $"&sucursalId={sucursal}" : string.Empty);

    // ---------- Despacho de pendientes y envíos ----------

    public async Task<PaginaPendientesCentral?> BuscarPendientesAsync(string? buscar, CgPos.Dominio.Entregas.EstadoPendiente? estado,
        CgPos.Dominio.Entregas.MetodoEntrega? metodo, int? sucursalId, bool soloAtrasados, bool soloAbiertos, int pagina, int tamano,
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

    /// <summary>Avanza la preparación del pendiente (RF-252).</summary>
    public Task<RespuestaAdministracion> CambiarEstadoPendienteAsync(int pendienteId, EstadoPendiente estado) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/despacho/pendientes/{pendienteId}/estado", new SolicitudEstadoPendiente(estado));

    /// <summary>Registra la entrega total o parcial con quien recibe (RF-253, RF-254).</summary>
    public Task<RespuestaAdministracion> EntregarPendienteAsync(int pendienteId, SolicitudEntregaPendiente solicitud) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/despacho/pendientes/{pendienteId}/entregas", solicitud);

    /// <summary>Anula el pendiente con motivo, liberando la mercancía (RF-255).</summary>
    public Task<RespuestaAdministracion> AnularPendienteAsync(int pendienteId, string motivo) =>
        EnviarAsync(HttpMethod.Post, $"api/manager/despacho/pendientes/{pendienteId}/anular", new SolicitudAnularPendiente(motivo));

    /// <summary>La constancia en carta que firma quien recibe la mercancía (RF-254).</summary>
    public async Task<(string Nombre, string TipoContenido, byte[] Contenido)?> DescargarConstanciaAsync(int pendienteId, int numeroEntrega,
        CancellationToken cancelacion = default)
    {
        try
        {
            using var respuesta = await Http.GetAsync($"api/manager/despacho/pendientes/{pendienteId}/entregas/{numeroEntrega}/pdf", cancelacion);
            if (!respuesta.IsSuccessStatusCode)
                return null;

            var nombre = respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName
                ?? $"constancia-{pendienteId}-{numeroEntrega}.pdf";
            return (nombre.Trim('"'), respuesta.Content.Headers.ContentType?.ToString() ?? "application/pdf",
                await respuesta.Content.ReadAsByteArrayAsync(cancelacion));
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Numeración de los documentos que emite el Central; la de los e-NCF es otra cosa y va aparte.</summary>
    public Task<IReadOnlyList<DatosSecuenciaCentral>?> ListarSecuenciasDocumentoAsync() =>
        ListarAsync<DatosSecuenciaCentral>("api/organizacion/secuencias");

    public Task<RespuestaAdministracion> GuardarSecuenciaDocumentoAsync(SolicitudSecuenciaCentral solicitud) =>
        EnviarAsync(HttpMethod.Post, "api/organizacion/secuencias", solicitud);

    public async Task<DetallePendienteCentral?> ObtenerPendienteAsync(int pendienteId)
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

    public async Task<RespuestaAjustePuntos> AjustarPuntosAsync(int miembroId, int puntos, string motivo)
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
