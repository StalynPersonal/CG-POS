using System.Security.Claims;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using static CgPos.Central.Api.Api.RespuestasAdministracion;

namespace CgPos.Central.Api.Api;

/// <summary>Catálogos de maestros: cada uno se lista y se guarda (crear o cambiar por Id) con su registro de carga de la caja.</summary>
public static class RutasApiMaestros
{
    public static IEndpointRouteBuilder MapearApiMaestros(this IEndpointRouteBuilder aplicacion)
    {
        var grupo = aplicacion.MapGroup("/api/maestros").RequireAuthorization(CatalogoPermisosCentral.AdministrarMaestros);

        // Referencia para los almacenes, sin exigir el permiso de organización.
        grupo.MapGet("/sucursales", async (IServicioOrganizacion servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarSucursalesAsync(cancelacion)));

        Catalogo<MonedaCarga>(grupo, "monedas", TipoMaestro.Moneda, d => d.Id, d => new PaqueteMaestros(Monedas: [d]));
        Catalogo<FamiliaCarga>(grupo, "familias", TipoMaestro.Familia, d => d.Id, d => new PaqueteMaestros(Familias: [d]));
        Catalogo<UnidadMedidaCarga>(grupo, "unidades-medida", TipoMaestro.UnidadMedida, d => d.Id, d => new PaqueteMaestros(UnidadesMedida: [d]));
        Catalogo<ImpuestoCarga>(grupo, "impuestos", TipoMaestro.Impuesto, d => d.Id, d => new PaqueteMaestros(Impuestos: [d]));
        Catalogo<FormaPagoCarga>(grupo, "formas-pago", TipoMaestro.FormaPago, d => d.Id, d => new PaqueteMaestros(FormasPago: [d]));
        Catalogo<BancoCarga>(grupo, "bancos", TipoMaestro.Banco, d => d.Id, d => new PaqueteMaestros(Bancos: [d]));
        Catalogo<TipoTarjetaCarga>(grupo, "tipos-tarjeta", TipoMaestro.TipoTarjeta, d => d.Id, d => new PaqueteMaestros(TiposTarjeta: [d]));
        Catalogo<DenominacionCarga>(grupo, "denominaciones", TipoMaestro.Denominacion, d => d.Id, d => new PaqueteMaestros(Denominaciones: [d]));
        Catalogo<TasaCambioCarga>(grupo, "tasas-cambio", TipoMaestro.TasaCambio, d => d.Id, d => new PaqueteMaestros(TasasCambio: [d]));
        Catalogo<MotivoDescuentoCarga>(grupo, "motivos-descuento", TipoMaestro.MotivoDescuento, d => d.Id, d => new PaqueteMaestros(MotivosDescuento: [d]));
        Catalogo<MotivoDevolucionCarga>(grupo, "motivos-devolucion", TipoMaestro.MotivoDevolucion, d => d.Id, d => new PaqueteMaestros(MotivosDevolucion: [d]));
        Catalogo<AlmacenCarga>(grupo, "almacenes", TipoMaestro.Almacen, d => d.Id, d => new PaqueteMaestros(Almacenes: [d]));

        return aplicacion;
    }

    private static void Catalogo<T>(RouteGroupBuilder grupo, string ruta, TipoMaestro tipo, Func<T, Guid> id, Func<T, PaqueteMaestros> paquete)
        where T : class
    {
        grupo.MapGet($"/{ruta}", async (IServicioMaestrosCentral servicio, CancellationToken cancelacion) =>
            Results.Ok(await servicio.ListarAsync<T>(tipo, cancelacion)));

        grupo.MapPut($"/{ruta}/{{maestroId:guid}}", async (Guid maestroId, T dato, ClaimsPrincipal usuario, IServicioMaestrosCentral servicio,
                CancellationToken cancelacion) =>
            id(dato) != maestroId
                ? Responder(ResultadoAdministracion.Error("El Id del registro no coincide con el de la ruta."))
                : Responder(await servicio.PublicarAsync(paquete(dato), maestroId, Actor(usuario), cancelacion)));
    }
}
