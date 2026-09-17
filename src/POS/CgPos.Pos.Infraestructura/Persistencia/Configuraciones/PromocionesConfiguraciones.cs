using CgPos.Dominio.Promociones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class DescuentoTarjetaConfiguracion : IEntityTypeConfiguration<DescuentoTarjeta>
{
    public void Configure(EntityTypeBuilder<DescuentoTarjeta> constructor)
    {
        constructor.ToTable("DescuentosTarjeta");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Codigo).HasMaxLength(DescuentoTarjeta.LargoMaximoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(d => d.Nombre).HasMaxLength(DescuentoTarjeta.LargoMaximoNombre).IsRequired();
        constructor.Property(d => d.Bines).HasMaxLength(DescuentoTarjeta.LargoMaximoBines).IsUnicode(false).IsRequired();
        constructor.HasIndex(d => d.Codigo).IsUnique();
    }
}

internal sealed class PromocionConfiguracion : IEntityTypeConfiguration<Promocion>
{
    // Las listas de artículos, departamentos y sucursales se guardan como Ids separados por coma: la caja filtra en memoria
    // las pocas ofertas vigentes, así no depende de funciones JSON (SQL Server 2014 en desarrollo).
    private static readonly ValueConverter<List<int>, string> ConversorIds = new(
        lista => string.Join(",", lista),
        texto => string.IsNullOrEmpty(texto)
            ? new List<int>()
            : texto.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(parte => int.Parse(parte, System.Globalization.CultureInfo.InvariantCulture)).ToList());

    private static readonly ValueComparer<List<int>> ComparadorIds = new(
        (a, b) => a!.SequenceEqual(b!),
        lista => lista.Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
        lista => lista.ToList());

    public void Configure(EntityTypeBuilder<Promocion> constructor)
    {
        constructor.ToTable("Promociones");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Codigo).HasMaxLength(Promocion.LargoMaximoCodigo).IsRequired();
        constructor.Property(p => p.Nombre).HasMaxLength(Promocion.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.Valor).HasPrecision(18, 2);
        constructor.Property(p => p.CantidadMinima).HasPrecision(18, 3);
        constructor.Property(p => p.LimitePorCliente).HasPrecision(18, 3);
        constructor.Ignore(p => p.DescripcionCorta);
        constructor.Ignore(p => p.Articulos);
        constructor.Ignore(p => p.Departamentos);
        constructor.Ignore(p => p.Categorias);
        constructor.Ignore(p => p.Marcas);
        constructor.Ignore(p => p.Sucursales);

        ListaIds(constructor, "_articulos", "Articulos");
        ListaIds(constructor, "_departamentos", "Departamentos");
        ListaIds(constructor, "_categorias", "Categorias");
        ListaIds(constructor, "_marcas", "Marcas");
        ListaIds(constructor, "_sucursales", "Sucursales");

        constructor.HasIndex(p => p.Codigo).IsUnique();
        constructor.HasIndex(p => new { p.Activa, p.VigenteHasta });
    }

    private static void ListaIds(EntityTypeBuilder<Promocion> constructor, string campo, string columna)
    {
        var propiedad = constructor.Property<List<int>>(campo)
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
        constructor.Property(t => t.PorcentajeMaximo).HasPrecision(5, 2);
        constructor.Property(t => t.MontoMaximo).HasPrecision(18, 2);
        constructor.Ignore(t => t.EsGeneral);
        constructor.HasIndex(t => t.Codigo).IsUnique();
        constructor.HasIndex(t => new { t.Nivel, t.DepartamentoId, t.ArticuloId });
    }
}
