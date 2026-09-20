using System.Collections.Frozen;

namespace CgPos.Dominio.Seguridad;

public sealed record DefinicionPermiso(string Codigo, string Modulo, string Descripcion);

/// <summary>
/// Catálogo único de permisos granulares por operación. El código es la fuente de verdad:
/// la tabla de permisos se sincroniza desde aquí. Formato de código: "Modulo.Accion".
/// </summary>
public static class CatalogoPermisos
{
    // Seguridad
    public const string AutorizarOperaciones = "Seguridad.AutorizarOperaciones";
    public const string AdministrarConfiguracion = "Seguridad.AdministrarConfiguracion";

    // Caja y turnos
    public const string AbrirTurno = "Caja.AbrirTurno";
    public const string CerrarTurno = "Caja.CerrarTurno";
    public const string RelevoCajero = "Caja.RelevoCajero";
    public const string RetiroEfectivo = "Caja.RetiroEfectivo";
    public const string AbrirGaveta = "Caja.AbrirGaveta";
    public const string PreCierre = "Caja.PreCierre";

    // Ventas
    public const string RegistrarVenta = "Ventas.Registrar";
    public const string EliminarLinea = "Ventas.EliminarLinea";
    public const string LimpiarPantalla = "Ventas.LimpiarPantalla";
    public const string AnularVenta = "Ventas.Anular";
    public const string SuspenderVenta = "Ventas.Suspender";
    public const string CambiarComprobante = "Ventas.CambiarComprobante";
    public const string FacturarCotizacionVencida = "Ventas.FacturarCotizacionVencida";
    public const string AplicarPrecioMayor = "Ventas.AplicarPrecioMayor";
    public const string VenderBajoPrecioMinimo = "Ventas.VenderBajoPrecioMinimo";
    public const string ExonerarItbis = "Ventas.ExonerarItbis";

    // Descuentos y promociones
    public const string DescuentoLinea = "Descuentos.Linea";
    public const string DescuentoFactura = "Descuentos.Factura";
    public const string DesactivarPromocion = "Promociones.Desactivar";

    // Devoluciones, pendientes y cobro
    public const string RegistrarDevolucion = "Devoluciones.Registrar";
    public const string AutorizarDevolucion = "Devoluciones.Autorizar";
    public const string AutorizarNotaCreditoInterna = "Devoluciones.AutorizarNotaCreditoInterna";
    public const string MarcarPendiente = "Pendientes.Marcar";
    public const string DespacharPendiente = "Pendientes.Despachar";
    public const string AnularPendiente = "Pendientes.Anular";
    public const string AprobacionManualTarjeta = "Cobro.AprobacionManualTarjeta";

    // Fidelidad
    public const string CanjearPuntos = "Fidelidad.CanjearPuntos";

    public static IReadOnlyList<DefinicionPermiso> Todos { get; } =
    [
        new(AutorizarOperaciones, "Seguridad", "Autorizar operaciones de otros usuarios (clave de supervisor)"),
        new(AdministrarConfiguracion, "Seguridad", "Administrar usuarios, roles y parámetros de la caja"),

        new(AbrirTurno, "Caja", "Abrir turno con fondo de caja"),
        new(CerrarTurno, "Caja", "Cerrar turno y realizar el cuadre"),
        new(RelevoCajero, "Caja", "Relevar temporalmente al cajero sin cerrar el turno"),
        new(RetiroEfectivo, "Caja", "Registrar retiros parciales de efectivo"),
        new(AbrirGaveta, "Caja", "Abrir la gaveta sin una venta"),
        new(PreCierre, "Caja", "Emitir el reporte de pre-cierre"),

        new(RegistrarVenta, "Ventas", "Registrar ventas"),
        new(EliminarLinea, "Ventas", "Eliminar líneas de una venta"),
        new(LimpiarPantalla, "Ventas", "Limpiar la pantalla de venta"),
        new(AnularVenta, "Ventas", "Anular una venta antes de cerrarla"),
        new(SuspenderVenta, "Ventas", "Suspender operaciones"),
        new(CambiarComprobante, "Ventas", "Cambiar el tipo de comprobante fiscal"),
        new(FacturarCotizacionVencida, "Ventas", "Facturar una cotización cuya vigencia ya pasó"),
        new(AplicarPrecioMayor, "Ventas", "Aplicar la lista de precios por mayor"),
        new(VenderBajoPrecioMinimo, "Ventas", "Vender por debajo del precio mínimo"),
        new(ExonerarItbis, "Ventas", "Exonerar el ITBIS de una venta"),

        new(DescuentoLinea, "Descuentos", "Aplicar descuento a un artículo"),
        new(DescuentoFactura, "Descuentos", "Aplicar descuento a la factura completa"),
        new(DesactivarPromocion, "Promociones", "Desactivar una promoción vigente en una venta"),

        new(RegistrarDevolucion, "Devoluciones", "Registrar devoluciones"),
        new(AutorizarDevolucion, "Devoluciones", "Autorizar devoluciones"),
        new(AutorizarNotaCreditoInterna, "Devoluciones", "Autorizar notas de crédito internas, sin comprobante fiscal"),
        new(MarcarPendiente, "Pendientes", "Marcar artículos como pendientes de entrega o envío"),
        new(DespacharPendiente, "Pendientes", "Preparar, despachar y registrar la entrega de pendientes"),
        new(AnularPendiente, "Pendientes", "Anular un pendiente de entrega no entregado"),
        new(AprobacionManualTarjeta, "Cobro", "Registrar aprobación manual de tarjeta (contingencia)"),

        new(CanjearPuntos, "Fidelidad", "Canjear puntos de fidelidad como forma de pago"),
    ];

    private static readonly FrozenSet<string> Codigos = Todos.Select(p => p.Codigo).ToFrozenSet(StringComparer.Ordinal);

    public static bool Existe(string codigo) => Codigos.Contains(codigo);
}
