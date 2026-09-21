using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

/// <summary>
/// El pendiente de entrega vive completo en el Central, que es quien lo despacha (RF-249 a RF-256). Son las mismas tablas que
/// en la caja, con el mismo agregado: la caja lo crea al cobrar y lo informa una sola vez, y de ahí en adelante lo opera el
/// Central. Aquí no hay llave foránea a la venta: la venta está en la base de la caja que la cobró.
/// </summary>
internal sealed class PendienteEntregaConfiguracion : IEntityTypeConfiguration<PendienteEntrega>
{
    public void Configure(EntityTypeBuilder<PendienteEntrega> constructor)
    {
        constructor.ToTable("PendientesEntrega");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Numero).HasMaxLength(PendienteEntrega.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(p => p.VentaNumero).HasMaxLength(Venta.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(p => p.SucursalRetiroNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre);
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
        constructor.Property(p => p.NumeroCentral).HasMaxLength(PendienteEntrega.LargoMaximoNumero).IsUnicode(false);
        constructor.HasIndex(p => p.NumeroCentral).IsUnique().HasFilter("[NumeroCentral] IS NOT NULL");

        // La venta y la sucursal de retiro son de la caja que lo emitió: aquí se guardan sus Id como referencia, sin llave foránea.
        constructor.Ignore(p => p.EstaAbierto);
        constructor.Ignore(p => p.Unidades);
        constructor.Ignore(p => p.UnidadesEntregadas);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(p => p.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(p => p.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // El número identifica el pendiente en toda la empresa: el mismo documento no se duplica aunque el mensaje se reenvíe.
        constructor.HasIndex(p => p.Numero).IsUnique();
        constructor.HasIndex(p => p.SucursalId);
        // El Central lista por estado y por la fecha comprometida, para ver primero los atrasados.
        constructor.HasIndex(p => new { p.Estado, p.FechaComprometida });

        constructor.HasMany(p => p.Lineas).WithOne().HasForeignKey(l => l.PendienteEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(p => p.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
        constructor.HasMany(p => p.Entregas).WithOne().HasForeignKey(e => e.PendienteEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(p => p.Entregas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaPendienteEntregaCentralConfiguracion : IEntityTypeConfiguration<LineaPendienteEntrega>
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
        constructor.HasIndex(l => l.PendienteEntregaId);
    }
}

internal sealed class EntregaPendienteCentralConfiguracion : IEntityTypeConfiguration<EntregaPendiente>
{
    public void Configure(EntityTypeBuilder<EntregaPendiente> constructor)
    {
        constructor.ToTable("EntregasPendiente");
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.RecibeNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre).IsRequired();
        constructor.Property(e => e.RecibeCedula).HasMaxLength(20).IsRequired();
        constructor.Property(e => e.UsuarioNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(e => new { e.PendienteEntregaId, e.Numero }).IsUnique();

        constructor.HasMany(e => e.Lineas).WithOne().HasForeignKey(l => l.EntregaPendienteId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(e => e.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaEntregaPendienteCentralConfiguracion : IEntityTypeConfiguration<LineaEntregaPendiente>
{
    public void Configure(EntityTypeBuilder<LineaEntregaPendiente> constructor)
    {
        constructor.ToTable("LineasEntregaPendiente");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(l => l.Cantidad).HasPrecision(18, 3);
        constructor.Property(l => l.Serial).HasMaxLength(LineaVenta.LargoMaximoSerial);
        constructor.HasIndex(l => l.EntregaPendienteId);
    }
}
