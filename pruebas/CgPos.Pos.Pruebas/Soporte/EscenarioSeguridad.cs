using CgPos.Contratos.CargaInicial;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Pruebas.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Pos.Pruebas.Soporte;

/// <summary>
/// Sucursal con dos cajas (la segunda deshabilitada), roles cajero/supervisor y usuarios para probar ingreso y autorización.
/// Códigos únicos por escenario (las pruebas de una clase comparten base); los Id son los que les da la caja al cargarlos.
/// </summary>
public sealed class EscenarioSeguridad
{
    public const string ClaveCajero = "Cajero.1111";
    public const string ClaveCajeroDos = "Cajero.4444";
    public const string ClaveSupervisor = "Supervisor.2222";
    public const string ClaveInactivo = "Inactivo.5555";
    public const string ClaveSinCaja = "SinCaja.6666";

    public static readonly DateTimeOffset Inicio = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly BaseDatosPruebas _baseDatos;

    private EscenarioSeguridad(BaseDatosPruebas baseDatos) => _baseDatos = baseDatos;

    public string Sufijo { get; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static int _escenarios;
    private readonly int _numero = Interlocked.Increment(ref _escenarios) - 1;

    /// <summary>Sucursal y cajas con códigos que no se repiten entre escenarios (hasta 99 sucursales con 49 pares de cajas cada una).</summary>
    public string CodigoSucursal => DosDigitos(_numero % 99 + 1);
    public string CodigoCajaUno => DosDigitos(_numero / 99 * 2 + 1);
    public string CodigoCajaDos => DosDigitos(_numero / 99 * 2 + 2);

    private static string DosDigitos(int numero) => numero.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    public string CodigoRolCajero => $"CAJ{Sufijo}";
    public string CodigoRolSupervisor => $"SUP{Sufijo}";

    public int Sucursal { get; private set; }
    public int CajaUno { get; private set; }
    public int CajaDos { get; private set; }
    public int RolCajero { get; private set; }
    public int RolSupervisor { get; private set; }
    public int Cajero { get; private set; }
    public int CajeroDos { get; private set; }
    public int Supervisor { get; private set; }
    public int Inactivo { get; private set; }
    public int SinCaja { get; private set; }

    public string CodigoCajero => $"C{Sufijo}";
    public string CodigoCajeroDos => $"K{Sufijo}";
    public string CodigoSupervisor => $"S{Sufijo}";
    public string CodigoInactivo => $"I{Sufijo}";
    public string CodigoSinCaja => $"N{Sufijo}";

    public static async Task<EscenarioSeguridad> CrearAsync(BaseDatosPruebas baseDatos, int empresaId)
    {
        var escenario = new EscenarioSeguridad(baseDatos);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarAsync(escenario.CrearPaquete(empresaId));
        await escenario.ResolverIdsAsync(ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>());

        return escenario;
    }

    /// <summary>Los Id que la caja les dio a la sucursal, las cajas, los roles y los usuarios del escenario.</summary>
    private async Task ResolverIdsAsync(ContextoDatosPos contexto)
    {
        Sucursal = await contexto.Sucursales.Where(s => s.Codigo == CodigoSucursal).Select(s => s.Id).SingleAsync();
        CajaUno = await contexto.Cajas.Where(c => c.SucursalId == Sucursal && c.Codigo == CodigoCajaUno).Select(c => c.Id).SingleAsync();
        CajaDos = await contexto.Cajas.Where(c => c.SucursalId == Sucursal && c.Codigo == CodigoCajaDos).Select(c => c.Id).SingleAsync();

        var roles = await contexto.Roles.Where(r => r.Codigo == CodigoRolCajero || r.Codigo == CodigoRolSupervisor).ToDictionaryAsync(r => r.Codigo, r => r.Id);
        (RolCajero, RolSupervisor) = (roles[CodigoRolCajero], roles[CodigoRolSupervisor]);

        var codigos = new[] { CodigoCajero, CodigoCajeroDos, CodigoSupervisor, CodigoInactivo, CodigoSinCaja };
        var usuarios = await contexto.Usuarios.Where(u => codigos.Contains(u.Codigo)).ToDictionaryAsync(u => u.Codigo, u => u.Id);
        (Cajero, CajeroDos, Supervisor, Inactivo, SinCaja) =
            (usuarios[CodigoCajero], usuarios[CodigoCajeroDos], usuarios[CodigoSupervisor], usuarios[CodigoInactivo], usuarios[CodigoSinCaja]);
    }

    /// <summary>Proveedor con reloj controlable y la caja indicada como caja actual.</summary>
    public (ServiceProvider Proveedor, RelojPrueba Reloj) CrearProveedor(int? cajaId, Action<IServiceCollection>? extras = null)
    {
        var reloj = new RelojPrueba(Inicio);
        var proveedor = _baseDatos.CrearProveedor(servicios =>
        {
            servicios.AddSingleton<TimeProvider>(reloj);
            var codigos = cajaId == CajaUno ? ((string?)CodigoSucursal, (string?)CodigoCajaUno) : cajaId == CajaDos ? (CodigoSucursal, CodigoCajaDos) : (null, null);
            servicios.AddSingleton<IContextoCaja>(new ContextoCajaFijo(cajaId, codigos.Item1, codigos.Item2));
            extras?.Invoke(servicios);
        });

        return (proveedor, reloj);
    }

    public static async Task<ResultadoAutenticacion> IngresarAsync(IServiceProvider proveedor, CredencialUsuario credencial)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IServicioAutenticacion>().IngresarAsync(credencial);
    }

    public static async Task<ResultadoAutorizacion> AutorizarAsync(IServiceProvider proveedor, SolicitudAutorizacionSupervisor solicitud)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IServicioAutorizacion>().AutorizarAsync(solicitud);
    }

    public static async Task<int> ContarAuditoriaAsync(IServiceProvider proveedor, string accion, int usuarioId)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var idTexto = usuarioId.ToString();
        return await contexto.Auditoria.CountAsync(registro => registro.Accion == accion && (registro.EntidadId == idTexto || registro.UsuarioId == usuarioId));
    }

    private CajaReferencia Uno => new(CodigoSucursal, CodigoCajaUno);

    private CajaReferencia Dos => new(CodigoSucursal, CodigoCajaDos);

    private PaqueteCargaInicial CrearPaquete(int empresaId) =>
        new(
            new EmpresaCarga("999000004", "Empresa Seguridad SRL", Direccion: "Calle de prueba 1, Santo Domingo"),
            Sucursales: [new SucursalCarga(CodigoSucursal, "Sucursal de seguridad")],
            Cajas:
            [
                new CajaCarga(CodigoSucursal, CodigoCajaUno, $"Caja {CodigoCajaUno}", DireccionIp: "10.12.1.101"),
                new CajaCarga(CodigoSucursal, CodigoCajaDos, $"Caja {CodigoCajaDos}", Habilitada: false, DireccionIp: "10.12.1.102"),
            ],
            Roles:
            [
                new RolCarga(CodigoRolCajero, "Cajero", 1,
                    [CatalogoPermisos.RegistrarVenta, CatalogoPermisos.AbrirTurno, CatalogoPermisos.CerrarTurno, CatalogoPermisos.DespacharPendiente]),
                new RolCarga(CodigoRolSupervisor, "Supervisor", 2,
                    [CatalogoPermisos.AutorizarOperaciones, CatalogoPermisos.EliminarLinea, CatalogoPermisos.LimpiarPantalla, CatalogoPermisos.AnularVenta,
                     CatalogoPermisos.RegistrarVenta, CatalogoPermisos.CambiarComprobante, CatalogoPermisos.SuspenderVenta,
                     CatalogoPermisos.FacturarCotizacionVencida,
                     CatalogoPermisos.DescuentoLinea, CatalogoPermisos.DescuentoFactura, CatalogoPermisos.DesactivarPromocion,
                     CatalogoPermisos.AprobacionManualTarjeta, CatalogoPermisos.AbrirGaveta, CatalogoPermisos.CerrarTurno,
                     CatalogoPermisos.RetiroEfectivo, CatalogoPermisos.RelevoCajero, CatalogoPermisos.PreCierre,
                     CatalogoPermisos.AutorizarDevolucion, CatalogoPermisos.AutorizarNotaCreditoInterna, CatalogoPermisos.CanjearPuntos,
                     CatalogoPermisos.MarcarPendiente,
                     CatalogoPermisos.DespacharPendiente, CatalogoPermisos.AnularPendiente]),
            ],
            Usuarios:
            [
                new UsuarioCarga(CodigoCajero, "Cajero Seguridad", CodigoRolCajero, [Uno, Dos], Clave: ClaveCajero),
                new UsuarioCarga(CodigoCajeroDos, "Cajero Dos", CodigoRolCajero, [Uno], Clave: ClaveCajeroDos),
                new UsuarioCarga(CodigoSupervisor, "Supervisor Seguridad", CodigoRolSupervisor, [], Clave: ClaveSupervisor),
                new UsuarioCarga(CodigoInactivo, "Usuario Inactivo", CodigoRolCajero, [Uno], Clave: ClaveInactivo, Activo: false),
                new UsuarioCarga(CodigoSinCaja, "Usuario Sin Caja", CodigoRolCajero, [], Clave: ClaveSinCaja),
            ],
            Parametros:
            [
                new ParametroCarga(ClavesParametros.MonedaLocal, "DOP", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.ValorPuntoFidelidad, "1", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.DiasRetencionXmlEnviados, "30", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.DiasRetencionMensajesConfirmados, "30", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.AlertaTamanoBaseDatosMb, "1", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.HorasAlertaPendientes, "24", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.ToleranciaRelojSegundos, "5", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.MesesVigenciaPuntos, "12", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.MaximoPuntosCanjeSinConexion, "5000", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.IntentosMaximosClave, "3", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.MinutosBloqueo, "5", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.MinutosVigenciaAutorizacion, "5", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.HorasSesion, "12", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.TipoIngresos, "1", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(CgPos.Dominio.Organizacion.CatalogoParametros.DigitosSecuenciaDocumentos, "7", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.UrlConsultaTimbre, "https://ecf.dgii.gov.do/testecf/consultatimbre", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.UrlConsultaTimbreConsumo, "https://fc.dgii.gov.do/testecf/ConsultaTimbreFC", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),

                // Reglas de negocio de la caja de prueba: en producción las configura un usuario en el Central.
                new ParametroCarga(ClavesParametros.MontoIdentificacionConsumo, "250000", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.PasoRedondeoEfectivo, "0", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.CierreCiego, "true", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.FondoEnCuadre, "false", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.DiasRetencionImpuestoDevolucion, "30", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.DiasVigenciaNotaCredito, "180", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.PorcentajeAlertaSecuenciaEcf, "10", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.DiasAlertaCertificado, "30", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.BalanzaPrefijoPeso, "21", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.BalanzaPrefijoPrecio, "22", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.BalanzaDigitosCodigoArticulo, "5", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.BalanzaDigitosValor, "5", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.BalanzaDecimalesPeso, "3", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
                new ParametroCarga(ClavesParametros.BalanzaDecimalesPrecio, "2", SucursalCodigo: CodigoSucursal, CajaCodigo: CodigoCajaUno),
            ]);
}
