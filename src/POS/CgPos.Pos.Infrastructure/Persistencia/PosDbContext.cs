using CgPos.Domain.Auditoria;
using CgPos.Pos.Application.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infrastructure.Persistencia;

/// <summary>Base de datos local de la caja (SQL Server Express en producción).</summary>
public sealed class PosDbContext(DbContextOptions<PosDbContext> options) : DbContext(options)
{
    public DbSet<MensajeOutbox> Outbox => Set<MensajeOutbox>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Montos y cantidades: 4 decimales en almacenamiento; el redondeo fiscal a 2 lo hace el dominio.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);

        // Evita nvarchar(max) por defecto (no indexable); cada configuración ajusta su largo si hace falta.
        configurationBuilder.Properties<string>().HaveMaxLength(256);

        // Precisión de milisegundos, suficiente para auditoría y orden de eventos.
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<DateTimeOffset>().HavePrecision(3);
    }
}
