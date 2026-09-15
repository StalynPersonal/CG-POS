using CgPos.Contratos.Seguridad;
using CgPos.Contratos.Ventas;
using CgPos.Pos.Web.Componentes;
using MudBlazor;

namespace CgPos.Pos.Web.Seguridad;

/// <summary>Pide autorización de supervisor solo cuando el usuario no tiene el permiso de la operación.</summary>
public sealed class ServicioAutorizacionPantalla(AlmacenSesion almacen, IDialogService dialogos)
{
    private const int IntentosMaximos = 3;

    /// <param name="forzarSupervisor">Pide clave aunque el usuario tenga el permiso (lo que excede su tope necesita un nivel superior).</param>
    /// <returns>La autorización concedida (con o sin supervisor), o nulo si el usuario canceló.</returns>
    public async Task<RespuestaAutorizacion?> SolicitarAsync(string permiso, string descripcionOperacion, string? tipoEntidad = null, string? entidadId = null,
        bool forzarSupervisor = false)
    {
        if (!forzarSupervisor && almacen.TienePermiso(permiso))
            return new RespuestaAutorizacion(true, RequirioSupervisor: false);

        var parametros = new DialogParameters<DialogoAutorizacionSupervisor>
        {
            { dialogo => dialogo.Permiso, permiso },
            { dialogo => dialogo.DescripcionOperacion, descripcionOperacion },
            { dialogo => dialogo.TipoEntidad, tipoEntidad },
            { dialogo => dialogo.EntidadId, entidadId },
            { dialogo => dialogo.ForzarSupervisor, forzarSupervisor },
        };
        var opciones = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false };

        var referencia = await dialogos.ShowAsync<DialogoAutorizacionSupervisor>("Autorización de supervisor", parametros, opciones);
        var resultado = await referencia.Result;

        return resultado is { Canceled: false, Data: RespuestaAutorizacion respuesta } ? respuesta : null;
    }

    /// <summary>
    /// Ejecuta una operación de venta; si el Agente responde que requiere autorización, la pide al supervisor y reintenta con ella.
    /// Si el descuento excede el tope de quien autorizó, pide la clave de un nivel superior (RN-10).
    /// </summary>
    /// <returns>La respuesta final, o nulo si el usuario canceló la autorización.</returns>
    public async Task<RespuestaVenta?> EjecutarVentaAsync(Func<Guid?, Task<RespuestaVenta>> operacion, string descripcion, string? numeroTransaccion)
    {
        var respuesta = await operacion(null);

        for (var intento = 0; intento < IntentosMaximos; intento++)
        {
            if (respuesta.Resultado is not (CodigoResultadoVenta.RequiereAutorizacion or CodigoResultadoVenta.AutorizacionInvalida or CodigoResultadoVenta.TopeDescuentoExcedido)
                || respuesta.PermisoRequerido is not { } permiso)
                return respuesta;

            var excedeTope = respuesta.Resultado == CodigoResultadoVenta.TopeDescuentoExcedido;
            var autorizacion = await SolicitarAsync(permiso, excedeTope ? $"{descripcion}. {respuesta.Mensaje}" : descripcion, "Venta", numeroTransaccion,
                forzarSupervisor: excedeTope);
            if (autorizacion is null)
                return null;

            if (autorizacion.AutorizacionId is not { } autorizacionId)
                return respuesta;

            respuesta = await operacion(autorizacionId);
        }

        return respuesta;
    }
}
