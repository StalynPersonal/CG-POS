using CgPos.Dominio.Promociones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class PromocionConfiguracion : IEntityTypeConfiguration<Promocion>
{
    // Las listas de artículos, familias y sucursales se guardan como Ids separados por coma: la caja filtra en memoria
    // las pocas ofertas vigentes, así no depende de funciones JSON (SQL Server 2014 en desarrollo).
    private static readonly ValueConverter<List<Guid>, string> ConversorIds = new(
        lista => string.Join(",", lista),
        texto => string.IsNullOrEmpty(texto)
            ? new List<Guid>()
            : texto.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(parte => Guid.Parse(parte)).ToList());

    private static readonly ValueComparer<List<Guid>> ComparadorIds = new(
        (a, b) => a!.SequenceEqual(b!),
        lista => lista.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
        lista => lista.ToList());

    public void Configure(EntityTypeBuilder<Promocion> constructor)
    {
        constructor.ToTable("Promociones");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Id).ValueGeneratedNever();
        constructor.Property(p => p.Codigo).HasMaxLength(Promocion.LargoMaximoCodigo).IsRequired();
        constructor.Property(p => p.Nombre).HasMaxLength(Promocion.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.Valor).HasPrecision(18, 2);
        constructor.Property(p => p.CantidadMinima).HasPrecision(18, 3);
        constructor.Property(p => p.LimitePorCliente).HasPrecision(18, 3);
        constructor.Ignore(p => p.DescripcionCorta);
        constructor.Ignore(p => p.Articulos);
        constructor.Ignore(p => p.Familias);
        constructor.Ignore(p => p.Sucursales);

        ListaIds(constructor, "_articulos", "Articulos");
        ListaIds(constructor, "_familias", "Familias");
        ListaIds(constructor, "_sucursales", "Sucursales");

        constructor.HasIndex(p => p.Codigo).IsUnique();
        constructor.HasIndex(p => new { p.Activa, p.VigenteHasta });
    }

    private static void ListaIds(EntityTypeBuilder<Promocion> constructor, string campo, string columna)
    {
        var propiedad = constructor.Property<List<Guid>>(campo)
            .HasColumnName(columna)
            .HasConversion(ConversorIds, ComparadorIds)
            .IsUnicode(false)
            .IsRequired();
        propiedad.Metadata.SetMaxLength(null);
    }
}

internal sealed class MotivoDescuentoConfiguracion : IEntityTypeConfiguration<MotivoDescuento>
{
    public void Configure(EntityTypeBuilder<MotivoDescuento> constructor)
    {
        constructor.ToTable("MotivosDescuento");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Id).ValueGeneratedNever();
        constructor.Property(m => m.Codigo).HasMaxLength(MotivoDescuento.LargoMaximoCodigo).IsRequired();
        constructor.Property(m => m.Nombre).HasMaxLength(MotivoDescuento.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
    }
}

internal sealed class TopeDescuentoConfiguracion : IEntityTypeConfiguration<TopeDescuento>
{
    public void Configure(EntityTypeBuilder<TopeDescuento> constructor)
    {
        constructor.ToTable("TopesDescuento");
        constructor.HasKey(t => t.Id);
        constructor.Property(t => t.Id).ValueGeneratedNever();
        constructor.Property(t => t.PorcentajeMaximo).HasPrecision(5, 2);
        constructor.Property(t => t.MontoMaximo).HasPrecision(18, 2);
        constructor.HasIndex(t => new { t.Nivel, t.FamiliaId, t.ArticuloId });
    }
}
