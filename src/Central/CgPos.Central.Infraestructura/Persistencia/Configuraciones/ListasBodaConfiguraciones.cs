using CgPos.Dominio.ListasBoda;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class ListaBodaConfiguracion : IEntityTypeConfiguration<ListaBoda>
{
    public void Configure(EntityTypeBuilder<ListaBoda> constructor)
    {
        constructor.ToTable("ListasBoda");
        constructor.HasKey(l => l.Id);

        constructor.Property(l => l.Numero).HasMaxLength(ListaBoda.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(l => l.Evento).HasMaxLength(ListaBoda.LargoMaximoNombre).IsRequired();
        constructor.Property(l => l.Lugar).HasMaxLength(ListaBoda.LargoMaximoLugar);
        constructor.Property(l => l.ClienteDocumento).HasMaxLength(ListaBoda.LargoMaximoDocumento).IsRequired();
        constructor.Property(l => l.ClienteNombre).HasMaxLength(ListaBoda.LargoMaximoNombre).IsRequired();
        constructor.Property(l => l.ClienteTelefono).HasMaxLength(ListaBoda.LargoMaximoContacto);
        constructor.Property(l => l.ClienteCorreo).HasMaxLength(ListaBoda.LargoMaximoContacto);
        constructor.Property(l => l.Observacion).HasMaxLength(ListaBoda.LargoMaximoObservacion);
        constructor.Property(l => l.Estado).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(l => l.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // La caja la busca por su número, que es el que el cliente dice en la tienda.
        constructor.HasIndex(l => l.Numero).IsUnique();
        constructor.HasIndex(l => l.ClienteDocumento);
        constructor.HasIndex(l => l.FechaEvento);

        constructor.HasMany(l => l.Articulos).WithOne().HasForeignKey(a => a.ListaBodaId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasMany(l => l.Compras).WithOne().HasForeignKey(c => c.ListaBodaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(l => l.Articulos).UsePropertyAccessMode(PropertyAccessMode.Field);
        constructor.Navigation(l => l.Compras).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ArticuloListaBodaConfiguracion : IEntityTypeConfiguration<ArticuloListaBoda>
{
    public void Configure(EntityTypeBuilder<ArticuloListaBoda> constructor)
    {
        constructor.ToTable("ArticulosListaBoda");
        constructor.HasKey(a => a.Id);
        constructor.Property(a => a.ArticuloCodigo).HasMaxLength(ArticuloListaBoda.LargoMaximoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(a => a.Descripcion).HasMaxLength(ListaBoda.LargoMaximoNombre).IsRequired();
        constructor.Property(a => a.Cantidad).HasPrecision(18, 3);
        constructor.Property(a => a.Comprado).HasPrecision(18, 3);
        constructor.Ignore(a => a.Pendiente);

        // Un artículo aparece una sola vez en cada lista.
        constructor.HasIndex(a => new { a.ListaBodaId, a.ArticuloCodigo }).IsUnique();
    }
}

internal sealed class CompraListaBodaConfiguracion : IEntityTypeConfiguration<CompraListaBoda>
{
    public void Configure(EntityTypeBuilder<CompraListaBoda> constructor)
    {
        constructor.ToTable("ComprasListaBoda");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.VentaNumero).HasMaxLength(CgPos.Dominio.Ventas.Venta.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(c => c.Monto).HasPrecision(18, 2);
        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);

        // La misma factura se registra una sola vez aunque la caja reenvíe el mensaje.
        constructor.HasIndex(c => new { c.ListaBodaId, c.VentaNumero }).IsUnique();
    }
}
