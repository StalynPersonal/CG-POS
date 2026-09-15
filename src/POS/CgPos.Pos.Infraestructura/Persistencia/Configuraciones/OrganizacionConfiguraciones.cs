using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class EmpresaConfiguracion : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> constructor)
    {
        constructor.ToTable("Empresas");
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Id).ValueGeneratedNever();

        constructor.Property(e => e.Rnc).HasMaxLength(Empresa.LargoRnc).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(e => e.RazonSocial).HasMaxLength(Empresa.LargoMaximoNombre).IsRequired();
        constructor.Property(e => e.NombreComercial).HasMaxLength(Empresa.LargoMaximoNombre);
        constructor.Property(e => e.Direccion).HasMaxLength(Empresa.LargoMaximoDireccion);
        constructor.Property(e => e.Telefono).HasMaxLength(Empresa.LargoMaximoTelefono);

        constructor.HasIndex(e => e.Rnc).IsUnique();
    }
}

internal sealed class SucursalConfiguracion : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> constructor)
    {
        constructor.ToTable("Sucursales");
        constructor.HasKey(s => s.Id);
        constructor.Property(s => s.Id).ValueGeneratedNever();

        constructor.Property(s => s.Codigo).HasMaxLength(Sucursal.LargoMaximoCodigo).IsRequired();
        constructor.Property(s => s.Nombre).HasMaxLength(Empresa.LargoMaximoNombre).IsRequired();
        constructor.Property(s => s.Direccion).HasMaxLength(Empresa.LargoMaximoDireccion);
        constructor.Property(s => s.Telefono).HasMaxLength(Empresa.LargoMaximoTelefono);

        constructor.HasOne<Empresa>().WithMany().HasForeignKey(s => s.EmpresaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(s => new { s.EmpresaId, s.Codigo }).IsUnique();
    }
}

internal sealed class CajaConfiguracion : IEntityTypeConfiguration<Caja>
{
    public void Configure(EntityTypeBuilder<Caja> constructor)
    {
        constructor.ToTable("Cajas");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.Codigo).HasMaxLength(Caja.LargoMaximoCodigo).IsRequired();
        constructor.Property(c => c.Nombre).HasMaxLength(Caja.LargoMaximoNombre).IsRequired();

        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(c => new { c.SucursalId, c.Codigo }).IsUnique();
    }
}

internal sealed class ParametroConfiguracion : IEntityTypeConfiguration<Parametro>
{
    public void Configure(EntityTypeBuilder<Parametro> constructor)
    {
        constructor.ToTable("Parametros", tabla =>
            tabla.HasCheckConstraint("CK_Parametros_UnSoloAmbito", "[SucursalId] IS NULL OR [CajaId] IS NULL"));
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.Id).ValueGeneratedNever();

        constructor.Property(p => p.Clave).HasMaxLength(Parametro.LargoMaximoClave).IsRequired();
        constructor.Property(p => p.Valor).HasMaxLength(Parametro.LargoMaximoValor).IsRequired();
        constructor.Property(p => p.Descripcion).HasMaxLength(Parametro.LargoMaximoDescripcion);

        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(p => p.SucursalId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Caja>().WithMany().HasForeignKey(p => p.CajaId).OnDelete(DeleteBehavior.Restrict);

        // Una clave por ámbito. SQL Server trata los NULL como iguales en índices únicos,
        // así que solo puede existir un valor general por clave.
        constructor.HasIndex(p => new { p.Clave, p.SucursalId, p.CajaId }).IsUnique().HasFilter(null);
    }
}
