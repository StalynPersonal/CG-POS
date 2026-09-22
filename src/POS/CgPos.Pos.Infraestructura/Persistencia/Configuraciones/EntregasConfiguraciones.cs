using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class DestinoEntregaEnProcesoConfiguracion : IEntityTypeConfiguration<DestinoEntregaEnProceso>
{
    public void Configure(EntityTypeBuilder<DestinoEntregaEnProceso> constructor) =>
        constructor.ConfigurarDestino<DestinoEntregaEnProceso, LineaDestinoEntregaEnProceso>("DestinosEntregaVentaTemp");
}

internal sealed class DestinoEntregaGuardadaConfiguracion : IEntityTypeConfiguration<DestinoEntregaGuardada>
{
    public void Configure(EntityTypeBuilder<DestinoEntregaGuardada> constructor) =>
        constructor.ConfigurarDestino<DestinoEntregaGuardada, LineaDestinoEntregaGuardada>("DestinosEntregaVentaGuardadas");
}

internal sealed class DestinoEntregaCobradaConfiguracion : IEntityTypeConfiguration<DestinoEntregaCobrada>
{
    public void Configure(EntityTypeBuilder<DestinoEntregaCobrada> constructor) =>
        constructor.ConfigurarDestino<DestinoEntregaCobrada, LineaDestinoEntregaCobrada>("DestinosEntregaVenta");
}

internal sealed class LineaDestinoEntregaEnProcesoConfiguracion : IEntityTypeConfiguration<LineaDestinoEntregaEnProceso>
{
    public void Configure(EntityTypeBuilder<LineaDestinoEntregaEnProceso> constructor) => constructor.ConfigurarLineaDestino("LineasDestinoEntregaTemp");
}

internal sealed class LineaDestinoEntregaGuardadaConfiguracion : IEntityTypeConfiguration<LineaDestinoEntregaGuardada>
{
    public void Configure(EntityTypeBuilder<LineaDestinoEntregaGuardada> constructor) => constructor.ConfigurarLineaDestino("LineasDestinoEntregaGuardadas");
}

internal sealed class LineaDestinoEntregaCobradaConfiguracion : IEntityTypeConfiguration<LineaDestinoEntregaCobrada>
{
    public void Configure(EntityTypeBuilder<LineaDestinoEntregaCobrada> constructor) => constructor.ConfigurarLineaDestino("LineasDestinoEntrega");
}

/// <summary>
/// Lo común de los destinos de las tres tablas de la venta. Cada juego es independiente, con su numeración de Id y sus
/// llaves foráneas.
/// </summary>
internal static class ConfiguracionComunDestino
{
    public static void ConfigurarDestino<TDestino, TLinea>(this EntityTypeBuilder<TDestino> constructor, string tabla)
        where TDestino : DestinoEntrega
        where TLinea : LineaDestinoEntrega
    {
        constructor.ToTable(tabla);
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.SucursalRetiroNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre);
        constructor.Property(d => d.Direccion).HasMaxLength(DestinoEntrega.LargoMaximoDireccion);
        constructor.Property(d => d.Sector).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(d => d.Ciudad).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(d => d.Referencia).HasMaxLength(DestinoEntrega.LargoMaximoDireccion);
        constructor.Property(d => d.Telefono).HasMaxLength(DestinoEntrega.LargoMaximoTelefono);
        constructor.Property(d => d.Transportista).HasMaxLength(DestinoEntrega.LargoMaximoTexto);
        constructor.Property(d => d.CostoEnvio).HasPrecision(18, 2);
        constructor.Property(d => d.Comentario).HasMaxLength(DestinoEntrega.LargoMaximoComentario);
        constructor.Property(d => d.AutorizadoPorNombre).HasMaxLength(DestinoEntrega.LargoMaximoNombre);
        constructor.HasIndex(d => new { d.VentaId, d.Numero }).IsUnique();

        constructor.Ignore(d => d.Lineas);
        constructor.HasMany<TLinea>("_lineas").WithOne().HasForeignKey(l => l.DestinoEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation("_lineas").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }

    public static void ConfigurarLineaDestino<TLinea>(this EntityTypeBuilder<TLinea> constructor, string tabla) where TLinea : LineaDestinoEntrega
    {
        constructor.ToTable(tabla);
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
        constructor.Ignore(p => p.EstaAbierto);
        constructor.Ignore(p => p.Unidades);
        constructor.Ignore(p => p.UnidadesEntregadas);

        // El aviso al cliente y el seguimiento del despacho son del Central: la caja solo crea el pendiente al cobrar.
        constructor.Ignore(p => p.AvisoEnviadoEn);
        constructor.Ignore(p => p.NumeroCentral);

        constructor.HasIndex(p => p.Numero).IsUnique();
        constructor.HasIndex(p => p.VentaId);
        constructor.HasIndex(p => new { p.Estado, p.CreadoEn });

        constructor.HasMany(p => p.Lineas).WithOne().HasForeignKey(l => l.PendienteEntregaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(p => p.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
        // Entregar es del Central (allá se despacha): la caja solo crea el pendiente al cobrar y lo sube.
        constructor.Ignore(p => p.Entregas);
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

