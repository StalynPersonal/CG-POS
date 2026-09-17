using System.Security.Claims;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>
/// Maestros y precios: cada catálogo se lista, se crea (POST) y se cambia (PUT) con su registro de carga, identificado por su código.
/// Los catálogos con código numérico sugieren el siguiente.
/// </summary>
public static class RutasApiMaestros
{
    private const int TamanoPaginaPredeterminado = 25;

    public static IEndpointRouteBuilder MapearApiMaestros(this IEndpointRouteBuilder aplicacion)
    {
        var maestros = aplicacion.MapGroup("/api/maestros").RequireAuthorization(CatalogoPermisosCentral.AdministrarMaestros);

        // Referencia para los almacenes, sin exigir el permiso de organización.
        maestros.MapGet("/sucursales", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSucursalesAsync(cancelacion)));

        Catalogo<MonedaCarga>(maestros, "monedas");
        Catalogo<DepartamentoCarga>(maestros, "departamentos", codigoNumerico: true);
        Catalogo<CategoriaCarga>(maestros, "categorias", codigoNumerico: true);
        Catalogo<MarcaCarga>(maestros, "marcas", codigoNumerico: true);
        Catalogo<UnidadMedidaCarga>(maestros, "unidades-medida", codigoNumerico: true);
        Catalogo<ImpuestoCarga>(maestros, "impuestos");
        Catalogo<FormaPagoCarga>(maestros, "formas-pago");
        Catalogo<BancoCarga>(maestros, "bancos");
        Catalogo<TipoTarjetaCarga>(maestros, "tipos-tarjeta", codigoNumerico: true);
        Catalogo<DenominacionCarga>(maestros, "denominaciones");
        Catalogo<TasaCambioCarga>(maestros, "tasas-cambio");
        Catalogo<MotivoDescuentoCarga>(maestros, "motivos-descuento", codigoNumerico: true);
        Catalogo<MotivoDevolucionCarga>(maestros, "motivos-devolucion", codigoNumerico: true);
        Catalogo<AlmacenCarga>(maestros, "almacenes");
        Catalogo<NivelFidelidadCarga>(maestros, "niveles-fidelidad", codigoNumerico: true);
        Catalogo<ReglaAcumulacionCarga>(maestros, "reglas-acumulacion", codigoNumerico: true);
        Catalogo<DescuentoTarjetaCarga>(maestros, "descuentos-tarjeta");

        maestros.MapGet("/clientes", async (string? buscar, int? pagina, int? tamano, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.BuscarAsync<ClienteCarga>(buscar, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion)));
        Catalogo<ClienteCarga>(maestros, "clientes", listar: false);
        maestros.MapPost("/clientes/{codigo}/documento", async (string codigo, SolicitudCorreccionDocumentoCliente solicitud, ClaimsPrincipal usuario,
                IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CorregirDocumentoClienteAsync(codigo, solicitud, Actor(usuario), cancelacion)))
            .RequireAuthorization(CatalogoPermisosCentral.CorregirDocumentoCliente);

        maestros.MapGet("/articulos", BuscarArticulosAsync);
        maestros.MapPost("/articulos", async (ArticuloCarga articulo, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.GuardarArticuloAsync(articulo, nuevo: true, Actor(usuario), cancelacion)));
        maestros.MapPut("/articulos", async (ArticuloCarga articulo, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.GuardarArticuloAsync(articulo, nuevo: false, Actor(usuario), cancelacion)));

        var precios = aplicacion.MapGroup("/api/precios").RequireAuthorization(CatalogoPermisosCentral.AdministrarPrecios);

        precios.MapGet("/articulos", BuscarArticulosAsync);
        precios.MapPut("/articulos/{codigo}", async (string codigo, SolicitudPreciosArticulo solicitud, ClaimsPrincipal usuario,
                IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarPreciosAsync(codigo, solicitud, Actor(usuario), cancelacion)));

        // Referencias para los topes por departamento, categoría o marca.
        precios.MapGet("/departamentos", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<DepartamentoCarga>(cancelacion)));
        precios.MapGet("/categorias", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<CategoriaCarga>(cancelacion)));
        precios.MapGet("/marcas", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<MarcaCarga>(cancelacion)));

        precios.MapGet("/topes", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarTopesAsync(cancelacion)));
        Catalogo<TopeDescuentoCarga>(precios, "topes", listar: false, codigoNumerico: true);

        return aplicacion;
    }

    private static async Task<IResult> BuscarArticulosAsync(string? buscar, int? pagina, int? tamano, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
        Results.Ok(await servicio.BuscarAsync<ArticuloCarga>(buscar, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion));

    private static void Catalogo<T>(RouteGroupBuilder grupo, string ruta, bool listar = true, bool codigoNumerico = false) where T : class
    {
        if (listar)
            grupo.MapGet($"/{ruta}", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
                Results.Ok(await servicio.ListarAsync<T>(cancelacion)));

        if (codigoNumerico)
            grupo.MapGet($"/{ruta}/siguiente-codigo", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
                Results.Ok(await servicio.SiguienteCodigoAsync<T>(cancelacion)));

        grupo.MapPost($"/{ruta}", async (T dato, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.GuardarAsync(dato, nuevo: true, Actor(usuario), cancelacion)));
        grupo.MapPut($"/{ruta}", async (T dato, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.GuardarAsync(dato, nuevo: false, Actor(usuario), cancelacion)));
    }
}
