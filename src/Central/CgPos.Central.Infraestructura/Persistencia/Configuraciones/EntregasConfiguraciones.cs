using CgPos.Dominio.Entregas;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class PendienteCentralConfiguracion : IEntityTypeConfiguration<PendienteCentral>
{
    public void Configure(EntityTypeBuilder<PendienteCentral> constructor)
    {
        constructor.ToTable("PendientesEntrega");
        constructor.HasKey(p => p.Id);

        constructor.Property(p => p.Numero).HasMaxLength(PendienteCentral.LargoMaximoNumero).IsRequired();
        constructor.Property(p => p.VentaNumero).HasMaxLength(PendienteCentral.LargoMaximoNumero);
        constructor.Property(p => p.AlmacenNombre).HasMaxLength(PendienteCentral.LargoMaximoTexto);
        constructor.Property(p => p.Ciudad).HasMaxLength(PendienteCentral.LargoMaximoTexto);
        constructor.Property(p => p.ClienteDocumento).HasMaxLength(PendienteCentral.LargoMaximoTexto);
        constructor.Property(p => p.ClienteNombre).HasMaxLength(PendienteCentral.LargoMaximoTexto);
        constructor.Property(p => p.Telefono).HasMaxLength(PendienteCentral.LargoMaximoTexto);
        constructor.Property(p => p.TextoBusqueda).HasMaxLength(PendienteCentral.LargoMaximoBusqueda).IsRequired();
        constructor.Property(p => p.Contenido).IsRequired().Metadata.SetMaxLength(null);
        constructor.Property(p => p.Unidades).HasPrecision(18, 3);
        constructor.Property(p => p.UnidadesEntregadas).HasPrecision(18, 3);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(p => p.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(p => p.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // El Central lista por estado y por la fecha comprometida, para ver primero los atrasados.
        constructor.HasIndex(p => new { p.Estado, p.FechaComprometida });
        // El número identifica el pendiente en toda la empresa: el mismo documento no se duplica aunque el mensaje se reenvíe.
        constructor.HasIndex(p => p.Numero).IsUnique();
        constructor.HasIndex(p => p.SucursalId);
    }
}
