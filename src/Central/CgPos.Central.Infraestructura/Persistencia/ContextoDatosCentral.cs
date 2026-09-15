using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Persistencia;

/// <summary>Base de datos del Central (SQL Server Standard en producción).</summary>
public sealed class ContextoDatosCentral(DbContextOptions<ContextoDatosCentral> opciones) : DbContext(opciones)
{
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    // Organización (M01)
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<CredencialDispositivo> CredencialesDispositivo => Set<CredencialDispositivo>();

    // Seguridad del Central Manager (M02)
    public DbSet<RolCentral> RolesCentral => Set<RolCentral>();
    public DbSet<UsuarioCentral> UsuariosCentral => Set<UsuarioCentral>();
    public DbSet<SesionCentral> SesionesCentral => Set<SesionCentral>();

    // Sincronización con las cajas (M14)
    public DbSet<CgPos.Dominio.Sincronizacion.DocumentoRecibido> DocumentosRecibidos => Set<CgPos.Dominio.Sincronizacion.DocumentoRecibido>();
    public DbSet<CgPos.Dominio.Sincronizacion.ComprobanteRecibido> ComprobantesRecibidos => Set<CgPos.Dominio.Sincronizacion.ComprobanteRecibido>();
    public DbSet<CgPos.Dominio.Sincronizacion.ConflictoSincronizacion> ConflictosSincronizacion => Set<CgPos.Dominio.Sincronizacion.ConflictoSincronizacion>();
    public DbSet<CgPos.Dominio.Sincronizacion.EstadoSincronizacionCaja> EstadosSincronizacionCaja => Set<CgPos.Dominio.Sincronizacion.EstadoSincronizacionCaja>();

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoDatosCentral).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder constructorConvenciones)
    {
        // Mismas convenciones que la base de la caja: los documentos viajan entre ambas sin perder precisión.
        constructorConvenciones.Properties<decimal>().HavePrecision(18, 4);
        constructorConvenciones.Properties<string>().HaveMaxLength(256);
        constructorConvenciones.Properties<DateTime>().HavePrecision(3);
        constructorConvenciones.Properties<DateTimeOffset>().HavePrecision(3);
    }
}
