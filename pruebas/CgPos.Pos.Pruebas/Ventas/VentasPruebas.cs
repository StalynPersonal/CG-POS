using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Promociones;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Ventas;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Ventas;

/// <summary>Turno y venta en curso contra SQL Server real: escaneo, precios, eliminación con autorización y recuperación.</summary>
public class VentasPruebas(BaseDatosPruebas baseDatos) : IClassFixture<BaseDatosPruebas>
{
    private static readonly Guid Empresa = Guid.CreateVersion7();

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

        Assert.Matches(@"^S[0-9A-F]{6}-01-\d{8}$", inicio.NumeroTransaccion);

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

        // 850 + 12×450 + 2.345×45 (105.53)
        Assert.Equal(6355.53m, conTomate.Totales.Total);
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
        Guid ventaId;
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
            Assert.Equal(17_000m, recuperada.Totales.Total);
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
        Assert.Equal(new[] { 1, 3, 2 }, eliminada.Venta!.Lineas.Select(l => l.NumeroLinea)); // el reverso (3) va debajo de la 1
        var reverso = eliminada.Venta.Lineas[1];
        Assert.True(reverso.EsReverso);
        Assert.Equal(-1m, reverso.Cantidad);
        Assert.Equal(0m, reverso.Importe);
        Assert.Equal(485m, eliminada.Venta.Totales.Total);

        var reutilizada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.EliminarLineaAsync(caja.Cajero, venta.Id, 2, autorizacion));
        Assert.Equal(CodigoResultadoVenta.AutorizacionInvalida, reutilizada.Resultado);

        var registro = await caja.EjecutarAsync<ContextoDatosPos, Dominio.Auditoria.RegistroAuditoria>(contexto =>
            contexto.Auditoria.SingleAsync(r => r.Accion == "Ventas.LineaEliminada" && r.EntidadId == venta.NumeroTransaccion));
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

        var deOtroPermiso = await caja.AutorizarAsync(CatalogoPermisos.AnularVenta, "Otro permiso");
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
    public async Task Limpiar_pantalla_anula_la_venta_y_empieza_otra_con_nuevo_numero()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.LimpiarPantalla, "Cliente desistió");
        var limpia = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.LimpiarAsync(caja.Cajero, venta.Id, autorizacion));

        Assert.True(limpia.Exitosa, limpia.Mensaje);
        Assert.NotEqual(venta.Id, limpia.Venta!.Id);
        Assert.NotEqual(venta.NumeroTransaccion, limpia.Venta.NumeroTransaccion);
        Assert.Empty(limpia.Venta.Lineas);

        var anulada = await caja.EjecutarAsync<ContextoDatosPos, Venta>(contexto => contexto.Ventas.SingleAsync(v => v.Id == venta.Id));
        Assert.Equal(EstadoVenta.Anulada, anulada.Estado);
        Assert.Equal("Pantalla limpiada", anulada.MotivoAnulacion);
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
        Assert.Equal(ReglasComprobante.MontoIdentificacionConsumoPredeterminado, grande.MontoIdentificacion);

        var identificada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AsignarClienteAsync(caja.Cajero, venta.Id, CedulaAleatoriaValida(), "Comprador grande"));
        Assert.False(identificada.Venta!.RequiereIdentificacion);
    }

    [SkippableFact]
    public async Task Facturas_en_espera_quedan_en_el_turno_y_se_retoman_intercambiando_la_actual()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var primera = await caja.VentaActualAsync();
        await caja.AgregarAsync(primera.Id, caja.Catalogo.BarrasCincel);

        var vacia = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, primera.Id));
        Assert.True(vacia.Exitosa, vacia.Mensaje);
        var segunda = vacia.Venta!;
        Assert.NotEqual(primera.Id, segunda.Id);
        Assert.Empty(segunda.Lineas);

        var sinArticulos = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, segunda.Id));
        Assert.Equal(CodigoResultadoVenta.SinLineas, sinArticulos.Resultado);

        await caja.AgregarAsync(segunda.Id, caja.Catalogo.BarrasCemento);
        var retomada = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.RetomarAsync(caja.Cajero, primera.Id));
        Assert.True(retomada.Exitosa, retomada.Mensaje);
        Assert.Equal(primera.Id, retomada.Venta!.Id);
        Assert.Equal(850m, retomada.Venta.Totales.Total);

        var enEspera = await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosVentaEnEspera>>(s => s.ListarEnEsperaAsync(caja.Cajero));
        var pendiente = Assert.Single(enEspera);
        Assert.Equal(segunda.Id, pendiente.Id);
        Assert.Equal(485m, pendiente.Total);

        var actual = await caja.VentaActualAsync();
        Assert.Equal(primera.Id, actual.Id);
    }

    [SkippableFact]
    public async Task Anular_y_suspender_requieren_autorizacion_y_quedan_auditados()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var sinPermiso = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AnularAsync(caja.Cajero, venta.Id, "Cliente se retiró", null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AnularVenta, "Cliente se retiró");
        var nueva = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AnularAsync(caja.Cajero, venta.Id, null, autorizacion));
        Assert.True(nueva.Exitosa, nueva.Mensaje);
        Assert.NotEqual(venta.Id, nueva.Venta!.Id);

        var anulada = await caja.EjecutarAsync<ContextoDatosPos, Venta>(contexto => contexto.Ventas.SingleAsync(v => v.Id == venta.Id));
        Assert.Equal(EstadoVenta.Anulada, anulada.Estado);
        Assert.Equal("Cliente se retiró", anulada.MotivoAnulacion);

        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion,
            (await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.SuspenderAsync(caja.Cajero, null))).Resultado);
        var suspension = await caja.AutorizarAsync(CatalogoPermisos.SuspenderVenta, "Almuerzo");
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.SuspenderAsync(caja.Cajero, suspension))).Exitosa);

        var cajaCodigo = caja.Cajero.CajaCodigo;
        var registros = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.Auditoria.CountAsync(r => (r.Accion == "Ventas.Anulada" && r.EntidadId == venta.NumeroTransaccion)
                                               || (r.Accion == "Caja.OperacionesSuspendidas" && r.UsuarioId == caja.Escenario.Cajero)));
        Assert.Equal(2, registros);
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
        var netoEsperado = BalanzaPrueba.PesoSimulado - EscenarioCatalogo.TaraCebolla; // 1.250 − 0.050
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
            new PromocionCarga(Guid.CreateVersion7(), $"P15{caja.Catalogo.Sufijo}", "Cincel 15 %", TipoPromocion.Porcentaje, 15m,
                EscenarioSeguridad.Inicio.AddDays(-1), EscenarioSeguridad.Inicio.AddDays(10), Articulos: [caja.Catalogo.ArticuloCincel]),
            new PromocionCarga(Guid.CreateVersion7(), $"E79{caja.Catalogo.Sufijo}", "Cincel a 799", TipoPromocion.PrecioEspecial, 799m,
                EscenarioSeguridad.Inicio.AddDays(-1), EscenarioSeguridad.Inicio.AddDays(10), Articulos: [caja.Catalogo.ArticuloCincel]));

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
            TopesDescuento: [new TopeDescuentoCarga(Guid.CreateVersion7(), 2, 5m, null, ArticuloId: caja.Catalogo.ArticuloCemento)]), "Pruebas"));

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
        Assert.Equal(460.75m, aplicado.Venta.Totales.Total);

        var registro = await caja.EjecutarAsync<ContextoDatosPos, Dominio.Auditoria.RegistroAuditoria>(contexto =>
            contexto.Auditoria.SingleAsync(r => r.Accion == "Ventas.DescuentoLinea" && r.EntidadId == venta.NumeroTransaccion));
        Assert.Equal(caja.Escenario.Supervisor, registro.AutorizadoPorId);
    }

    [SkippableFact]
    public async Task Descuento_a_la_factura_excluye_ofertas_y_al_desactivar_la_oferta_se_reprorratea()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        await caja.CargarOfertasAsync(new PromocionCarga(Guid.CreateVersion7(), $"F15{caja.Catalogo.Sufijo}", "Cincel 15 %", TipoPromocion.Porcentaje, 15m,
            EscenarioSeguridad.Inicio.AddDays(-1), EscenarioSeguridad.Inicio.AddDays(10), Articulos: [caja.Catalogo.ArticuloCincel]));

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
        Assert.Equal(850m + 485m - 50m, sinOferta.Venta.Totales.Total);
    }

    private static class BalanzaPrueba
    {
        public const decimal PesoSimulado = CgPos.Pos.Infraestructura.Perifericos.BalanzaSimulada.PesoPredeterminado;
    }

    [SkippableFact]
    public async Task Las_secuencias_no_se_repiten_con_pedidos_simultaneos()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var cajaId = Guid.CreateVersion7();

        var pedidos = Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
            return await ambito.ServiceProvider.GetRequiredService<GeneradorSecuencias>().SiguienteAsync(cajaId, "Prueba", CancellationToken.None);
        });
        var numeros = await Task.WhenAll(pedidos);

        Assert.Equal(Enumerable.Range(1, 25).Select(n => (long)n), numeros.Order());
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
            ambito.ServiceProvider.GetRequiredService<IBandejaSalida>().Encolar("Prueba.Documento", Guid.CreateVersion7(), new { Total = 1m });
            await ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>().SaveChangesAsync();
        }

        await using var ambitoLectura = baseDatos.Servicios!.CreateAsyncScope();
        var estado = await ambitoLectura.ServiceProvider.GetRequiredService<IEstadoSincronizacion>().ObtenerAsync();
        Assert.Equal(antes + 1, estado.DocumentosPendientes);
        Assert.False(estado.CentralConfigurado);
        Assert.False(estado.EnLinea);
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
    private sealed class CajaEnPruebas : IAsyncDisposable
    {
        private readonly BaseDatosPruebas _baseDatos;
        private ServiceProvider _proveedor = null!;

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

        public static async Task<CajaEnPruebas> CrearAsync(BaseDatosPruebas baseDatos, Guid empresa, bool abrirTurno = true)
        {
            var caja = new CajaEnPruebas(baseDatos, await EscenarioSeguridad.CrearAsync(baseDatos, empresa));
            caja.PrepararProveedor();

            // Los maestros se cargan con el mismo reloj de la prueba, para que los precios ya estén vigentes.
            await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(caja.Catalogo.Paquete(), "Pruebas"));

            caja.Cajero = await caja.IngresarAsync(caja.Escenario.CodigoCajero, EscenarioSeguridad.PinCajero);
            caja.CajeroDos = await caja.IngresarAsync(caja.Escenario.CodigoCajeroDos, EscenarioSeguridad.PinCajeroDos);

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

        public async Task<DatosVenta> AgregarAsync(Guid ventaId, string codigo)
        {
            var respuesta = await EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.AgregarArticuloAsync(Cajero, ventaId, codigo, null));
            Assert.True(respuesta.Exitosa, $"{codigo}: {respuesta.Mensaje}");
            return respuesta.Venta!;
        }

        public Task<ResultadoCargaMaestros> CargarOfertasAsync(params PromocionCarga[] promociones) =>
            EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(Promociones: promociones), "Pruebas"));

        public async Task<Guid> AutorizarAsync(string permiso, string motivo)
        {
            var resultado = await EscenarioSeguridad.AutorizarAsync(_proveedor, new SolicitudAutorizacionSupervisor(
                Cajero, permiso, motivo, new CredencialUsuario.Pin(Escenario.CodigoSupervisor, EscenarioSeguridad.PinSupervisor)));
            Assert.True(resultado.Concedida, resultado.Motivo?.ToString());
            return resultado.AutorizacionId!.Value;
        }

        public async ValueTask DisposeAsync() => await _proveedor.DisposeAsync();

        private void PrepararProveedor()
        {
            (_proveedor, var reloj) = Escenario.CrearProveedor(Escenario.CajaUno);
            Reloj = reloj;
        }

        private async Task<SesionUsuario> IngresarAsync(string codigo, string pin)
        {
            var resultado = await EscenarioSeguridad.IngresarAsync(_proveedor, new CredencialUsuario.Pin(codigo, pin));
            Assert.True(resultado.Exitoso, resultado.Motivo?.ToString());
            return resultado.Sesion!;
        }
    }
}
