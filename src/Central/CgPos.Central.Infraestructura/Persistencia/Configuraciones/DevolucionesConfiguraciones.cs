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
        constructor.Property(n => n.Id).ValueGeneratedNever();

        constructor.Property(n => n.Numero).HasMaxLength(NotaCreditoCentral.LargoMaximoNumero).IsRequired();
        constructor.Property(n => n.Encf).HasMaxLength(NotaCreditoCentral.LargoMaximoEncf).IsFixedLength().IsUnicode(false);
        constructor.Property(n => n.ClienteDocumento).HasMaxLength(NotaCreditoCentral.LargoMaximoTexto);
        constructor.Property(n => n.ClienteNombre).HasMaxLength(NotaCreditoCentral.LargoMaximoTexto);
        constructor.Property(n => n.Moneda).HasMaxLength(NotaCreditoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(n => n.ProrrogadaPor).HasMaxLength(NotaCreditoCentral.LargoMaximoTexto);
        constructor.Property(n => n.MotivoProrroga).HasMaxLength(NotaCreditoCentral.LargoMaximoMotivo);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(n => n.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(n => n.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // La caja busca por e-NCF o por número; ambos identifican una sola nota.
        constructor.HasIndex(n => n.Encf).IsUnique().HasFilter("[Encf] IS NOT NULL");
        constructor.HasIndex(n => n.Numero);
        constructor.HasIndex(n => n.ClienteDocumento);
        constructor.HasIndex(n => n.VenceEn);
    }
}

internal sealed class ConsumoNotaCreditoCentralConfiguracion : IEntityTypeConfiguration<ConsumoNotaCreditoCentral>
{
    public void Configure(EntityTypeBuilder<ConsumoNotaCreditoCentral> constructor)
    {
        constructor.ToTable("ConsumosNotaCredito");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();
        constructor.Property(c => c.VentaNumero).HasMaxLength(NotaCreditoCentral.LargoMaximoNumero);

        // Sin clave foránea a la nota: el consumo de una caja puede llegar antes que la emisión de otra, y al registrarla se descuenta.
        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);

        // Una venta consume una nota de crédito una sola vez, aunque su mensaje llegue repetido.
        constructor.HasIndex(c => new { c.NotaCreditoId, c.VentaId }).IsUnique();
    }
}

internal sealed class ReservaNotaCreditoCentralConfiguracion : IEntityTypeConfiguration<ReservaNotaCreditoCentral>
{
    public void Configure(EntityTypeBuilder<ReservaNotaCreditoCentral> constructor)
    {
        constructor.ToTable("ReservasNotaCredito");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Id).ValueGeneratedNever();
        constructor.Property(r => r.Cierre).HasMaxLength(ReservaNotaCreditoCentral.LargoMaximoCierre);

        constructor.HasOne<NotaCreditoCentral>().WithMany().HasForeignKey(r => r.NotaCreditoId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasOne<Caja>().WithMany().HasForeignKey(r => r.CajaId).OnDelete(DeleteBehavior.Restrict);

        // El saldo disponible se calcula con las reservas vigentes de cada nota.
        constructor.HasIndex(r => new { r.NotaCreditoId, r.CerradaEn, r.VenceEn });
    }
}
