using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.CargaInicial;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Auditoria;
using CgPos.Central.Infraestructura.CargaInicial;
using CgPos.Central.Infraestructura.Dispositivos;
using CgPos.Central.Infraestructura.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CgPos.Central.Infraestructura;

public static class InyeccionDependencias
{
    public const string NombreConexion = "BaseDatosCentral";

    /// <summary>SQL Server 2019 (150) por defecto; se puede bajar por configuración para un servidor de desarrollo anterior.</summary>
    public const int NivelCompatibilidadPorDefecto = 150;

    public static IServiceCollection AgregarInfraestructuraCentral(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var cadenaConexion = configuracion.GetConnectionString(NombreConexion);
        if (string.IsNullOrWhiteSpace(cadenaConexion))
            throw new InvalidOperationException($"Falta la cadena de conexión 'ConnectionStrings:{NombreConexion}'.");

        var nivelCompatibilidad = int.TryParse(configuracion["BaseDatos:NivelCompatibilidad"], out var nivel) ? nivel : NivelCompatibilidadPorDefecto;
        servicios.AddDbContext<ContextoDatosCentral>(opciones => ConfigurarSqlServer(opciones, cadenaConexion, nivelCompatibilidad));

        servicios.TryAddSingleton(TimeProvider.System);
        servicios.AddScoped<IAuditoriaCentral, EscritorAuditoriaCentral>();
        servicios.AddSingleton<IHashContrasenas, HashContrasenas>();
        servicios.AddScoped<IParametrosCentral, ServicioParametrosCentral>();
        servicios.AddScoped<IServicioOrganizacion, ServicioOrganizacionCentral>();
        servicios.AddScoped<IServicioConfiguracionCajas, ServicioConfiguracionCajas>();
        servicios.AddScoped<CgPos.Central.Aplicacion.Maestros.IServicioMaestrosCentral, Maestros.ServicioMaestrosCentral>();
        servicios.AddScoped<CgPos.Central.Aplicacion.Maestros.IServicioPromocionesCentral, Maestros.ServicioPromocionesCentral>();
        servicios.AddScoped<ICargaInicialCentral, ServicioCargaInicialCentral>();

        // Seguridad (M02): sesiones del Central Manager y credenciales de las cajas.
        servicios.AddScoped<IServicioSesionesCentral, ServicioSesionesCentral>();
        servicios.AddScoped<IServicioAdministracionSeguridad, ServicioAdministracionSeguridad>();
        servicios.AddScoped<IServicioDispositivos, ServicioDispositivos>();

        // Sincronización con las cajas (M14).
        servicios.AddScoped<Aplicacion.Sincronizacion.IServicioRecepcion, Sincronizacion.ServicioRecepcion>();
        servicios.AddScoped<Aplicacion.Sincronizacion.IPublicadorMaestros, Sincronizacion.PublicadorMaestros>();
        servicios.AddScoped<Aplicacion.Sincronizacion.IServicioBajadaMaestros, Sincronizacion.ServicioBajadaMaestros>();
        servicios.AddScoped<Aplicacion.Sincronizacion.IServicioMonitorCentral, Sincronizacion.ServicioMonitorCentral>();

        // Notas de crédito entre sucursales (M10).
        servicios.AddScoped<Aplicacion.Devoluciones.IServicioNotasCreditoCentral, Devoluciones.ServicioNotasCreditoCentral>();
        servicios.AddScoped<Aplicacion.ListasBoda.IServicioListasBoda, ListasBoda.ServicioListasBoda>();
        servicios.AddScoped<Aplicacion.Organizacion.INumeracionCentral, Organizacion.NumeracionCentral>();
        servicios.AddScoped<Aplicacion.Cotizaciones.IServicioCotizaciones, Cotizaciones.ServicioCotizaciones>();
        servicios.AddScoped<Aplicacion.Ventas.IServicioFacturasParaCaja, Ventas.ServicioFacturasParaCaja>();
        servicios.AddScoped<Aplicacion.Ventas.IServicioComprobantesRecibidos, Ventas.ServicioComprobantesRecibidos>();
        servicios.AddScoped<Aplicacion.Auditoria.IServicioConsultaAuditoria, Auditoria.ServicioConsultaAuditoria>();
        servicios.AddScoped<Aplicacion.Catalogo.IServicioChequeadorPrecios, Catalogo.ServicioChequeadorPrecios>();

        // Saldo central de puntos del programa de fidelidad (M11).
        servicios.AddScoped<Fidelidad.RecalculadorPuntos>();
        servicios.AddScoped<Aplicacion.Fidelidad.IServicioFidelidadCentral, Fidelidad.ServicioFidelidadCentral>();

        // Pendientes de entrega y envíos de todas las sucursales (M12).
        servicios.AddScoped<Aplicacion.Entregas.IServicioDespachoCentral, Entregas.ServicioDespachoCentral>();
        servicios.AddSingleton<Reportes.GeneradorPdfConstanciaEntrega>();

        // Reportes y su exportación a Excel, PDF y al formato 607 (M16).
        servicios.AddScoped<Reportes.RegistroVentasCentral>();
        servicios.AddScoped<Aplicacion.Reportes.IServicioReportesCentral, Reportes.ServicioReportesCentral>();
        servicios.AddScoped<Aplicacion.Reportes.IServicioCierresSucursal, Reportes.ServicioCierresSucursal>();
        servicios.AddScoped<Aplicacion.Reportes.IServicioCierresCaja, Reportes.ServicioCierresCaja>();
        servicios.AddSingleton<Aplicacion.Reportes.IExportadorReportes, Reportes.ExportadorReportes>();

        // Actualización remota del Agente de las cajas (H7).
        servicios.AddScoped<Aplicacion.Actualizaciones.IServicioActualizacionesCaja, Actualizaciones.ServicioActualizacionesCaja>();

        // Correo de la empresa y aviso al cliente cuando su pedido está listo (RF-256).
        servicios.AddSingleton(new Notificaciones.OpcionesCorreo(configuracion["Correo:Contrasena"]));
        servicios.AddScoped<Aplicacion.Notificaciones.IServicioCorreo, Notificaciones.ServicioCorreoSmtp>();
        servicios.AddScoped<Aplicacion.Notificaciones.IAvisosDespacho, Notificaciones.AvisosDespacho>();
        if (!string.Equals(configuracion["Despacho:TrabajadorHabilitado"], "false", StringComparison.OrdinalIgnoreCase))
            servicios.AddHostedService<Notificaciones.TrabajadorAvisosDespacho>();
        if (!string.Equals(configuracion["Fidelidad:TrabajadorHabilitado"], "false", StringComparison.OrdinalIgnoreCase))
            servicios.AddHostedService<Fidelidad.TrabajadorVencimientoPuntos>();

        return servicios;
    }

    public static DbContextOptionsBuilder ConfigurarSqlServer(DbContextOptionsBuilder opciones, string cadenaConexion, int nivelCompatibilidad) =>
        opciones.UseSqlServer(cadenaConexion, sql =>
        {
            sql.UseCompatibilityLevel(nivelCompatibilidad);
            sql.MigrationsHistoryTable("__HistorialMigraciones");
        });
}
