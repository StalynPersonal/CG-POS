using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class MotivoDevolucionConfiguracion : IEntityTypeConfiguration<MotivoDevolucion>
{
    public void Configure(EntityTypeBuilder<MotivoDevolucion> constructor)
    {
        constructor.ToTable("MotivosDevolucion");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Id).ValueGeneratedNever();
                constructor.Property(m => m.Nombre).HasMaxLength(MotivoDevolucion.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
    }
}

internal sealed class DevolucionConfiguracion : IEntityTypeConfiguration<Devolucion>
{
    public void Configure(EntityTypeBuilder<Devolucion> constructor)
    {
        constructor.ToTable("Devoluciones");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Id).ValueGeneratedNever();
        constructor.Property(d => d.Numero).HasMaxLength(Devolucion.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(d => d.UsuarioNombre).HasMaxLength(Devolucion.LargoMaximoNombre).IsRequired();
        constructor.Property(d => d.VentaOrigenNumero).HasMaxLength(30).IsUnicode(false).IsRequired();
        constructor.Property(d => d.EncfOrigen).HasMaxLength(DocumentoElectronico.LargoEncf).IsUnicode(false);
        constructor.Property(d => d.Encf).HasMaxLength(DocumentoElectronico.LargoEncf).IsUnicode(false);
        constructor.Property(d => d.ClienteDocumento).HasMaxLength(20).IsUnicode(false).IsRequired();
        constructor.Property(d => d.ClienteNombre).HasMaxLength(Devolucion.LargoMaximoNombre).IsRequired();
                constructor.Property(d => d.MotivoNombre).HasMaxLength(MotivoDevolucion.LargoMaximoNombre).IsRequired();
        constructor.Property(d => d.Observacion).HasMaxLength(Devolucion.LargoMaximoObservacion);
        constructor.Property(d => d.AutorizadoPorNombre).HasMaxLength(Devolucion.LargoMaximoNombre);
        constructor.Property(d => d.Subtotal).HasPrecision(18, 2);
        constructor.Property(d => d.Impuesto).HasPrecision(18, 2);
        constructor.Property(d => d.ImpuestoRetenido).HasPrecision(18, 2);
        constructor.Property(d => d.Total).HasPrecision(18, 2);
        constructor.Property(d => d.Saldo).HasPrecision(18, 2);
        constructor.Property(d => d.Moneda).HasMaxLength(CgPos.Dominio.Pagos.Moneda.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(d => d.SimboloMoneda).HasMaxLength(CgPos.Dominio.Pagos.Moneda.LargoMaximoSimbolo).IsRequired();

        constructor.HasOne<Venta>().WithMany().HasForeignKey(d => d.VentaOrigenId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(d => d.VentaOrigenId);
        constructor.HasIndex(d => new { d.CajaId, d.Numero }).IsUnique();
        constructor.HasIndex(d => d.Encf).IsUnique().HasFilter("[Encf] IS NOT NULL");

        constructor.HasMany(d => d.Lineas).WithOne().HasForeignKey(l => l.DevolucionId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(d => d.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
        constructor.HasMany(d => d.Consumos).WithOne().HasForeignKey(c => c.DevolucionId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(d => d.Consumos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaDevolucionConfiguracion : IEntityTypeConfiguration<LineaDevolucion>
{
    public void Configure(EntityTypeBuilder<LineaDevolucion> constructor)
    {
        constructor.ToTable("LineasDevolucion");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Id).ValueGeneratedNever();
        constructor.Property(l => l.CodigoInterno).HasMaxLength(50).IsRequired();
        constructor.Property(l => l.CodigoLeido).HasMaxLength(50).IsRequired();
        constructor.Property(l => l.Descripcion).HasMaxLength(200).IsRequired();
        constructor.Property(l => l.UnidadMedidaCodigo).HasMaxLength(20).IsRequired();
        constructor.Property(l => l.Cantidad).HasPrecision(18, 4);
        constructor.Property(l => l.PrecioUnitario).HasPrecision(18, 2);
        constructor.Property(l => l.PorcentajeImpuesto).HasPrecision(9, 4);
        constructor.Property(l => l.ImporteFactura).HasPrecision(18, 2);
        constructor.Property(l => l.Base).HasPrecision(18, 2);
        constructor.Property(l => l.Impuesto).HasPrecision(18, 2);
        constructor.Property(l => l.ImpuestoRetenido).HasPrecision(18, 2);
        constructor.Property(l => l.Importe).HasPrecision(18, 2);
        constructor.Property(l => l.Serial).HasMaxLength(100);
    }
}

internal sealed class ConsumoNotaCreditoConfiguracion : IEntityTypeConfiguration<ConsumoNotaCredito>
{
    public void Configure(EntityTypeBuilder<ConsumoNotaCredito> constructor)
    {
        constructor.ToTable("ConsumosNotaCredito");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();
        constructor.Property(c => c.VentaNumero).HasMaxLength(30).IsUnicode(false).IsRequired();
        constructor.Property(c => c.Monto).HasPrecision(18, 2);
        constructor.Property(c => c.SaldoRestante).HasPrecision(18, 2);
        constructor.HasIndex(c => c.VentaId);
    }
}
