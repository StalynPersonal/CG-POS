using CgPos.Contratos.Catalogo;
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
        Assert.Equal(250_000m, grande.MontoIdentificacion); // el parámetro de la caja de prueba

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

    [SkippableFact]
    public async Task Cobro_en_efectivo_guarda_pagos_mensaje_para_el_central_y_ticket_y_empieza_otra_venta()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var cobro = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1000m)], null));

        Assert.True(cobro.Exitosa, cobro.Mensaje);
        Assert.Equal(150m, cobro.Cobro!.Devuelta);
        Assert.True(cobro.Cobro.Impreso);
        Assert.True(cobro.Cobro.GavetaAbierta);
        Assert.Equal(EstadoVenta.Cobrada, cobro.Venta!.Estado);
        Assert.NotEqual(venta.Id, cobro.NuevaVenta!.Id);
        Assert.Empty(cobro.NuevaVenta.Lineas);

        var mensajes = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.BandejaSalida.CountAsync(m => m.TipoMensaje == "Venta.Cobrada" && m.AgregadoId == venta.Id));
        Assert.Equal(1, mensajes);

        var ticket = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*ticket-{venta.NumeroTransaccion}.txt")));
        Assert.Contains("DEVUELTA", ticket);
        Assert.Contains("150.00", ticket);
        Assert.Contains(venta.NumeroTransaccion, ticket);

        var repetido = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 1000m)], null));
        Assert.Equal(CodigoResultadoVenta.VentaNoEditable, repetido.Resultado);
    }

    [SkippableFact]
    public async Task Tarjeta_pasa_por_el_terminal_se_aplica_una_vez_y_la_ultima_se_puede_anular()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);

        var primera = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s => s.CobrarConTerminalAsync(caja.Cajero, venta.Id, 850m));
        Assert.True(primera.Exitosa, primera.Mensaje);

        var anulada = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s => s.AnularUltimaOperacionAsync(caja.Cajero, venta.Id));
        Assert.True(anulada.Exitosa, anulada.Mensaje);

        var conAnulada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaTarjeta, 850m, OperacionTerminalId: primera.Operacion!.Id)], null));
        Assert.Equal(CodigoResultadoVenta.OperacionTerminalInvalida, conAnulada.Resultado);

        var segunda = await caja.EjecutarAsync<IServicioCobro, RespuestaOperacionTerminal>(s => s.CobrarConTerminalAsync(caja.Cajero, venta.Id, 850m));
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s =>
            s.CobrarAsync(caja.Cajero, venta.Id,
                [new SolicitudPago(caja.Catalogo.FormaTarjeta, 850m, TipoTarjetaId: caja.Catalogo.TipoTarjeta, OperacionTerminalId: segunda.Operacion!.Id)], null));

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
        SolicitudPago[] pagos = [new SolicitudPago(caja.Catalogo.FormaTarjeta, 850m, Referencia: "MAN-778", UltimosDigitos: "1111", AprobacionManual: true)];

        var sinPermiso = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinPermiso.Resultado);
        Assert.Equal(CatalogoPermisos.AprobacionManualTarjeta, sinPermiso.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.AprobacionManualTarjeta, "Pasarela sin conexión");
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, autorizacion));
        Assert.True(cobrada.Exitosa, cobrada.Mensaje);

        var paraConciliar = await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto =>
            contexto.Set<PagoVenta>().Where(p => p.VentaId == venta.Id).Select(p => p.ParaConciliar).SingleAsync());
        Assert.True(paraConciliar);
    }

    [SkippableFact]
    public async Task Dolares_se_cobran_a_la_tasa_del_dia_con_devuelta_en_pesos()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var formaDolares = Guid.CreateVersion7();
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(
            FormasPago: [new FormaPagoCarga(formaDolares, $"USD{caja.Catalogo.Sufijo}", "Dólares", TipoFormaPago.MonedaExtranjera, 5, "USD")],
            TasasCambio: [new TasaCambioCarga(Guid.CreateVersion7(), "USD", 60.25m, EscenarioSeguridad.Inicio.AddDays(-1))]), "Pruebas"));

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
        Assert.Equal(355m, cobrada.Cobro!.Devuelta);
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
            contexto.BandejaSalida.Where(m => m.AgregadoId == primera.Venta.Id).Select(m => m.Contenido).SingleAsync());
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

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.RetiroEfectivo, "Remesa a bóveda");
        var retiro = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RetirarEfectivoAsync(caja.Cajero, 100m, "Remesa a bóveda", autorizacion));
        Assert.True(retiro.Exitosa, retiro.Mensaje);
        Assert.Equal(1, retiro.Movimiento!.Numero);

        // Cierre ciego: el cajero no ve lo esperado.
        var resumen = (await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.ObtenerResumenAsync(caja.Cajero))).Resumen!;
        Assert.True(resumen.CierreCiego);
        Assert.False(resumen.MuestraEsperado);
        Assert.All(resumen.FormasPago, forma => Assert.Null(forma.Esperado));
        Assert.Empty(resumen.Bloqueos);

        // Declara RD$5 menos del efectivo esperado: lo cobrado menos la devuelta menos el retiro.
        var efectivoEsperado = total - 100m;
        var respuesta = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero,
            [new SolicitudDeclaracionFormaPago(caja.Catalogo.FormaEfectivo, efectivoEsperado - 5m)], [], null));

        Assert.True(respuesta.Exitosa, respuesta.Mensaje);
        var cierre = respuesta.Cierre!;
        var efectivo = cierre.FormasPago.Single(f => f.FormaPagoId == caja.Catalogo.FormaEfectivo);
        Assert.Equal(efectivoEsperado, efectivo.Esperado);
        Assert.Equal(-5m, efectivo.Diferencia);
        Assert.Equal(-5m, cierre.Diferencia);
        Assert.Equal(1, cierre.CantidadVentas);
        Assert.Equal(100m, cierre.TotalRetiros);
        Assert.Single(cierre.Movimientos);

        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.Null(estado.TurnoAbierto);

        var retiroId = retiro.Movimiento.Id;
        var mensajes = await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.BandejaSalida.CountAsync(m =>
            (m.TipoMensaje == "Caja.TurnoCerrado" && m.AgregadoId == cierre.Id) || (m.TipoMensaje == "Caja.RetiroEfectivo" && m.AgregadoId == retiroId)));
        Assert.Equal(2, mensajes);

        var reportes = Directory.GetFiles(baseDatos.CarpetaImpresiones, "*cierre-*.txt").Select(File.ReadAllText);
        Assert.Contains(reportes, texto => texto.Contains("FALTANTE RD$") && texto.Contains("CUADRE POR FORMA DE PAGO"));
    }

    [SkippableFact]
    public async Task No_se_cierra_el_turno_con_facturas_en_espera_y_se_listan()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var espera = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.PonerEnEsperaAsync(caja.Cajero, venta.Id));
        Assert.True(espera.Exitosa, espera.Mensaje);

        var cierre = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.Cajero, [], [], null));

        Assert.Equal(CodigoResultadoCaja.CierreBloqueado, cierre.Resultado);
        Assert.Contains(cierre.Bloqueos!, bloqueo => bloqueo.Contains(venta.NumeroTransaccion));
        var estado = await caja.EjecutarAsync<IServicioTurnos, DatosEstadoTurno>(s => s.ObtenerEstadoAsync(caja.Cajero));
        Assert.NotNull(estado.TurnoAbierto);
    }

    [SkippableFact]
    public async Task Relevo_con_autorizacion_pasa_el_turno_y_la_reapertura_del_cierre_queda_auditada()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);

        var sinAutorizacion = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RelevarAsync(caja.CajeroDos, null));
        Assert.Equal(CodigoResultadoCaja.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.RelevoCajero, sinAutorizacion.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.RelevoCajero, "Almuerzo", caja.CajeroDos);
        var relevo = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.RelevarAsync(caja.CajeroDos, autorizacion));
        Assert.True(relevo.Exitosa, relevo.Mensaje);
        Assert.Equal(caja.Escenario.CajeroDos, relevo.Turno!.UsuarioActualId);

        var cajeroOriginal = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.ObtenerActualAsync(caja.Cajero));
        Assert.Equal(CodigoResultadoVenta.TurnoDeOtroUsuario, cajeroOriginal.Resultado);

        var cierre = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.CerrarAsync(caja.CajeroDos, [], [], null));
        Assert.True(cierre.Exitosa, cierre.Mensaje);
        var cierreId = cierre.Cierre!.Id;
        Assert.Equal(0m, cierre.Cierre.Diferencia);

        var sinMotivo = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.ReabrirCierreAsync(caja.CajeroDos, cierreId, null, null));
        Assert.Equal(CodigoResultadoCaja.MotivoRequerido, sinMotivo.Resultado);

        const string Motivo = "Faltó declarar un voucher";
        var autorizacionReapertura = await caja.AutorizarAsync(CatalogoPermisos.ReabrirCierre, Motivo, caja.CajeroDos);
        var reapertura = await caja.EjecutarAsync<IServicioCaja, RespuestaCaja>(s => s.ReabrirCierreAsync(caja.CajeroDos, cierreId, Motivo, autorizacionReapertura));
        Assert.True(reapertura.Exitosa, reapertura.Mensaje);
        Assert.Equal(EstadoTurno.Abierto, reapertura.Turno!.Estado);

        var cierres = await caja.EjecutarAsync<IServicioCaja, IReadOnlyList<DatosCierre>>(s => s.ListarCierresAsync(caja.CajeroDos));
        Assert.Equal(EstadoCierre.Reabierto, Assert.Single(cierres).Estado);

        var turnoId = reapertura.Turno.Id.ToString();
        Assert.Equal(1, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto =>
            contexto.Auditoria.CountAsync(registro => registro.Accion == "Caja.CierreReabierto" && registro.EntidadId == turnoId)));
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

        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, factura.NumeroTransaccion));
        Assert.True(buscada.Exitosa, buscada.Mensaje);
        var linea = Assert.Single(buscada.Factura!.Lineas);
        Assert.Equal(1m, linea.CantidadDisponible);
        Assert.False(buscada.Factura.RetieneImpuesto);

        // La factura no tiene cliente: se pide el RNC; la devolución la autoriza el encargado.
        var solicitud = new SolicitudDevolucion(factura.Id, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], "401007551", "Cliente Devolución",
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
            contexto.DocumentosElectronicos.Where(d => d.VentaId == nota.Id).Select(d => d.RutaXml).SingleAsync());
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
            [new SolicitudPago(caja.Catalogo.FormaNotaCredito, mitad, encfNota), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1000m)], null));
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
            [new SolicitudPago(caja.Catalogo.FormaPuntos, EscenarioCatalogo.SaldoMiembro + 1m), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1000m)], null));
        Assert.Equal(CodigoResultadoVenta.PagoInvalido, excedido.Resultado);
        Assert.Contains("puntos disponibles", excedido.Mensaje);

        SolicitudPago[] pagos = [new SolicitudPago(caja.Catalogo.FormaPuntos, 100m), new SolicitudPago(caja.Catalogo.FormaEfectivo, 1000m)];
        var sinAutorizacion = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, null));
        Assert.Equal(CodigoResultadoVenta.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.CanjearPuntos, sinAutorizacion.PermisoRequerido);

        var autorizacion = await caja.AutorizarAsync(CatalogoPermisos.CanjearPuntos, "Canje del cliente");
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, pagos, autorizacion));
        Assert.True(cobrada.Exitosa, cobrada.Mensaje);

        // Cincel de 850 en ferretería: 2 puntos por cada 100 = 17, × 1.5 del nivel Oro, sobre lo no pagado con puntos (750 de 850) = 22.
        var fidelidad = cobrada.Venta!.Fidelidad!;
        Assert.Equal(22, fidelidad.PuntosAcumulados);
        Assert.Equal(100, fidelidad.PuntosCanjeados);
        Assert.Equal(2, await caja.EjecutarAsync<ContextoDatosPos, int>(contexto => contexto.MovimientosPuntos.CountAsync(m => m.VentaId == venta.Id)));

        var saldo = await caja.EjecutarAsync<IServicioFidelidad, RespuestaFidelidad>(s => s.ConsultarAsync(caja.Cajero, caja.Catalogo.CedulaMiembro));
        Assert.True(saldo.Exitosa, saldo.Mensaje);
        Assert.Equal(EscenarioCatalogo.SaldoMiembro - 100 + 22, saldo.Miembro!.SaldoDisponible);

        // La nota de crédito reversa los puntos acumulados en la compra (RF-244, RN-21).
        var buscada = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, cobrada.Venta.NumeroTransaccion));
        var linea = Assert.Single(buscada.Factura!.Lineas);
        var autorizacionDevolucion = await caja.AutorizarAsync(CatalogoPermisos.AutorizarDevolucion, "Artículo defectuoso");
        var devolucion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero,
            new SolicitudDevolucion(cobrada.Venta.Id, [new SolicitudLineaDevolucion(linea.NumeroLinea, 1m)], "401007551", "Cliente Devolución",
                caja.Catalogo.CodigoMotivoDevolucion, null, autorizacionDevolucion)));
        Assert.True(devolucion.Exitosa, devolucion.Mensaje);
        Assert.Equal(22, devolucion.NotaCredito!.PuntosReversados);

        var despues = await caja.EjecutarAsync<IServicioFidelidad, RespuestaFidelidad>(s => s.ConsultarAsync(caja.Cajero, caja.Catalogo.CedulaMiembro));
        Assert.Equal(EscenarioCatalogo.SaldoMiembro - 100, despues.Miembro!.SaldoDisponible);
    }

    [SkippableFact]
    public async Task Retiro_y_envio_con_autorizacion_generan_pendientes_con_voucher_y_bloquean_la_devolucion_de_lo_no_entregado()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var almacen = Guid.CreateVersion7();
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(
            Almacenes: [new AlmacenCarga(almacen, $"ALM{caja.Catalogo.Sufijo}", $"Almacén Kennedy {caja.Catalogo.Sufijo}", caja.Escenario.Sucursal)]), "Pruebas"));

        var almacenes = await caja.EjecutarAsync<IServicioVentas, IReadOnlyList<DatosAlmacen>>(s => s.ListarAlmacenesAsync(caja.Cajero));
        Assert.True(almacenes.Single(a => a.Id == almacen).EsDeLaSucursal);

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
        var retiro = new SolicitudMarcarEntrega(MetodoEntrega.RetiroAlmacen, almacen, null, hoy.AddDays(2), "Retira el jueves", [new CantidadEntrega(cemento, 2m)]);
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
            contexto.PendientesEntrega.AsNoTracking().Include(p => p.Lineas).Where(p => p.VentaId == venta.Id).ToListAsync());
        Assert.Equal(2m, pendientes.Single(p => p.Metodo == MetodoEntrega.RetiroAlmacen).CantidadPorEntregar(cemento));
        Assert.Equal("809-555-1111", pendientes.Single(p => p.Metodo == MetodoEntrega.Envio).Telefono);

        // Lo pendiente de entrega no se devuelve (RF-233): solo el cemento que salió en caja.
        var factura = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, cobrada.Venta!.NumeroTransaccion));
        Assert.Equal(1m, factura.Factura!.Lineas.Single(l => l.NumeroLinea == cemento).CantidadDisponible);
        var devolucion = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaDevolucion>(s => s.RegistrarAsync(caja.Cajero,
            new SolicitudDevolucion(venta.Id, [new SolicitudLineaDevolucion(cemento, 3m)], "401007551", "Cliente Devolución", caja.Catalogo.CodigoMotivoDevolucion, null, null)));
        Assert.Equal(CodigoResultadoDevolucion.DevolucionInvalida, devolucion.Resultado);
        Assert.Contains("pendientes de entrega", devolucion.Mensaje);
    }

    [SkippableFact]
    public async Task Despacho_prepara_entrega_por_partes_con_constancia_y_anula_con_autorizacion()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        await using var caja = await CajaEnPruebas.CrearAsync(baseDatos, Empresa);
        var almacen = Guid.CreateVersion7();
        await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(
            Almacenes: [new AlmacenCarga(almacen, $"DSP{caja.Catalogo.Sufijo}", $"Almacén despacho {caja.Catalogo.Sufijo}", caja.Escenario.Sucursal)]), "Pruebas"));

        // Factura con cemento y taladro para retiro y un cincel para envío.
        var venta = await caja.VentaActualAsync();
        await caja.AgregarAsync(venta.Id, $"3*{caja.Catalogo.BarrasCemento}");
        await caja.AgregarAsync(venta.Id, caja.Catalogo.BarrasCincel);
        var conTaladro = await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s =>
            s.AgregarArticuloAsync(caja.Cajero, venta.Id, caja.Catalogo.CodigoTaladro, null, serialEnDespacho: true));
        int Linea(string codigo) => conTaladro.Venta!.Lineas.Single(l => l.CodigoInterno == codigo).NumeroLinea;
        var cemento = Linea(caja.Catalogo.CodigoCemento);
        var cincel = Linea(caja.Catalogo.CodigoCincel);
        var taladro = Linea(caja.Catalogo.CodigoTaladro);

        var autorizacionRetiro = await caja.AutorizarAsync(CatalogoPermisos.MarcarPendiente, "Retira en almacén");
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.MarcarEntregaAsync(caja.Cajero, venta.Id, new SolicitudMarcarEntrega(
            MetodoEntrega.RetiroAlmacen, almacen, null, null, null, [new CantidadEntrega(cemento, 3m), new CantidadEntrega(taladro, 1m)], autorizacionRetiro)))).Exitosa);
        var autorizacionEnvio = await caja.AutorizarAsync(CatalogoPermisos.MarcarPendiente, "Envío");
        Assert.True((await caja.EjecutarAsync<IServicioVentas, RespuestaVenta>(s => s.MarcarEntregaAsync(caja.Cajero, venta.Id, new SolicitudMarcarEntrega(
            MetodoEntrega.Envio, null, new DatosEnvio("Calle 3", null, null, null, "809-555-2222", null, null), null, null, [new CantidadEntrega(cincel, 1m)],
            autorizacionEnvio)))).Exitosa);
        var cobrada = await caja.EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(caja.Cajero, venta.Id, [new SolicitudPago(caja.Catalogo.FormaEfectivo, 10_000m)], null));
        Assert.True(cobrada.Exitosa, cobrada.Mensaje);

        // Se llama por la factura (varios pendientes) o por el voucher (RF-251).
        var porFactura = await caja.EjecutarAsync<IServicioDespacho, RespuestaBusquedaPendientes>(s => s.BuscarAsync(caja.Cajero, cobrada.Venta!.NumeroTransaccion));
        Assert.Equal(2, porFactura.Pendientes.Count);
        var retiro = porFactura.Pendientes.Single(p => p.Metodo == MetodoEntrega.RetiroAlmacen);
        var envio = porFactura.Pendientes.Single(p => p.Metodo == MetodoEntrega.Envio);
        var porVoucher = await caja.EjecutarAsync<IServicioDespacho, RespuestaBusquedaPendientes>(s => s.BuscarAsync(caja.Cajero, retiro.Numero.ToLowerInvariant()));
        Assert.Equal(retiro.Id, Assert.Single(porVoucher.Pendientes).Id);
        var abiertos = await caja.EjecutarAsync<IServicioDespacho, IReadOnlyList<DatosPendienteEntrega>>(s => s.ListarAbiertosAsync(caja.Cajero));
        Assert.Contains(abiertos, p => p.Id == retiro.Id);

        // Preparación: un retiro no pasa a despachado (eso es de envíos).
        var preparacion = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s =>
            s.CambiarEstadoAsync(caja.Cajero, retiro.Id, new SolicitudEstadoPendiente(EstadoPendiente.EnPreparacion)));
        Assert.True(preparacion.Exitosa, preparacion.Mensaje);
        var despachado = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s =>
            s.CambiarEstadoAsync(caja.Cajero, retiro.Id, new SolicitudEstadoPendiente(EstadoPendiente.Despachado)));
        Assert.Equal(CodigoResultadoPendiente.OperacionInvalida, despachado.Resultado);

        // Entrega parcial con quien retira e impresión de la constancia (RF-253, RF-254); el taladro exige su serial.
        var sinSerial = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s => s.EntregarAsync(caja.Cajero, retiro.Id,
            new SolicitudEntregaPendiente([new CantidadEntregada(taladro, 1m)], "Juan Pérez", "00113918205")));
        Assert.Equal(CodigoResultadoPendiente.OperacionInvalida, sinSerial.Resultado);
        Assert.Contains("serial", sinSerial.Mensaje);

        var parcial = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s => s.EntregarAsync(caja.Cajero, retiro.Id,
            new SolicitudEntregaPendiente([new CantidadEntregada(cemento, 2m)], "Juan Pérez", "00113918205")));
        Assert.True(parcial.Exitosa, parcial.Mensaje);
        Assert.Equal(EstadoPendiente.Parcial, parcial.Pendiente!.Estado);
        Assert.NotEmpty(Directory.GetFiles(baseDatos.CarpetaImpresiones, $"*constancia-{retiro.Numero}-1.txt"));

        // Con entregas ya no se anula (RF-255).
        var anularRetiro = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s => s.AnularAsync(caja.Cajero, retiro.Id, new SolicitudAnularPendiente("Cambio de idea")));
        Assert.Equal(CodigoResultadoPendiente.OperacionInvalida, anularRetiro.Resultado);

        var completa = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s => s.EntregarAsync(caja.Cajero, retiro.Id,
            new SolicitudEntregaPendiente([new CantidadEntregada(cemento, 1m), new CantidadEntregada(taladro, 1m, "sn-500")], "Juan Pérez", "00113918205")));
        Assert.True(completa.Exitosa, completa.Mensaje);
        Assert.Equal(EstadoPendiente.Entregado, completa.Pendiente!.Estado);
        Assert.Equal("SN-500", completa.Pendiente.Lineas.Single(l => l.NumeroLineaVenta == taladro).Serial);

        // Entregado, el cemento vuelve a estar disponible para devolución.
        var factura = await caja.EjecutarAsync<IServicioDevoluciones, RespuestaFacturaDevolucion>(s => s.BuscarFacturaAsync(caja.Cajero, cobrada.Venta!.NumeroTransaccion));
        Assert.Equal(3m, factura.Factura!.Lineas.Single(l => l.NumeroLinea == cemento).CantidadDisponible);

        // El envío sin entregas se anula con clave de supervisor y libera la mercancía.
        var sinAutorizacion = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s =>
            s.AnularAsync(caja.Cajero, envio.Id, new SolicitudAnularPendiente("Cliente canceló el envío")));
        Assert.Equal(CodigoResultadoPendiente.RequiereAutorizacion, sinAutorizacion.Resultado);
        Assert.Equal(CatalogoPermisos.AnularPendiente, sinAutorizacion.PermisoRequerido);

        var autorizacionAnular = await caja.AutorizarAsync(CatalogoPermisos.AnularPendiente, "Cliente canceló el envío");
        var anulado = await caja.EjecutarAsync<IServicioDespacho, RespuestaPendiente>(s =>
            s.AnularAsync(caja.Cajero, envio.Id, new SolicitudAnularPendiente("Cliente canceló el envío", autorizacionAnular)));
        Assert.True(anulado.Exitosa, anulado.Mensaje);
        Assert.Equal(EstadoPendiente.Anulado, anulado.Pendiente!.Estado);

        var despues = await caja.EjecutarAsync<IServicioDespacho, IReadOnlyList<DatosPendienteEntrega>>(s => s.ListarAbiertosAsync(caja.Cajero));
        Assert.DoesNotContain(despues, p => p.VentaId == venta.Id);
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
                 await contexto.BandejaSalida.AsNoTracking().SingleAsync(m => m.AgregadoId == ventaId && m.TipoMensaje == "Venta.Cobrada")));

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
        var familia = new FamiliaCarga(Guid.CreateVersion7(), $"DESC{escenario.Sufijo}", "Familia bajada del Central");
        var clave = $"Pruebas.Descarga{escenario.Sufijo}";
        var organizacion = new CgPos.Contratos.CargaInicial.PaqueteCargaInicial(
            new CgPos.Contratos.CargaInicial.EmpresaCarga(Empresa, "999000003", "Empresa Seguridad SRL", Direccion: "Calle de prueba 1, Santo Domingo"),
            Parametros: [new CgPos.Contratos.CargaInicial.ParametroCarga(Guid.CreateVersion7(), clave, "valor del Central", CajaId: escenario.CajaUno)]);

        var aplicada = await DescargarAsync(new PaqueteBajadaMaestros(desde, desde + 500, organizacion, new PaqueteMaestros(Familias: [familia])));
        Assert.True(aplicada.Descargado, aplicada.Error);
        Assert.Equal(desde + 500, await MarcaAsync());
        Assert.True(await caja.EjecutarAsync<ContextoDatosPos, bool>(contexto => contexto.Familias.AnyAsync(f => f.Id == familia.Id)));
        Assert.Equal("valor del Central", await caja.EjecutarAsync<CgPos.Pos.Aplicacion.Organizacion.IParametros, string?>(p => p.ObtenerAsync(clave, escenario.CajaUno)));

        // Un paquete que la caja no puede aplicar no mueve la marca: el próximo ciclo lo vuelve a pedir.
        var articuloSinFamilia = new ArticuloCarga(Guid.CreateVersion7(), $"SINFAM{escenario.Sufijo}", "Artículo sin familia", Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), 100m);
        var rechazada = await DescargarAsync(new PaqueteBajadaMaestros(desde + 500, desde + 900, null, new PaqueteMaestros(Articulos: [articuloSinFamilia])));
        Assert.False(rechazada.Descargado);
        Assert.Contains("familia inexistente", rechazada.Error);
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

        Task<(string RutaXml, Guid MensajeId)> DocumentoAsync(Guid ventaId) =>
            caja.EjecutarAsync<ContextoDatosPos, (string, Guid)>(async contexto =>
                (await contexto.DocumentosElectronicos.AsNoTracking().Where(d => d.VentaId == ventaId).Select(d => d.RutaXml).SingleAsync(),
                 await contexto.BandejaSalida.AsNoTracking().Where(m => m.AgregadoId == ventaId && m.TipoMensaje == "Venta.Cobrada").Select(m => m.Id).SingleAsync()));

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

        public static async Task<CajaEnPruebas> CrearAsync(BaseDatosPruebas baseDatos, Guid empresa, bool abrirTurno = true, bool cargarCertificado = true,
            long hastaSecuenciaConsumo = 1000)
        {
            var caja = new CajaEnPruebas(baseDatos, await EscenarioSeguridad.CrearAsync(baseDatos, empresa)) { _cargarCertificado = cargarCertificado };
            caja.PrepararProveedor();

            // Los maestros se cargan con el mismo reloj de la prueba, para que los precios ya estén vigentes.
            await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(caja.Catalogo.Paquete(), "Pruebas"));

            // Rangos de e-CF de la caja (los asigna el Central). Como en la realidad, cada caja tiene un rango distinto:
            // las pruebas de la clase comparten base y el e-NCF es único.
            var vence = new DateOnly(2027, 12, 31);
            var desde = caja.DesdeSecuencia;
            await caja.EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(SecuenciasEcf:
            [
                new SecuenciaEcfCarga(Guid.CreateVersion7(), caja.Escenario.CajaUno, TipoComprobante.FacturaConsumo, desde, desde + hastaSecuenciaConsumo - 1, vence),
                new SecuenciaEcfCarga(Guid.CreateVersion7(), caja.Escenario.CajaUno, TipoComprobante.FacturaCreditoFiscal, desde, desde + 999, vence),
                new SecuenciaEcfCarga(Guid.CreateVersion7(), caja.Escenario.CajaUno, TipoComprobante.RegimenesEspeciales, desde, desde + 999, vence),
                new SecuenciaEcfCarga(Guid.CreateVersion7(), caja.Escenario.CajaUno, TipoComprobante.Gubernamental, desde, desde + 999, vence),
                new SecuenciaEcfCarga(Guid.CreateVersion7(), caja.Escenario.CajaUno, TipoComprobante.NotaCredito, desde, desde + 999, vence),
            ]), "Pruebas"));

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

        /// <summary>Venta nueva con un cincel cobrada con RD$1,000 en efectivo.</summary>
        public async Task<RespuestaCobro> CobrarCincelEnEfectivoAsync()
        {
            var venta = await VentaActualAsync();
            await AgregarAsync(venta.Id, Catalogo.BarrasCincel);
            return await EjecutarAsync<IServicioCobro, RespuestaCobro>(s => s.CobrarAsync(Cajero, venta.Id, [new SolicitudPago(Catalogo.FormaEfectivo, 1000m)], null));
        }

        public Task<ResultadoCargaMaestros> CargarOfertasAsync(params PromocionCarga[] promociones) =>
            EjecutarAsync<ICargaMaestros, ResultadoCargaMaestros>(s => s.AplicarAsync(new PaqueteMaestros(Promociones: promociones), "Pruebas"));

        public async Task<Guid> AutorizarAsync(string permiso, string motivo, SesionUsuario? solicitante = null)
        {
            var resultado = await EscenarioSeguridad.AutorizarAsync(_proveedor, new SolicitudAutorizacionSupervisor(
                solicitante ?? Cajero, permiso, motivo, new CredencialUsuario.Pin(Escenario.CodigoSupervisor, EscenarioSeguridad.PinSupervisor)));
            Assert.True(resultado.Concedida, resultado.Motivo?.ToString());
            return resultado.AutorizacionId!.Value;
        }

        public async ValueTask DisposeAsync() => await _proveedor.DisposeAsync();

        private static long _siguienteRango;

        private bool _cargarCertificado = true;

        /// <summary>Primer número del rango de e-CF de esta caja de prueba.</summary>
        public long DesdeSecuencia { get; } = Interlocked.Increment(ref _siguienteRango) * 10_000 + 1;

        private void PrepararProveedor()
        {
            (_proveedor, var reloj) = Escenario.CrearProveedor(Escenario.CajaUno);
            Reloj = reloj;

            // El certificado vive en memoria del proveedor, como en el Agente: se carga con su PIN (RF-217).
            if (_cargarCertificado)
                Assert.Null(_proveedor.GetRequiredService<CgPos.Pos.Aplicacion.Ecf.ICertificadoCaja>().Cargar(BaseDatosPruebas.PinCertificado));
        }

        private async Task<SesionUsuario> IngresarAsync(string codigo, string pin)
        {
            var resultado = await EscenarioSeguridad.IngresarAsync(_proveedor, new CredencialUsuario.Pin(codigo, pin));
            Assert.True(resultado.Exitoso, resultado.Motivo?.ToString());
            return resultado.Sesion!;
        }
    }
}
