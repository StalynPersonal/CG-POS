using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Pruebas.Entregas;

public class EntregasPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(-4));
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.DateTime);
    private static readonly int Almacen = Ids.Siguiente();

    private static readonly ArticuloParaVenta Cemento = new(Ids.Siguiente(), "CEM", "CEM", "Cemento gris", TipoArticulo.Normal, Ids.Siguiente(), true, "UN",
        false, 0, Ids.Siguiente(), 18m, 1, 485m, null, null, null, null, null);

    private static readonly ArticuloParaVenta Taladro = new(Ids.Siguiente(), "TAL", "TAL", "Taladro", TipoArticulo.Serializado, Ids.Siguiente(), true, "UN",
        false, 0, Ids.Siguiente(), 18m, 1, 6950m, null, null, null, null, null);

    private static readonly FormaPagoParaCobro Efectivo = new(Ids.Siguiente(), "EFE", "Efectivo", TipoFormaPago.Efectivo, "DOP", true, false, false, true, true);

    private static readonly DatosEnvio Envio = new("Calle 1 #2", "Naco", "Santo Domingo", null, "809-555-1111", "Mensajería", 350m);

    private static Venta VentaConCementoYTaladroSinSerial()
    {
        var venta = Venta.Iniciar(Ids.Siguiente(), 1, Ids.Siguiente(), 1, Ids.Siguiente(), 1, 7, Ids.Siguiente(), "Cajera", "DOP", "RD$", Ahora);
        venta.AgregarArticulo(Cemento, 3m, Ahora);
        venta.AgregarArticulo(Taladro, null, Ahora, serialEnDespacho: true);
        return venta;
    }

    private static CodigoErrorVenta Rechazo(Action accion) => Assert.Throws<ReglaVentaExcepcion>(accion).Codigo;

    [Fact]
    public void Marcar_entrega_valida_destino_cantidades_y_bloquea_cambios_de_la_linea()
    {
        var venta = VentaConCementoYTaladroSinSerial();

        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() =>
            venta.MarcarEntrega(MetodoEntrega.RetiroAlmacen, null, null, null, null, null, [new CantidadEntrega(1, 2m)], null, null, Hoy, Ahora)));
        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() =>
            venta.MarcarEntrega(MetodoEntrega.Envio, null, null, Envio with { Telefono = " " }, null, null, [new CantidadEntrega(1, 2m)], null, null, Hoy, Ahora)));
        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() =>
            venta.MarcarEntrega(MetodoEntrega.RetiroAlmacen, Almacen, "Kennedy", null, Hoy.AddDays(-1), null, [new CantidadEntrega(1, 2m)], null, null, Hoy, Ahora)));
        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() =>
            venta.MarcarEntrega(MetodoEntrega.RetiroAlmacen, Almacen, "Kennedy", null, null, null, [new CantidadEntrega(1, 4m)], null, null, Hoy, Ahora)));

        var retiro = venta.MarcarEntrega(MetodoEntrega.RetiroAlmacen, Almacen, "Kennedy", null, Hoy.AddDays(2), "Retira el jueves", [new CantidadEntrega(1, 2m)],
            Ids.Siguiente(), "Supervisor", Hoy, Ahora);
        Assert.Equal(1, retiro.Numero);
        Assert.Equal(2m, venta.CantidadEnEntregas(1));

        // Solo queda 1 cemento por marcar en otro destino; la línea no se modifica mientras tenga entrega.
        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() =>
            venta.MarcarEntrega(MetodoEntrega.Envio, null, null, Envio, null, null, [new CantidadEntrega(1, 2m)], null, null, Hoy, Ahora)));
        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() => venta.CambiarCantidad(1, 5m, Ahora)));
        Assert.Equal(CodigoErrorVenta.EntregaInvalida, Rechazo(() => venta.EliminarLinea(1, Ahora)));

        venta.QuitarEntrega(retiro.Numero, Ahora);
        venta.CambiarCantidad(1, 5m, Ahora);
        Assert.Empty(venta.DestinosEntrega);
    }

    [Fact]
    public void Serializado_sin_serial_solo_se_cobra_si_se_entrega_despues_y_genera_el_pendiente()
    {
        var venta = VentaConCementoYTaladroSinSerial();
        Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Taladro, null, Ahora));

        Assert.Equal(CodigoErrorVenta.RequiereSerial, Rechazo(() => venta.Cobrar([new PagoSolicitado(Efectivo, 10_000m)], 0m, 250_000m, Ids.Siguiente(), "Cajera", Ahora)));

        venta.MarcarEntrega(MetodoEntrega.Envio, null, null, Envio, Hoy.AddDays(1), null, [new CantidadEntrega(2, 1m), new CantidadEntrega(1, 1m)],
            Ids.Siguiente(), "Supervisor", Hoy, Ahora);
        venta.Cobrar([new PagoSolicitado(Efectivo, 10_000m)], 0m, 250_000m, Ids.Siguiente(), "Cajera", Ahora);

        var pendiente = PendienteEntrega.Crear(venta, venta.DestinosEntrega.Single(), "PE-01-00000001", Ahora);
        Assert.Equal(EstadoPendiente.Pendiente, pendiente.Estado);
        Assert.Equal(2, pendiente.Lineas.Count);
        Assert.Equal(1m, pendiente.CantidadPorEntregar(1));
        Assert.Equal("Calle 1 #2", pendiente.Direccion);
        Assert.True(pendiente.Lineas.Single(l => l.NumeroLineaVenta == 2).Serializado);
    }

    [Fact]
    public void Pendiente_se_prepara_entrega_por_partes_con_serial_y_no_se_anula_con_entregas()
    {
        var venta = VentaConCementoYTaladroSinSerial();
        venta.MarcarEntrega(MetodoEntrega.RetiroAlmacen, Almacen, "Kennedy", null, null, null, [new CantidadEntrega(1, 3m), new CantidadEntrega(2, 1m)],
            Ids.Siguiente(), "Supervisor", Hoy, Ahora);
        venta.Cobrar([new PagoSolicitado(Efectivo, 10_000m)], 0m, 250_000m, Ids.Siguiente(), "Cajera", Ahora);
        var pendiente = PendienteEntrega.Crear(venta, venta.DestinosEntrega.Single(), "PE-01-00000002", Ahora);

        pendiente.CambiarEstado(EstadoPendiente.EnPreparacion, "Almacén", Ahora);
        pendiente.CambiarEstado(EstadoPendiente.Preparado, "Almacén", Ahora);
        Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.CambiarEstado(EstadoPendiente.Despachado, "Almacén", Ahora)); // solo envíos

        Assert.Equal(CodigoErrorPendiente.RecibeRequerido,
            Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.Entregar([new CantidadEntregada(1, 1m)], null, null, "Almacén", Ahora)).Codigo);
        Assert.Equal(CodigoErrorPendiente.SerialRequerido,
            Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.Entregar([new CantidadEntregada(2, 1m)], "Juan Pérez", "00113918205", "Almacén", Ahora)).Codigo);
        Assert.Equal(CodigoErrorPendiente.CantidadInvalida,
            Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.Entregar([new CantidadEntregada(1, 4m)], "Juan Pérez", "00113918205", "Almacén", Ahora)).Codigo);

        pendiente.Entregar([new CantidadEntregada(1, 2m)], "Juan Pérez", "00113918205", "Almacén", Ahora);
        Assert.Equal(EstadoPendiente.Parcial, pendiente.Estado);
        Assert.Equal(1m, pendiente.CantidadPorEntregar(1));
        Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.Anular("Cliente desistió", "Supervisor", Ahora));

        var entrega = pendiente.Entregar([new CantidadEntregada(1, 1m), new CantidadEntregada(2, 1m, "sn-778")], "Juan Pérez", "00113918205", "Almacén", Ahora);
        Assert.Equal(EstadoPendiente.Entregado, pendiente.Estado);
        Assert.Equal(2, entrega.Numero);
        Assert.Equal("SN-778", pendiente.Lineas.Single(l => l.NumeroLineaVenta == 2).Serial);
        Assert.Equal(0m, pendiente.CantidadPorEntregar(1));
    }

    [Fact]
    public void Pendiente_sin_entregas_se_anula_con_motivo_y_libera_la_mercancia()
    {
        var venta = VentaConCementoYTaladroSinSerial();
        venta.MarcarEntrega(MetodoEntrega.Envio, null, null, Envio, null, null, [new CantidadEntrega(1, 3m), new CantidadEntrega(2, 1m)], null, null, Hoy, Ahora);
        venta.Cobrar([new PagoSolicitado(Efectivo, 10_000m)], 0m, 250_000m, Ids.Siguiente(), "Cajera", Ahora);
        var pendiente = PendienteEntrega.Crear(venta, venta.DestinosEntrega.Single(), "PE-01-00000003", Ahora);

        pendiente.CambiarEstado(EstadoPendiente.Preparado, "Almacén", Ahora);
        pendiente.CambiarEstado(EstadoPendiente.Despachado, "Almacén", Ahora);
        Assert.Equal(CodigoErrorPendiente.MotivoRequerido, Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.Anular(" ", "Supervisor", Ahora)).Codigo);

        pendiente.Anular("Dirección no existe", "Supervisor", Ahora);
        Assert.Equal(EstadoPendiente.Anulado, pendiente.Estado);
        Assert.Equal(0m, pendiente.CantidadPorEntregar(1));
        Assert.Throws<ReglaPendienteExcepcion>(() => pendiente.Entregar([new CantidadEntregada(1, 1m)], "Juan", "00113918205", "Almacén", Ahora));
    }
}
