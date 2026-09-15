using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Ventas;
using CgPos.Dominio.Catalogo;
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
