using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Persistencia;

/// <summary>Base de datos local de la caja (SQL Server Express en producción).</summary>
public sealed class ContextoDatosPos(DbContextOptions<ContextoDatosPos> opciones) : DbContext(opciones)
{
    // Sincronización y auditoría
    public DbSet<MensajeSalida> BandejaSalida => Set<MensajeSalida>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    // Organización (M01)
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Parametro> Parametros => Set<Parametro>();

    // Seguridad (M02)
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoDatosPos).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder constructorConvenciones)
    {
        // Montos y cantidades: 4 decimales en almacenamiento; el redondeo fiscal a 2 lo hace el dominio.
        constructorConvenciones.Properties<decimal>().HavePrecision(18, 4);

        // Evita nvarchar(maximo) por defecto (no indexable); cada configuración ajusta su largo si hace falta.
        constructorConvenciones.Properties<string>().HaveMaxLength(256);

        // Precisión de milisegundos, suficiente para auditoría y orden de eventos.
        constructorConvenciones.Properties<DateTime>().HavePrecision(3);
        constructorConvenciones.Properties<DateTimeOffset>().HavePrecision(3);
    }
}
