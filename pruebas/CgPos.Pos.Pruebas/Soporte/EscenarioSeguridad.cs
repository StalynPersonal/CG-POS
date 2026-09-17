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
/// Sucursal con dos cajas (la 02 deshabilitada), roles cajero/supervisor y usuarios para probar ingreso y autorización.
/// Códigos e Ids únicos por escenario; la empresa la fija cada clase de pruebas (una caja admite una sola empresa).
/// </summary>
public sealed class EscenarioSeguridad
{
    public const string PinCajero = "1111";
    public const string PinCajeroDos = "4444";
    public const string PinSupervisor = "2222";
    public const string PinInactivo = "5555";
    public const string PinSinCaja = "6666";

    public static readonly DateTimeOffset Inicio = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly BaseDatosPruebas _baseDatos;

    private EscenarioSeguridad(BaseDatosPruebas baseDatos) => _baseDatos = baseDatos;

    public string Sufijo { get; } = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    public Guid Sucursal { get; } = Guid.CreateVersion7();
    public Guid CajaUno { get; } = Guid.CreateVersion7();
    public Guid CajaDos { get; } = Guid.CreateVersion7();
    public Guid RolCajero { get; } = Guid.CreateVersion7();
    public Guid RolSupervisor { get; } = Guid.CreateVersion7();
    public Guid Cajero { get; } = Guid.CreateVersion7();
    public Guid CajeroDos { get; } = Guid.CreateVersion7();
    public Guid Supervisor { get; } = Guid.CreateVersion7();
    public Guid Inactivo { get; } = Guid.CreateVersion7();
    public Guid SinCaja { get; } = Guid.CreateVersion7();

    public string CodigoCajero => $"C{Sufijo}";
    public string CodigoCajeroDos => $"K{Sufijo}";
    public string CodigoSupervisor => $"S{Sufijo}";
    public string CodigoInactivo => $"I{Sufijo}";
    public string CodigoSinCaja => $"N{Sufijo}";
    public string CarneCajero => $"CARNE-C{Sufijo}";
    public string CarneSupervisor => $"CARNE-S{Sufijo}";

    public static async Task<EscenarioSeguridad> CrearAsync(BaseDatosPruebas baseDatos, Guid empresaId)
    {
        var escenario = new EscenarioSeguridad(baseDatos);

        await using var ambito = baseDatos.Servicios!.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<ICargaInicial>().AplicarAsync(escenario.CrearPaquete(empresaId));

        return escenario;
    }

    /// <summary>Proveedor con reloj controlable, la caja indicada como caja actual y un lector de huella fijo.</summary>
    public (ServiceProvider Proveedor, RelojPrueba Reloj) CrearProveedor(Guid? cajaId, Guid? usuarioHuella = null, Action<IServiceCollection>? extras = null)
    {
        var reloj = new RelojPrueba(Inicio);
        var proveedor = _baseDatos.CrearProveedor(servicios =>
        {
            servicios.AddSingleton<TimeProvider>(reloj);
            servicios.AddSingleton<IContextoCaja>(new ContextoCajaFijo(cajaId));
            servicios.AddScoped<ILectorHuella>(_ => new LectorHuellaFijo(usuarioHuella));
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

    public static async Task<int> ContarAuditoriaAsync(IServiceProvider proveedor, string accion, Guid usuarioId)
    {
        await using var ambito = proveedor.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
        var idTexto = usuarioId.ToString();
        return await contexto.Auditoria.CountAsync(registro => registro.Accion == accion && (registro.EntidadId == idTexto || registro.UsuarioId == usuarioId));
    }

    private PaqueteCargaInicial CrearPaquete(Guid empresaId) =>
        new(
            new EmpresaCarga(empresaId, "999000003", "Empresa Seguridad SRL", Direccion: "Calle de prueba 1, Santo Domingo"),
            Sucursales: [new SucursalCarga(Sucursal, $"S{Sufijo}", "Sucursal de seguridad")],
            Cajas:
            [
                new CajaCarga(CajaUno, Sucursal, "01", "Caja 01"),
                new CajaCarga(CajaDos, Sucursal, "02", "Caja 02", Habilitada: false),
            ],
            Roles:
            [
                new RolCarga(RolCajero, $"CAJ{Sufijo}", "Cajero", 1,
                    [CatalogoPermisos.RegistrarVenta, CatalogoPermisos.AbrirTurno, CatalogoPermisos.CerrarTurno, CatalogoPermisos.DespacharPendiente]),
                new RolCarga(RolSupervisor, $"SUP{Sufijo}", "Supervisor", 2,
                    [CatalogoPermisos.AutorizarOperaciones, CatalogoPermisos.EliminarLinea, CatalogoPermisos.LimpiarPantalla, CatalogoPermisos.AnularVenta,
                     CatalogoPermisos.RegistrarVenta, CatalogoPermisos.CambiarComprobante, CatalogoPermisos.SuspenderVenta,
                     CatalogoPermisos.DescuentoLinea, CatalogoPermisos.DescuentoFactura, CatalogoPermisos.DesactivarPromocion,
                     CatalogoPermisos.AprobacionManualTarjeta, CatalogoPermisos.AbrirGaveta, CatalogoPermisos.CerrarTurno,
                     CatalogoPermisos.RetiroEfectivo, CatalogoPermisos.RelevoCajero, CatalogoPermisos.PreCierre, CatalogoPermisos.ReabrirCierre,
                     CatalogoPermisos.AutorizarDevolucion, CatalogoPermisos.CanjearPuntos, CatalogoPermisos.MarcarPendiente,
                     CatalogoPermisos.DespacharPendiente, CatalogoPermisos.AnularPendiente]),
            ],
            Usuarios:
            [
                new UsuarioCarga(Cajero, CodigoCajero, "Cajero Seguridad", RolCajero, [CajaUno, CajaDos], Pin: PinCajero, CredencialBarras: CarneCajero),
                new UsuarioCarga(CajeroDos, CodigoCajeroDos, "Cajero Dos", RolCajero, [CajaUno], Pin: PinCajeroDos),
                new UsuarioCarga(Supervisor, CodigoSupervisor, "Supervisor Seguridad", RolSupervisor, [], Pin: PinSupervisor, CredencialBarras: CarneSupervisor),
                new UsuarioCarga(Inactivo, CodigoInactivo, "Usuario Inactivo", RolCajero, [CajaUno], Pin: PinInactivo, Activo: false),
                new UsuarioCarga(SinCaja, CodigoSinCaja, "Usuario Sin Caja", RolCajero, [], Pin: PinSinCaja),
            ],
            Parametros:
            [
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MonedaLocal, "DOP", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.ValorPuntoFidelidad, "1", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.DiasRetencionXmlEnviados, "30", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.DiasRetencionMensajesConfirmados, "30", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.AlertaTamanoBaseDatosMb, "1", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.HorasAlertaPendientes, "24", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.ToleranciaRelojSegundos, "5", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MesesVigenciaPuntos, "12", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MaximoPuntosCanjeSinConexion, "5000", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.IntentosMaximosPin, "3", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MinutosBloqueo, "5", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MinutosVigenciaAutorizacion, "5", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.HorasSesion, "12", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.TipoIngresos, "1", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), CgPos.Dominio.Organizacion.CatalogoParametros.DigitosSecuenciaDocumentos, "7", CajaId: CajaUno),

                // Reglas de negocio de la caja de prueba: en producción las configura un usuario en el Central.
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MontoIdentificacionConsumo, "250000", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.PasoRedondeoEfectivo, "0", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.CierreCiego, "true", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.FondoEnCuadre, "false", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.DiasRetencionImpuestoDevolucion, "30", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.MesesVigenciaNotaCredito, "6", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.PorcentajeAlertaSecuenciaEcf, "10", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.DiasAlertaCertificado, "30", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.BalanzaPrefijoPeso, "21", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.BalanzaPrefijoPrecio, "22", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.BalanzaDigitosCodigoArticulo, "5", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.BalanzaDigitosValor, "5", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.BalanzaDecimalesPeso, "3", CajaId: CajaUno),
                new ParametroCarga(Guid.CreateVersion7(), ClavesParametros.BalanzaDecimalesPrecio, "2", CajaId: CajaUno),
            ]);
}
