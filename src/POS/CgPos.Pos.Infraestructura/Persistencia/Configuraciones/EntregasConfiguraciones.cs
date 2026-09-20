using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class AlmacenConfiguracion : IEntityTypeConfiguration<Almacen>
{
    public void Configure(EntityTypeBuilder<Almacen> constructor)
    {
        constructor.ToTable("Almacenes");
        constructor.HasKey(a => a.Id);
        constructor.Property(a => a.Codigo).HasMaxLength(Almacen.LargoMaximoCodigo).IsRequired();
        constructor.Property(a => a.Nombre).HasMaxLength(Almacen.LargoMaximoNombre).IsRequired();
        constructor.Property(a => a.Direccion).HasMaxLength(Almacen.LargoMaximoDireccion);
        constructor.HasIndex(a => a.Codigo).IsUnique();
    }
}

internal sealed class DestinoEntregaConfiguracion : IEntityTypeConfiguration<DestinoEntrega>
{
    public void Configure(EntityTypeBuilder<DestinoEntrega> constructor)
    {
        constructor.ToTable("DestinosEntregaVenta");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.AlmacenNombre).HasMaxLength(Almacen.LargoMaximoNombre);
        ConfigurarEnvio(constructor);
        constructor.Property(d => d.Comentario).HasMaxLength(DestinoEntrega.LargoMaximoComentario);
        constructor.Property(d => d.AutorizadoPorNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre);
        constructor.HasIndex(d => new { d.VentaId, d.Numero }).IsUnique();

        constructor.HasMany(d => d.Lineas).WithOne().HasForeignKey(l => l.DestinoEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(d => d.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }

    private static void ConfigurarEnvio(EntityTypeBuilder<DestinoEntrega> constructor)
    {
        constructor.Property(d => d.Direccion).HasMaxLength(DestinoEntrega.LargoMaximoDireccion);
        constructor.Property(d => d.Sector).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(d => d.Ciudad).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(d => d.Referencia).HasMaxLength(DestinoEntrega.LargoMaximoDireccion);
        constructor.Property(d => d.Telefono).HasMaxLength(DestinoEntrega.LargoMaximoTelefono);
        constructor.Property(d => d.Transportista).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(d => d.CostoEnvio).HasPrecision(18, 2);
    }
}

internal sealed class LineaDestinoEntregaConfiguracion : IEntityTypeConfiguration<LineaDestinoEntrega>
{
    public void Configure(EntityTypeBuilder<LineaDestinoEntrega> constructor)
    {
        constructor.ToTable("LineasDestinoEntrega");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Cantidad).HasPrecision(18, 3);
    }
}

internal sealed class PendienteEntregaConfiguracion : IEntityTypeConfiguration<PendienteEntrega>
{
    public void Configure(EntityTypeBuilder<PendienteEntrega> constructor)
    {
        constructor.ToTable("PendientesEntrega");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Numero).HasMaxLength(PendienteEntrega.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(p => p.VentaNumero).HasMaxLength(Venta.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(p => p.AlmacenNombre).HasMaxLength(Almacen.LargoMaximoNombre);
        constructor.Property(p => p.Direccion).HasMaxLength(DestinoEntrega.LargoMaximoDireccion);
        constructor.Property(p => p.Sector).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(p => p.Ciudad).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(p => p.Referencia).HasMaxLength(DestinoEntrega.LargoMaximoDireccion);
        constructor.Property(p => p.Telefono).HasMaxLength(DestinoEntrega.LargoMaximoTelefono);
        constructor.Property(p => p.Transportista).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(p => p.CostoEnvio).HasPrecision(18, 2);
        constructor.Property(p => p.Comentario).HasMaxLength(DestinoEntrega.LargoMaximoComentario);
        constructor.Property(p => p.ClienteDocumento).HasMaxLength(Venta.LargoMaximoDocumento).IsUnicode(false);
        constructor.Property(p => p.ClienteNombre).HasMaxLength(Venta.LargoMaximoNombreCliente);
        constructor.Property(p => p.VendidoPorNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.AutorizadoPorNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre);
        constructor.Property(p => p.ActualizadoPorNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.MotivoAnulacion).HasMaxLength(PendienteEntrega.LargoMaximoMotivo);
        constructor.Ignore(p => p.EstaAbierto);
        constructor.Ignore(p => p.Unidades);
        constructor.Ignore(p => p.UnidadesEntregadas);

        // El aviso al cliente y el seguimiento del despacho son del Central: la caja solo crea el pendiente al cobrar.
        constructor.Ignore(p => p.AvisoEnviadoEn);

        constructor.HasIndex(p => p.Numero).IsUnique();
        constructor.HasIndex(p => p.VentaId);
        constructor.HasIndex(p => new { p.Estado, p.CreadoEn });

        constructor.HasMany(p => p.Lineas).WithOne().HasForeignKey(l => l.PendienteEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(p => p.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
        constructor.HasMany(p => p.Entregas).WithOne().HasForeignKey(e => e.PendienteEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(p => p.Entregas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaPendienteEntregaConfiguracion : IEntityTypeConfiguration<LineaPendienteEntrega>
{
    public void Configure(EntityTypeBuilder<LineaPendienteEntrega> constructor)
    {
        constructor.ToTable("LineasPendienteEntrega");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.CodigoInterno).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(l => l.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(l => l.UnidadMedidaCodigo).HasMaxLength(UnidadMedida.LargoMaximoAbreviatura).IsRequired();
        constructor.Property(l => l.Cantidad).HasPrecision(18, 3);
        constructor.Property(l => l.CantidadEntregada).HasPrecision(18, 3);
        constructor.Property(l => l.Serial).HasMaxLength(LineaVenta.LargoMaximoSerial);
        constructor.Ignore(l => l.CantidadPendiente);
    }
}

internal sealed class EntregaPendienteConfiguracion : IEntityTypeConfiguration<EntregaPendiente>
{
    public void Configure(EntityTypeBuilder<EntregaPendiente> constructor)
    {
        constructor.ToTable("EntregasPendiente");
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.RecibeNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre).IsRequired();
        constructor.Property(e => e.RecibeCedula).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.UsuarioNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre).IsRequired();
        constructor.HasMany(e => e.Lineas).WithOne().HasForeignKey(l => l.EntregaPendienteId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(e => e.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaEntregaPendienteConfiguracion : IEntityTypeConfiguration<LineaEntregaPendiente>
{
    public void Configure(EntityTypeBuilder<LineaEntregaPendiente> constructor)
    {
        constructor.ToTable("LineasEntregaPendiente");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(l => l.Cantidad).HasPrecision(18, 3);
        constructor.Property(l => l.Serial).HasMaxLength(LineaVenta.LargoMaximoSerial);
    }
}

internal static class EntregasVentaConfiguracion
{
    /// <summary>Los destinos se cargan siempre con la venta: sus cantidades condicionan los cambios de línea y el cobro.</summary>
    public static void ConfigurarEntregas(this EntityTypeBuilder<Venta> constructor)
    {
        constructor.HasMany(v => v.DestinosEntrega).WithOne().HasForeignKey(d => d.VentaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(v => v.DestinosEntrega).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}
