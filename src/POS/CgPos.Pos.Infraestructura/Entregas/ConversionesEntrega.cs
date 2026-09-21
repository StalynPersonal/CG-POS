using CgPos.Contratos.Ventas;
using CgPos.Dominio.Entregas;

namespace CgPos.Pos.Infraestructura.Entregas;

internal static class ConversionesEntrega
{
    public static DatosDestinoEntrega ADatos(this DestinoEntrega destino) =>
        new(destino.Numero, destino.Metodo, destino.SucursalRetiroId, destino.SucursalRetiroNombre, destino.Direccion, destino.Sector, destino.Ciudad, destino.Referencia,
            destino.Telefono, destino.Transportista, destino.CostoEnvio, destino.FechaComprometida, destino.Comentario, destino.AutorizadoPorNombre,
            destino.Lineas.OrderBy(l => l.NumeroLinea).Select(l => new DatosLineaDestinoEntrega(l.NumeroLinea, l.Cantidad)).ToList());

    public static DatosPendienteEntrega ADatos(this PendienteEntrega pendiente) =>
        new(pendiente.Id, pendiente.Numero, pendiente.VentaId, pendiente.VentaNumero, pendiente.SucursalId, pendiente.CajaId, pendiente.Metodo, pendiente.Estado,
            pendiente.SucursalRetiroId, pendiente.SucursalRetiroNombre, pendiente.Direccion, pendiente.Sector, pendiente.Ciudad, pendiente.Referencia, pendiente.Telefono,
            pendiente.Transportista, pendiente.CostoEnvio, pendiente.FechaComprometida, pendiente.Comentario, pendiente.ClienteDocumento, pendiente.ClienteNombre,
            pendiente.VendidoPorNombre, pendiente.AutorizadoPorNombre, pendiente.CreadoEn, pendiente.ActualizadoEn, pendiente.ActualizadoPorNombre,
            pendiente.MotivoAnulacion,
            pendiente.Lineas.OrderBy(l => l.NumeroLineaVenta)
                .Select(l => new DatosLineaPendiente(l.NumeroLineaVenta, l.CodigoInterno, l.Descripcion, l.UnidadMedidaCodigo, l.DecimalesCantidad, l.Serializado,
                    l.Cantidad, l.CantidadEntregada, l.Serial))
                .ToList(),
            pendiente.Entregas.OrderBy(e => e.Numero)
                .Select(e => new DatosEntregaPendiente(e.Numero, e.RecibeNombre, e.RecibeCedula, e.UsuarioNombre, e.Fecha,
                    e.Lineas.Select(l => new DatosLineaEntregaPendiente(l.NumeroLineaVenta, l.Descripcion, l.Cantidad, l.Serial)).ToList()))
                .ToList());
}
