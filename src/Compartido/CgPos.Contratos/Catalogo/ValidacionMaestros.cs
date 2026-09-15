using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;

namespace CgPos.Contratos.Catalogo;

/// <summary>
/// Valida un paquete de maestros con las mismas reglas del dominio que aplica la caja al cargarlo, construyendo cada registro en memoria.
/// El Central lo usa antes de publicar: un maestro inválido detendría la sincronización de todas las cajas.
/// Las referencias entre maestros (familia del artículo, moneda de la forma de pago…) las valida quien conoce lo ya publicado.
/// </summary>
public static class ValidacionMaestros
{
    public static IReadOnlyList<string> Validar(PaqueteMaestros paquete)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        var errores = new List<string>();

        void Probar(string etiqueta, Action construir)
        {
            try
            {
                construir();
            }
            catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
            {
                errores.Add($"{etiqueta}: {excepcion.Message}");
            }
        }

        foreach (var d in paquete.Monedas ?? [])
            Probar($"Moneda '{d.Codigo}'", () => Moneda.Crear(d.Codigo, d.Nombre, d.Simbolo, d.Id));

        foreach (var d in paquete.Familias ?? [])
            Probar($"Familia '{d.Codigo}'", () => Familia.Crear(d.Codigo, d.Nombre, d.PermiteDescuentoManual, d.EsNoCodificada, d.Id));

        foreach (var d in paquete.UnidadesMedida ?? [])
            Probar($"Unidad de medida '{d.Codigo}'", () => UnidadMedida.Crear(d.Codigo, d.Nombre, d.PermiteDecimales, d.Decimales, d.Id));

        foreach (var d in paquete.Impuestos ?? [])
            Probar($"Impuesto '{d.Codigo}'", () => Impuesto.Crear(d.Codigo, d.Nombre, d.Porcentaje, d.IndicadorFacturacion, d.Id));

        foreach (var d in paquete.Articulos ?? [])
            Probar($"Artículo '{d.Codigo}'", () =>
            {
                if (d.PrecioDetalle <= 0)
                    throw new ArgumentException("El precio detalle debe ser mayor que cero.");
                if (d.PrecioMayor <= 0)
                    throw new ArgumentException("El precio por mayor debe ser mayor que cero.");

                var articulo = Articulo.Crear(d.Codigo, d.Descripcion, d.FamiliaId, d.UnidadMedidaId, d.ImpuestoId, d.Tipo, d.Id);
                articulo.ActualizarDatos(d.Descripcion, d.Referencia, d.FamiliaId, d.UnidadMedidaId, d.ImpuestoId, d.Tipo);
                articulo.ConfigurarPrecios(d.Costo, d.PrecioMinimo, d.CantidadMinimaMayor);
                articulo.ConfigurarTara(d.Tara);
                articulo.ConfigurarNaturaleza(d.EsServicio);
                articulo.ConfigurarPresentacion(d.RutaImagen, d.MostrarEnCatalogo, d.VentaEnPos);
                articulo.ReemplazarCodigos(
                    (d.CodigosBarras ?? []).Select(c => (c, TipoCodigoArticulo.Barras))
                        .Concat((d.CodigosProveedor ?? []).Select(c => (c, TipoCodigoArticulo.Proveedor))));
            });

        foreach (var d in paquete.Clientes ?? [])
            Probar($"Cliente '{d.Documento}'", () =>
            {
                var cliente = Cliente.Crear(d.TipoDocumento, d.Documento, d.Nombre, d.Id);
                cliente.ActualizarContacto(d.Nombre, d.Telefono, d.Correo);
                cliente.ConfigurarFacturacion(d.TipoComprobante, d.ExoneradoItbis, d.AplicaRetencion, d.ListaPrecio);
                foreach (var direccion in d.Direcciones ?? [])
                    cliente.AgregarDireccion(direccion.Alias, direccion.Direccion, direccion.Sector, direccion.Ciudad, direccion.Referencia, direccion.Telefono, id: direccion.Id);
                if ((d.Direcciones ?? []).FirstOrDefault(x => x.EsPrincipal) is { } principal)
                    cliente.MarcarPrincipal(principal.Id);
            });

        foreach (var d in paquete.FormasPago ?? [])
            Probar($"Forma de pago '{d.Codigo}'", () =>
            {
                var forma = FormaPago.Crear(d.Codigo, d.Nombre, d.Tipo, d.Orden, d.Moneda, d.Id);
                var sugeridos = FormaPago.ValoresPorTipo(d.Tipo);
                forma.Configurar(d.Nombre, d.Orden, d.AbreGaveta ?? sugeridos.AbreGaveta, d.PermiteDevuelta ?? sugeridos.PermiteDevuelta,
                    d.RequiereReferencia ?? sugeridos.RequiereReferencia, d.RequiereBanco ?? sugeridos.RequiereBanco, d.PermiteComprobanteFiscal ?? sugeridos.PermiteComprobanteFiscal);
            });

        foreach (var d in paquete.Bancos ?? [])
            Probar($"Banco '{d.Codigo}'", () => Banco.Crear(d.Codigo, d.Nombre, d.RutaLogo, d.Id));

        foreach (var d in paquete.TiposTarjeta ?? [])
            Probar($"Tipo de tarjeta '{d.Codigo}'", () => TipoTarjeta.Crear(d.Codigo, d.Nombre, d.Id));

        foreach (var d in paquete.Denominaciones ?? [])
            Probar($"Denominación {d.Moneda} {d.Valor}", () => Denominacion.Crear(d.Moneda, d.Valor, d.Tipo, d.Id));

        foreach (var d in paquete.Promociones ?? [])
            Probar($"Promoción '{d.Codigo}'", () =>
            {
                var promocion = Promocion.Crear(d.Codigo, d.Nombre, d.Tipo, d.Valor, d.VigenteDesde, d.VigenteHasta, d.Id);
                promocion.ConfigurarCantidades(d.CantidadLleva, d.CantidadPaga, d.CantidadMinima, d.LimitePorCliente);
                promocion.Programar(d.Dias, d.HoraDesde, d.HoraHasta);
                promocion.AsignarAlcance(d.Articulos, d.Familias, d.Sucursales);
                promocion.ConfigurarFidelidad(d.SoloFidelidad);
            });

        foreach (var d in paquete.MotivosDescuento ?? [])
            Probar($"Motivo de descuento '{d.Codigo}'", () => MotivoDescuento.Crear(d.Codigo, d.Nombre, d.Id));

        foreach (var d in paquete.TopesDescuento ?? [])
            Probar($"Tope de descuento {d.Id}", () => TopeDescuento.Crear(d.Nivel, d.PorcentajeMaximo, d.MontoMaximo, d.FamiliaId, d.ArticuloId, d.Id));

        foreach (var d in paquete.TasasCambio ?? [])
            Probar($"Tasa de cambio {d.Moneda}", () => TasaCambio.Registrar(d.Moneda, d.Tasa, d.VigenteDesde, d.Id));

        foreach (var d in paquete.SecuenciasEcf ?? [])
            Probar($"Rango de e-CF {d.TipoComprobante} {d.Desde}-{d.Hasta}", () =>
                SecuenciaEcf.Asignar(d.CajaId, d.TipoComprobante, d.Desde, d.Hasta, d.VenceEn, d.Id).Actualizar(d.Hasta, d.VenceEn, d.Activa));

        foreach (var d in paquete.MotivosDevolucion ?? [])
            Probar($"Motivo de devolución '{d.Codigo}'", () => MotivoDevolucion.Crear(d.Codigo, d.Nombre, d.Id));

        foreach (var d in paquete.NivelesFidelidad ?? [])
            Probar($"Nivel de fidelidad '{d.Codigo}'", () => NivelFidelidad.Crear(d.Codigo, d.Nombre, d.Orden, d.FactorAcumulacion, d.Id));

        foreach (var d in paquete.ReglasAcumulacion ?? [])
            Probar($"Regla de acumulación '{d.Codigo}'", () =>
                ReglaAcumulacion.Crear(d.Codigo, d.Nombre, d.Tipo, d.MontoBase, d.Puntos, d.ReferenciaId, d.DiaSemana, d.VigenteDesde, d.VigenteHasta, d.Id));

        foreach (var d in paquete.MiembrosFidelidad ?? [])
            Probar($"Miembro de fidelidad '{d.Cedula}'", () =>
            {
                var miembro = MiembroFidelidad.DesdeCentral(MiembroFidelidad.ValidarCedula(d.Cedula), d.Nombre, d.InscritoEn ?? DateTimeOffset.UnixEpoch, d.Id);
                miembro.ActualizarContacto(d.Nombre, d.Telefono, d.Correo);
                miembro.AsignarNivel(d.NivelId);
                if (d.SaldoAl is { } saldoAl)
                    miembro.SincronizarSaldo(d.SaldoPuntos, saldoAl, d.PuntosPorVencer, d.ProximoVencimiento);
            });

        foreach (var d in paquete.Almacenes ?? [])
            Probar($"Almacén '{d.Codigo}'", () => Almacen.Crear(d.Codigo, d.Nombre, d.SucursalId, d.Direccion, d.Id));

        return errores;
    }
}
