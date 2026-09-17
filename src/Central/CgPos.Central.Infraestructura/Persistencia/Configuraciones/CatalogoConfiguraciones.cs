using CgPos.Central.Infraestructura.Maestros;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

/// <summary>Columnas comunes de las tablas de maestros: versión de fila para la bajada a las cajas y quién lo cambió por última vez.</summary>
internal static class ColumnasMaestro
{
    public static void Configurar<T>(EntityTypeBuilder<T> constructor) where T : class
    {
        constructor.Property<long>(ContextoDatosCentral.ColumnaVersion).IsRowVersion().HasConversion<byte[]>();
        constructor.HasIndex(ContextoDatosCentral.ColumnaVersion);
        constructor.Property<DateTimeOffset>(TablaMaestro.ColumnaModificadoEn).HasPrecision(3);
        constructor.Property<string>(TablaMaestro.ColumnaModificadoPor).HasMaxLength(MaestroCentral.LargoMaximoUsuario).IsRequired();
    }
}

internal sealed class DepartamentoConfiguracion : IEntityTypeConfiguration<Departamento>
{
    public void Configure(EntityTypeBuilder<Departamento> constructor)
    {
        constructor.ToTable("Departamentos");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Id).ValueGeneratedNever();
        constructor.Property(d => d.Codigo).HasMaxLength(Departamento.LargoMaximoCodigo).IsRequired();
        constructor.Property(d => d.Nombre).HasMaxLength(Departamento.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(d => d.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> constructor)
    {
        constructor.ToTable("Categorias");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();
        constructor.Property(c => c.Codigo).HasMaxLength(Categoria.LargoMaximoCodigo).IsRequired();
        constructor.Property(c => c.Nombre).HasMaxLength(Categoria.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(c => c.Codigo).IsUnique();
        constructor.HasOne<Departamento>().WithMany().HasForeignKey(c => c.DepartamentoId).OnDelete(DeleteBehavior.Restrict);
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class MarcaConfiguracion : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> constructor)
    {
        constructor.ToTable("Marcas");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Id).ValueGeneratedNever();
        constructor.Property(m => m.Codigo).HasMaxLength(Marca.LargoMaximoCodigo).IsRequired();
        constructor.Property(m => m.Nombre).HasMaxLength(Marca.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class UnidadMedidaConfiguracion : IEntityTypeConfiguration<UnidadMedida>
{
    public void Configure(EntityTypeBuilder<UnidadMedida> constructor)
    {
        constructor.ToTable("UnidadesMedida");
        constructor.HasKey(u => u.Id);
        constructor.Property(u => u.Id).ValueGeneratedNever();
        constructor.Property(u => u.Codigo).HasMaxLength(UnidadMedida.LargoMaximoCodigo).IsRequired();
        constructor.Property(u => u.Nombre).HasMaxLength(UnidadMedida.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(u => u.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}

internal sealed class ImpuestoConfiguracion : IEntityTypeConfiguration<Impuesto>
{
    public void Configure(EntityTypeBuilder<Impuesto> constructor)
    {
        constructor.ToTable("Impuestos");
        constructor.HasKey(i => i.Id);
        constructor.Property(i => i.Id).ValueGeneratedNever();
        constructor.Property(i => i.Codigo).HasMaxLength(Impuesto.LargoMaximoCodigo).IsRequired();
        constructor.Property(i => i.Nombre).HasMaxLength(Impuesto.LargoMaximoNombre).IsRequired();
        constructor.Property(i => i.Porcentaje).HasPrecision(5, 2);
        constructor.HasIndex(i => i.Codigo).IsUnique();
        ColumnasMaestro.Configurar(constructor);
    }
}
