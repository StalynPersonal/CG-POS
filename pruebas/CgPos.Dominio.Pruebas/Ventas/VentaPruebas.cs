using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;

namespace CgPos.Dominio.Pruebas.Ventas;

public class VentaPruebas
{
    private static readonly DateTimeOffset Ahora = new(2026, 9, 15, 14, 0, 0, TimeSpan.FromHours(-4));

    private static Venta NuevaVenta() =>
        Venta.Iniciar(Guid.CreateVersion7(), "01", Guid.CreateVersion7(), "02", Guid.CreateVersion7(), 123, Guid.CreateVersion7(), "Cajera Prueba", Ahora);

    private static ArticuloParaVenta Cincel() => new(
        Guid.CreateVersion7(), "43138", "7891114119695", "Cincel de punta", TipoArticulo.Normal, Guid.CreateVersion7(), true,
        "UND", false, 0, Guid.CreateVersion7(), 18m, 1, 850.00m, null, null, null, null, null);

    private static ArticuloParaVenta Cemento() => new(
        Guid.CreateVersion7(), "CEM-425", "7460001000017", "Cemento gris", TipoArticulo.Normal, Guid.CreateVersion7(), true,
        "UND", false, 0, Guid.CreateVersion7(), 18m, 1, 485.00m, 450.00m, 12m, 440m, null, null);

    private static ArticuloParaVenta Cable() => new(
        Guid.CreateVersion7(), "CAB-12", "7460001000048", "Cable #12 por pie", TipoArticulo.Normal, Guid.CreateVersion7(), true,
        "PIE", true, 2, Guid.CreateVersion7(), 18m, 1, 18.50m, null, null, null, null, null);

    private static ArticuloParaVenta Tomate(decimal? peso = null, decimal? precioEtiqueta = null) => new(
        Guid.CreateVersion7(), "12345", peso is null && precioEtiqueta is null ? "12345" : "2112345023457", "Tomate", TipoArticulo.Pesado, Guid.CreateVersion7(), false,
        "LB", true, 3, Guid.CreateVersion7(), 0m, 4, 45.00m, null, null, null, peso, precioEtiqueta);

    [Fact]
    public void Numero_de_transaccion_combina_sucursal_caja_y_secuencia()
    {
        var venta = NuevaVenta();

        Assert.Equal("01-02-00000123", venta.NumeroTransaccion);
        Assert.Equal(EstadoVenta.EnCurso, venta.Estado);
    }

    [Fact]
    public void Totales_separan_itbis_de_precios_con_impuesto_incluido()
    {
        var venta = NuevaVenta();

        venta.AgregarArticulo(Cincel(), null, Ahora);
        var totales = venta.CalcularTotales();

        Assert.Equal(720.34m, totales.Subtotal);
        Assert.Equal(129.66m, totales.Impuesto);
        Assert.Equal(850.00m, totales.Total);
        Assert.Equal(1, totales.CantidadLineas);
    }

    [Fact]
    public void Desglose_por_tasa_cuadra_con_los_totales()
    {
        var venta = NuevaVenta();
        venta.AgregarArticulo(Cincel(), 2m, Ahora);
        venta.AgregarArticulo(Cable(), 10.5m, Ahora);
        venta.AgregarArticulo(Tomate(peso: 2.345m), null, Ahora);

        var totales = venta.CalcularTotales();

        Assert.Equal(2, totales.Desglose.Count);
        Assert.Equal(totales.Subtotal, totales.Desglose.Sum(d => d.Base));
        Assert.Equal(totales.Impuesto, totales.Desglose.Sum(d => d.Impuesto));
        Assert.Equal(totales.Total, totales.Subtotal + totales.Impuesto);
        Assert.Equal(1700.00m + 194.25m + 105.53m, totales.Total); // 2×850 + 10.5×18.50 + 2.345×45 (redondeado por línea)
        Assert.Equal(0m, totales.Desglose.Single(d => d.IndicadorFacturacion == 4).Impuesto);
    }

    [Fact]
    public void Cantidad_mayor_o_igual_a_la_minima_aplica_precio_por_mayor_y_al_bajarla_vuelve_a_detalle()
    {
        var venta = NuevaVenta();
        var linea = venta.AgregarArticulo(Cemento(), 12m, Ahora);

        Assert.Equal(ListaPrecio.Mayor, linea.Lista);
        Assert.Equal(450m, linea.PrecioUnitario);

        venta.CambiarCantidad(linea.NumeroLinea, 5m, Ahora);

        Assert.Equal(ListaPrecio.Detalle, linea.Lista);
        Assert.Equal(485m, linea.PrecioUnitario);
        Assert.Equal(2425m, venta.CalcularTotales().Total);
    }

    [Fact]
    public void Unidades_con_decimales_se_redondean_y_las_enteras_rechazan_fracciones()
    {
        var venta = NuevaVenta();

        var cable = venta.AgregarArticulo(Cable(), 2.356m, Ahora);
        Assert.Equal(2.36m, cable.Cantidad);

        var error = Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Cincel(), 1.5m, Ahora));
        Assert.Equal(CodigoErrorVenta.CantidadInvalida, error.Codigo);
        Assert.Equal(CodigoErrorVenta.CantidadInvalida, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Cincel(), 0m, Ahora)).Codigo);
    }

    [Fact]
    public void Articulo_pesado_exige_balanza_o_etiqueta_y_no_permite_cambiar_su_cantidad()
    {
        var venta = NuevaVenta();

        var sinPeso = Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Tomate(), null, Ahora));
        Assert.Equal(CodigoErrorVenta.RequiereBalanza, sinPeso.Codigo);

        var conPeso = venta.AgregarArticulo(Tomate(peso: 2.345m), null, Ahora);
        Assert.Equal(2.345m, conPeso.Cantidad);
        Assert.True(conPeso.LeidaDeBalanza);
        Assert.Equal(CodigoErrorVenta.RequiereBalanza, Assert.Throws<ReglaVentaExcepcion>(() => venta.CambiarCantidad(conPeso.NumeroLinea, 3m, Ahora)).Codigo);
    }

    [Fact]
    public void Etiqueta_con_precio_embebido_respeta_el_importe_de_la_etiqueta()
    {
        var venta = NuevaVenta();

        var linea = venta.AgregarArticulo(Tomate(precioEtiqueta: 105.55m), null, Ahora);

        Assert.Equal(2.346m, linea.Cantidad); // 105.55 / 45 redondeado a 3 decimales
        Assert.Equal(105.55m, linea.ImporteConImpuesto);
        Assert.Equal(105.55m, venta.CalcularTotales().Total);
    }

    [Fact]
    public void Eliminar_linea_agrega_reverso_negativo_con_precio_cero_y_la_saca_del_total()
    {
        var venta = NuevaVenta();
        var cincel = venta.AgregarArticulo(Cincel(), 2m, Ahora);
        venta.AgregarArticulo(Cemento(), 1m, Ahora);

        var reverso = venta.EliminarLinea(cincel.NumeroLinea, Ahora);

        Assert.True(cincel.Anulada);
        Assert.True(reverso.EsReverso);
        Assert.Equal(-2m, reverso.Cantidad);
        Assert.Equal(0m, reverso.PrecioUnitario);
        Assert.Equal(cincel.NumeroLinea, reverso.LineaAnuladaNumero);
        Assert.Equal(3, reverso.NumeroLinea);
        Assert.Equal(485m, venta.CalcularTotales().Total);

        Assert.Equal(CodigoErrorVenta.LineaNoEncontrada, Assert.Throws<ReglaVentaExcepcion>(() => venta.EliminarLinea(cincel.NumeroLinea, Ahora)).Codigo);
        Assert.Equal(CodigoErrorVenta.LineaNoEncontrada, Assert.Throws<ReglaVentaExcepcion>(() => venta.EliminarLinea(reverso.NumeroLinea, Ahora)).Codigo);
    }

    [Fact]
    public void Eliminar_por_escaneo_quita_la_ultima_linea_activa_de_ese_codigo()
    {
        var venta = NuevaVenta();
        var primera = venta.AgregarArticulo(Cincel(), null, Ahora);
        var segunda = venta.AgregarArticulo(Cincel(), null, Ahora);

        venta.EliminarPorCodigo("7891114119695", Ahora);
        Assert.True(segunda.Anulada);
        Assert.False(primera.Anulada);

        venta.EliminarPorCodigo("43138", Ahora); // también por código interno
        Assert.True(primera.Anulada);
        Assert.Equal(CodigoErrorVenta.LineaNoEncontrada, Assert.Throws<ReglaVentaExcepcion>(() => venta.EliminarPorCodigo("43138", Ahora)).Codigo);
    }

    [Fact]
    public void Venta_anulada_no_admite_cambios_y_exige_motivo()
    {
        var venta = NuevaVenta();
        venta.AgregarArticulo(Cincel(), null, Ahora);

        Assert.Equal(CodigoErrorVenta.MotivoRequerido,
            Assert.Throws<ReglaVentaExcepcion>(() => venta.Anular(" ", Guid.CreateVersion7(), "Supervisor", Ahora)).Codigo);

        venta.Anular("Pantalla limpiada", Guid.CreateVersion7(), "Supervisor", Ahora);

        Assert.Equal(EstadoVenta.Anulada, venta.Estado);
        Assert.Equal(CodigoErrorVenta.VentaNoEditable, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Cincel(), null, Ahora)).Codigo);
    }

    [Fact]
    public void Articulo_sin_precio_vigente_no_se_agrega()
    {
        var venta = NuevaVenta();
        var sinPrecio = Cincel() with { PrecioDetalle = null };

        Assert.Equal(CodigoErrorVenta.SinPrecio, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(sinPrecio, null, Ahora)).Codigo);
    }

    [Fact]
    public void Cliente_con_rnc_toma_su_comprobante_y_al_quitarlo_vuelve_a_consumidor_final()
    {
        var venta = NuevaVenta();

        venta.AsignarCliente(new ClienteVenta(Guid.CreateVersion7(), TipoDocumentoIdentidad.Rnc, "131-24679-6", "Constructora Ejemplo SRL",
            TipoComprobante.FacturaCreditoFiscal), Ahora);

        Assert.Equal("131246796", venta.ClienteDocumento);
        Assert.Equal(TipoComprobante.FacturaCreditoFiscal, venta.TipoComprobante);

        venta.QuitarCliente(Ahora);
        Assert.Null(venta.ClienteNombre);
        Assert.Equal(TipoComprobante.FacturaConsumo, venta.TipoComprobante);
    }

    [Fact]
    public void Comprobante_valida_el_documento_que_exige_cada_tipo()
    {
        var venta = NuevaVenta();

        Assert.Equal(CodigoErrorVenta.DocumentoRequerido,
            Assert.Throws<ReglaVentaExcepcion>(() => venta.CambiarComprobante(TipoComprobante.FacturaCreditoFiscal, Ahora)).Codigo);
        Assert.Equal(CodigoErrorVenta.ComprobanteNoPermitido,
            Assert.Throws<ReglaVentaExcepcion>(() => venta.CambiarComprobante(TipoComprobante.NotaCredito, Ahora)).Codigo);

        // Con cédula: crédito fiscal sí, gubernamental no (exige RNC).
        venta.AsignarCliente(new ClienteVenta(null, TipoDocumentoIdentidad.Cedula, "001-1391820-5", "Juan Pérez", TipoComprobante.Gubernamental), Ahora);
        Assert.Equal(TipoComprobante.FacturaConsumo, venta.TipoComprobante); // su predeterminado no aplica sin RNC

        venta.CambiarComprobante(TipoComprobante.FacturaCreditoFiscal, Ahora);
        Assert.Equal(TipoComprobante.FacturaCreditoFiscal, venta.TipoComprobante);
        Assert.Equal(CodigoErrorVenta.DocumentoRequerido,
            Assert.Throws<ReglaVentaExcepcion>(() => venta.CambiarComprobante(TipoComprobante.Gubernamental, Ahora)).Codigo);
    }

    [Fact]
    public void Factura_de_consumo_desde_el_monto_minimo_exige_identificacion()
    {
        var venta = NuevaVenta();
        venta.AgregarArticulo(Cemento(), 600m, Ahora); // 600 × 450 = 270,000

        Assert.True(venta.RequiereIdentificacion(250_000m));
        Assert.False(venta.RequiereIdentificacion(300_000m));

        venta.AsignarCliente(new ClienteVenta(null, TipoDocumentoIdentidad.Cedula, "00113918205", "Cliente con cédula", TipoComprobante.FacturaConsumo), Ahora);
        Assert.False(venta.RequiereIdentificacion(250_000m));
    }

    [Fact]
    public void Limite_de_compra_avisa_cuando_el_total_lo_supera()
    {
        var venta = NuevaVenta();
        venta.EstablecerLimiteCompra(1000m, Ahora);
        venta.AgregarArticulo(Cincel(), null, Ahora);

        Assert.False(venta.LimiteCompraExcedido());

        venta.AgregarArticulo(Cemento(), null, Ahora);
        Assert.True(venta.LimiteCompraExcedido());

        Assert.Equal(CodigoErrorVenta.CantidadInvalida, Assert.Throws<ReglaVentaExcepcion>(() => venta.EstablecerLimiteCompra(0m, Ahora)).Codigo);
        venta.EstablecerLimiteCompra(null, Ahora);
        Assert.False(venta.LimiteCompraExcedido());
    }

    [Fact]
    public void Venta_en_espera_no_se_modifica_hasta_retomarla_y_no_se_guarda_vacia()
    {
        var vacia = NuevaVenta();
        Assert.Equal(CodigoErrorVenta.SinLineas, Assert.Throws<ReglaVentaExcepcion>(() => vacia.PonerEnEspera(Ahora)).Codigo);

        var venta = NuevaVenta();
        venta.AgregarArticulo(Cincel(), null, Ahora);
        venta.PonerEnEspera(Ahora);

        Assert.Equal(EstadoVenta.EnEspera, venta.Estado);
        Assert.Equal(Ahora, venta.PuestaEnEsperaEn);
        Assert.Equal(CodigoErrorVenta.VentaNoEditable, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(Cincel(), null, Ahora)).Codigo);

        venta.Retomar(Ahora.AddMinutes(5));
        Assert.Equal(EstadoVenta.EnCurso, venta.Estado);
        Assert.Null(venta.PuestaEnEsperaEn);
        venta.AgregarArticulo(Cincel(), null, Ahora);
        Assert.Equal(1700m, venta.CalcularTotales().Total);
    }

    [Fact]
    public void Serializado_exige_serial_unico_y_se_vende_de_uno_en_uno()
    {
        var venta = NuevaVenta();
        var taladro = Cincel() with { Tipo = TipoArticulo.Serializado, Descripcion = "Taladro inalámbrico" };

        Assert.Equal(CodigoErrorVenta.RequiereSerial, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(taladro, null, Ahora)).Codigo);
        Assert.Equal(CodigoErrorVenta.CantidadInvalida, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(taladro, 2m, Ahora, "SN-1")).Codigo);

        var linea = venta.AgregarArticulo(taladro, null, Ahora, " sn-001 ");
        Assert.Equal("SN-001", linea.Serial);
        Assert.Equal(CodigoErrorVenta.SerialDuplicado, Assert.Throws<ReglaVentaExcepcion>(() => venta.AgregarArticulo(taladro, null, Ahora, "SN-001")).Codigo);
        Assert.Equal(CodigoErrorVenta.CantidadInvalida, Assert.Throws<ReglaVentaExcepcion>(() => venta.CambiarCantidad(linea.NumeroLinea, 2m, Ahora)).Codigo);

        // Al eliminar la línea, el serial queda libre para volver a escanearlo.
        var reverso = venta.EliminarLinea(linea.NumeroLinea, Ahora);
        Assert.Equal("SN-001", reverso.Serial);
        venta.AgregarArticulo(taladro, null, Ahora, "SN-001");

        // A un artículo normal no se le guarda serial.
        Assert.Null(venta.AgregarArticulo(Cincel(), null, Ahora, "IGNORADO").Serial);
    }

    [Theory]
    [InlineData(2.500, 0.150, 2.350)]
    [InlineData(1.2345, null, 1.235)]
    public void Peso_neto_descuenta_la_tara_del_empaque(decimal bruto, double? tara, decimal esperado) =>
        Assert.Equal(esperado, ReglasBalanza.PesoNeto(bruto, (decimal?)tara));

    [Fact]
    public void Peso_neto_nulo_si_la_tara_iguala_o_supera_el_peso()
    {
        Assert.Null(ReglasBalanza.PesoNeto(0.150m, 0.150m));
        Assert.Null(ReglasBalanza.PesoNeto(0m, null));
    }

    [Fact]
    public void Turno_valida_fondo_y_no_se_cierra_dos_veces()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Turno.Abrir(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, DateOnly.FromDateTime(Ahora.Date), Guid.CreateVersion7(), "Cajera", -1m, Ahora));

        var turno = Turno.Abrir(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, DateOnly.FromDateTime(Ahora.Date), Guid.CreateVersion7(), "Cajera", 5000.005m, Ahora);
        Assert.Equal(5000.01m, turno.FondoInicial);
        Assert.True(turno.EstaAbierto);

        turno.Cerrar(Ahora.AddHours(8));
        Assert.Throws<InvalidOperationException>(() => turno.Cerrar(Ahora.AddHours(9)));
    }
}
