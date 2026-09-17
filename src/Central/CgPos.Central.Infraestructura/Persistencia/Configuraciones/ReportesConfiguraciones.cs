using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class ComprobanteVentaCentralConfiguracion : IEntityTypeConfiguration<ComprobanteVentaCentral>
{
    public void Configure(EntityTypeBuilder<ComprobanteVentaCentral> constructor)
    {
        constructor.ToTable("VentasCentral");
        constructor.HasKey(c => c.Id);

        // El Id es el de la venta o la nota de crédito en la caja: un reenvío no duplica la fila.
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.Numero).HasMaxLength(ComprobanteVentaCentral.LargoMaximoNumero).IsRequired();
        constructor.Property(c => c.Encf).HasMaxLength(ComprobanteVentaCentral.LargoMaximoEncf).IsFixedLength().IsUnicode(false);
        constructor.Property(c => c.EncfModificado).HasMaxLength(ComprobanteVentaCentral.LargoMaximoEncf).IsFixedLength().IsUnicode(false);
        constructor.Property(c => c.ClienteDocumento).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(c => c.ClienteNombre).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(c => c.UsuarioNombre).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(c => c.Moneda).HasMaxLength(ComprobanteVentaCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();

        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasMany(c => c.Impuestos).WithOne().HasForeignKey(i => i.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasMany(c => c.Pagos).WithOne().HasForeignKey(p => p.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Impuestos).AutoInclude(false);
        constructor.Navigation(c => c.Pagos).AutoInclude(false);

        // Todos los reportes filtran por día de operación, y varios por sucursal o caja.
        constructor.HasIndex(c => new { c.FechaOperacion, c.SucursalId, c.CajaId });
        constructor.HasIndex(c => c.Encf);

        // El número de factura o de nota de crédito identifica el documento en toda la empresa.
        constructor.HasIndex(c => new { c.Tipo, c.Numero }).IsUnique();
    }
}

internal sealed class ImpuestoVentaCentralConfiguracion : IEntityTypeConfiguration<ImpuestoVentaCentral>
{
    public void Configure(EntityTypeBuilder<ImpuestoVentaCentral> constructor)
    {
        constructor.ToTable("ImpuestosVenta");
        constructor.HasKey(i => i.Id);

        // El Id lo pone el dominio: sin esto EF toma una fila nueva de un comprobante ya guardado como una modificación.
        constructor.Property(i => i.Id).ValueGeneratedNever();
        constructor.HasIndex(i => i.ComprobanteId);
    }
}

internal sealed class PagoVentaCentralConfiguracion : IEntityTypeConfiguration<PagoVentaCentral>
{
    public void Configure(EntityTypeBuilder<PagoVentaCentral> constructor)
    {
        constructor.ToTable("PagosVenta");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Id).ValueGeneratedNever();
        constructor.Property(p => p.FormaPagoNombre).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(p => p.Moneda).HasMaxLength(ComprobanteVentaCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false);
        constructor.HasIndex(p => p.ComprobanteId);
    }
}

internal sealed class CierreTurnoCentralConfiguracion : IEntityTypeConfiguration<CierreTurnoCentral>
{
    public void Configure(EntityTypeBuilder<CierreTurnoCentral> constructor)
    {
        constructor.ToTable("CierresTurno");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();
        constructor.Property(c => c.UsuarioNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(c => c.ReabiertoPorNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(c => c.MotivoReapertura).HasMaxLength(CierreTurnoCentral.LargoMaximoMotivo);
        constructor.Property(c => c.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();

        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasMany(c => c.FormasPago).WithOne().HasForeignKey(f => f.CierreId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.FormasPago).AutoInclude(false);

        constructor.HasIndex(c => new { c.FechaOperacion, c.SucursalId, c.CajaId });
    }
}

internal sealed class CierreFormaPagoCentralConfiguracion : IEntityTypeConfiguration<CierreFormaPagoCentral>
{
    public void Configure(EntityTypeBuilder<CierreFormaPagoCentral> constructor)
    {
        constructor.ToTable("CierresFormaPago");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Id).ValueGeneratedNever();
        constructor.Property(f => f.Nombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(f => f.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false);
        constructor.HasIndex(f => f.CierreId);
    }
}
