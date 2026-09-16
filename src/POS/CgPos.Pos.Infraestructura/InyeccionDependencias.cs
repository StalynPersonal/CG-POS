using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Perifericos;
using CgPos.Pos.Aplicacion.Ventas;
using CgPos.Pos.Infraestructura.Catalogo;
using CgPos.Pos.Infraestructura.Perifericos;
using CgPos.Pos.Infraestructura.Ventas;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Auditoria;
using CgPos.Pos.Infraestructura.CargaInicial;
using CgPos.Pos.Infraestructura.Organizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Seguridad;
using CgPos.Pos.Infraestructura.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CgPos.Pos.Infraestructura;

public static class InyeccionDependencias
{
    public const string NombreConexion = "BaseDatosPos";

    /// <summary>
    /// Nivel de compatibilidad por defecto: SQL Server 2019 (150), mínimo exigido para la caja.
    /// Se puede bajar por configuración (ej. 120 para un SQL Server 2014 de desarrollo).
    /// </summary>
    public const int NivelCompatibilidadPorDefecto = 150;

    public static IServiceCollection AgregarInfraestructuraPos(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var cadenaConexion = configuracion.GetConnectionString(NombreConexion);
        if (string.IsNullOrWhiteSpace(cadenaConexion))
            throw new InvalidOperationException($"Falta la cadena de conexión 'ConnectionStrings:{NombreConexion}'.");

        var nivelCompatibilidad = int.TryParse(configuracion["BaseDatos:NivelCompatibilidad"], out var nivel)
            ? nivel
            : NivelCompatibilidadPorDefecto;

        servicios.AddDbContext<ContextoDatosPos>(opciones => ConfigurarSqlServer(opciones, cadenaConexion, nivelCompatibilidad));

        servicios.TryAddSingleton(TimeProvider.System);
        servicios.AddScoped<IBandejaSalida, EscritorBandejaSalida>();
        servicios.AddScoped<IAuditoria, EscritorAuditoria>();
        servicios.AddSingleton<IHashCredenciales, HashCredenciales>();
        servicios.AddScoped<ICargaInicial, ServicioCargaInicial>();

        // Organización y seguridad (M01, M02)
        servicios.AddSingleton<IContextoCaja>(new ContextoCajaConfigurado(configuracion));
        servicios.AddScoped<IParametros, ServicioParametros>();
        servicios.AddScoped<IEstadoCaja, ServicioEstadoCaja>();
        servicios.AddScoped<ILectorHuella>(proveedor => new LectorHuellaSimulado(configuracion, proveedor.GetRequiredService<ContextoDatosPos>()));
        servicios.AddScoped<VerificadorCredenciales>();
        servicios.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();
        servicios.AddScoped<IServicioAutorizacion, ServicioAutorizacion>();

        // Maestros, catálogos y precios (M03, M04)
        servicios.AddScoped<ICargaMaestros, ServicioCargaMaestros>();
        servicios.AddScoped<IImportadorArticulos, ImportadorArticulosCsv>();
        servicios.AddScoped<IImportadorPadronDgii, ImportadorPadronDgii>();
        servicios.AddScoped<IConsultaArticulos, ConsultaArticulos>();
        servicios.AddScoped<IConsultaDocumentos, ConsultaDocumentos>();
        servicios.AddScoped<IConsultaCatalogoCobro, ConsultaCatalogoCobro>();
        servicios.AddScoped<IServicioPrecios, ServicioPrecios>();

        // Periféricos (simulados hasta definir modelos; la impresora se elige por configuración)
        servicios.AddSingleton<IBalanza>(proveedor => Perifericos.FabricaPerifericos.CrearBalanza(proveedor, configuracion));
        servicios.AddSingleton<ITerminalPago>(proveedor => Perifericos.FabricaPerifericos.CrearTerminal(proveedor, configuracion));
        servicios.AddSingleton<IImpresoraTicket>(proveedor => new ImpresoraTicket(configuracion,
            proveedor.GetRequiredService<TimeProvider>(), proveedor.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ImpresoraTicket>>()));

        // Turnos y ventas (M13, M05)
        servicios.AddScoped<GeneradorSecuencias>();
        servicios.AddScoped<IValidadorAutorizaciones, ValidadorAutorizaciones>();
        servicios.AddScoped<IServicioTurnos, ServicioTurnos>();
        servicios.AddScoped<IServicioCaja, Turnos.ServicioCaja>();
        servicios.AddScoped<Aplicacion.Devoluciones.IServicioDevoluciones, Devoluciones.ServicioDevoluciones>();
        servicios.AddScoped<Aplicacion.Fidelidad.IServicioFidelidad, Fidelidad.ServicioFidelidad>();
        servicios.AddScoped<IServicioDespacho, Entregas.ServicioDespacho>();
        servicios.AddScoped<ServicioVentas>();
        servicios.AddScoped<IServicioVentas>(proveedor => proveedor.GetRequiredService<ServicioVentas>());
        servicios.AddScoped<IServicioCobro>(proveedor => proveedor.GetRequiredService<ServicioVentas>());

        // Sincronización con el Central (M14): HTTP, simulado o sin Central según la configuración de la instalación.
        servicios.AddSingleton(OpcionesSincronizacion.Leer(configuracion));
        servicios.AddSingleton<Aplicacion.Sincronizacion.IEstadoConexionCentral, EstadoConexionCentral>();
        servicios.AddSingleton(_ => FabricaClienteCentral.Crear(configuracion));
        servicios.AddScoped<Aplicacion.Sincronizacion.IProcesadorBandejaSalida, ProcesadorBandejaSalida>();
        servicios.AddScoped<Aplicacion.Sincronizacion.IDescargaMaestros, DescargaMaestros>();
        servicios.AddScoped<Aplicacion.Sincronizacion.IActualizacionPadron, ActualizacionPadron>();
        servicios.AddScoped<IEstadoSincronizacion, ServicioEstadoSincronizacion>();

        // Mantenimiento de la caja: respaldo, purga controlada, hora y alertas.
        servicios.AddSingleton(OpcionesMantenimiento.Leer(configuracion));
        servicios.AddSingleton<Aplicacion.Sincronizacion.EstadoMantenimiento>();
        servicios.AddScoped<Aplicacion.Sincronizacion.IServicioMantenimiento, ServicioMantenimiento>();

        return servicios;
    }

    /// <summary>Configuración de SQL Server compartida por la aplicacion y las pruebas de integración.</summary>
    public static DbContextOptionsBuilder ConfigurarSqlServer(DbContextOptionsBuilder opciones, string cadenaConexion, int nivelCompatibilidad) =>
        opciones.UseSqlServer(cadenaConexion, sql =>
        {
            sql.UseCompatibilityLevel(nivelCompatibilidad);
            sql.MigrationsHistoryTable("__HistorialMigraciones");
        });
}
