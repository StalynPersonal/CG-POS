using System.Security.Claims;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>Maestros y precios: cada catálogo se lista y se guarda (crear o cambiar por Id) con su registro de carga de la caja.</summary>
public static class RutasApiMaestros
{
    private const int TamanoPaginaPredeterminado = 25;

    public static IEndpointRouteBuilder MapearApiMaestros(this IEndpointRouteBuilder aplicacion)
    {
        var maestros = aplicacion.MapGroup("/api/maestros").RequireAuthorization(CatalogoPermisosCentral.AdministrarMaestros);

        // Referencia para los almacenes, sin exigir el permiso de organización.
        maestros.MapGet("/sucursales", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSucursalesAsync(cancelacion)));

        Catalogo<MonedaCarga>(maestros, "monedas", TipoMaestro.Moneda, d => d.Id, d => new PaqueteMaestros(Monedas: [d]));
        Catalogo<DepartamentoCarga>(maestros, "departamentos", TipoMaestro.Departamento, d => d.Id, d => new PaqueteMaestros(Departamentos: [d]));
        Catalogo<CategoriaCarga>(maestros, "categorias", TipoMaestro.Categoria, d => d.Id, d => new PaqueteMaestros(Categorias: [d]));
        Catalogo<MarcaCarga>(maestros, "marcas", TipoMaestro.Marca, d => d.Id, d => new PaqueteMaestros(Marcas: [d]));
        Catalogo<UnidadMedidaCarga>(maestros, "unidades-medida", TipoMaestro.UnidadMedida, d => d.Id, d => new PaqueteMaestros(UnidadesMedida: [d]));
        Catalogo<ImpuestoCarga>(maestros, "impuestos", TipoMaestro.Impuesto, d => d.Id, d => new PaqueteMaestros(Impuestos: [d]));
        Catalogo<FormaPagoCarga>(maestros, "formas-pago", TipoMaestro.FormaPago, d => d.Id, d => new PaqueteMaestros(FormasPago: [d]));
        Catalogo<BancoCarga>(maestros, "bancos", TipoMaestro.Banco, d => d.Id, d => new PaqueteMaestros(Bancos: [d]));
        Catalogo<TipoTarjetaCarga>(maestros, "tipos-tarjeta", TipoMaestro.TipoTarjeta, d => d.Id, d => new PaqueteMaestros(TiposTarjeta: [d]));
        Catalogo<DenominacionCarga>(maestros, "denominaciones", TipoMaestro.Denominacion, d => d.Id, d => new PaqueteMaestros(Denominaciones: [d]));
        Catalogo<TasaCambioCarga>(maestros, "tasas-cambio", TipoMaestro.TasaCambio, d => d.Id, d => new PaqueteMaestros(TasasCambio: [d]));
        Catalogo<MotivoDescuentoCarga>(maestros, "motivos-descuento", TipoMaestro.MotivoDescuento, d => d.Id, d => new PaqueteMaestros(MotivosDescuento: [d]));
        Catalogo<MotivoDevolucionCarga>(maestros, "motivos-devolucion", TipoMaestro.MotivoDevolucion, d => d.Id, d => new PaqueteMaestros(MotivosDevolucion: [d]));
        Catalogo<AlmacenCarga>(maestros, "almacenes", TipoMaestro.Almacen, d => d.Id, d => new PaqueteMaestros(Almacenes: [d]));
        Catalogo<NivelFidelidadCarga>(maestros, "niveles-fidelidad", TipoMaestro.NivelFidelidad, d => d.Id, d => new PaqueteMaestros(NivelesFidelidad: [d]));
        Catalogo<ReglaAcumulacionCarga>(maestros, "reglas-acumulacion", TipoMaestro.ReglaAcumulacion, d => d.Id, d => new PaqueteMaestros(ReglasAcumulacion: [d]));
        Catalogo<DescuentoTarjetaCarga>(maestros, "descuentos-tarjeta", TipoMaestro.DescuentoTarjeta, d => d.Id, d => new PaqueteMaestros(DescuentosTarjeta: [d]));

        maestros.MapGet("/clientes", async (string? buscar, int? pagina, int? tamano, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.BuscarAsync<ClienteCarga>(TipoMaestro.Cliente, buscar, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion)));
        Catalogo<ClienteCarga>(maestros, "clientes", TipoMaestro.Cliente, d => d.Id, d => new PaqueteMaestros(Clientes: [d]), listar: false);

        maestros.MapGet("/articulos", BuscarArticulosAsync);
        maestros.MapPut("/articulos/{articuloId:guid}", async (Guid articuloId, ArticuloCarga articulo, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio,
                CancellationToken cancelacion) =>
            Responder(await servicio.GuardarArticuloAsync(articuloId, articulo, Actor(usuario), cancelacion)));

        var precios = aplicacion.MapGroup("/api/precios").RequireAuthorization(CatalogoPermisosCentral.AdministrarPrecios);

        precios.MapGet("/articulos", BuscarArticulosAsync);
        precios.MapPut("/articulos/{articuloId:guid}", async (Guid articuloId, SolicitudPreciosArticulo solicitud, ClaimsPrincipal usuario,
                IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Responder(await servicio.CambiarPreciosAsync(articuloId, solicitud, Actor(usuario), cancelacion)));

        // Referencias para los topes por departamento, categoría o marca.
        precios.MapGet("/departamentos", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<DepartamentoCarga>(TipoMaestro.Departamento, cancelacion)));
        precios.MapGet("/categorias", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<CategoriaCarga>(TipoMaestro.Categoria, cancelacion)));
        precios.MapGet("/marcas", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<MarcaCarga>(TipoMaestro.Marca, cancelacion)));

        precios.MapGet("/topes", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarTopesAsync(cancelacion)));
        Catalogo<TopeDescuentoCarga>(precios, "topes", TipoMaestro.TopeDescuento, d => d.Id, d => new PaqueteMaestros(TopesDescuento: [d]), listar: false);

        return aplicacion;
    }

    private static async Task<IResult> BuscarArticulosAsync(string? buscar, int? pagina, int? tamano, IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
        Results.Ok(await servicio.BuscarAsync<ArticuloCarga>(TipoMaestro.Articulo, buscar, pagina ?? 0, tamano ?? TamanoPaginaPredeterminado, cancelacion));

    private static void Catalogo<T>(RouteGroupBuilder grupo, string ruta, TipoMaestro tipo, Func<T, Guid> id, Func<T, PaqueteMaestros> paquete, bool listar = true)
        where T : class
    {
        if (listar)
            grupo.MapGet($"/{ruta}", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
                Results.Ok(await servicio.ListarAsync<T>(tipo, cancelacion)));

        grupo.MapPut($"/{ruta}/{{maestroId:guid}}", async (Guid maestroId, T dato, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio,
                CancellationToken cancelacion) =>
            id(dato) != maestroId
                ? Responder(ResultadoAdministracion.Error("El Id del registro no coincide con el de la ruta."))
                : Responder(await servicio.PublicarAsync(paquete(dato), maestroId, Actor(usuario), cancelacion)));
    }
}
