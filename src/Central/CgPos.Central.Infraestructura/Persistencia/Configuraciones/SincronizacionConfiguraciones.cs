using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class DocumentoRecibidoConfiguracion : IEntityTypeConfiguration<DocumentoRecibido>
{
    public void Configure(EntityTypeBuilder<DocumentoRecibido> constructor)
    {
        constructor.ToTable("DocumentosRecibidos");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Id).ValueGeneratedNever();

        constructor.Property(d => d.TipoMensaje).HasMaxLength(DocumentoRecibido.LargoMaximoTipo).IsRequired();
        constructor.Property(d => d.Contenido).IsRequired().Metadata.SetMaxLength(null);
        constructor.Property(d => d.HashContenido).HasMaxLength(DocumentoRecibido.LargoHash).IsFixedLength().IsUnicode(false).IsRequired();

        constructor.HasOne<Caja>().WithMany().HasForeignKey(d => d.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(d => d.SucursalId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasIndex(d => new { d.CajaId, d.RecibidoEn });
        constructor.HasIndex(d => d.AgregadoId);
        constructor.HasIndex(d => new { d.TipoMensaje, d.RecibidoEn });
    }
}

internal sealed class ComprobanteRecibidoConfiguracion : IEntityTypeConfiguration<ComprobanteRecibido>
{
    public void Configure(EntityTypeBuilder<ComprobanteRecibido> constructor)
    {
        constructor.ToTable("ComprobantesRecibidos");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.Encf).HasMaxLength(DocumentoElectronico.LargoEncf).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(c => c.XmlFirmado).IsRequired().Metadata.SetMaxLength(null);
        constructor.Property(c => c.HashXml).HasMaxLength(DocumentoRecibido.LargoHash).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(c => c.EstadoDgii).HasConversion<string>().HasMaxLength(30);
        constructor.Property(c => c.MensajeDgii).HasMaxLength(2000);

        constructor.HasOne<DocumentoRecibido>().WithMany().HasForeignKey(c => c.DocumentoId).OnDelete(DeleteBehavior.Restrict);

        // Un e-NCF se envía a la DGII una sola vez.
        constructor.HasIndex(c => c.Encf).IsUnique();
        constructor.HasIndex(c => new { c.EstadoDgii, c.RecibidoEn });
        constructor.HasIndex(c => c.DocumentoId);
    }
}

internal sealed class ConflictoSincronizacionConfiguracion : IEntityTypeConfiguration<ConflictoSincronizacion>
{
    public void Configure(EntityTypeBuilder<ConflictoSincronizacion> constructor)
    {
        constructor.ToTable("ConflictosSincronizacion");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.TipoMensaje).HasMaxLength(DocumentoRecibido.LargoMaximoTipo).IsRequired();
        constructor.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(40);
        constructor.Property(c => c.Detalle).HasMaxLength(ConflictoSincronizacion.LargoMaximoDetalle).IsRequired();
        constructor.Property(c => c.ResueltoPor).HasMaxLength(ConflictoSincronizacion.LargoMaximoNombre);
        constructor.Property(c => c.Resolucion).HasMaxLength(ConflictoSincronizacion.LargoMaximoResolucion);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasIndex(c => new { c.MensajeId, c.CajaId, c.Tipo });
        constructor.HasIndex(c => new { c.CajaId, c.ResueltoEn });
    }
}

internal sealed class MaestroCentralConfiguracion : IEntityTypeConfiguration<MaestroCentral>
{
    public void Configure(EntityTypeBuilder<MaestroCentral> constructor)
    {
        constructor.ToTable("MaestrosCentral");
        constructor.HasKey(m => new { m.Tipo, m.Id });

        constructor.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(40);
        constructor.Property(m => m.Codigo).HasMaxLength(MaestroCentral.LargoMaximoCodigo);
        constructor.Property(m => m.Contenido).IsRequired().Metadata.SetMaxLength(null);
        constructor.Property(m => m.ModificadoPor).HasMaxLength(MaestroCentral.LargoMaximoUsuario).IsRequired();

        constructor.HasOne<Caja>().WithMany().HasForeignKey(m => m.CajaId).OnDelete(DeleteBehavior.Restrict);

        // Un código (artículo, cédula del miembro…) no se repite dentro de su tipo.
        constructor.HasIndex(m => new { m.Tipo, m.Codigo }).IsUnique().HasFilter("[Codigo] IS NOT NULL");

        // La bajada a las cajas filtra por versión de fila.
        constructor.Property<long>(ContextoDatosCentral.ColumnaVersion).IsRowVersion().HasConversion<byte[]>();
        constructor.HasIndex(ContextoDatosCentral.ColumnaVersion);
    }
}

internal sealed class EstadoSincronizacionCajaConfiguracion : IEntityTypeConfiguration<EstadoSincronizacionCaja>
{
    public void Configure(EntityTypeBuilder<EstadoSincronizacionCaja> constructor)
    {
        constructor.ToTable("EstadosSincronizacionCaja");
        constructor.HasKey(e => e.CajaId);
        constructor.Property(e => e.UltimoError).HasMaxLength(EstadoSincronizacionCaja.LargoMaximoError);

        constructor.HasOne<Caja>().WithOne().HasForeignKey<EstadoSincronizacionCaja>(e => e.CajaId).OnDelete(DeleteBehavior.Restrict);
    }
}
