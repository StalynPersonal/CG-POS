using CgPos.Dominio.Fiscal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class SecuenciaEcfConfiguracion : IEntityTypeConfiguration<SecuenciaEcf>
{
    public void Configure(EntityTypeBuilder<SecuenciaEcf> constructor)
    {
        constructor.ToTable("SecuenciasEcf");

        // Una letra: la que la DGII use en ese momento. Cada rango conserva la suya.
        constructor.Property(s => s.Serie).HasMaxLength(1).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.HasKey(s => s.Id);
        constructor.Ignore(s => s.Total);
        constructor.Ignore(s => s.Restantes);
        constructor.Ignore(s => s.PorcentajeRestante);
        constructor.HasIndex(s => new { s.CajaId, s.TipoComprobante, s.Activa, s.Desde });

        // De qué sucursal y caja es el rango, por código: los Id son de esta base, y el rango de e-NCF es lo último que
        // se puede confundir si la base se recrea o se restaura otra. Solo existen aquí; el Central lo sabe por su caja.
        constructor.Property<string>(CodigosSecuenciaEcf.Sucursal).HasMaxLength(CgPos.Dominio.Comun.CodigosCatalogo.LargoSucursalCaja)
            .IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property<string>(CodigosSecuenciaEcf.Caja).HasMaxLength(CgPos.Dominio.Comun.CodigosCatalogo.LargoSucursalCaja)
            .IsFixedLength().IsUnicode(false).IsRequired();
        // Un rango se identifica por su tipo y su inicio: los rangos no se solapan en la empresa.
        constructor.HasIndex(s => new { s.TipoComprobante, s.Desde }).IsUnique();
    }
}

/// <summary>Nombres de las columnas con los códigos de sucursal y caja del rango de e-CF (solo en la base de la caja).</summary>
internal static class CodigosSecuenciaEcf
{
    public const string Sucursal = "SucursalCodigo";
    public const string Caja = "CajaCodigo";
}

internal sealed class DocumentoElectronicoConfiguracion : IEntityTypeConfiguration<DocumentoElectronico>
{
    public void Configure(EntityTypeBuilder<DocumentoElectronico> constructor)
    {
        constructor.ToTable("DocumentosElectronicos");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.NumeroDocumento).HasMaxLength(CgPos.Dominio.Comun.NumeroDocumento.LargoMaximo).IsUnicode(false).IsRequired();
        constructor.Property(d => d.Encf).HasMaxLength(DocumentoElectronico.LargoEncf).IsUnicode(false).IsRequired();
        constructor.Property(d => d.CodigoSeguridad).HasMaxLength(20).IsUnicode(false).IsRequired();
        constructor.Property(d => d.HashXml).HasMaxLength(64).IsUnicode(false).IsRequired();
        constructor.Property(d => d.RutaXml).HasMaxLength(DocumentoElectronico.LargoMaximoRuta).IsRequired();
        constructor.Property(d => d.UrlTimbre).HasMaxLength(DocumentoElectronico.LargoMaximoUrl).IsRequired();
        constructor.Property(d => d.MensajeEstado).HasMaxLength(DocumentoElectronico.LargoMaximoMensaje);
        constructor.Property(d => d.MontoTotal).HasPrecision(18, 2);

        constructor.HasIndex(d => d.Encf).IsUnique();
        // Ventas y devoluciones numeran sus Id por separado: el par con el tipo de origen es lo único que identifica al documento.
        constructor.HasIndex(d => new { d.VentaId, d.TipoOrigen }).IsUnique();
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
        constructor.Property(h => h.Mensaje).HasMaxLength(DocumentoElectronico.LargoMaximoMensaje);
    }
}
