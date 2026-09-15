using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Web.Componentes;
using MudBlazor;

namespace CgPos.Pos.Web.Seguridad;

/// <summary>Pide autorización de supervisor solo cuando el usuario no tiene el permiso de la operación.</summary>
public sealed class ServicioAutorizacionPantalla(AlmacenSesion almacen, IDialogService dialogos)
{
    /// <returns>La autorización concedida (con o sin supervisor), o nulo si el usuario canceló.</returns>
    public async Task<RespuestaAutorizacion?> SolicitarAsync(string permiso, string descripcionOperacion, string? tipoEntidad = null, string? entidadId = null)
    {
        if (almacen.TienePermiso(permiso))
            return new RespuestaAutorizacion(true, RequirioSupervisor: false);

        var parametros = new DialogParameters<DialogoAutorizacionSupervisor>
        {
            { dialogo => dialogo.Permiso, permiso },
            { dialogo => dialogo.DescripcionOperacion, descripcionOperacion },
            { dialogo => dialogo.TipoEntidad, tipoEntidad },
            { dialogo => dialogo.EntidadId, entidadId },
        };
        var opciones = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false };

        var referencia = await dialogos.ShowAsync<DialogoAutorizacionSupervisor>("Autorización de supervisor", parametros, opciones);
        var resultado = await referencia.Result;

        return resultado is { Canceled: false, Data: RespuestaAutorizacion respuesta } ? respuesta : null;
    }

    /// <summary>
    /// Ejecuta una operación de venta; si el Agente responde que requiere autorización, la pide al supervisor y reintenta con ella.
    /// </summary>
    /// <returns>La respuesta final, o nulo si el usuario canceló la autorización.</returns>
    public async Task<RespuestaVenta?> EjecutarVentaAsync(Func<Guid?, Task<RespuestaVenta>> operacion, string descripcion, string? numeroTransaccion)
    {
        var respuesta = await operacion(null);
        if (respuesta.Resultado is not (CodigoResultadoVenta.RequiereAutorizacion or CodigoResultadoVenta.AutorizacionInvalida)
            || respuesta.PermisoRequerido is not { } permiso)
            return respuesta;

        var autorizacion = await SolicitarAsync(permiso, descripcion, "Venta", numeroTransaccion);
        if (autorizacion is null)
            return null;

        return autorizacion.AutorizacionId is { } autorizacionId ? await operacion(autorizacionId) : respuesta;
    }
}
