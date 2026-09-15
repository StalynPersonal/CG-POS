using CgPos.Dominio.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class SecuenciaEcfConfiguracion : IEntityTypeConfiguration<SecuenciaEcf>
{
    public void Configure(EntityTypeBuilder<SecuenciaEcf> constructor)
    {
        constructor.ToTable("SecuenciasEcf");
        constructor.HasKey(s => s.Id);
        constructor.Property(s => s.Id).ValueGeneratedNever();
        constructor.Ignore(s => s.Total);
        constructor.Ignore(s => s.Restantes);
        constructor.Ignore(s => s.PorcentajeRestante);
        constructor.HasIndex(s => new { s.CajaId, s.TipoComprobante, s.Activa, s.Desde });
    }
}

internal sealed class DocumentoElectronicoConfiguracion : IEntityTypeConfiguration<DocumentoElectronico>
{
    public void Configure(EntityTypeBuilder<DocumentoElectronico> constructor)
    {
        constructor.ToTable("DocumentosElectronicos");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Id).ValueGeneratedNever();
        constructor.Property(d => d.Encf).HasMaxLength(DocumentoElectronico.LargoEncf).IsUnicode(false).IsRequired();
        constructor.Property(d => d.CodigoSeguridad).HasMaxLength(20).IsUnicode(false).IsRequired();
        constructor.Property(d => d.HashXml).HasMaxLength(64).IsUnicode(false).IsRequired();
        constructor.Property(d => d.RutaXml).HasMaxLength(DocumentoElectronico.LargoMaximoRuta).IsRequired();
        constructor.Property(d => d.UrlTimbre).HasMaxLength(DocumentoElectronico.LargoMaximoUrl).IsRequired();
        constructor.Property(d => d.MensajeEstado).HasMaxLength(DocumentoElectronico.LargoMaximoMensaje);
        constructor.Property(d => d.MontoTotal).HasPrecision(18, 2);

        constructor.HasIndex(d => d.Encf).IsUnique();
        constructor.HasIndex(d => d.VentaId).IsUnique();
        constructor.HasIndex(d => new { d.CajaId, d.Estado });

        constructor.HasMany(d => d.Historial).WithOne().HasForeignKey(h => h.DocumentoElectronicoId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(d => d.Historial).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class HistorialEstadoEcfConfiguracion : IEntityTypeConfiguration<HistorialEstadoEcf>
{
    public void Configure(EntityTypeBuilder<HistorialEstadoEcf> constructor)
    {
        constructor.ToTable("HistorialEstadosEcf");
        constructor.HasKey(h => h.Id);
        constructor.Property(h => h.Id).ValueGeneratedNever();
        constructor.Property(h => h.Mensaje).HasMaxLength(DocumentoElectronico.LargoMaximoMensaje);
    }
}
