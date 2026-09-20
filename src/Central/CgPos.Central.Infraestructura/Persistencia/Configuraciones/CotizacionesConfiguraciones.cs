using CgPos.Central.Infraestructura.Organizacion;
using CgPos.Dominio.Cotizaciones;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class CotizacionConfiguracion : IEntityTypeConfiguration<Cotizacion>
{
    public void Configure(EntityTypeBuilder<Cotizacion> constructor)
    {
        constructor.ToTable("Cotizaciones");
        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Numero).HasMaxLength(Cotizacion.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(c => c.ClienteNombre).HasMaxLength(Cotizacion.LargoMaximoNombre).IsRequired();
        constructor.Property(c => c.ClienteDocumento).HasMaxLength(Cotizacion.LargoMaximoDocumento);
        constructor.Property(c => c.ClienteTelefono).HasMaxLength(Cotizacion.LargoMaximoContacto);
        constructor.Property(c => c.ClienteCorreo).HasMaxLength(Cotizacion.LargoMaximoContacto);
        constructor.Property(c => c.Observacion).HasMaxLength(Cotizacion.LargoMaximoObservacion);
        constructor.Property(c => c.MotivoAnulacion).HasMaxLength(Cotizacion.LargoMaximoObservacion);
        constructor.Property(c => c.VentaNumero).HasMaxLength(Cotizacion.LargoMaximoNumero).IsUnicode(false);
        constructor.Property(c => c.CreadaPor).HasMaxLength(Cotizacion.LargoMaximoUsuario).IsRequired();
        constructor.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20).IsUnicode(false);

        // Se calculan al leerlas; no son columnas.
        constructor.Ignore(c => c.EsEditable);

        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // La caja la busca por su número, que es el que el cliente trae impreso.
        constructor.HasIndex(c => c.Numero).IsUnique();
        constructor.HasIndex(c => c.ClienteDocumento);
        constructor.HasIndex(c => c.VenceEn);

        constructor.HasMany(c => c.Lineas).WithOne().HasForeignKey(l => l.CotizacionId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaCotizacionConfiguracion : IEntityTypeConfiguration<LineaCotizacion>
{
    public void Configure(EntityTypeBuilder<LineaCotizacion> constructor)
    {
        constructor.ToTable("LineasCotizacion");
        constructor.HasKey(l => l.Id);

        constructor.Property(l => l.ArticuloCodigo).HasMaxLength(LineaCotizacion.LargoMaximoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(l => l.Descripcion).HasMaxLength(LineaCotizacion.LargoMaximoDescripcion).IsRequired();
        constructor.Property(l => l.UnidadMedida).HasMaxLength(LineaCotizacion.LargoMaximoUnidad).IsUnicode(false);
        constructor.Property(l => l.Cantidad).HasPrecision(18, 3);
        constructor.Property(l => l.PrecioUnitario).HasPrecision(18, 2);
        constructor.Property(l => l.Descuento).HasPrecision(18, 2);
        constructor.Property(l => l.PorcentajeImpuesto).HasPrecision(5, 2);

        // Los importes salen de la cantidad, el precio y el descuento: no se guardan para que no puedan contradecirse.
        constructor.Ignore(l => l.ImporteBruto);
        constructor.Ignore(l => l.Importe);
        constructor.Ignore(l => l.Base);
        constructor.Ignore(l => l.Impuesto);

        // Un artículo aparece una sola vez en cada cotización.
        constructor.HasIndex(l => new { l.CotizacionId, l.ArticuloCodigo }).IsUnique();
    }
}

/// <summary>Contador de los documentos que numera el Central; su clave es el prefijo (COT, LB…).</summary>
internal sealed class SecuenciaCentralConfiguracion : IEntityTypeConfiguration<SecuenciaCentral>
{
    public void Configure(EntityTypeBuilder<SecuenciaCentral> constructor)
    {
        constructor.ToTable("SecuenciasCentral");
        constructor.HasKey(s => s.Prefijo);
        constructor.Property(s => s.Prefijo).HasMaxLength(SecuenciaCentral.LargoMaximoPrefijo).IsUnicode(false);
    }
}
