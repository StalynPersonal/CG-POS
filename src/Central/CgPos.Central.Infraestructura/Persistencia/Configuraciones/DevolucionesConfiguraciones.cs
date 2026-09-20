using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class NotaCreditoCentralConfiguracion : IEntityTypeConfiguration<NotaCreditoCentral>
{
    public void Configure(EntityTypeBuilder<NotaCreditoCentral> constructor)
    {
        constructor.ToTable("NotasCredito");
        constructor.HasKey(n => n.Id);

        constructor.Property(n => n.Numero).HasMaxLength(NotaCreditoCentral.LargoMaximoNumero).IsRequired();
        constructor.Property(n => n.Encf).HasMaxLength(NotaCreditoCentral.LargoMaximoEncf).IsFixedLength().IsUnicode(false);
        constructor.Property(n => n.ClienteDocumento).HasMaxLength(NotaCreditoCentral.LargoMaximoTexto);
        constructor.Property(n => n.ClienteNombre).HasMaxLength(NotaCreditoCentral.LargoMaximoTexto);
        constructor.Property(n => n.Moneda).HasMaxLength(NotaCreditoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();

        constructor.HasOne<Caja>().WithMany().HasForeignKey(n => n.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(n => n.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // La caja busca por e-NCF o por número; ambos identifican una sola nota.
        constructor.HasIndex(n => n.Encf).IsUnique().HasFilter("[Encf] IS NOT NULL");
        constructor.HasIndex(n => n.Numero).IsUnique();
        constructor.HasIndex(n => n.ClienteDocumento);
        constructor.HasIndex(n => n.FechaEmision);
    }
}

internal sealed class ConsumoNotaCreditoCentralConfiguracion : IEntityTypeConfiguration<ConsumoNotaCreditoCentral>
{
    public void Configure(EntityTypeBuilder<ConsumoNotaCreditoCentral> constructor)
    {
        constructor.ToTable("ConsumosNotaCredito");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.NotaCreditoNumero).HasMaxLength(NotaCreditoCentral.LargoMaximoNumero).IsRequired();
        constructor.Property(c => c.VentaNumero).HasMaxLength(NotaCreditoCentral.LargoMaximoNumero).IsRequired();

        // Sin clave foránea a la nota: el consumo de una caja puede llegar antes que la emisión de otra, y al registrarla se descuenta.
        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);

        // Una factura consume una nota de crédito una sola vez, aunque su mensaje llegue repetido.
        constructor.HasIndex(c => new { c.NotaCreditoNumero, c.VentaNumero }).IsUnique();
    }
}

internal sealed class ReservaNotaCreditoCentralConfiguracion : IEntityTypeConfiguration<ReservaNotaCreditoCentral>
{
    public void Configure(EntityTypeBuilder<ReservaNotaCreditoCentral> constructor)
    {
        constructor.ToTable("ReservasNotaCredito");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Cierre).HasMaxLength(ReservaNotaCreditoCentral.LargoMaximoCierre);
        constructor.Property(r => r.VentaNumero).HasMaxLength(NotaCreditoCentral.LargoMaximoNumero).IsRequired();

        constructor.HasOne<NotaCreditoCentral>().WithMany().HasForeignKey(r => r.NotaCreditoId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasOne<Caja>().WithMany().HasForeignKey(r => r.CajaId).OnDelete(DeleteBehavior.Restrict);

        // El saldo disponible se calcula con las reservas vigentes de cada nota.
        constructor.HasIndex(r => new { r.NotaCreditoId, r.CerradaEn, r.VenceEn });
    }
}

/// <summary>
/// Líneas de una factura retenidas mientras una caja emite su nota de crédito. Se consultan por factura para saber qué queda
/// realmente disponible, y las vencidas se cierran en la misma consulta que las lee.
/// </summary>
internal sealed class ReservaFacturaCentralConfiguracion : IEntityTypeConfiguration<ReservaFacturaCentral>
{
    public void Configure(EntityTypeBuilder<ReservaFacturaCentral> constructor)
    {
        constructor.ToTable("ReservasFactura");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.FacturaNumero).HasMaxLength(ReservaFacturaCentral.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(r => r.Cierre).HasMaxLength(ReservaFacturaCentral.LargoMaximoCierre);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(r => r.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(r => new { r.FacturaNumero, r.CerradaEn, r.VenceEn });

        constructor.HasMany(r => r.Lineas).WithOne().HasForeignKey(l => l.ReservaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(r => r.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaReservaFacturaCentralConfiguracion : IEntityTypeConfiguration<LineaReservaFacturaCentral>
{
    public void Configure(EntityTypeBuilder<LineaReservaFacturaCentral> constructor)
    {
        constructor.ToTable("LineasReservaFactura");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Cantidad).HasPrecision(18, 4);
    }
}
