using CgPos.Contratos.Catalogo;
using System.Text.Json;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Fidelidad;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Promociones;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Devoluciones;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Fidelidad;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Ventas;
using CgPos.Pos.Pruebas.Sincronizacion;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Ventas;

/// <summary>Turno y venta en curso contra SQL Server real: escaneo, precios, eliminación con autorización y recuperación.</summary>
public class VentasPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private static readonly int Empresa = Ids.Siguiente();

    [SkippableFact]
    public async Task Sin_turno_abierto_no_se_puede_vender_y_se_ofrece_abrirlo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, abrirTurno: false);

        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        var venta = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.ObtenerActualAsync(caja.Cajero));

        Assert.Null(estado.TurnoAbierto);
        Assert.True(estado.PuedeAbrir);
        Assert.Equal(CodigoResultadoVenta.TurnoNoAbierto, venta.Resultado);
    }

    [SkippableFact]
    public async Task Solo_puede_haber_un_turno_abierto_por_caja()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, abrirTurno: false);

        var primero = await caja.EjecutarAsync<IServicioTurnos, RespuestaTurno>(s => s.AbrirAsync(caja.Cajero, 5000m));
        var segundo = await caja.EjecutarAsync<IServicioTurnos, RespuestaTurno>(s => s.AbrirAsync(caja.Cajero, 1000m));
        var otroCajero = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.ObtenerActualAsync(caja.CajeroDos));

        Assert.True(primero.Exitosa, primero.Mensaje);
        Assert.Equal(5000m, primero.Turno!.FondoInicial);
        Assert.Equal(1, primero.Turno.Numero);
        Assert.Equal(CodigoResultadoTurno.YaExisteTurnoAbierto, segundo.Resultado);
        Assert.Equal(CodigoResultadoVenta.TurnoDeOtroUsuario, otroCajero.Resultado);
    }

    [SkippableFact]
    public async Task Venta_registra_articulos_por_barras_cantidad_y_balanza_con_precios_y_totales()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var inicio = await caja.VentaActualAsync();

        // Muestra el número que le tocaría si se cobrara ahora; se toma de verdad al cobrar.
        Assert.Matches($@"^{caja.Escenario.CodigoSucursal}{caja.Escenario.CodigoCajaUno}1\d{{7}}$", inicio.NumeroTransaccion);

        await caja.AgregarAsync(inicio.Id, caja.Catalogo.BarrasCincel);
        var conMayor = await caja.AgregarAsync(inicio.Id, $"12*{caja.Catalogo.CodigoCemento}");
        var conTomate = await caja.AgregarAsync(inicio.Id, caja.Catalogo.EtiquetaPesoTomate(2.345m));

        var cemento = conMayor.Lineas.Single(l => l.CodigoInterno == caja.Catalogo.CodigoCemento);
        Assert.Equal(ListaPrecio.Mayor, cemento.Lista);
        Assert.Equal(450m, cemento.PrecioUnitario);
        Assert.Equal(12m, cemento.Cantidad);

        var tomate = conTomate.Lineas.Single(l => l.CodigoInterno == caja.Catalogo.PluTomate);
        Assert.True(tomate.LeidaDeBalanza);
        Assert.Equal(2.345m, tomate.Cantidad);

        // 850 + 12×450 + 2.345×45 (105.53), sin ITBIS
        Assert.Equal(6355.53m, conTomate.Totales.Subtotal);
        Assert.Equal(conTomate.Totales.Total, conTomate.Totales.Subtotal + conTomate.Totales.Impuesto);

        var menosCemento = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.CambiarCantidadAsync(caja.Cajero, inicio.Id, cemento.NumeroLinea, 2m));
        var cementoDetalle = menosCemento.Venta!.Lineas.Single(l => l.NumeroLinea == cemento.NumeroLinea);
        Assert.Equal(ListaPrecio.Detalle, cementoDetalle.Lista);
        Assert.Equal(485m, cementoDetalle.PrecioUnitario);
    }

    [SkippableFact]
    public async Task Codigo_inexistente_y_pesado_sin_balanza_devuelven_un_mensaje_claro_sin_alterar_la_venta()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();

        var inexistente = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, venta.Id, "NO-EXISTE", null));
        var sinBalanza = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, venta.Id, caja.Catalogo.PluTomate, null));
        var fraccion = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, venta.Id, $"1.5*{caja.Catalogo.BarrasCincel}", null));

        Assert.Equal(CodigoResultadoVenta.ArticuloNoEncontrado, inexistente.Resultado);
        Assert.Contains("NO-EXISTE", inexistente.Mensaje);
        Assert.Equal(CodigoResultadoVenta.RequiereBalanza, sinBalanza.Resultado);
        Assert.Equal(CodigoResultadoVenta.CantidadInvalida, fraccion.Resultado);
        Assert.Empty(fraccion.Venta!.Lineas);
    }

    [SkippableFact]
    public async Task La_venta_en_curso_se_recupera_tras_un_cierre_inesperado()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        int ventaId;
        await using (var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa))
        {
            var venta = await caja.VentaActualAsync();
            ventaId = venta.Id;
            for (var i = 0; i < 20; i++)
                await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

            // Se "apaga" la caja: se descarta el proveedor y se abre uno nuevo contra la misma base.
            await using var reabierta = caja.Reabrir();
            var recuperada = await reabierta.VentaActualAsync();

            Assert.Equal(ventaId, recuperada.Id);
            Assert.Equal(20, recuperada.Lineas.Count);
            Assert.Equal(17_000m, recuperada.Totales.Subtotal);
        }
    }

    [SkippableFact]
    public async Task Eliminar_linea_requiere_permiso_o_una_autorizacion_de_un_solo_uso()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCemento);

        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 1, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);
        Assert.Equal(CatalogoPermisos.EliminarLinea, sinPermiso.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.EliminarLinea, "Artículo mal escaneado");
        var eliminada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 1, autorizacion));

        Assert.True(eliminada.Exitosa, eliminada.Mensaje);
        // La eliminada queda en su lugar, anulada: no se agrega otra línea y la numeración sigue continua.
        Assert.Equal(new[] { 1, 2 }, eliminada.Venta!.Lineas.Select(l => l.NumeroLinea));
        Assert.True(eliminada.Venta.Lineas[0].Anulada);
        Assert.False(eliminada.Venta.Lineas[1].Anulada);
        Assert.Equal(485m, eliminada.Venta.Totales.Subtotal);

        var reutilizada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 2, autorizacion));
        Assert.Equal(CodigoResultadoVenta.AutorizacionInvalida, reutilizada.Resultado);

        var registro = await caja.EjecutarAsync<ContextoDatosPos, Dominio.Auditoria.RegistroAuditoria>(contexto =>
            contexto.Auditoria.SingleAsync(r => r.Accion == "Ventas.LineaEliminada" && r.EntidadId == $"B-{venta.Id:000000}"));
        Assert.Equal(caja.Escenario.Supervisor, registro.AutorizadoPorId);
        Assert.Equal("Artículo mal escaneado", registro.Motivo);
    }

    [SkippableFact]
    public async Task Autorizacion_de_otro_permiso_vencida_o_para_una_linea_inexistente_no_se_consume_indebidamente()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var deOtroPermiso = await caja.AutorizarAsync(CatalogoPermisos.SuspenderVenta, "Otro permiso");
        Assert.Equal(CodigoResultadoVenta.AutorizacionInvalida,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 1, deOtroPermiso))).Resultado);

        // Si la línea no existe, la autorización no se gasta y sirve para el intento correcto.
        var valida = await caja.AutorizarAsync(CatalogoPermisos.EliminarLinea, "Prueba");
        Assert.Equal(CodigoResultadoVenta.LineaNoEncontrada,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 99, valida))).Resultado);
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 1, valida))).Exitosa);

        var vencida = await caja.AutorizarAsync(CatalogoPermisos.EliminarLinea, "Vencerá");
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        caja.Reloj.Avanzar(TimeSpan.FromMinutes(6));
        Assert.Equal(CodigoResultadoVenta.AutorizacionInvalida,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 3, vencida))).Resultado);
    }

    [SkippableFact]
    public async Task Limpiar_pantalla_descarta_el_borrador_sin_gastar_numero_y_queda_en_la_auditoria()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.LimpiarPantalla, "Cliente desistió");
        var limpia = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.LimpiarAsync(caja.Cajero, venta.Id, null, autorizacion));

        Assert.True(limpia.Exitosa, limpia.Mensaje);
        Assert.NotEqual(venta.Id, limpia.Venta!.Id);
        Assert.Equal(venta.NumeroTransaccion, limpia.Venta.NumeroTransaccion); // no se gastó: la nueva muestra el mismo próximo número
        Assert.Empty(limpia.Venta.Lineas);

        // El borrador no era un documento: sale de la mesa de trabajo y lo que tenía queda en la auditoría.
        Assert.False(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.VentasEnProceso.AnyAsync(v => v.Id == venta.Id)));
        // El cajero no dio motivo, así que queda el que escribió el supervisor al autorizar.
        Assert.True(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.Auditoria.AnyAsync(a =>
            a.Accion == "Ventas.PantallaLimpiada" && a.EntidadId == $"B-{venta.Id:000000}" && a.Motivo == "Cliente desistió")));
        Assert.Equal(0, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.Ventas.CountAsync(v => v.CajaId == caja.Escenario.CajaUno)));
    }

    [SkippableFact]
    public async Task La_proxima_factura_configurada_adelanta_la_numeracion_pero_nunca_la_hace_retroceder()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        async Task<string> CobrarConProximaAsync(string proxima)
        {
            await caja.EjecutarAsync<ContextoDatosPos, int>(async contexto =>
            {
                var clave = CgPos.Dominio.Organizacion.CatalogoParametros.ProximaFactura;
                var existente = await contexto.Parametros.SingleOrDefaultAsync(p => p.Clave == clave && p.CajaId == caja.Escenario.CajaUno);
                if (existente is null)
                    contexto.Parametros.Add(CgPos.Dominio.Organizacion.Parametro.Crear(clave, proxima, cajaId: caja.Escenario.CajaUno));
                else
                    existente.CambiarValor(proxima);
                return await contexto.SaveChangesAsync();
            });

            var cobro = await caja.CobrarCincelEnEfectivoAsync();
            Assert.True(cobro.Exitosa, cobro.Mensaje);
            return cobro.Cobro!.NumeroTransaccion;
        }

        var adelantada = await CobrarConProximaAsync("5000");
        Assert.EndsWith("0005000", adelantada);

        // Un valor menor al que ya se usó no repite números: la secuencia sigue desde donde iba.
        Assert.Equal(adelantada[..^7] + "0005001", await CobrarConProximaAsync("10"));
    }

    [SkippableFact]
    public async Task Cliente_registrado_toma_su_comprobante_y_cambiarlo_a_mano_requiere_autorizacion()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();

        var conCliente = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, caja.Catalogo.RncCliente, null));
        Assert.True(conCliente.Exitosa, conCliente.Mensaje);
        Assert.Equal(TipoComprobante.FacturaCreditoFiscal, conCliente.Venta!.TipoComprobante);
        Assert.Equal(caja.Catalogo.Cliente, conCliente.Venta.Cliente!.ClienteId);

        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.FacturaConsumo, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);
        Assert.Equal(CatalogoPermisos.CambiarComprobante, sinPermiso.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.CambiarComprobante, "Cliente pide consumo");
        var cambiado = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.FacturaConsumo, autorizacion));
        Assert.True(cambiado.Exitosa, cambiado.Mensaje);
        Assert.Equal(TipoComprobante.FacturaConsumo, cambiado.Venta!.TipoComprobante);

        // Un cambio imposible se rechaza sin pedir clave de supervisor.
        var sinCliente = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.QuitarClienteAsync(caja.Cajero, venta.Id));
        Assert.Null(sinCliente.Venta!.Cliente);
        var imposible = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.Gubernamental, null));
        Assert.Equal(CodigoResultadoVenta.DocumentoRequerido, imposible.Resultado);
    }

    [SkippableFact]
    public async Task Una_cotizacion_del_central_se_factura_con_sus_precios_congelados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        // El Central cotizó el cincel a 700 (el maestro lo tiene a 850) y con 50 de descuento en la línea.
        const string Numero = "COT000123";
        caja.Central.Cotizaciones[Numero] = new DatosCotizacionParaCaja(Numero, "Constructora del Este", "131234567",
            CgPos.Dominio.Cotizaciones.EstadoCotizacion.Abierta, DateOnly.FromDateTime(DateTime.Today).AddDays(10), Vencida: false, 1350m,
            [new DatosLineaCotizacionParaCaja(caja.Catalogo.CodigoCincel, "Cincel", 2, 700m, 50m)]);

        var venta = await caja.VentaActualAsync();
        var facturada = await caja.EjecutarAsync<IServicioVentas, RespuestaCotizacion>(s =>
            s.FacturarCotizacionAsync(caja.Cajero, venta.Id, Numero, null));
        Assert.True(facturada.Exitosa, facturada.Mensaje);

        // Se respeta lo cotizado, no el precio del maestro: 2 × 700 − 50.
        var linea = Assert.Single(facturada.Venta!.Lineas);
        Assert.Equal((700m, 1350m), (linea.PrecioUnitario, facturada.Venta.Totales.Subtotal));
        Assert.Equal(Numero, facturada.Venta.CotizacionNumero);

        // Ya con artículos, la venta no acepta otra cotización encima: primero se termina o se limpia.
        var repetida = await caja.EjecutarAsync<IServicioVentas, RespuestaCotizacion>(s =>
            s.FacturarCotizacionAsync(caja.Cajero, venta.Id, Numero, null));
        Assert.Equal(CodigoResultadoVenta.DocumentoInvalido, repetida.Resultado);
    }

    [SkippableFact]
    public async Task Una_cotizacion_vencida_necesita_autorizacion_y_una_que_no_existe_se_informa()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();

        // Una que el Central no tiene se distingue de no haber podido preguntar.
        var inexistente = await caja.EjecutarAsync<IServicioVentas, RespuestaCotizacion>(s =>
            s.FacturarCotizacionAsync(caja.Cajero, venta.Id, "COT999999", null));
        Assert.Equal(CodigoResultadoVenta.DocumentoInvalido, inexistente.Resultado);

        const string Vencida = "COT000999";
        caja.Central.Cotizaciones[Vencida] = new DatosCotizacionParaCaja(Vencida, "Constructora del Este", null,
            CgPos.Dominio.Cotizaciones.EstadoCotizacion.Abierta, DateOnly.FromDateTime(DateTime.Today).AddDays(-3), Vencida: true, 700m,
            [new DatosLineaCotizacionParaCaja(caja.Catalogo.CodigoCincel, "Cincel", 1, 700m, 0m)]);

        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaCotizacion>(s =>
            s.FacturarCotizacionAsync(caja.Cajero, venta.Id, Vencida, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);
        Assert.Equal(CatalogoPermisos.FacturarCotizacionVencida, sinPermiso.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.FacturarCotizacionVencida, "El cliente la trajo tarde");
        var conPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaCotizacion>(s =>
            s.FacturarCotizacionAsync(caja.Cajero, venta.Id, Vencida, autorizacion));
        Assert.True(conPermiso.Exitosa, conPermiso.Mensaje);
        Assert.Equal(Vencida, conPermiso.Venta!.CotizacionNumero);
    }

    [SkippableFact]
    public async Task La_factura_de_regimen_especial_va_exenta_de_itbis_y_el_cliente_paga_la_base()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var conCliente = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, caja.Catalogo.RncCliente, null));
        Assert.True(conCliente.Exitosa, conCliente.Mensaje);

        var gravados = conCliente.Venta!.Totales;
        Assert.True(gravados.Impuesto > 0, "El artículo de la prueba lleva ITBIS.");

        // Régimen especial: la DGII manda facturar exento, así que el cliente paga la base y no el precio con ITBIS.
        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.CambiarComprobante, "Zona franca con carné de exención");
        var especial = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.RegimenesEspeciales, autorizacion));
        Assert.True(especial.Exitosa, especial.Mensaje);

        var exentos = especial.Venta!.Totales;
        Assert.True(especial.Venta.ExentaDeImpuesto);
        Assert.Equal(0m, exentos.Impuesto);
        Assert.Equal(exentos.Subtotal, exentos.Total);
        Assert.Equal(gravados.Subtotal, exentos.Total);
        Assert.Equal(0m, exentos.Retencion);

        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaEfectivo, exentos.Total)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);

        // En el e-CF todo va como exento: sin ITBIS y con el total igual a la base.
        var rutaXml = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == cobro.Venta!.Id).Select(d => d.RutaXml).SingleAsync());
        var xml = await File.ReadAllTextAsync(rutaXml);
        var total = exentos.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"<MontoExento>{total}</MontoExento>", xml, StringComparison.Ordinal);
        Assert.Contains($"<MontoTotal>{total}</MontoTotal>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TotalITBIS>", xml, StringComparison.Ordinal);

        // Y el ticket lo dice, que es lo que se lleva el cliente.
        var ticket = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*ticket-{cobro.Venta!.NumeroTransaccion}.txt")));
        Assert.Contains("EXENTA DE ITBIS", ticket, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task La_entidad_del_estado_con_certificacion_factura_sin_itbis()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, caja.Catalogo.RncCliente, null));

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.CambiarComprobante, "Institución del Estado");
        var gubernamental = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.Gubernamental, autorizacion));
        Assert.True(gubernamental.Exitosa, gubernamental.Mensaje);

        // Sin certificación, el gubernamental lleva ITBIS como cualquier otra factura.
        Assert.False(gubernamental.Venta!.ExentaDeImpuesto);
        Assert.True(gubernamental.Venta.Totales.Impuesto > 0);
        var conImpuesto = gubernamental.Venta.Totales;

        // Con la certificación que presenta la entidad, la factura se emite exenta (Norma General 05-19).
        const string Certificacion = "CE-2026-00815";
        var exenta = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.RegistrarCertificacionExencionAsync(caja.Cajero, venta.Id, Certificacion));
        Assert.True(exenta.Exitosa, exenta.Mensaje);
        Assert.True(exenta.Venta!.ExentaDeImpuesto);
        Assert.Equal(Certificacion, exenta.Venta.CertificacionExencion);
        Assert.Equal(0m, exenta.Venta.Totales.Impuesto);
        Assert.Equal(conImpuesto.Subtotal, exenta.Venta.Totales.Total);

        // Quitarla devuelve la factura a su estado con ITBIS.
        var revertida = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.RegistrarCertificacionExencionAsync(caja.Cajero, venta.Id, null));
        Assert.False(revertida.Venta!.ExentaDeImpuesto);
        Assert.Equal(conImpuesto.Total, revertida.Venta.Totales.Total);

        // Y en un comprobante que no es gubernamental no se puede registrar.
        var autorizacionConsumo = await caja.AutorizarAsync(CatalogoPermisos.CambiarComprobante, "Vuelve a consumo");
        var consumo = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.FacturaConsumo, autorizacionConsumo));
        Assert.True(consumo.Exitosa, consumo.Mensaje);

        var rechazada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.RegistrarCertificacionExencionAsync(caja.Cajero, venta.Id, Certificacion));
        Assert.False(rechazada.Exitosa);
        Assert.Contains("gubernamental", rechazada.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task La_retencion_de_la_ley_32_23_solo_aplica_a_lo_gubernamental_y_baja_lo_que_paga_el_cliente()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.CambiarParametroAsync(CgPos.Dominio.Organizacion.CatalogoParametros.PorcentajeRetencionLey3223, "5");

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var conCliente = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, caja.Catalogo.RncCliente, null));
        Assert.True(conCliente.Exitosa, conCliente.Mensaje);

        // Crédito fiscal: sin retención.
        Assert.Equal(0m, conCliente.Venta!.Totales.Retencion);

        // La retención es del comprobante gubernamental: la hace el Estado al pagarle a su proveedor.
        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.CambiarComprobante, "Cliente del Estado");
        var gubernamental = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.CambiarComprobanteAsync(caja.Cajero, venta.Id, TipoComprobante.Gubernamental, autorizacion));
        Assert.True(gubernamental.Exitosa, gubernamental.Mensaje);

        // La retención se calcula sobre el subtotal (ya con descuentos) y se descuenta de lo que paga el cliente.
        var totales = gubernamental.Venta!.Totales;
        Assert.True(totales.Impuesto > 0, "La factura gubernamental lleva ITBIS.");
        var esperada = decimal.Round(totales.Subtotal * 0.05m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(esperada, totales.Retencion);
        Assert.Equal(totales.Total - esperada, totales.TotalAPagar);

        // Con el total completo sobra: la devuelta sale de lo que el cliente no tenía que pagar.
        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaEfectivo, totales.TotalAPagar)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(totales.TotalAPagar, cobro.Venta!.TotalCobrado);
        Assert.Equal(esperada, cobro.Venta.Totales.Retencion);

        // El e-CF lleva el total de la factura y, aparte, lo que el cliente pagó.
        var rutaXml = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == cobro.Venta!.Id).Select(d => d.RutaXml).SingleAsync());
        var xml = await File.ReadAllTextAsync(rutaXml);
        Assert.Contains($"<MontoTotal>{totales.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</MontoTotal>", xml, StringComparison.Ordinal);
        Assert.Contains($"<ValorPagar>{totales.TotalAPagar.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</ValorPagar>", xml, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Documento_no_registrado_pide_nombre_y_el_invalido_se_rechaza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        var cedula = CedulaAleatoriaValida();

        var invalido = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, "12345", null));
        Assert.Equal(CodigoResultadoVenta.DocumentoInvalido, invalido.Resultado);

        var sinNombre = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, cedula, null));
        Assert.Equal(CodigoResultadoVenta.NombreRequerido, sinNombre.Resultado);

        var conNombre = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, cedula, "María Gómez"));
        Assert.True(conNombre.Exitosa, conNombre.Mensaje);
        Assert.Equal(TipoComprobante.FacturaConsumo, conNombre.Venta!.TipoComprobante);
        Assert.Equal(cedula, conNombre.Venta.Cliente!.Documento);
        Assert.Null(conNombre.Venta.Cliente.ClienteId);
    }

    [SkippableFact]
    public async Task Factura_de_consumo_grande_exige_identificacion_segun_el_parametro()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();

        var grande = await caja.AgregarAsync(venta.Id, $"600*{caja.Catalogo.CodigoCemento}"); // 600 × 450 = 270,000
        Assert.True(grande.RequiereIdentificacion);
        Assert.Equal(250_000m, grande.MontoIdentificacion); // el parámetro de la caja de prueba

        var identificada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, CedulaAleatoriaValida(), "Comprador grande"));
        Assert.False(identificada.Venta!.RequiereIdentificacion);
    }

    [SkippableFact]
    public async Task Facturas_en_espera_se_nombran_con_una_referencia_y_se_retoman_intercambiando_la_actual()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var primera = await caja.VentaActualAsync();
        await caja.AgregarAsync(primera.Id, caja.Catalogo.BarrasCincel);

        Assert.Equal(CodigoResultadoVenta.NombreRequerido,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, primera.Id, " "))).Resultado);

        var vacia = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, primera.Id, "Sra. María"));
        Assert.True(vacia.Exitosa, vacia.Mensaje);
        var segunda = vacia.Venta!;
        Assert.NotEqual(primera.Id, segunda.Id);
        Assert.Empty(segunda.Lineas);

        var sinArticulos = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, segunda.Id, "102"));
        Assert.Equal(CodigoResultadoVenta.SinLineas, sinArticulos.Resultado);

        await caja.AgregarAsync(segunda.Id, caja.Catalogo.BarrasCemento);
        var guardada = Assert.Single(await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosVentaEnEspera>>(s => s.ListarEnEsperaAsync(caja.Cajero)));
        Assert.Equal("Sra. María", guardada.Referencia);
        Assert.Equal(1003m, guardada.Total); // 850 + 153 de ITBIS

        // La que está en pantalla tiene artículos: para retomar otra hay que nombrarla, y no con una referencia repetida.
        Assert.Equal(CodigoResultadoVenta.NombreRequerido,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.RetomarAsync(caja.Cajero, guardada.Id, null))).Resultado);
        Assert.Equal(CodigoResultadoVenta.NombreRequerido,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.RetomarAsync(caja.Cajero, guardada.Id, "Sra. María"))).Resultado);

        var retomada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.RetomarAsync(caja.Cajero, guardada.Id, "Camioneta azul"));
        Assert.True(retomada.Exitosa, retomada.Mensaje);
        Assert.Equal(primera.NumeroTransaccion, retomada.Venta!.NumeroTransaccion); // nada se cobró: sigue el mismo próximo número
        Assert.Equal(1003m, retomada.Venta.Totales.Total); // 850 + 153 de ITBIS

        var pendiente = Assert.Single(await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosVentaEnEspera>>(s => s.ListarEnEsperaAsync(caja.Cajero)));
        Assert.Equal("Camioneta azul", pendiente.Referencia);
        Assert.Equal(572.30m, pendiente.Total); // 485 + 87.30 de ITBIS

        var actual = await caja.VentaActualAsync();
        Assert.Equal(retomada.Venta.Id, actual.Id);
    }

    [SkippableFact]
    public async Task La_factura_en_espera_no_se_lleva_un_numero_los_numeros_salen_en_el_orden_en_que_se_cobra()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var primera = await caja.VentaActualAsync();
        await caja.AgregarAsync(primera.Id, caja.Catalogo.BarrasCincel);
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, primera.Id, "Primero"))).Exitosa);

        // El segundo cliente se cobra antes: toma el primer número.
        var cobroSegundo = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobroSegundo.Exitosa, cobroSegundo.Mensaje);

        var guardada = Assert.Single(await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosVentaEnEspera>>(s => s.ListarEnEsperaAsync(caja.Cajero)));
        var retomada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.RetomarAsync(caja.Cajero, guardada.Id, null));
        Assert.True(retomada.Exitosa, retomada.Mensaje);
        var cobroPrimero = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, retomada.Venta!.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(cobroPrimero.Exitosa, cobroPrimero.Mensaje);

        var numeroSegundo = cobroSegundo.Cobro!.NumeroTransaccion;
        var numeroPrimero = cobroPrimero.Cobro!.NumeroTransaccion;
        Assert.Equal(long.Parse(numeroSegundo[^7..]) + 1, long.Parse(numeroPrimero[^7..]));

        // En la tabla de ventas solo está lo cobrado, con Ids correlativos; las tablas de trabajo quedan sin esas ventas.
        var ids = await caja.EjecutarAsync<ContextoDatosPos, List<int>>(contexto => contexto.Ventas.Where(v => v.CajaId == caja.Escenario.CajaUno).OrderBy(v => v.Id).Select(v => v.Id).ToListAsync());
        Assert.Equal(2, ids.Count);
        Assert.Equal(ids[0] + 1, ids[1]);
        Assert.Equal(0, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.VentasGuardadas.CountAsync(v => v.CajaId == caja.Escenario.CajaUno)));
        Assert.Equal(1, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.VentasEnProceso.CountAsync(v => v.CajaId == caja.Escenario.CajaUno))); // la nueva, vacía

        // Cada venta cobrada guarda la foto de su sucursal, su caja y su turno, igual que los del número de factura.
        var origen = await caja.EjecutarAsync<ContextoDatosPos, List<(string, string, string, long, long)>>(async contexto =>
            (await (from venta in contexto.Ventas
                    join sucursal in contexto.Sucursales on venta.SucursalId equals sucursal.Id
                    join turno in contexto.Turnos on venta.TurnoId equals turno.Id
                    where venta.CajaId == caja.Escenario.CajaUno
                    select new { venta.SucursalCodigo, venta.SucursalNombre, venta.CajaCodigo, venta.TurnoNumero, turno.Numero, Nombre = sucursal.Nombre, venta.NumeroTransaccion })
                .ToListAsync())
            .Select(v =>
            {
                Assert.StartsWith(v.SucursalCodigo + v.CajaCodigo, v.NumeroTransaccion);
                Assert.Equal(v.Nombre, v.SucursalNombre);
                return (v.SucursalCodigo, v.SucursalNombre, v.CajaCodigo, v.TurnoNumero, v.Numero);
            })
            .ToList());
        Assert.All(origen, v => Assert.Equal(v.Item5, v.Item4));
    }

    [SkippableFact]
    public async Task Limpiar_con_motivo_y_suspender_requieren_autorizacion_y_quedan_auditados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        // El cajero de la prueba no tiene el permiso: sin autorización no bota la venta, aunque dé el motivo.
        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.LimpiarAsync(caja.Cajero, venta.Id, "Cliente se retiró", null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.LimpiarPantalla, "Cliente se retiró");
        var nueva = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.LimpiarAsync(caja.Cajero, venta.Id, "Cliente se retiró", autorizacion));
        Assert.True(nueva.Exitosa, nueva.Mensaje);
        Assert.NotEqual(venta.Id, nueva.Venta!.Id);

        // El borrador no era un documento: se borra y el motivo que dio el cajero queda en la auditoría.
        Assert.False(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.VentasEnProceso.AnyAsync(v => v.Id == venta.Id)));
        Assert.True(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.Auditoria.AnyAsync(a =>
            a.Accion == "Ventas.PantallaLimpiada" && a.EntidadId == $"B-{venta.Id:000000}" && a.Motivo == "Cliente se retiró" && a.AutorizadoPorId != null)));

        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.SuspenderAsync(caja.Cajero, null, null, null))).Resultado);
        var suspension = await caja.AutorizarAsync(CatalogoPermisos.SuspenderVenta, "Almuerzo");
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.SuspenderAsync(caja.Cajero, null, null, suspension))).Exitosa);

        var cajaCodigo = caja.Cajero.CajaCodigo;
        var registros = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.Auditoria.CountAsync(r => (r.Accion == "Ventas.PantallaLimpiada" && r.EntidadId == $"B-{venta.Id:000000}")
                                               || (r.Accion == "Caja.OperacionesSuspendidas" && r.UsuarioId == caja.Escenario.Cajero)));
        Assert.Equal(2, registros);
    }

    [SkippableFact]
    public async Task La_caja_parada_guarda_el_motivo_y_sube_el_tiempo_al_reanudar()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        // Los motivos bajan del Central como cualquier otro maestro.
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(
            new PaqueteMaestros(MotivosSuspension:
            [
                new MotivoSuspensionCarga(2, "Almuerzo", Programado: true),
                new MotivoSuspensionCarga(9, "Otro", ExigeNota: true),
            ]), "Pruebas"));

        var motivos = await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosMotivoSuspension>>(s => s.ListarMotivosSuspensionAsync());
        Assert.Equal([2, 9], motivos.Select(m => m.Codigo));

        // «Otro» no pasa sin explicación: si no, el reporte no diría nada.
        var sinNota = await caja.AutorizarAsync(CatalogoPermisos.SuspenderVenta, "Sale de la caja");
        Assert.Equal(CodigoResultadoVenta.MotivoRequerido,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.SuspenderAsync(caja.Cajero, 9, null, sinNota))).Resultado);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.SuspenderVenta, "Sale de la caja");
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.SuspenderAsync(caja.Cajero, 2, null, autorizacion))).Exitosa);

        var abierta = await caja.EjecutarAsync<IServicioVentas, DatosSuspension?>(s => s.SuspensionAbiertaAsync(caja.Cajero));
        Assert.NotNull(abierta);
        Assert.Equal("Almuerzo", abierta!.MotivoNombre);

        // El cajero vuelve media hora después: el rato parado se cierra con ese tiempo y sube al Central.
        caja.Reloj.Ahora = caja.Reloj.Ahora.AddMinutes(30);
        await caja.EjecutarAsync<IServicioVentas, bool>(async s => { await s.ReanudarAsync(caja.Cajero); return true; });
        Assert.Null(await caja.EjecutarAsync<IServicioVentas, DatosSuspension?>(s => s.SuspensionAbiertaAsync(caja.Cajero)));

        var mensaje = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.BandejaSalida.AsNoTracking().Where(m => m.TipoMensaje == "Caja.Suspension").Select(m => m.Contenido).SingleAsync());
        var informado = JsonSerializer.Deserialize<DocumentoSuspensionCaja>(mensaje, OpcionesJson.Predeterminadas)!;
        Assert.Equal("Almuerzo", informado.MotivoNombre);
        Assert.True(informado.Programado);
        Assert.False(informado.CerradaPorCierreDeTurno);
        Assert.Equal(30, (informado.ReanudadaEn - informado.SuspendidaEn).TotalMinutes, 0);
    }

    [SkippableFact]
    public async Task Serializado_pide_serial_y_no_acepta_repetidos()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        var codigo = caja.Catalogo.CodigoTaladro;

        var sinSerial = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, venta.Id, codigo, null));
        Assert.Equal(CodigoResultadoVenta.RequiereSerial, sinSerial.Resultado);

        var conSerial = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, venta.Id, codigo, null, "dw-778812"));
        Assert.True(conSerial.Exitosa, conSerial.Mensaje);
        Assert.Equal("DW-778812", conSerial.Venta!.Lineas.Single().Serial);

        var repetido = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, venta.Id, codigo, null, "DW-778812"));
        Assert.Equal(CodigoResultadoVenta.SerialDuplicado, repetido.Resultado);

        // El serial se guarda con la línea y se recupera tras un reinicio.
        await using var reabierta = caja.Reabrir();
        Assert.Equal("DW-778812", (await reabierta.VentaActualAsync()).Lineas.Single().Serial);
    }

    [SkippableFact]
    public async Task Balanza_agrega_el_peso_neto_descontando_la_tara_y_rechaza_articulos_no_pesados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();

        var pesada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarDesdeBalanzaAsync(caja.Cajero, venta.Id, caja.Catalogo.PluCebolla));
        Assert.True(pesada.Exitosa, pesada.Mensaje);

        var linea = pesada.Venta!.Lineas.Single();
        var netoEsperado = BalanzaPrueba.PesoSimulado - EscenarioCatalogo.PesoEmpaqueCebolla; // 1.250 − 0.050
        Assert.Equal(netoEsperado, linea.Cantidad);
        Assert.True(linea.LeidaDeBalanza);
        Assert.Equal(decimal.Round(netoEsperado * 55m, 2), pesada.Venta.Totales.Total);

        var noPesado = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarDesdeBalanzaAsync(caja.Cajero, venta.Id, caja.Catalogo.BarrasCincel));
        Assert.Equal(CodigoResultadoVenta.CantidadInvalida, noPesado.Resultado);
    }

    [SkippableFact]
    public async Task Oferta_cargada_del_maestro_se_aplica_al_escanear_y_gana_la_mejor()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.CargarOfertasAsync(
            new PromocionCarga($"P15{caja.Catalogo.Sufijo}", "Cincel 15 %", TipoPromocion.Porcentaje, 15m,
                EscenarioSeguridad.Inicio.AddDays(-1), EscenarioSeguridad.Inicio.AddDays(10), Articulos: [caja.Catalogo.CodigoCincel]),
            new PromocionCarga($"E79{caja.Catalogo.Sufijo}", "Cincel a 799", TipoPromocion.PrecioEspecial, 799m,
                EscenarioSeguridad.Inicio.AddDays(-1), EscenarioSeguridad.Inicio.AddDays(10), Articulos: [caja.Catalogo.CodigoCincel]));

        var venta = await caja.VentaActualAsync();
        var conOferta = await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var linea = conOferta.Lineas.Single();
        Assert.Equal($"P15{caja.Catalogo.Sufijo}", linea.PromocionCodigo);
        Assert.Equal("-15%", linea.PromocionDescripcion);
        Assert.Equal(722.50m, linea.Importe);
        Assert.Equal(127.50m, conOferta.Totales.Descuento);

        var vigentes = await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosPromocionVigente>>(s => s.ListarPromocionesVigentesAsync(caja.Cajero, caja.Catalogo.ArticuloCincel));
        Assert.Equal(2, vigentes.Count);
    }

    [SkippableFact]
    public async Task Descuento_de_linea_pide_autorizacion_y_respeta_el_tope_del_nivel_que_autoriza()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(
            TopesDescuento: [new TopeDescuentoCarga(Codigos.Siguiente(), 2, 5m, null, ArticuloCodigo: caja.Catalogo.CodigoCemento)]), "Pruebas"));

        var venta = await caja.VentaActualAsync();
        var linea = (await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCemento)).Lineas.Single();

        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AplicarDescuentoLineaAsync(caja.Cajero, venta.Id, linea.NumeroLinea, TipoDescuento.Porcentaje, 10m, "Cliente frecuente", null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);
        Assert.Equal(CatalogoPermisos.DescuentoLinea, sinPermiso.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.DescuentoLinea, "Cliente frecuente");
        var excedido = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AplicarDescuentoLineaAsync(caja.Cajero, venta.Id, linea.NumeroLinea, TipoDescuento.Porcentaje, 10m, "Cliente frecuente", autorizacion));
        Assert.Equal(CodigoResultadoVenta.TopeDescuentoExcedido, excedido.Resultado);

        // La autorización no se gastó: el mismo supervisor aplica un descuento dentro de su tope.
        var aplicado = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AplicarDescuentoLineaAsync(caja.Cajero, venta.Id, linea.NumeroLinea, TipoDescuento.Porcentaje, 5m, "Cliente frecuente", autorizacion));
        Assert.True(aplicado.Exitosa, aplicado.Mensaje);

        var conDescuento = aplicado.Venta!.Lineas.Single();
        Assert.Equal(24.25m, conDescuento.DescuentoManual);
        Assert.Equal("Supervisor Seguridad", conDescuento.DescuentoAutorizadoPorNombre);
        Assert.Equal(460.75m, aplicado.Venta.Totales.Subtotal);

        var registro = await caja.EjecutarAsync<ContextoDatosPos, Dominio.Auditoria.RegistroAuditoria>(contexto =>
            contexto.Auditoria.SingleAsync(r => r.Accion == "Ventas.DescuentoLinea" && r.EntidadId == $"B-{venta.Id:000000}"));
        Assert.Equal(caja.Escenario.Supervisor, registro.AutorizadoPorId);
    }

    [SkippableFact]
    public async Task Descuento_a_la_factura_excluye_ofertas_y_al_desactivar_la_oferta_se_reprorratea()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.CargarOfertasAsync(new PromocionCarga($"F15{caja.Catalogo.Sufijo}", "Cincel 15 %", TipoPromocion.Porcentaje, 15m,
            EscenarioSeguridad.Inicio.AddDays(-1), EscenarioSeguridad.Inicio.AddDays(10), Articulos: [caja.Catalogo.CodigoCincel]));

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCemento);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.DescuentoFactura, "Cliente frecuente");
        var conDescuento = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AplicarDescuentoFacturaAsync(caja.Cajero, venta.Id, TipoDescuento.Monto, 50m, null, "Cliente frecuente", autorizacion));

        Assert.True(conDescuento.Exitosa, conDescuento.Mensaje);
        Assert.Equal([1], conDescuento.LineasExcluidas);
        Assert.Contains("no tomaron el descuento", conDescuento.Mensaje);
        Assert.Equal(50m, conDescuento.Venta!.Lineas.Single(l => l.NumeroLinea == 2).DescuentoFactura);

        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.DesactivarPromocionAsync(caja.Cajero, venta.Id, 1, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);

        var desactivar = await caja.AutorizarAsync(CatalogoPermisos.DesactivarPromocion, "Cliente prefiere descuento");
        var sinOferta = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.DesactivarPromocionAsync(caja.Cajero, venta.Id, 1, desactivar));

        Assert.True(sinOferta.Exitosa, sinOferta.Mensaje);
        var lineas = sinOferta.Venta!.Lineas;
        Assert.Null(lineas.Single(l => l.NumeroLinea == 1).PromocionCodigo);
        Assert.True(lineas.Single(l => l.NumeroLinea == 1).PromocionDesactivada);
        Assert.Equal(50m, lineas.Sum(l => l.DescuentoFactura));
        Assert.Equal(850m + 485m - 50m, sinOferta.Venta.Totales.Subtotal);
    }

    [SkippableFact]
    public async Task Cobrar_por_encima_del_limite_de_compra_se_permite_y_queda_en_la_auditoria()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        // El límite lo pone el cliente: superarlo no bloquea el cobro, pero deja constancia de que se cobró igual.
        var venta = await caja.VentaActualAsync();
        await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EstablecerLimiteCompraAsync(caja.Cajero, venta.Id, 500m));
        var conArticulo = await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        Assert.True(conArticulo.LimiteCompraExcedido);

        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.True(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.Auditoria.AnyAsync(a =>
            a.Accion == "Ventas.LimiteCompraSuperado" && a.EntidadId == cobro.Venta!.NumeroTransaccion)));

        // Dentro del límite no hay nada que anotar.
        var dentro = await caja.VentaActualAsync();
        await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EstablecerLimiteCompraAsync(caja.Cajero, dentro.Id, 5000m));
        await caja.AgregarAsync(dentro.Id, caja.Catalogo.BarrasCincel);
        var cobroDentro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, dentro.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(cobroDentro.Exitosa, cobroDentro.Mensaje);
        Assert.False(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.Auditoria.AnyAsync(a =>
            a.Accion == "Ventas.LimiteCompraSuperado" && a.EntidadId == cobroDentro.Venta!.NumeroTransaccion)));
    }

    [SkippableFact]
    public async Task Cobro_en_efectivo_guarda_pagos_mensaje_para_el_central_y_ticket_y_empieza_otra_venta()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));

        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(97m, cobro.Cobro!.Devuelta); // 1,100 − (850 + 153 de ITBIS)
        Assert.True(cobro.Cobro.Impreso);
        Assert.True(cobro.Cobro.GavetaAbierta);
        Assert.Equal(EstadoVenta.Cobrada, cobro.Venta!.Estado);
        Assert.NotEqual(venta.Id, cobro.NuevaVenta!.Id);
        Assert.Empty(cobro.NuevaVenta.Lineas);

        var mensajes = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.BandejaSalida.CountAsync(m => m.TipoMensaje == "Venta.Cobrada" && m.Referencia == cobro.Venta.NumeroTransaccion));
        Assert.Equal(1, mensajes);

        var ticket = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*ticket-{cobro.Venta.NumeroTransaccion}.txt")));
        Assert.Contains("DEVUELTA", ticket);
        Assert.Contains("97.00", ticket);
        Assert.Contains(cobro.Venta.NumeroTransaccion, ticket);

        var repetido = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.Equal(CodigoResultadoVenta.VentaNoEditable, repetido.Resultado);
    }

    [SkippableFact]
    public async Task Tarjeta_pasa_por_el_terminal_se_aplica_una_vez_y_la_ultima_se_puede_anular()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var primera = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s => s.CobrarConTerminalAsync(caja.Cajero, venta.Id, 1003m));
        Assert.True(primera.Exitosa, primera.Mensaje);

        var anulada = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s => s.AnularUltimaOperacionAsync(caja.Cajero, venta.Id));
        Assert.True(anulada.Exitosa, anulada.Mensaje);

        var conAnulada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaTarjeta, 1003m, OperacionTerminalId: primera.Operacion!.Id)], null));
        Assert.Equal(CodigoResultadoVenta.OperacionTerminalInvalida, conAnulada.Resultado);

        var segunda = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s => s.CobrarConTerminalAsync(caja.Cajero, venta.Id, 1003m));
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id,
                [new SolicitudPago(caja.Catalogo.FormaTarjeta, 1003m, TipoTarjetaId: caja.Catalogo.TipoTarjeta, OperacionTerminalId: segunda.Operacion!.Id)], null));

        Assert.True(cobrada.Exitosa, cobrada.Mensaje);
        var pago = Assert.Single(cobrada.Venta!.Pagos!);
        Assert.Equal(segunda.Operacion!.Aprobacion, pago.Referencia);
        Assert.Equal("4242", pago.UltimosDigitos);
        Assert.False(cobrada.Cobro!.GavetaAbierta); // la tarjeta no abre la gaveta
    }

    [SkippableFact]
    public async Task Aprobacion_manual_de_tarjeta_requiere_autorizacion_y_queda_para_conciliar()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        SolicitudPago[] pagos = [new SolicitudPago(caja.Catalogo.FormaTarjeta, 1003m, Referencia: "MAN-778", UltimosDigitos: "1111", AprobacionManual: true)];

        var sinPermiso = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);
        Assert.Equal(CatalogoPermisos.AprobacionManualTarjeta, sinPermiso.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AprobacionManualTarjeta, "Pasarela sin conexión");
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, autorizacion));
        Assert.True(cobrada.Exitosa, cobrada.Mensaje);

        var paraConciliar = await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto =>
            contexto.Set<PagoVenta>().Where(p => p.VentaId == cobrada.Venta!.Id).Select(p => p.ParaConciliar).SingleAsync());
        Assert.True(paraConciliar);
    }

    [SkippableFact]
    public async Task Dolares_se_cobran_a_la_tasa_del_dia_con_devuelta_en_pesos()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(
            FormasPago: [new FormaPagoCarga($"USD{caja.Catalogo.Sufijo}", "Dólares", TipoFormaPago.MonedaExtranjera, 5, "USD")],
            TasasCambio: [new TasaCambioCarga("USD", 60.25m, EscenarioSeguridad.Inicio.AddDays(-1))]), "Pruebas"));
        var formaDolares = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.FormasPago.Where(f => f.Codigo == $"USD{caja.Catalogo.Sufijo}").Select(f => f.Id).SingleAsync());

        var catalogo = await caja.EjecutarAsync<IConsultaCatalogoCobro, DatosCatalogoCobro>(s => s.ObtenerAsync());
        Assert.Contains(catalogo.Tasas!, t => t.Moneda == "USD" && t.Tasa == 60.25m);

        var venta = await caja.VentaActualAsync();
        // La venta toma la moneda local configurada (General.MonedaLocal) con su símbolo del maestro.
        Assert.Equal("DOP", venta.Moneda);
        Assert.Equal("RD$", venta.SimboloMoneda);
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(formaDolares, 20m)], null));

        Assert.True(cobrada.Exitosa, cobrada.Mensaje);
        var pago = Assert.Single(cobrada.Venta!.Pagos!);
        Assert.Equal(1205m, pago.MontoAplicado);
        Assert.Equal(202m, cobrada.Cobro!.Devuelta); // 1,205 − 1,003
    }

    [SkippableFact]
    public async Task Cobro_emite_el_e_cf_firmado_con_secuencia_consecutiva_y_lo_deja_pendiente_de_sincronizar()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var primera = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(primera.Exitosa, primera.Mensaje);
        var comprobante = primera.Venta!.Comprobante!;
        var encfPrimera = $"E32{caja.DesdeSecuencia:0000000000}";
        Assert.Equal(encfPrimera, comprobante.Encf);
        Assert.Equal(EstadoDocumentoElectronico.PendienteSincronizar, comprobante.Estado);
        Assert.Equal(6, comprobante.CodigoSeguridad.Length);
        Assert.Contains("ConsultaTimbreFC", comprobante.UrlTimbre);

        var documento = await caja.EjecutarAsync<ContextoDatosPos, DocumentoElectronico>(contexto =>
            contexto.DocumentosElectronicos.Include(d => d.Historial).SingleAsync(d => d.VentaId == primera.Venta.Id));
        Assert.Equal(2, documento.Historial.Count); // Emitido → Pendiente por sincronizar
        Assert.StartsWith(Path.Combine(baseDatos.CarpetaEcf, "Pendientes"), documento.RutaXml);

        var xml = await File.ReadAllTextAsync(documento.RutaXml);
        Assert.True(CgPos.ECF.Firma.VerificadorFirmaEcf.Verificar(xml).EsValida);
        Assert.Contains($"<eNCF>{encfPrimera}</eNCF>", xml);
        Assert.Equal(documento.HashXml, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(xml))));

        var mensaje = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.BandejaSalida.Where(m => m.Referencia == primera.Venta.NumeroTransaccion).Select(m => m.Contenido).SingleAsync());
        Assert.Contains(encfPrimera, mensaje);

        var ticket = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*ticket-{primera.Venta.NumeroTransaccion}.txt")));
        Assert.Contains($"e-NCF: {encfPrimera}", ticket);
        Assert.Contains($"Código de seguridad: {comprobante.CodigoSeguridad}", ticket);

        var segunda = await caja.CobrarCincelEnEfectivoAsync();
        Assert.Equal($"E32{caja.DesdeSecuencia + 1:0000000000}", segunda.Venta!.Comprobante!.Encf);
    }

    [SkippableFact]
    public async Task Sin_certificado_cargado_no_se_cobra_ni_se_consume_la_secuencia()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, cargarCertificado: false);

        var rechazo = await caja.CobrarCincelEnEfectivoAsync();

        Assert.Equal(CodigoResultadoVenta.CertificadoNoCargado, rechazo.Resultado);
        Assert.Equal(EstadoVenta.EnCurso, rechazo.Venta!.Estado);
        var ultimo = await caja.EjecutarAsync<ContextoDatosPos, long>(contexto =>
            contexto.SecuenciasEcf.Where(s => s.CajaId == caja.Escenario.CajaUno && s.TipoComprobante == TipoComprobante.FacturaConsumo).Select(s => s.Ultimo).SingleAsync());
        Assert.Equal(caja.DesdeSecuencia - 1, ultimo);

        var estado = await caja.EjecutarAsync<IServicioEcf, DatosEstadoEcf>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.False(estado.CertificadoCargado);
        Assert.Contains(estado.Alertas, a => a.Contains("PIN del certificado"));

        var incorrecto = await caja.EjecutarAsync<IServicioEcf, RespuestaCertificado>(s => s.CargarCertificadoAsync(caja.Cajero, "PIN-malo"));
        Assert.False(incorrecto.Correcto);
        var correcto = await caja.EjecutarAsync<IServicioEcf, RespuestaCertificado>(s => s.CargarCertificadoAsync(caja.Cajero, BaseDatosPruebas.PinCertificado));
        Assert.True(correcto.Correcto, correcto.Mensaje);
    }

    [SkippableFact]
    public async Task Secuencia_agotada_bloquea_el_cobro_y_el_estado_lo_alerta()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, hastaSecuenciaConsumo: 1);

        Assert.True((await caja.CobrarCincelEnEfectivoAsync()).Exitosa);
        var agotada = await caja.CobrarCincelEnEfectivoAsync();

        Assert.Equal(CodigoResultadoVenta.ComprobanteNoDisponible, agotada.Resultado);
        var estado = await caja.EjecutarAsync<IServicioEcf, DatosEstadoEcf>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.Contains(estado.Alertas, a => a.Contains("E32") && a.Contains("No hay secuencia"));
    }

    [SkippableFact]
    public async Task Cierre_ciego_cuadra_el_efectivo_con_devuelta_y_retiros_e_imprime_el_reporte()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var total = cobro.Venta!.TotalCobrado!.Value;

        // El cajero no tiene permiso de retiro y no puede retirar más de lo que hay en la gaveta.
        var excesivo = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RetirarEfectivoAsync(caja.Cajero, total + 1m, null, null));
        Assert.Equal(CodigoResultadoCaja.EfectivoInsuficiente, excesivo.Resultado);
        var sinPermiso = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RetirarEfectivoAsync(caja.Cajero, 100m, "Remesa a bóveda", null));
        Assert.Equal(CodigoResultadoCaja.RequiereAutorizacion, sinPermiso.Resultado);

        // Otras cajas de prueba comparten la base y numeran sus turnos igual: se cuentan solo los mensajes de esta prueba.
        var mensajesPrevios = await caja.EjecutarAsync<ContextoDatosPos, List<Guid>>(contexto => contexto.BandejaSalida.Select(m => m.Id).ToListAsync());

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.RetiroEfectivo, "Remesa a bóveda");
        var retiro = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RetirarEfectivoAsync(caja.Cajero, 100m, "Remesa a bóveda", autorizacion));
        Assert.True(retiro.Exitosa, retiro.Mensaje);
        Assert.Equal(1, retiro.Movimiento!.Numero);

        // La cajera no ve lo esperado: el cuadre se lo lleva el supervisor.
        var resumen = (await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.ObtenerResumenAsync(caja.Cajero))).Resumen!;
        Assert.False(resumen.MuestraEsperado);
        Assert.All(resumen.FormasPago, forma => Assert.Null(forma.Esperado));
        Assert.Empty(resumen.Bloqueos);

        // Cierra sin declarar nada: el cierre informa lo esperado, que es lo cobrado menos la devuelta menos el retiro.
        var efectivoEsperado = total - 100m;
        var respuesta = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero, null));

        Assert.True(respuesta.Exitosa, respuesta.Mensaje);
        var cierre = respuesta.Cierre!;
        var efectivo = cierre.FormasPago.Single(f => f.FormaPagoId == caja.Catalogo.FormaEfectivo);
        Assert.Equal(efectivoEsperado, efectivo.Esperado);
        Assert.Equal(1, cierre.CantidadVentas);
        Assert.Equal(100m, cierre.TotalRetiros);
        Assert.Single(cierre.Movimientos);

        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.Null(estado.TurnoAbierto);

        // Los mensajes del turno se identifican por su número en la caja, nunca por Id.
        var turnoNumero = cierre.TurnoNumero.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var retiroReferencia = $"{cierre.TurnoNumero}-{retiro.Movimiento.Tipo}-{retiro.Movimiento.Numero}";
        var mensajes = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.BandejaSalida.CountAsync(m => !mensajesPrevios.Contains(m.Id) &&
            ((m.TipoMensaje == "Caja.TurnoCerrado" && m.Referencia == turnoNumero) || (m.TipoMensaje == "Caja.RetiroEfectivo" && m.Referencia == retiroReferencia))));
        Assert.Equal(2, mensajes);

        var reportes = Directory.GetFiles(baseDatos.CarpetaImpresiones, "*cierre-*.txt").Select(File.ReadAllText);
        Assert.Contains(reportes, texto => texto.Contains("TOTAL ESPERADO RD$") && texto.Contains("ESPERADO POR FORMA DE PAGO"));
    }

    [SkippableFact]
    public async Task No_se_cierra_el_turno_con_facturas_en_espera_y_se_listan()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var espera = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, venta.Id, "Sra. María"));
        Assert.True(espera.Exitosa, espera.Mensaje);

        var cierre = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero, null));

        Assert.Equal(CodigoResultadoCaja.CierreBloqueado, cierre.Resultado);
        Assert.Contains(cierre.Bloqueos!, bloqueo => bloqueo.Contains("Sra. María"));
        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.NotNull(estado.TurnoAbierto);
    }

    [SkippableFact]
    public async Task Con_turno_de_un_dia_anterior_no_se_vende_ni_cobra_hasta_limpiar_y_cerrar()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var espera = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, venta.Id, "Sra. María"));
        Assert.True(espera.Exitosa, espera.Mensaje);
        await caja.TurnoDeAyerAsync();

        // La pantalla recibe el aviso fijo y la caja ya no agrega artículos.
        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.NotNull(estado.BloqueoDiaAnterior);
        var nueva = await caja.VentaActualAsync();
        var agregar = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(caja.Cajero, nueva.Id, caja.Catalogo.BarrasCincel, null));
        Assert.Equal(CodigoResultadoVenta.TurnoDiaAnterior, agregar.Resultado);

        // El cierre avisa que la factura en espera hay que limpiarla, porque ya no se puede cobrar.
        var cierre = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero, null));
        Assert.Equal(CodigoResultadoCaja.CierreBloqueado, cierre.Resultado);
        Assert.Contains(cierre.Bloqueos!, bloqueo => bloqueo.Contains("Sra. María") && bloqueo.Contains("límpielas"));

        // Se retoma, no se deja cobrar, se limpia y entonces sí cierra.
        var guardada = Assert.Single(await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosVentaEnEspera>>(s => s.ListarEnEsperaAsync(caja.Cajero)));
        var retomada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.RetomarAsync(caja.Cajero, guardada.Id, null));
        Assert.True(retomada.Exitosa, retomada.Mensaje);
        var retomadaVenta = retomada.Venta!;
        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, retomadaVenta.Id,
            [new SolicitudPago(caja.Catalogo.FormaEfectivo, retomadaVenta.Totales.TotalAPagar)], null));
        Assert.Equal(CodigoResultadoVenta.TurnoDiaAnterior, cobro.Resultado);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.LimpiarPantalla, "Turno de ayer");
        var limpia = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.LimpiarAsync(caja.Cajero, retomadaVenta.Id, null, autorizacion));
        Assert.True(limpia.Exitosa, limpia.Mensaje);

        var cerrado = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero, null));
        Assert.True(cerrado.Exitosa, cerrado.Mensaje);
    }

    [SkippableFact]
    public async Task Con_el_parametro_apagado_el_turno_de_un_dia_anterior_sigue_vendiendo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.TurnoDeAyerAsync();
        await caja.CambiarParametroAsync(CgPos.Pos.Aplicacion.Organizacion.ClavesParametros.BloquearVentaTurnoDiaAnterior, "false");

        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.Null(estado.BloqueoDiaAnterior);
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
    }

    [SkippableFact]
    public async Task Relevo_con_autorizacion_pasa_el_turno_y_el_cierre_queda_firme()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var sinAutorizacion = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RelevarAsync(caja.CajeroDos, null));
        Assert.Equal(CodigoResultadoCaja.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.RelevoCajero, sinAutorizacion.PermisoRequerido);

        // El primer cajero deja una venta a medias: es del turno, así que el que releva la encuentra en su pantalla.
        var enCurso = await caja.VentaActualAsync();
        await caja.AgregarAsync(enCurso.Id, caja.Catalogo.BarrasCincel);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.RelevoCajero, "Almuerzo", caja.CajeroDos);
        var relevo = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RelevarAsync(caja.CajeroDos, autorizacion));
        Assert.True(relevo.Exitosa, relevo.Mensaje);
        Assert.Equal(caja.Escenario.CajeroDos, relevo.Turno!.UsuarioActualId);
        Assert.Equal(caja.Escenario.Cajero, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.Turnos.AsNoTracking().Where(t => t.Id == relevo.Turno!.Id).Select(t => t.UsuarioAperturaId).SingleAsync())); // quien abrió no cambia

        var retomada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.ObtenerActualAsync(caja.CajeroDos));
        Assert.True(retomada.Exitosa, retomada.Mensaje);
        Assert.Equal(enCurso.Id, retomada.Venta!.Id);
        Assert.Single(retomada.Venta.Lineas);

        var cajeroOriginal = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.ObtenerActualAsync(caja.Cajero));
        Assert.Equal(CodigoResultadoVenta.TurnoDeOtroUsuario, cajeroOriginal.Resultado);

        // La cobra el que relevó, y la factura queda a su nombre aunque la haya empezado el otro.
        var totalDeLaVenta = retomada.Venta.Totales.TotalAPagar;
        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.CajeroDos, enCurso.Id,
            [new SolicitudPago(caja.Catalogo.FormaEfectivo, totalDeLaVenta)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var cobrada = await caja.EjecutarAsync<ContextoDatosPos, VentaCobrada>(contexto =>
            contexto.Ventas.AsNoTracking().SingleAsync(v => v.NumeroTransaccion == cobro.Cobro!.NumeroTransaccion));
        Assert.Equal(caja.Escenario.CajeroDos, cobrada.UsuarioId);
        Assert.Equal(caja.Escenario.CajeroDos, cobrada.CobradaPorId);

        // El turno cierra sin declarar: lo cobrado queda como esperado y el supervisor lo cuadra en el Central.
        var cierre = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.CajeroDos, null));
        Assert.True(cierre.Exitosa, cierre.Mensaje);
        var cierreId = cierre.Cierre!.Id;
        Assert.Equal(totalDeLaVenta, cierre.Cierre.TotalEsperado);

        // El cierre es definitivo: la caja no lo puede deshacer. Una corrección posterior se hace en el Central.
        var cerrado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.CajeroDos));
        Assert.Null(cerrado.TurnoAbierto);

        var cierres = await caja.EjecutarAsync<IServicioCaja, IReadOnlyList<DatosCierre>>(s => s.ListarCierresAsync(caja.CajeroDos));
        Assert.Equal(cierreId, Assert.Single(cierres).Id);
    }

    [SkippableFact]
    public async Task La_nota_de_credito_interna_ajusta_la_factura_sin_comprobante_y_no_sirve_como_pago()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var factura = cobro.Venta!;

        // La factura sube al Central: toda nota de crédito se emite contra la que él tiene registrada.
        caja.PublicarFacturaAsync(factura);
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        var linea = Assert.Single(buscada.Factura!.Lineas);

        // No pide cliente, pero sí la autorización de un supervisor, con su propio permiso.
        var solicitud = new SolicitudDevolucion(factura.NumeroTransaccion, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], null, null,
            caja.Catalogo.CodigoMotivoDevolucion, "Error de digitación", null, Interna: true);
        var sinAutorizacion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero, solicitud));
        Assert.Equal(CodigoResultadoDevolucion.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.AutorizarNotaCreditoInterna, sinAutorizacion.PermisoRequerido);

        // Tampoco devuelve dinero: es solo un ajuste.
        var conReembolso = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s =>
            s.RegistrarAsync(caja.Cajero, solicitud with { Reembolso = CgPos.Dominio.Devoluciones.TipoReembolso.Efectivo }));
        Assert.Contains("no devuelve dinero", conReembolso.Mensaje);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarNotaCreditoInterna, "Se facturó de más");
        var interna = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s =>
            s.RegistrarAsync(caja.Cajero, solicitud with { AutorizacionId = autorizacion }));
        Assert.True(interna.Exitosa, interna.Mensaje);

        var nota = interna.NotaCredito!;
        Assert.True(nota.EsInterna);
        Assert.Null(nota.Comprobante); // sin e-NCF: no va a la DGII ni al 607
        Assert.Equal(0m, nota.Saldo);
        Assert.Equal("Supervisor Seguridad", nota.AutorizadoPorNombre);
        Assert.Empty(await caja.EjecutarAsync<ContextoDatosPos, List<int>>(contexto =>
            contexto.DocumentosElectronicos.Where(d => d.VentaId == nota.Id && d.TipoOrigen == OrigenComprobante.Devolucion)
                .Select(d => d.Id).ToListAsync()));

        // No se usa como forma de pago en otra venta.
        var consulta = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaSaldoNotaCredito>(s => s.ConsultarNotaCreditoAsync(caja.Cajero, nota.Numero));
        Assert.Contains("no se usa como forma de pago", consulta.Mensaje);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var conNotaInterna = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, 10m, nota.Numero)], null));
        Assert.Equal(CodigoResultadoVenta.PagoInvalido, conNotaInterna.Resultado);
        Assert.Contains("es interna", conNotaInterna.Mensaje);

        // La factura queda ajustada: lo devuelto por la nota interna no se puede devolver otra vez.
        // La factura sube al Central: toda nota de crédito se emite contra la que él tiene registrada.
        caja.PublicarFacturaAsync(factura);
        var otraVez = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        Assert.Equal(CodigoResultadoDevolucion.TodoDevuelto, otraVez.Resultado);
    }

    [SkippableFact]
    public async Task Una_factura_de_esta_misma_caja_tambien_se_devuelve_contra_la_que_tiene_el_Central()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var factura = cobro.Venta!;

        // Todavía no ha subido: aunque la venta esté en esta caja, no se le puede hacer la nota de crédito.
        var sinSubir = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        Assert.Equal(CodigoResultadoDevolucion.FacturaNoEncontrada, sinSubir.Resultado);
        Assert.Contains("no está registrada en el Central", sinSubir.Mensaje);

        // Ya sincronizada, el Central la entrega y la caja la reconoce como suya.
        caja.PublicarFacturaAsync(factura);
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        Assert.True(buscada.Exitosa, buscada.Mensaje);
        Assert.Equal(factura.Id, buscada.Factura!.VentaId);
        Assert.False(buscada.Factura.Origen!.DelCentral);

        // Y la nota se emite pasando por el Central: se le reservan las líneas igual que a una factura de otra tienda.
        var linea = Assert.Single(buscada.Factura.Lineas);
        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Artículo defectuoso");
        var emitida = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero,
            new SolicitudDevolucion(factura.NumeroTransaccion, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], "401007551", "Cliente Devolución",
                caja.Catalogo.CodigoMotivoDevolucion, null, autorizacion)));
        Assert.True(emitida.Exitosa, emitida.Mensaje);
        Assert.Equal(factura.Id, emitida.NotaCredito!.VentaOrigenId);
        Assert.Equal(factura.NumeroTransaccion, caja.Central.ReservasFactura[^1].FacturaNumero);

        // El Central todavía no sabe de esa nota (aún no ha subido), pero la caja sí: no la deja devolver dos veces.
        var otraVez = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        Assert.Equal(CodigoResultadoDevolucion.TodoDevuelto, otraVez.Resultado);
    }

    [SkippableFact]
    public async Task Una_factura_de_otra_tienda_se_devuelve_consultandola_al_Central_y_su_copia_no_queda_guardada()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        // Una factura que esta caja nunca vendió: solo existe en el Central, con 2 de 5 ya devueltas en otra tienda.
        const string Numero = "02-03-000000123";
        const string EncfFactura = "E320000099001";
        caja.Central.Facturas[Numero] = FacturaDelCentral(caja, Numero, EncfFactura, cantidad: 5m, devuelta: 2m);
        caja.Central.Facturas[EncfFactura] = caja.Central.Facturas[Numero];

        // Sin red no se emite ninguna nota de crédito, y se dice con esas palabras en vez de dejar esperando al cajero.
        caja.Central.Responde = false;
        var sinRed = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, Numero));
        Assert.Equal(CodigoResultadoDevolucion.SinConexionCentral, sinRed.Resultado);
        Assert.Contains("sin conexión no se pueden emitir", sinRed.Mensaje);

        caja.Central.Responde = true;

        // Una que no existe en ninguna tienda se distingue de no haber podido preguntar.
        var inventada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, "99-99-000000001"));
        Assert.Equal(CodigoResultadoDevolucion.FacturaNoEncontrada, inventada.Resultado);

        // El Central la entrega con lo ya devuelto en otra tienda descontado: quedan 3 de 5.
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, EncfFactura));
        Assert.True(buscada.Exitosa, buscada.Mensaje);
        var factura = buscada.Factura!;
        Assert.Null(factura.VentaId);
        Assert.Equal(new DatosOrigenFactura(true, "02", "03"), factura.Origen);
        var linea = Assert.Single(factura.Lineas);
        Assert.Equal((5m, 2m, 3m), (linea.CantidadVendida, linea.CantidadDevuelta, linea.CantidadDisponible));

        // No se devuelve más de lo que queda, aunque la factura diga 5.
        var solicitud = new SolicitudDevolucion(Numero, [new SolicitudLineaDevolucion(linea.NumeroLinea, 4m)], null, null,
            caja.Catalogo.CodigoMotivoDevolucion, null, null);
        var deMas = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero, solicitud));
        Assert.Equal(CodigoResultadoDevolucion.DevolucionInvalida, deMas.Resultado);
        Assert.Contains("solo quedan 3", deMas.Mensaje);

        // Si otra caja se adelantó y el Central no reserva las líneas, no se emite nada.
        caja.Central.RechazaReservaDe = Numero;
        var solicitudUna = solicitud with
        {
            Lineas = [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)],
            AutorizacionId = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Defectuoso"),
        };
        var ocupada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero, solicitudUna));
        Assert.Equal(CodigoResultadoDevolucion.DevolucionInvalida, ocupada.Resultado);
        Assert.Contains("otra caja", ocupada.Mensaje);
        caja.Central.RechazaReservaDe = null;

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Artículo defectuoso");
        var emitida = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero,
            solicitud with { Lineas = [new SolicitudLineaDevolucion(linea.NumeroLinea, 3m)], AutorizacionId = autorizacion }));
        Assert.True(emitida.Exitosa, emitida.Mensaje);

        var nota = emitida.NotaCredito!;
        Assert.Equal(Numero, nota.VentaOrigenNumero);
        Assert.Null(nota.VentaOrigenId); // no hay venta local a la que apuntar
        Assert.Equal(EncfFactura, nota.EncfOrigen);
        Assert.True(nota.EsTotal); // con lo que ya se había devuelto, la factura queda completa

        // La nota sale con el rango de e-NCF y el número de ESTA caja, no de la que vendió.
        Assert.StartsWith("E34", nota.Comprobante!.Encf, StringComparison.Ordinal);
        Assert.Contains(caja.Escenario.CodigoCajaUno, nota.Numero, StringComparison.Ordinal);

        // El Central retuvo las líneas antes de emitir.
        Assert.Equal((Numero, 3m), (caja.Central.ReservasFactura[^1].FacturaNumero, caja.Central.ReservasFactura[^1].Lineas[linea.NumeroLinea]));

        // Y la copia temporal de la factura se borró: no queda rastro de una factura de otra tienda en esta caja.
        Assert.DoesNotContain(Numero, await caja.EjecutarAsync<ContextoDatosPos, List<string>>(contexto =>
            contexto.FacturasConsultadas.Select(f => f.Numero).ToListAsync()));
    }

    /// <summary>Factura que el Central de prueba entrega como si la hubiera vendido la caja 03 de la sucursal 02.</summary>
    private static DatosFacturaParaCaja FacturaDelCentral(CajaEnPruebas caja, string numero, string encf, decimal cantidad, decimal devuelta)
    {
        const decimal Precio = 850m;
        var importe = cantidad * Precio;
        return new DatosFacturaParaCaja(numero, encf, "02", "03", false, TipoComprobante.FacturaConsumo,
            DateOnly.FromDateTime(caja.Reloj.Ahora.Date), caja.Reloj.Ahora.AddDays(-1), "401007551", "Cliente de Otra Tienda", "DOP", importe,
            [new DatosLineaFacturaParaCaja(1, caja.Catalogo.CodigoCincel, caja.Catalogo.BarrasCincel, "Cincel de punta", TipoArticulo.Normal, "UND", 0,
                18m, cantidad, Precio, 0m, importe - decimal.Round(importe / 1.18m, 2, MidpointRounding.AwayFromZero), importe, null, devuelta)]);
    }

    [SkippableFact]
    public async Task Devolucion_emite_nota_de_credito_E34_que_se_consume_por_partes_en_otra_venta()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var factura = cobro.Venta!;
        var total = factura.TotalCobrado!.Value;

        // La factura sube al Central: toda nota de crédito se emite contra la que él tiene registrada.
        caja.PublicarFacturaAsync(factura);
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        Assert.True(buscada.Exitosa, buscada.Mensaje);
        var linea = Assert.Single(buscada.Factura!.Lineas);
        Assert.Equal(1m, linea.CantidadDisponible);
        Assert.False(buscada.Factura.RetieneImpuesto);

        // La factura no tiene cliente: se pide el RNC; la devolución la autoriza el encargado.
        var solicitud = new SolicitudDevolucion(factura.NumeroTransaccion, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], "401007551", "Cliente Devolución",
            caja.Catalogo.CodigoMotivoDevolucion, null, null);
        var sinAutorizacion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero, solicitud));
        Assert.Equal(CodigoResultadoDevolucion.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.AutorizarDevolucion, sinAutorizacion.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Artículo defectuoso");
        var devolucion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero, solicitud with { AutorizacionId = autorizacion }));
        Assert.True(devolucion.Exitosa, devolucion.Mensaje);
        var nota = devolucion.NotaCredito!;
        Assert.Equal(total, nota.Total);
        Assert.True(nota.EsTotal);
        Assert.Equal("Supervisor Seguridad", nota.AutorizadoPorNombre);
        var encfNota = nota.Comprobante!.Encf;
        Assert.StartsWith("E34", encfNota);

        var rutaXml = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.DocumentosElectronicos.Where(d => d.VentaId == nota.Id && d.TipoOrigen == OrigenComprobante.Devolucion)
                .Select(d => d.RutaXml).SingleAsync());
        var xml = await File.ReadAllTextAsync(rutaXml);
        Assert.Contains($"<NCFModificado>{factura.Comprobante!.Encf}</NCFModificado>", xml);
        Assert.Contains("<CodigoModificacion>1</CodigoModificacion>", xml);

        // No se devuelve dos veces lo mismo (RF-42).
        var otraVez = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.Comprobante.Encf));
        Assert.Equal(CodigoResultadoDevolucion.TodoDevuelto, otraVez.Resultado);

        // Se consume la mitad en otra venta y queda saldo (RF-43).
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var mitad = decimal.Round(total / 2, 2);
        var segundoCobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, mitad, encfNota), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(segundoCobro.Exitosa, segundoCobro.Mensaje);

        var saldo = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaSaldoNotaCredito>(s => s.ConsultarNotaCreditoAsync(caja.Cajero, encfNota));
        Assert.True(saldo.Exitosa, saldo.Mensaje);
        Assert.Equal(total - mitad, saldo.NotaCredito!.Saldo);
        Assert.NotEmpty(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*saldo-nc-{encfNota}-*.txt"));

        // Más de lo disponible se rechaza.
        var tercera = await caja.VentaActualAsync();
        await caja.AgregarAsync(tercera.Id, caja.Catalogo.BarrasCincel);
        var excedido = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, tercera.Id,
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, total, encfNota)], null));
        Assert.Equal(CodigoResultadoVenta.PagoInvalido, excedido.Resultado);
        Assert.Contains("disponibles", excedido.Mensaje);
    }

    [SkippableFact]
    public async Task Nota_de_credito_de_otra_sucursal_se_reserva_en_el_Central_y_se_libera_si_el_cobro_no_ocurre()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        const string encf = "E340000000777";
        const string numeroNota = "990120000777";
        caja.Central.NotasCredito[encf] = new DatosNotaCreditoParaCaja(numeroNota, encf, "99", "01", "401007551", "Cliente de Otra Sucursal", "DOP", 500m, 500m,
            new DateOnly(2027, 12, 31), CgPos.Dominio.Devoluciones.EstadoNotaCreditoCentral.Vigente);

        // Lo que no está en esta caja ni en el Central se rechaza.
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var desconocida = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, 50m, "E340000000001")], null));
        Assert.Equal(CodigoResultadoVenta.PagoInvalido, desconocida.Resultado);
        Assert.Contains("no existe", desconocida.Mensaje);

        // Si el cobro no se completa, el Central recupera el saldo que retuvo.
        var insuficiente = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, 1m, encf)], null));
        Assert.False(insuficiente.Exitosa);
        Assert.Single(caja.Central.Reservas);
        Assert.Single(caja.Central.ReservasLiberadas);

        // Cobro con la nota de otra sucursal: el consumo se le informa al Central por la bandeja de salida (RF-43).
        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, 50m, encf), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(2, caja.Central.Reservas.Count);
        Assert.Single(caja.Central.ReservasLiberadas);

        var mensaje = await caja.EjecutarAsync<ContextoDatosPos, MensajeSalida>(contexto =>
            contexto.BandejaSalida.AsNoTracking().SingleAsync(m => m.TipoMensaje == "NotaCredito.Consumida" && m.Referencia == numeroNota));
        Assert.Contains(encf, mensaje.Contenido);

        // La reserva se hace mientras la venta es un borrador, así que va con su identificación; el consumo lleva el número de
        // la factura y, aparte, esa identificación, para que el Central cierre la reserva que corresponde.
        Assert.All(caja.Central.Reservas, r => Assert.Equal((numeroNota, $"B-{venta.Id:000000}"), (r.NotaCreditoNumero, r.VentaNumero)));
        var consumo = System.Text.Json.JsonSerializer.Deserialize<DocumentoConsumoNotaCredito>(mensaje.Contenido, OpcionesJson.Predeterminadas)!;
        Assert.Equal(cobro.Venta!.NumeroTransaccion, consumo.VentaNumero);
        Assert.Equal($"B-{venta.Id:000000}", consumo.VentaReserva);
    }

    [SkippableFact]
    public async Task Miembro_de_fidelidad_acumula_por_reglas_y_nivel_canjea_con_autorizacion_y_la_devolucion_reversa()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        // Inscripción desde la caja sin interrumpir la venta (RF-237).
        var nueva = EscenarioCatalogo.CedulaAleatoriaValida();
        var inscripcion = await caja.EjecutarAsync<IServicioFidelidad, RespuestaFidelidad>(s =>
            s.InscribirAsync(caja.Cajero, new SolicitudInscripcionFidelidad(nueva, "Cliente Nuevo", "809-555-0000", "nuevo@correo.do")));
        Assert.True(inscripcion.Exitosa, inscripcion.Mensaje);
        Assert.Equal(0, inscripcion.Miembro!.SaldoDisponible);
        Assert.True(inscripcion.Miembro.PendienteDeConfirmar);
        var repetida = await caja.EjecutarAsync<IServicioFidelidad, RespuestaFidelidad>(s =>
            s.InscribirAsync(caja.Cajero, new SolicitudInscripcionFidelidad(nueva, "Otro nombre", null, null)));
        Assert.Equal(CodigoResultadoFidelidad.YaInscrito, repetida.Resultado);

        // La cédula es el ID/PIN del miembro (RF-236).
        var venta = await caja.VentaActualAsync();
        var noInscrita = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AsignarFidelidadAsync(caja.Cajero, venta.Id, EscenarioCatalogo.CedulaAleatoriaValida()));
        Assert.Equal(CodigoResultadoVenta.NoInscritoFidelidad, noInscrita.Resultado);
        var asignada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarFidelidadAsync(caja.Cajero, venta.Id, caja.Catalogo.CedulaMiembro));
        Assert.True(asignada.Exitosa, asignada.Mensaje);
        Assert.Equal("Oro", asignada.Venta!.Fidelidad!.Nivel);
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        // Canje (RF-239): primero se valida el saldo y después se pide la clave del supervisor.
        var excedido = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaPuntos, EscenarioCatalogo.SaldoMiembro + 1m), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.Equal(CodigoResultadoVenta.PagoInvalido, excedido.Resultado);
        Assert.Contains("puntos disponibles", excedido.Mensaje);

        SolicitudPago[] pagos = [new SolicitudPago(caja.Catalogo.FormaPuntos, 100m), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)];
        var sinAutorizacion = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.CanjearPuntos, sinAutorizacion.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.CanjearPuntos, "Canje del cliente");
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, autorizacion));
        Assert.True(cobrada.Exitosa, cobrada.Mensaje);

        // Cincel de 850 + 153 de ITBIS = 1,003 en ferretería: 2 puntos por cada 100 = 20.06, × 1.5 del nivel Oro, sobre lo no
        // pagado con puntos (903 de 1,003) = 27. Los puntos se ganan sobre lo que paga el cliente.
        var fidelidad = cobrada.Venta!.Fidelidad!;
        Assert.Equal(27, fidelidad.PuntosAcumulados);
        Assert.Equal(100, fidelidad.PuntosCanjeados);
        Assert.Equal(2, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.MovimientosPuntos.CountAsync(m => m.VentaId == cobrada.Venta.Id)));

        var saldo = await caja.EjecutarAsync<IServicioFidelidad, RespuestaFidelidad>(s => s.ConsultarAsync(caja.Cajero, caja.Catalogo.CedulaMiembro));
        Assert.True(saldo.Exitosa, saldo.Mensaje);
        Assert.Equal(EscenarioCatalogo.SaldoMiembro - 100 + 27, saldo.Miembro!.SaldoDisponible);

        // La nota de crédito reversa los puntos acumulados en la compra (RF-244, RN-21).
        // La factura sube al Central: toda nota de crédito se emite contra la que él tiene registrada.
        caja.PublicarFacturaAsync(cobrada.Venta!);
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, cobrada.Venta.NumeroTransaccion));
        var linea = Assert.Single(buscada.Factura!.Lineas);
        var autorizacionDevolucion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Artículo defectuoso");
        var devolucion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero,
            new SolicitudDevolucion(cobrada.Venta.NumeroTransaccion, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], "401007551", "Cliente Devolución",
                caja.Catalogo.CodigoMotivoDevolucion, null, autorizacionDevolucion)));
        Assert.True(devolucion.Exitosa, devolucion.Mensaje);
        Assert.Equal(27, devolucion.NotaCredito!.PuntosReversados);

        var despues = await caja.EjecutarAsync<IServicioFidelidad, RespuestaFidelidad>(s => s.ConsultarAsync(caja.Cajero, caja.Catalogo.CedulaMiembro));
        Assert.Equal(EscenarioCatalogo.SaldoMiembro - 100, despues.Miembro!.SaldoDisponible);
    }

    [SkippableFact]
    public async Task Retiro_y_envio_con_autorizacion_generan_pendientes_con_voucher_y_bloquean_la_devolucion_de_lo_no_entregado()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        // El retiro se ofrece por sucursal: la de la caja viene marcada para proponerla por defecto.
        var sucursales = await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosSucursalRetiro>>(s => s.ListarSucursalesRetiroAsync(caja.Cajero));
        var sucursalRetiro = sucursales.Single(s => s.EsDeLaCaja).Id;

        // Tres cementos y un taladro sin serial: el serial se captura al entregar (RN-16).
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, $"3*{caja.Catalogo.BarrasCemento}");
        var sinSerial = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AgregarArticuloAsync(caja.Cajero, venta.Id, caja.Catalogo.CodigoTaladro, null, serialEnDespacho: true));
        Assert.True(sinSerial.Exitosa, sinSerial.Mensaje);
        var cemento = sinSerial.Venta!.Lineas.Single(l => l.CodigoInterno == caja.Catalogo.CodigoCemento).NumeroLinea;
        var taladro = sinSerial.Venta.Lineas.Single(l => l.CodigoInterno == caja.Catalogo.CodigoTaladro).NumeroLinea;
        Assert.True(sinSerial.Venta.Lineas.Single(l => l.NumeroLinea == taladro).SerialPendiente);

        SolicitudPago[] pagos = [new SolicitudPago(caja.Catalogo.FormaEfectivo, 10_000m)];
        var sinMarcar = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, null));
        Assert.Equal(CodigoResultadoVenta.RequiereSerial, sinMarcar.Resultado);

        // Marcar pendientes: primero se validan las cantidades y después se pide la clave del supervisor (RF-53, RN-15).
        var hoy = DateOnly.FromDateTime(caja.Reloj.Ahora.ToLocalTime().DateTime);
        var retiro = new SolicitudMarcarEntrega(MetodoEntrega.RetiroSucursal, sucursalRetiro, null, hoy.AddDays(2), "Retira el jueves", [new CantidadEntrega(cemento, 2m)]);
        var excedido = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.MarcarEntregaAsync(caja.Cajero, venta.Id, retiro with { Lineas = [new CantidadEntrega(cemento, 4m)] }));
        Assert.Equal(CodigoResultadoVenta.EntregaInvalida, excedido.Resultado);
        var sinAutorizacion = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.MarcarEntregaAsync(caja.Cajero, venta.Id, retiro));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.MarcarPendiente, sinAutorizacion.PermisoRequerido);

        var autorizacionRetiro = await caja.AutorizarAsync(CatalogoPermisos.MarcarPendiente, "Cliente retira en almacén");
        var marcada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.MarcarEntregaAsync(caja.Cajero, venta.Id, retiro with { AutorizacionId = autorizacionRetiro }));
        Assert.True(marcada.Exitosa, marcada.Mensaje);
        Assert.Equal(2m, marcada.Venta!.Lineas.Single(l => l.NumeroLinea == cemento).CantidadEnEntrega);
        var cambio = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.CambiarCantidadAsync(caja.Cajero, venta.Id, cemento, 5m));
        Assert.Equal(CodigoResultadoVenta.EntregaInvalida, cambio.Resultado);

        var autorizacionEnvio = await caja.AutorizarAsync(CatalogoPermisos.MarcarPendiente, "Envío a domicilio");
        var envio = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.MarcarEntregaAsync(caja.Cajero, venta.Id, new SolicitudMarcarEntrega(
            MetodoEntrega.Envio, null, new DatosEnvio("Calle 1 #2", "Naco", "Santo Domingo", "Frente al parque", "809-555-1111", "Mensajería", 350m), null, null,
            [new CantidadEntrega(taladro, 1m)], autorizacionEnvio)));
        Assert.True(envio.Exitosa, envio.Mensaje);
        Assert.Equal(2, envio.Venta!.DestinosEntrega!.Count);

        // Al cobrar cada destino genera su pendiente numerado, con voucher para el cliente y el despacho (RF-249, RF-88).
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, null));
        Assert.True(cobrada.Exitosa, cobrada.Mensaje);
        var numeros = cobrada.Cobro!.PendientesEntrega!;
        Assert.Equal(2, numeros.Count);
        foreach (var numero in numeros)
        {
            Assert.NotEmpty(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*pendiente-{numero}-cliente.txt"));
            Assert.NotEmpty(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*pendiente-{numero}-despacho.txt"));
        }

        var pendientes = await caja.EjecutarAsync<ContextoDatosPos, List<CgPos.Dominio.Entregas.PendienteEntrega>>(contexto =>
            contexto.PendientesEntrega.AsNoTracking().Include(p => p.Lineas).Where(p => p.VentaId == cobrada.Venta!.Id).ToListAsync());
        Assert.Equal(2m, pendientes.Single(p => p.Metodo == MetodoEntrega.RetiroSucursal).CantidadPorEntregar(cemento));
        Assert.Equal("809-555-1111", pendientes.Single(p => p.Metodo == MetodoEntrega.Envio).Telefono);

        // Lo pendiente de entrega no se devuelve (RF-233). Quien lo descuenta es el Central, que es quien despacha: aquí lo
        // informa como no disponible, igual que haría el Central real con 2 de los 3 cementos todavía en el almacén.
        var lineaCemento = cobrada.Venta!.Lineas.Single(l => l.NumeroLinea == cemento);
        caja.PublicarFacturaAsync(cobrada.Venta!, noDisponible: new Dictionary<int, decimal> { [cemento] = 2m });

        var factura = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, cobrada.Venta!.NumeroTransaccion));
        Assert.Equal(1m, factura.Factura!.Lineas.Single(l => l.NumeroLinea == cemento).CantidadDisponible);
        var devolucion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero,
            new SolicitudDevolucion(cobrada.Venta!.NumeroTransaccion, [new SolicitudLineaDevolucion(cemento, 3m)], "401007551", "Cliente Devolución", caja.Catalogo.CodigoMotivoDevolucion, null, null)));
        Assert.Equal(CodigoResultadoDevolucion.DevolucionInvalida, devolucion.Resultado);
        Assert.Contains($"solo quedan 1", devolucion.Mensaje);
        Assert.Equal(3m, lineaCemento.Cantidad);
    }

    [SkippableFact]
    public async Task Sincronizacion_reintenta_sin_conexion_y_al_confirmar_mueve_el_xml_del_e_cf_a_enviados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var ventaId = cobro.Venta!.Id;

        async Task<(CgPos.Dominio.Fiscal.DocumentoElectronico Documento, MensajeSalida Mensaje)> LeerAsync() =>
            await caja.EjecutarAsync<ContextoDatosPos, (CgPos.Dominio.Fiscal.DocumentoElectronico, MensajeSalida)>(async contexto =>
                (await contexto.DocumentosElectronicos.AsNoTracking().SingleAsync(d => d.VentaId == ventaId),
                 await contexto.BandejaSalida.AsNoTracking().SingleAsync(m => m.Referencia == cobro.Venta.NumeroTransaccion && m.TipoMensaje == "Venta.Cobrada")));

        Task<ResultadoProcesoBandeja> ProcesarAsync(IClienteCentral central) =>
            caja.EjecutarAsync<IServiceProvider, ResultadoProcesoBandeja>(proveedor =>
                ActivatorUtilities.CreateInstance<CgPos.Pos.Infraestructura.Sincronizacion.ProcesadorBandejaSalida>(proveedor, central).ProcesarAsync());

        var (antes, _) = await LeerAsync();
        Assert.Contains($"{Path.DirectorySeparatorChar}Pendientes{Path.DirectorySeparatorChar}", antes.RutaXml);
        Assert.True(File.Exists(antes.RutaXml));

        // Sin conexión: el lote se detiene, nada se confirma y el XML no sale de Pendientes (RF-268, RN-19).
        var caido = await ProcesarAsync(new CentralDePrueba(ResultadoEnvioCentral.SinConexion("Red caída")));
        Assert.Equal(0, caido.Confirmados);
        Assert.Equal(1, caido.Fallidos);
        Assert.True(File.Exists(antes.RutaXml));

        // Con conexión se envía todo lo que venció su espera; el e-CF queda sincronizado y su XML en Enviados.
        caja.Reloj.Avanzar(TimeSpan.FromMinutes(5));
        var central = new CentralDePrueba(ResultadoEnvioCentral.Recibido());
        for (var ciclo = 0; ciclo < 50 && (await LeerAsync()).Mensaje.Estado != EstadoMensajeSalida.Confirmado; ciclo++)
            await ProcesarAsync(central);

        var (despues, mensaje) = await LeerAsync();
        Assert.Equal(EstadoMensajeSalida.Confirmado, mensaje.Estado);
        Assert.Equal(CgPos.Dominio.Fiscal.EstadoDocumentoElectronico.Sincronizado, despues.Estado);
        Assert.Contains($"{Path.DirectorySeparatorChar}Enviados{Path.DirectorySeparatorChar}", despues.RutaXml);
        Assert.True(File.Exists(despues.RutaXml));
        Assert.False(File.Exists(antes.RutaXml));

        var estado = await caja.EjecutarAsync<IEstadoSincronizacion, DatosEstadoSincronizacion>(s => s.ObtenerAsync());
        Assert.NotNull(estado.UltimaSincronizacion);
    }

    [SkippableFact]
    public async Task Descarga_de_maestros_aplica_organizacion_y_catalogo_y_la_marca_solo_avanza_si_se_aplica()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var escenario = caja.Escenario;

        Task<ResultadoDescargaMaestros> DescargarAsync(PaqueteBajadaMaestros paquete) =>
            caja.EjecutarAsync<IServiceProvider, ResultadoDescargaMaestros>(proveedor =>
                ActivatorUtilities.CreateInstance<CgPos.Pos.Infraestructura.Sincronizacion.DescargaMaestros>(proveedor, new CentralDePrueba(ResultadoEnvioCentral.Recibido(), paquete))
                    .DescargarAsync());

        Task<long> MarcaAsync() =>
            caja.EjecutarAsync<ContextoDatosPos, long>(async contexto =>
                await contexto.MarcasSincronizacion.Where(m => m.Clave == MarcaSincronizacion.VersionMaestros).Select(m => (long?)m.Valor).SingleOrDefaultAsync() ?? 0);

        var desde = await MarcaAsync();
        var departamento = new DepartamentoCarga(Codigos.Siguiente(), "Departamento bajada del Central");
        var clave = $"Pruebas.Descarga{escenario.Sufijo}";
        var organizacion = new CgPos.Contratos.CargaInicial.PaqueteCargaInicial(
            new CgPos.Contratos.CargaInicial.EmpresaCarga("999000004", "Empresa Seguridad SRL", Direccion: "Calle de prueba 1, Santo Domingo"),
            Parametros: [new CgPos.Contratos.CargaInicial.ParametroCarga(clave, "valor del Central", SucursalCodigo: escenario.CodigoSucursal, CajaCodigo: escenario.CodigoCajaUno)]);

        var aplicada = await DescargarAsync(new PaqueteBajadaMaestros(desde, desde + 500, organizacion, new PaqueteMaestros(Departamentos: [departamento])));
        Assert.True(aplicada.Descargado, aplicada.Error);
        Assert.Equal(desde + 500, await MarcaAsync());
        Assert.True(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.Departamentos.AnyAsync(f => f.Codigo == departamento.Codigo)));
        Assert.Equal("valor del Central", await caja.EjecutarAsync<CgPos.Pos.Aplicacion.Organizacion.IParametros, string?>(p => p.ObtenerAsync(clave, escenario.CajaUno)));

        // Un paquete que la caja no puede aplicar no mueve la marca: el próximo ciclo lo vuelve a pedir.
        var articuloSinDepartamento = new ArticuloCarga($"SINFAM{escenario.Sufijo}", "Artículo sin departamento", 999_999, caja.Catalogo.CodigoUnidad,
            caja.Catalogo.CodigoItbis18, 100m, CategoriaCodigo: caja.Catalogo.CodigoCategoriaHerramientas);
        var rechazada = await DescargarAsync(new PaqueteBajadaMaestros(desde + 500, desde + 900, null, new PaqueteMaestros(Articulos: [articuloSinDepartamento])));
        Assert.False(rechazada.Descargado);
        Assert.Contains("departamento", rechazada.Error);
        Assert.Equal(desde + 500, await MarcaAsync());

        // Sin cambios, la marca llega a la versión que informó el Central.
        var sinCambios = await DescargarAsync(new PaqueteBajadaMaestros(desde + 500, desde + 600, null, null));
        Assert.True(sinCambios.Descargado);
        Assert.Equal(desde + 600, await MarcaAsync());

        // El Central devuelve el resultado que la DGII dio al e-CF: la caja lo aplica a su documento y lo alerta al cajero.
        var ventaCobrada = (await caja.CobrarCincelEnEfectivoAsync()).Venta!.Id;
        var encf = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == ventaCobrada).Select(d => d.Encf).SingleAsync());

        var conResultado = await DescargarAsync(new PaqueteBajadaMaestros(desde + 600, desde + 700, null, null,
            [new EstadoDgiiCarga(encf, CgPos.Dominio.Sincronizacion.EstadoEnvioDgii.Rechazado, null, "Firma inválida", "TRK-1")]));
        Assert.True(conResultado.Descargado);
        Assert.Equal(desde + 700, await MarcaAsync());

        var documento = await caja.EjecutarAsync<ContextoDatosPos, DocumentoElectronico>(contexto =>
            contexto.DocumentosElectronicos.AsNoTracking().SingleAsync(d => d.Encf == encf));
        Assert.Equal((EstadoDocumentoElectronico.Rechazado, "Firma inválida"), (documento.Estado, documento.MensajeEstado));

        var estadoEcf = await caja.EjecutarAsync<IServicioEcf, DatosEstadoEcf>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.Equal(1, estadoEcf.RechazadosDgii);
        Assert.Contains(estadoEcf.Alertas, a => a.Contains("rechazados por la DGII"));

        // Un parámetro borrado en el Central se borra en la caja: no viaja en el rango, así que el Central manda los vigentes (RN-24).
        var vigentes = (await caja.EjecutarAsync<ContextoDatosPos, List<CgPos.Dominio.Organizacion.Parametro>>(contexto =>
                contexto.Parametros.AsNoTracking().Where(p => p.Clave != clave).ToListAsync()))
            .Select(p => new CgPos.Contratos.CargaInicial.ParametroReferencia(p.Clave,
                p.CajaId == escenario.CajaUno || p.SucursalId == escenario.Sucursal ? escenario.CodigoSucursal : null,
                p.CajaId == escenario.CajaUno ? escenario.CodigoCajaUno : null))
            .ToList();
        var conBaja = await DescargarAsync(new PaqueteBajadaMaestros(desde + 700, desde + 800, null, null, null, vigentes));
        Assert.True(conBaja.Descargado);
        Assert.Null(await caja.EjecutarAsync<CgPos.Pos.Aplicacion.Organizacion.IParametros, string?>(p => p.ObtenerAsync(clave, escenario.CajaUno)));
    }

    [SkippableFact]
    public async Task Una_factura_de_consumo_bajo_el_monto_de_identificacion_viaja_al_central_como_resumen()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.StartsWith("E32", cobro.Venta!.Comprobante!.Encf);

        var mensaje = await caja.EjecutarAsync<ContextoDatosPos, MensajeSalida>(contexto =>
            contexto.BandejaSalida.AsNoTracking().SingleAsync(m => m.Referencia == cobro.Venta.NumeroTransaccion && m.TipoMensaje == "Venta.Cobrada"));
        var documento = JsonSerializer.Deserialize<DocumentoVentaCobrada>(mensaje.Contenido, OpcionesJson.Predeterminadas)!;
        Assert.Equal(cobro.Venta.NumeroTransaccion, documento.Numero);
        // Al Central no le viaja ningún Id de la base de la caja: todo va por número o por código.
        Assert.Empty(PropiedadesId(JsonDocument.Parse(mensaje.Contenido).RootElement));

        // La venta no llega al monto de identificación: a la DGII va el resumen (RFCE), no el e-CF completo.
        Assert.NotNull(documento.Ecf);
        Assert.True(documento.Ecf.EsResumenConsumo);
        Assert.Contains("<RFCE", documento.Ecf.XmlFirmado, StringComparison.Ordinal);
        Assert.Contains("<CodigoSeguridadeCF>", documento.Ecf.XmlFirmado, StringComparison.Ordinal);
        Assert.DoesNotContain("<DetallesItems>", documento.Ecf.XmlFirmado, StringComparison.Ordinal);

        // El e-CF completo sigue guardado en la caja: es el que se le entrega al cliente.
        var rutaXml = await caja.EjecutarAsync<ContextoDatosPos, string>(contexto =>
            contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == cobro.Venta.Id).Select(d => d.RutaXml).SingleAsync());
        Assert.Contains("<DetallesItems>", await File.ReadAllTextAsync(rutaXml), StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task El_descuento_del_banco_por_bin_baja_el_total_antes_de_emitir_el_ecf()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var ahora = caja.Reloj.GetLocalNow();
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(DescuentosTarjeta:
        [
            new DescuentoTarjetaCarga($"BIN{caja.Escenario.Sufijo}", "10 % con tarjetas del banco", "455123,401288",
                CgPos.Dominio.Promociones.TipoDescuentoTarjeta.Porcentaje, 10m, ahora.AddDays(-1), ahora.AddMonths(1)),
        ]), "Pruebas"));

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var total = (await caja.VentaActualAsync()).Totales.Total;

        // Una tarjeta que no participa deja la venta igual.
        var sinDescuento = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AplicarDescuentoTarjetaAsync(caja.Cajero, venta.Id, "521000"));
        Assert.True(sinDescuento.Exitosa, sinDescuento.Mensaje);
        Assert.Contains("no tiene descuento", sinDescuento.Mensaje);
        Assert.Equal(total, sinDescuento.Venta!.Totales.Total);

        // Con el BIN del banco, el total baja antes de cobrar y el e-CF sale por el monto real.
        var conDescuento = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AplicarDescuentoTarjetaAsync(caja.Cajero, venta.Id, "4551234567"));
        Assert.True(conDescuento.Exitosa, conDescuento.Mensaje);
        Assert.Contains("descuento por pagar con esa tarjeta", conDescuento.Mensaje);
        var esperado = decimal.Round(total * 0.9m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(esperado, conDescuento.Venta!.Totales.Total);

        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(esperado, cobro.Venta!.TotalCobrado);
    }

    [SkippableFact]
    public async Task El_rango_de_ecf_agotado_se_borra_de_la_caja_y_se_le_informa_al_central()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, hastaSecuenciaConsumo: 1);

        // El rango de consumo queda con un solo número: la venta que sigue lo agota.
        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);

        // La caja no se queda con un rango que ya no sirve: volver a bajarlo la haría repetir números.
        Assert.False(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto =>
            contexto.SecuenciasEcf.AnyAsync(s => s.TipoComprobante == TipoComprobante.FacturaConsumo && s.CajaId == caja.Escenario.CajaUno)));

        // Y el Central se entera de hasta dónde llegó, que es lo que le permite avisar a soporte.
        // La base es compartida entre pruebas: se busca el mensaje de esta caja y este tipo, que van en la referencia.
        var referencia = $"{caja.Escenario.CajaUno}-{(int)TipoComprobante.FacturaConsumo}-";
        var mensaje = await caja.EjecutarAsync<ContextoDatosPos, MensajeSalida>(contexto =>
            contexto.BandejaSalida.AsNoTracking()
                .SingleAsync(m => m.TipoMensaje == TiposMensaje.ConsumoSecuenciaEcf && m.Referencia.StartsWith(referencia)));
        var consumo = JsonSerializer.Deserialize<DocumentoConsumoSecuenciaEcf>(mensaje.Contenido, OpcionesJson.Predeterminadas)!;
        Assert.True(consumo.Agotada);
        Assert.Equal(consumo.Hasta, consumo.Ultimo);

        // Sin rango de ese tipo no se factura, y el mensaje dice qué falta en vez de un error cualquiera.
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var sinRango = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaEfectivo, 5000m)], null));

        Assert.Equal(CodigoResultadoVenta.ComprobanteNoDisponible, sinRango.Resultado);
        Assert.Contains("No hay secuencia de e-CF disponible", sinRango.Mensaje);
    }

    [SkippableFact]
    public async Task Sin_terminal_conectado_la_tarjeta_se_cobra_con_la_aprobacion_del_volante_y_sin_autorizacion()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, terminal: new TerminalAusente());

        // La pantalla se entera por el catálogo de cobro: así pide la aprobación en vez de ofrecer «Pasar tarjeta».
        var catalogo = await caja.EjecutarAsync<IConsultaCatalogoCobro, DatosCatalogoCobro>(s => s.ObtenerAsync());
        Assert.False(catalogo.TerminalIntegrado);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var total = (await caja.VentaActualAsync()).Totales.Total;

        // Sin el número del volante no se cobra: sería un cobro que nadie puede comprobar.
        var sinAprobacion = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaTarjeta, total, TipoTarjetaId: caja.Catalogo.TipoTarjeta, AprobacionManual: true)], null));
        Assert.Equal(CodigoResultadoVenta.PagoInvalido, sinAprobacion.Resultado);
        Assert.Contains("número de aprobación", sinAprobacion.Mensaje);

        // Con el número sí, y sin pedir autorización de supervisor: en esta caja es la forma normal de cobrar.
        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaTarjeta, total, "874512", TipoTarjetaId: caja.Catalogo.TipoTarjeta, UltimosDigitos: "4242",
                AprobacionManual: true)], null));

        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(total, cobro.Venta!.TotalCobrado);
        var pago = Assert.Single(cobro.Venta.Pagos!);
        Assert.Equal(("874512", true), (pago.Referencia, pago.AprobacionManual));
    }

    [SkippableFact]
    public async Task El_terminal_lee_la_tarjeta_aplica_el_descuento_del_banco_y_cobra_lo_rebajado()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var terminal = new TerminalConLectura();
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, terminal: terminal);

        var ahora = caja.Reloj.GetLocalNow();
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(DescuentosTarjeta:
        [
            new DescuentoTarjetaCarga($"POS{caja.Escenario.Sufijo}", "10 % con tarjetas del banco", "455123",
                CgPos.Dominio.Promociones.TipoDescuentoTarjeta.Porcentaje, 10m, ahora.AddDays(-1), ahora.AddMonths(1)),
        ]), "Pruebas"));

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var total = (await caja.VentaActualAsync()).Totales.Total;
        var rebajado = decimal.Round(total * 0.9m, 2, MidpointRounding.AwayFromZero);

        // La tarjeta paga todo lo que falta: el terminal la lee, el descuento del banco baja el total y se le cobra menos.
        var conDescuento = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s =>
            s.CobrarConTerminalAsync(caja.Cajero, venta.Id, total, pagaSaldo: true));
        Assert.True(conDescuento.Exitosa, conDescuento.Mensaje);
        Assert.Contains("descuento por pagar con esa tarjeta", conDescuento.Mensaje);
        Assert.Equal(rebajado, conDescuento.Operacion!.Monto);
        Assert.Equal(rebajado, conDescuento.Venta!.Totales.Total);
        Assert.Equal(rebajado, terminal.UltimoMontoCobrado);

        // Si otra tarjeta no aprueba, su descuento no se queda puesto: la venta vuelve a como estaba.
        terminal.Aprueba = false;
        terminal.Bin = "401288";
        var rechazada = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s =>
            s.CobrarConTerminalAsync(caja.Cajero, venta.Id, rebajado, pagaSaldo: true));
        Assert.Equal(CodigoResultadoVenta.TerminalRechazo, rechazada.Resultado);
        Assert.Equal(rebajado, (await caja.VentaActualAsync()).Totales.Total);

        // El e-CF sale por lo que el cliente realmente pagó.
        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaTarjeta, rebajado, TipoTarjetaId: caja.Catalogo.TipoTarjeta,
                OperacionTerminalId: conDescuento.Operacion!.Id)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(rebajado, cobro.Venta!.TotalCobrado);
    }

    [SkippableFact]
    public async Task El_cierre_de_lote_cuadra_las_tarjetas_del_turno_con_lo_que_reporta_el_terminal()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var operacion = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s =>
            s.CobrarConTerminalAsync(caja.Cajero, venta.Id, 100m));
        Assert.True(operacion.Exitosa, operacion.Mensaje);

        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id,
            [new SolicitudPago(caja.Catalogo.FormaTarjeta, 100m, OperacionTerminalId: operacion.Operacion!.Id),
             new SolicitudPago(caja.Catalogo.FormaEfectivo, 1100m)], null));
        Assert.True(cobro.Exitosa, cobro.Mensaje);

        // El terminal simulado cierra el lote sin detallarlo: se informa lo de la caja para compararlo con su comprobante.
        var conciliacion = await caja.EjecutarAsync<CgPos.Pos.Aplicacion.Ventas.IServicioCaja, DatosConciliacionTarjetas>(s =>
            s.ConciliarTarjetasAsync(caja.Cajero));

        Assert.True(conciliacion.LoteCerrado, conciliacion.Mensaje);
        Assert.Equal((1, 100m), (conciliacion.TransaccionesCaja, conciliacion.MontoCaja));
        Assert.False(conciliacion.DetalleDelTerminal);
        Assert.Contains("comprobante", conciliacion.Mensaje);

        // El lote queda guardado en la caja y sube con el cierre: es lo que el Central cuadra después contra el banco.
        var cierre = await caja.EjecutarAsync<CgPos.Pos.Aplicacion.Ventas.IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero, null));
        Assert.True(cierre.Exitosa, cierre.Mensaje);

        var lote = cierre.Cierre!.Lote;
        Assert.NotNull(lote);
        Assert.Equal((1, 100m, false), (lote.TransaccionesCaja, lote.MontoCaja, lote.DetalleDelTerminal));
        Assert.Equal(caja.Cajero.Nombre, lote.UsuarioNombre);

        var mensajeCierre = await caja.EjecutarAsync<ContextoDatosPos, MensajeSalida>(contexto =>
            contexto.BandejaSalida.AsNoTracking().SingleAsync(m => m.TipoMensaje == "Caja.TurnoCerrado"));
        Assert.Equal(100m, JsonSerializer.Deserialize<DocumentoCierreTurno>(mensajeCierre.Contenido, OpcionesJson.Predeterminadas)!.Lote!.MontoCaja);
    }

    [SkippableFact]
    public async Task La_devolucion_puede_pagarse_en_efectivo_y_baja_lo_esperado_del_cuadre()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var cobro = await caja.CobrarCincelEnEfectivoAsync();
        Assert.True(cobro.Exitosa, cobro.Mensaje);
        var factura = cobro.Venta!;
        var total = factura.TotalCobrado!.Value;

        // La factura sube al Central: toda nota de crédito se emite contra la que él tiene registrada.
        caja.PublicarFacturaAsync(factura);
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        var linea = Assert.Single(buscada.Factura!.Lineas);
        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Cliente pidió su dinero");
        var solicitud = new SolicitudDevolucion(factura.NumeroTransaccion, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], "401007551", "Cliente Reembolso",
            caja.Catalogo.CodigoMotivoDevolucion, null, autorizacion, CgPos.Dominio.Devoluciones.TipoReembolso.Efectivo);

        // Sin habilitarlo en los parámetros, el dinero no sale de la gaveta.
        var sinPermitir = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero, solicitud));
        Assert.Equal(CodigoResultadoDevolucion.DevolucionInvalida, sinPermitir.Resultado);
        Assert.Contains("no está habilitada", sinPermitir.Mensaje);

        await caja.EjecutarAsync<ContextoDatosPos, int>(async contexto =>
        {
            contexto.Parametros.Add(CgPos.Dominio.Organizacion.Parametro.Crear(CgPos.Pos.Aplicacion.Organizacion.ClavesParametros.ReembolsoEfectivo, "true",
                cajaId: caja.Escenario.CajaUno));
            return await contexto.SaveChangesAsync();
        });

        var nuevaAutorizacion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Cliente pidió su dinero");
        var devuelta = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s =>
            s.RegistrarAsync(caja.Cajero, solicitud with { AutorizacionId = nuevaAutorizacion }));
        Assert.True(devuelta.Exitosa, devuelta.Mensaje);

        // La nota de crédito se emite igual (la DGII la exige) pero sin saldo: el cliente ya se llevó el dinero.
        var nota = devuelta.NotaCredito!;
        Assert.Equal((CgPos.Dominio.Devoluciones.TipoReembolso.Efectivo, 0m), (nota.Reembolso, nota.Saldo));
        Assert.StartsWith("E34", nota.Comprobante!.Encf);

        var saldo = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaSaldoNotaCredito>(s => s.ConsultarNotaCreditoAsync(caja.Cajero, nota.Comprobante.Encf));
        Assert.Equal(CodigoResultadoDevolucion.NotaCreditoConsumida, saldo.Resultado);

        // El efectivo salió de la gaveta: el cuadre espera menos.
        var resumen = await caja.EjecutarAsync<CgPos.Pos.Aplicacion.Ventas.IServicioCaja, RespuestaCaja>(s => s.ObtenerResumenAsync(caja.Cajero));
        Assert.True(resumen.Exitosa, resumen.Mensaje);
        Assert.Equal(total, resumen.Resumen!.TotalRetiros);
    }

    [SkippableFact]
    public async Task Mantenimiento_purga_solo_lo_confirmado_vencido_alerta_y_respalda_la_base()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        Task<ResultadoProcesoBandeja> ProcesarAsync() =>
            caja.EjecutarAsync<IServiceProvider, ResultadoProcesoBandeja>(proveedor =>
                ActivatorUtilities.CreateInstance<CgPos.Pos.Infraestructura.Sincronizacion.ProcesadorBandejaSalida>(proveedor, new CentralDePrueba(ResultadoEnvioCentral.Recibido()))
                    .ProcesarAsync());

        Task<(string RutaXml, Guid MensajeId)> DocumentoAsync(int ventaId) =>
            caja.EjecutarAsync<ContextoDatosPos, (string, Guid)>(async contexto =>
                (await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == ventaId).Select(d => d.RutaXml).SingleAsync(),
                 await contexto.BandejaSalida.AsNoTracking()
                     .Where(m => m.TipoMensaje == "Venta.Cobrada" && contexto.Ventas.Any(v => v.Id == ventaId && v.NumeroTransaccion == m.Referencia))
                     .Select(m => m.Id).SingleAsync()));

        // Una venta confirmada por el Central (XML en Enviados) y otra aún pendiente.
        var sincronizada = (await caja.CobrarCincelEnEfectivoAsync()).Venta!.Id;
        var (_, mensajeSincronizado) = await DocumentoAsync(sincronizada);
        for (var ciclo = 0; ciclo < 50 && (await caja.EjecutarAsync<ContextoDatosPos, EstadoMensajeSalida>(contexto =>
                 contexto.BandejaSalida.Where(m => m.Id == mensajeSincronizado).Select(m => m.Estado).SingleAsync())) != EstadoMensajeSalida.Confirmado; ciclo++)
            await ProcesarAsync();
        var (xmlEnviado, _) = await DocumentoAsync(sincronizada);
        Assert.Contains($"{Path.DirectorySeparatorChar}Enviados{Path.DirectorySeparatorChar}", xmlEnviado);

        var pendiente = (await caja.CobrarCincelEnEfectivoAsync()).Venta!.Id;
        var (xmlPendiente, mensajePendiente) = await DocumentoAsync(pendiente);

        // Pasados 40 días (retención de 30): se purga lo confirmado; lo pendiente nunca (RN-19).
        caja.Reloj.Avanzar(TimeSpan.FromDays(40));
        var purga = await caja.EjecutarAsync<IServicioMantenimiento, ResultadoPurga>(s => s.PurgarAsync());
        Assert.True(purga.XmlEliminados >= 1);
        Assert.True(purga.MensajesEliminados >= 1);
        Assert.False(File.Exists(xmlEnviado));
        Assert.True(File.Exists(xmlPendiente));
        Assert.False(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.BandejaSalida.AnyAsync(m => m.Id == mensajeSincronizado)));
        Assert.True(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.BandejaSalida.AnyAsync(m => m.Id == mensajePendiente)));

        // Alertas: base sobre el umbral de 1 MB, documento de hace 40 días sin sincronizar y reloj 30 s desfasado (tolerancia 5 s).
        await caja.EjecutarAsync<EstadoMantenimiento, bool>(estado =>
        {
            estado.UltimaHora = new ResultadoHora(true, TimeSpan.FromSeconds(30), "ntp.prueba", null, caja.Reloj.Ahora);
            return Task.FromResult(true);
        });
        var alertas = await caja.EjecutarAsync<IServicioMantenimiento, IReadOnlyList<string>>(s => s.ObtenerAlertasAsync());
        Assert.Contains(alertas, a => a.Contains("MB"));
        Assert.Contains(alertas, a => a.Contains("sin sincronizar"));
        Assert.Contains(alertas, a => a.Contains("hora"));
        var estadoSincronizacion = await caja.EjecutarAsync<IEstadoSincronizacion, DatosEstadoSincronizacion>(s => s.ObtenerAsync());
        Assert.Equal(alertas.Count, estadoSincronizacion.Alertas!.Count);

        // Respaldo de la base en la carpeta de respaldos de la instancia; luego se borra el archivo de prueba desde SQL Server.
        var respaldo = await caja.EjecutarAsync<IServicioMantenimiento, ResultadoRespaldo>(s => s.RespaldarAsync());
        Assert.True(respaldo.Correcto, respaldo.Error);
        Assert.EndsWith(".bak", respaldo.Ruta);
        await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.Database.ExecuteSqlRawAsync("EXEC master.dbo.xp_delete_file 0, {0}", respaldo.Ruta!));
    }

    private static class BalanzaPrueba
    {
        public const decimal PesoSimulado = CgPos.Pos.Infraestructura.Perifericos.Balanzas.BalanzaSimulada.PesoPredeterminado;
    }

    [SkippableFact]
    public async Task Los_Id_de_las_ventas_de_trabajo_vuelven_a_1_solo_si_sus_tablas_quedaron_vacias()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var secuencias = ambito.ServiceProvider.GetRequiredService<GeneradorSecuencias>();

        // Con una venta en curso no se toca nada: su Id no puede volver a entregarse.
        Assert.False(await secuencias.ReiniciarIdsDeTrabajoAsync(CancellationToken.None));

        // Vacías, sí. Se prueba dentro de una transacción que se deshace, porque la base la comparten las demás pruebas.
        await using var transaccion = await contexto.Database.BeginTransactionAsync();
        await contexto.Database.ExecuteSqlRawAsync("""
            DELETE FROM LineasDestinoEntregaEnProceso; DELETE FROM DestinosEntregaVentaEnProceso; DELETE FROM LineasVentaEnProceso; DELETE FROM VentasEnProceso;
            DELETE FROM LineasDestinoEntregaGuardadas; DELETE FROM DestinosEntregaVentaGuardadas; DELETE FROM LineasVentaGuardadas; DELETE FROM VentasGuardadas;
            """);
        Assert.True(await secuencias.ReiniciarIdsDeTrabajoAsync(CancellationToken.None));
        Assert.Equal(1, (await contexto.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR SecuenciaVentasEnProceso AS Value").ToListAsync()).Single());
        Assert.Equal(1, (await contexto.Database.SqlQuery<int>($"SELECT NEXT VALUE FOR SecuenciaLineasVentaGuardadas AS Value").ToListAsync()).Single());
        await transaccion.RollbackAsync();
    }

    [SkippableFact]
    public async Task El_cajero_solo_digita_hasta_la_cantidad_del_parametro()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.CambiarParametroAsync(CgPos.Dominio.Organizacion.CatalogoParametros.CantidadMaximaDigitada, "10");
        var venta = await caja.VentaActualAsync();

        // Hasta el tope se digita; más, hay que pasar el artículo por el lector uno a uno.
        var permitida = await caja.AgregarAsync(venta.Id, $"10*{caja.Catalogo.CodigoCemento}");
        Assert.Equal(10m, Assert.Single(permitida.Lineas).Cantidad);

        var excedida = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AgregarArticuloAsync(caja.Cajero, venta.Id, $"11*{caja.Catalogo.CodigoCemento}", null));
        Assert.Equal(CodigoResultadoVenta.CantidadInvalida, excedida.Resultado);
        Assert.Contains("10", excedida.Mensaje);

        var cambio = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.CambiarCantidadAsync(caja.Cajero, venta.Id, 1, 25m));
        Assert.Equal(CodigoResultadoVenta.CantidadInvalida, cambio.Resultado);

        // Bajarla sí: 3 no pasa del tope.
        var bajada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.CambiarCantidadAsync(caja.Cajero, venta.Id, 1, 3m));
        Assert.True(bajada.Exitosa, bajada.Mensaje);
        Assert.Equal(3m, Assert.Single(bajada.Venta!.Lineas).Cantidad);

        // El pesado se queda fuera de la regla: su cantidad la da la balanza o la etiqueta, no el cajero.
        var conPeso = await caja.AgregarAsync(venta.Id, caja.Catalogo.EtiquetaPesoTomate(25.500m));
        Assert.Equal(25.5m, conPeso.Lineas.Single(l => l.CodigoInterno == caja.Catalogo.PluTomate).Cantidad);
    }

    [SkippableFact]
    public async Task Las_secuencias_no_se_repiten_con_pedidos_simultaneos()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa, abrirTurno: false);
        var cajaId = caja.Escenario.CajaUno;

        var pedidos = Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
            return await ambito.ServiceProvider.GetRequiredService<GeneradorSecuencias>().SiguienteAsync(cajaId, "Prueba", CancellationToken.None);
        });
        var numeros = await Task.WhenAll(pedidos);

        Assert.Equal(Enumerable.Range(1, 25).Select(n => (long)n), numeros.Order());

        // La fila dice de qué sucursal y caja es por código: los Id cambian si la base se recrea.
        var fila = await caja.EjecutarAsync<ContextoDatosPos, SecuenciaCaja>(contexto =>
            contexto.Set<SecuenciaCaja>().AsNoTracking().SingleAsync(s => s.CajaId == cajaId && s.Tipo == "Prueba"));
        Assert.Equal((caja.Escenario.CodigoSucursal, caja.Escenario.CodigoCajaUno), (fila.SucursalCodigo, fila.CajaCodigo));
    }

    [SkippableFact]
    public async Task Indicador_de_sincronizacion_cuenta_los_documentos_pendientes()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);

        int antes;
        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
            antes = (await ambito.ServiceProvider.GetRequiredService<IEstadoSincronizacion>().ObtenerAsync()).DocumentosPendientes;

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            ambito.ServiceProvider.GetRequiredService<IBandejaSalida>().Encolar("Prueba.Documento", Guid.NewGuid().ToString("N"), new { Total = 1m });
            await ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>().SaveChangesAsync();
        }

        await using var ambitoLectura = baseDatos.Servicios!.CreateAsyncScope();
        var estado = await ambitoLectura.ServiceProvider.GetRequiredService<IEstadoSincronizacion>().ObtenerAsync();
        Assert.Equal(antes + 1, estado.DocumentosPendientes);
        // Sin configurar la caja no hay Central al que hablarle, y se dice por qué: son las credenciales, no la red.
        Assert.False(estado.CentralConfigurado);
        Assert.False(estado.EnLinea);
        Assert.Equal(MotivosSinConexion.Credenciales, estado.SinConexionPor);
    }

    private static string CedulaAleatoriaValida()
    {
        while (true)
        {
            var candidata = Random.Shared.NextInt64(1_000_000_000L, 9_999_999_999L).ToString() + Random.Shared.Next(0, 10);
            if (DocumentoIdentidad.CedulaValida(candidata))
                return candidata;
        }
    }

    /// <summary>Caja lista para vender: usuarios y maestros cargados, sesión del cajero y (opcional) turno abierto.</summary>
    /// <summary>Caja sin terminal conectado: se cobra en un equipo aparte y el cajero digita la aprobación del volante.</summary>
    private sealed class TerminalAusente : CgPos.Pos.Aplicacion.Perifericos.ITerminalPago
    {
        public bool Integrado => false;

        public bool ConsultaTarjeta => false;

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoConsultaTarjeta> ConsultarTarjetaAsync(CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoConsultaTarjeta(false, false, null, null, "No hay terminal."));

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal> CobrarAsync(decimal monto, decimal impuesto, string referenciaVenta,
            CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal(false, false, null, null, null, "No hay terminal."));

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal> AnularAsync(string aprobacion, string? referenciaTerminal, decimal monto,
            CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal(false, false, null, null, null, "No hay terminal."));

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoLoteTerminal(true, "No hay terminal: cierre el lote en el equipo."));
    }

    /// <summary>Terminal que lee la tarjeta antes de cobrar, como el Ingenico 7000 de CardNet con la consulta CS00 activa.</summary>
    private sealed class TerminalConLectura : CgPos.Pos.Aplicacion.Perifericos.ITerminalPago
    {
        public string Bin { get; set; } = "4551234567";

        public bool Aprueba { get; set; } = true;

        public decimal UltimoMontoCobrado { get; private set; }

        public bool ConsultaTarjeta => true;

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoConsultaTarjeta> ConsultarTarjetaAsync(CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoConsultaTarjeta(true, false, Bin, "VISA", null));

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal> CobrarAsync(decimal monto, decimal impuesto, string referenciaVenta,
            CancellationToken cancelacion = default)
        {
            UltimoMontoCobrado = monto;
            return Task.FromResult(Aprueba
                ? new CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal(true, false, "654321", "4242", "VISA", "Aprobada", "06-001")
                : new CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal(false, false, null, null, null, "Transacción declinada por el banco emisor."));
        }

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal> AnularAsync(string aprobacion, string? referenciaTerminal, decimal monto,
            CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoTerminal(true, false, aprobacion, null, null, "Anulada", referenciaTerminal));

        public Task<CgPos.Pos.Aplicacion.Perifericos.ResultadoLoteTerminal> CerrarLoteAsync(CancellationToken cancelacion = default) =>
            Task.FromResult(new CgPos.Pos.Aplicacion.Perifericos.ResultadoLoteTerminal(true, "Lote cerrado."));
    }

    /// <summary>Nombres de propiedades que parecen un Id en el documento que se le envía al Central.</summary>
    private static IReadOnlyList<string> PropiedadesId(JsonElement elemento)
    {
        var encontrados = new List<string>();
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propiedad in elemento.EnumerateObject())
                {
                    if (propiedad.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase))
                        encontrados.Add(propiedad.Name);
                    encontrados.AddRange(PropiedadesId(propiedad.Value));
                }

                break;
            case JsonValueKind.Array:
                foreach (var hijo in elemento.EnumerateArray())
                    encontrados.AddRange(PropiedadesId(hijo));
                break;
        }

        return encontrados;
    }

    private sealed class CajaEnPruebas : IAsyncDisposable
    {
        private readonly BaseDatosPruebas _baseDatos;
        private ServiceProvider _proveedor = null!;
        private CgPos.Pos.Aplicacion.Perifericos.ITerminalPago? _terminal;

        private CajaEnPruebas(BaseDatosPruebas baseDatos, EscenarioSeguridad escenario)
        {
            _baseDatos = baseDatos;
            Escenario = escenario;
        }

        public EscenarioSeguridad Escenario { get; }
        public EscenarioCatalogo Catalogo { get; } = new();
        public RelojPrueba Reloj { get; private set; } = null!;
        public SesionUsuario Cajero { get; private set; } = null!;
        public SesionUsuario CajeroDos { get; private set; } = null!;

        public static async Task<CajaEnPruebas> CrearAsync(BaseDatosPruebas baseDatos, int empresa, bool abrirTurno = true, bool cargarCertificado = true,
            long hastaSecuenciaConsumo = 1000, CgPos.Pos.Aplicacion.Perifericos.ITerminalPago? terminal = null)
        {
            var caja = new CajaEnPruebas(baseDatos, await EscenarioSeguridad.CrearAsync(baseDatos, empresa))
            {
                _cargarCertificado = cargarCertificado,
                _terminal = terminal,
            };
            caja.PrepararProveedor();

            // Los maestros se cargan con el mismo reloj de la prueba, para que los precios ya estén vigentes.
            await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(caja.Catalogo.Paquete(), "Pruebas"));
            await caja.EjecutarAsync<ContextoDatosPos, bool>(async contexto =>
            {
                await caja.Catalogo.ResolverIdsAsync(contexto);
                return true;
            });

            // Rangos de e-CF de la caja (los asigna el Central). Como en la realidad, cada caja tiene un rango distinto:
            // las pruebas de la clase comparten base y el e-NCF es único.
            var vence = new DateOnly(2027, 12, 31);
            var desde = caja.DesdeSecuencia;
            await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(SecuenciasEcf:
            [
                new SecuenciaEcfCarga(caja.Escenario.CodigoSucursal, caja.Escenario.CodigoCajaUno, TipoComprobante.FacturaConsumo, desde, desde + hastaSecuenciaConsumo - 1, vence),
                new SecuenciaEcfCarga(caja.Escenario.CodigoSucursal, caja.Escenario.CodigoCajaUno, TipoComprobante.FacturaCreditoFiscal, desde, desde + 999, vence),
                new SecuenciaEcfCarga(caja.Escenario.CodigoSucursal, caja.Escenario.CodigoCajaUno, TipoComprobante.RegimenesEspeciales, desde, desde + 999, vence),
                new SecuenciaEcfCarga(caja.Escenario.CodigoSucursal, caja.Escenario.CodigoCajaUno, TipoComprobante.Gubernamental, desde, desde + 999, vence),
                new SecuenciaEcfCarga(caja.Escenario.CodigoSucursal, caja.Escenario.CodigoCajaUno, TipoComprobante.NotaCredito, desde, desde + 999, vence),
            ]), "Pruebas"));

            caja.Cajero = await caja.IngresarAsync(caja.Escenario.CodigoCajero, EscenarioSeguridad.ClaveCajero);
            caja.CajeroDos = await caja.IngresarAsync(caja.Escenario.CodigoCajeroDos, EscenarioSeguridad.ClaveCajeroDos);

            if (abrirTurno)
            {
                var turno = await caja.EjecutarAsync<IServicioTurnos, RespuestaTurno>(s => s.AbrirAsync(caja.Cajero, 0m));
                Assert.True(turno.Exitosa, turno.Mensaje);
            }

            return caja;
        }

        /// <summary>Simula reiniciar la caja: nuevo proveedor de servicios contra la misma base y las mismas sesiones.</summary>
        public CajaEnPruebas Reabrir()
        {
            var reabierta = new CajaEnPruebas(_baseDatos, Escenario) { Cajero = Cajero, CajeroDos = CajeroDos };
            reabierta.PrepararProveedor();
            reabierta.Reloj.Ahora = Reloj.Ahora;
            return reabierta;
        }

        public async Task<TResultado> EjecutarAsync<TServicio, TResultado>(Func<TServicio, Task<TResultado>> operacion) where TServicio : notnull
        {
            await using var ambito = _proveedor.CreateAsyncScope();
            return await operacion(ambito.ServiceProvider.GetRequiredService<TServicio>());
        }

        public async Task<DatosVenta> VentaActualAsync()
        {
            var respuesta = await EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.ObtenerActualAsync(Cajero));
            Assert.True(respuesta.Exitosa, respuesta.Mensaje);
            return respuesta.Venta!;
        }

        public async Task<DatosVenta> AgregarAsync(int ventaId, string codigo)
        {
            var respuesta = await EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(Cajero, ventaId, codigo, null));
            Assert.True(respuesta.Exitosa, $"{codigo}: {respuesta.Mensaje}");
            return respuesta.Venta!;
        }

        /// <summary>Venta nueva con un cincel cobrada con RD$1,000 en efectivo.</summary>
        public async Task<RespuestaCobro> CobrarCincelEnEfectivoAsync()
        {
            var venta = await VentaActualAsync();
            await AgregarAsync(venta.Id, Catalogo.BarrasCincel);
            return await EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(Cajero, venta.Id, [new SolicitudPago(Catalogo.FormaEfectivo, 1100m)], null));
        }

        /// <summary>
        /// Registra en el Central de prueba la factura que se acaba de cobrar, como haría la sincronización. Hace falta para
        /// devolverla: la caja siempre le pide la factura al Central, también las suyas.
        /// </summary>
        /// <param name="noDisponible">
        /// Lo que el Central resta de cada línea: lo ya devuelto en cualquier tienda y la mercancía que todavía no se ha
        /// entregado (RF-233). La caja solo ve el resultado.
        /// </param>
        public DatosFacturaParaCaja PublicarFacturaAsync(DatosVenta venta, IReadOnlyDictionary<int, decimal>? noDisponible = null)
        {
            var lineas = venta.Lineas.Where(l => !l.Anulada).OrderBy(l => l.NumeroLinea).Select(l =>
                new DatosLineaFacturaParaCaja(l.NumeroLinea, l.CodigoInterno, l.CodigoLeido, l.Descripcion, l.TipoArticulo, l.UnidadMedidaCodigo,
                    l.DecimalesCantidad, l.PorcentajeImpuesto, l.Cantidad, l.PrecioUnitario, l.DescuentoPromocion + l.DescuentoManual + l.DescuentoFactura,
                    l.Impuesto, l.ImporteConImpuesto, l.Serial,
                    noDisponible?.GetValueOrDefault(l.NumeroLinea) ?? 0m))
                .ToList();

            var factura = new DatosFacturaParaCaja(venta.NumeroTransaccion, venta.Comprobante?.Encf, Escenario.CodigoSucursal, Escenario.CodigoCajaUno,
                true, venta.TipoComprobante, DateOnly.FromDateTime(Reloj.Ahora.Date), venta.CobradaEn ?? Reloj.Ahora,
                venta.Cliente?.Documento, venta.Cliente?.Nombre, venta.Moneda, venta.Totales.Total, lineas);

            Central.Facturas[factura.Numero] = factura;
            if (factura.Encf is { } encf)
                Central.Facturas[encf] = factura;
            return factura;
        }

        public Task<ResultadoCargaMaestros> CargarOfertasAsync(params PromocionCarga[] promociones) =>
            EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(Promociones: promociones), "Pruebas"));

        public async Task<Guid> AutorizarAsync(string permiso, string motivo, SesionUsuario? solicitante = null)
        {
            var resultado = await EscenarioSeguridad.AutorizarAsync(_proveedor, new SolicitudAutorizacionSupervisor(
                solicitante ?? Cajero, permiso, motivo, new CredencialUsuario(Escenario.CodigoSupervisor, EscenarioSeguridad.ClaveSupervisor)));
            Assert.True(resultado.Concedida, resultado.Motivo?.ToString());
            return resultado.AutorizacionId!.Value;
        }

        public async ValueTask DisposeAsync() => await _proveedor.DisposeAsync();

        private static long _siguienteRango;

        private bool _cargarCertificado = true;

        /// <summary>Primer número del rango de e-CF de esta caja de prueba.</summary>
        public long DesdeSecuencia { get; } = Interlocked.Increment(ref _siguienteRango) * 10_000 + 1;

        /// <summary>Central de prueba de esta caja: responde las consultas y reservas de notas de crédito de otras sucursales (RF-43).</summary>
        public CentralDePrueba Central { get; } = new(ResultadoEnvioCentral.Recibido());

        private void PrepararProveedor()
        {
            (_proveedor, var reloj) = Escenario.CrearProveedor(Escenario.CajaUno, extras: servicios =>
            {
                servicios.AddSingleton<CgPos.Pos.Aplicacion.Sincronizacion.IClienteCentral>(Central);
                if (_terminal is not null)
                    servicios.AddSingleton(_terminal);
            });
            Reloj = reloj;

            // El certificado vive en memoria del proveedor, como en el Agente: se carga con su PIN (RF-217).
            if (_cargarCertificado)
                Assert.Null(_proveedor.GetRequiredService<CgPos.Pos.Aplicacion.Ecf.ICertificadoCaja>().Cargar(BaseDatosPruebas.PinCertificado));
        }

        /// <summary>Deja el turno abierto de la caja con fecha de ayer, como si nadie lo hubiera cerrado anoche.</summary>
        public Task<int> TurnoDeAyerAsync() =>
            EjecutarAsync<ContextoDatosPos, int>(async contexto =>
            {
                var turno = await contexto.Turnos.SingleAsync(t => t.CajaId == Escenario.CajaUno && t.Estado == EstadoTurno.Abierto);
                var ayer = turno.FechaOperacion.AddDays(-1);
                return await contexto.Turnos.Where(t => t.Id == turno.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.FechaOperacion, ayer));
            });

        /// <summary>Cambia un parámetro de esta caja, como se haría desde el Central.</summary>
        public Task<int> CambiarParametroAsync(string clave, string valor) =>
            EjecutarAsync<ContextoDatosPos, int>(async contexto =>
            {
                var existente = await contexto.Parametros.SingleOrDefaultAsync(p => p.Clave == clave && p.CajaId == Escenario.CajaUno);
                if (existente is null)
                    contexto.Parametros.Add(CgPos.Dominio.Organizacion.Parametro.Crear(clave, valor, cajaId: Escenario.CajaUno));
                else
                    existente.CambiarValor(valor);
                return await contexto.SaveChangesAsync();
            });

        private async Task<SesionUsuario> IngresarAsync(string codigo, string clave)
        {
            var resultado = await EscenarioSeguridad.IngresarAsync(_proveedor, new CredencialUsuario(codigo, clave));
            Assert.True(resultado.Exitoso, resultado.Motivo?.ToString());
            return resultado.Sesion!;
        }
    }
}
