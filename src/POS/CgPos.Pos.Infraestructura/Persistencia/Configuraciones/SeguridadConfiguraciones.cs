using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class PermisoConfiguracion : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> constructor)
    {
        constructor.ToTable("Permisos");
        constructor.HasKey(p => p.Codigo);

        constructor.Property(p => p.Codigo).HasMaxLength(Permiso.LargoMaximoCodigo).IsUnicode(false);
        constructor.Property(p => p.Modulo).HasMaxLength(Permiso.LargoMaximoModulo).IsRequired();
        constructor.Property(p => p.Descripcion).HasMaxLength(Permiso.LargoMaximoDescripcion).IsRequired();
    }
}

internal sealed class RolConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> constructor)
    {
        constructor.ToTable("Roles");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Id).ValueGeneratedNever();

        constructor.Property(r => r.Codigo).HasMaxLength(Rol.LargoMaximoCodigo).IsRequired();
        constructor.Property(r => r.Nombre).HasMaxLength(Rol.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(r => r.Codigo).IsUnique();

        constructor.HasMany(r => r.PermisosAsignados).WithOne().HasForeignKey(rp => rp.RolId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(r => r.PermisosAsignados).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RolPermisoConfiguracion : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> constructor)
    {
        constructor.ToTable("RolesPermisos");
        constructor.HasKey(rp => new { rp.RolId, rp.PermisoCodigo });

        constructor.Property(rp => rp.PermisoCodigo).HasMaxLength(Permiso.LargoMaximoCodigo).IsUnicode(false);
        constructor.HasOne<Permiso>().WithMany().HasForeignKey(rp => rp.PermisoCodigo).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> constructor)
    {
        constructor.ToTable("Usuarios");
        constructor.HasKey(u => u.Id);
        constructor.Property(u => u.Id).ValueGeneratedNever();

        constructor.Property(u => u.Codigo).HasMaxLength(Usuario.LargoMaximoCodigo).IsRequired();
        constructor.Property(u => u.Nombre).HasMaxLength(Usuario.LargoMaximoNombre).IsRequired();
        constructor.Property(u => u.PinHash).HasMaxLength(Usuario.LargoMaximoHashPin).IsUnicode(false);
        constructor.Property(u => u.CredencialBarrasHash)
            .HasMaxLength(Usuario.LargoHashCredencialBarras).IsFixedLength().IsUnicode(false);

        constructor.HasIndex(u => u.Codigo).IsUnique();
        // Único solo entre los usuarios que tienen credencial de barras (EF agrega el filtro IS NOT NULL).
        constructor.HasIndex(u => u.CredencialBarrasHash).IsUnique();

        constructor.HasOne<Rol>().WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasMany(u => u.CajasAsignadas).WithOne().HasForeignKey(uc => uc.UsuarioId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(u => u.CajasAsignadas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class UsuarioCajaConfiguracion : IEntityTypeConfiguration<UsuarioCaja>
{
    public void Configure(EntityTypeBuilder<UsuarioCaja> constructor)
    {
        constructor.ToTable("UsuariosCajas");
        constructor.HasKey(uc => new { uc.UsuarioId, uc.CajaId });

        constructor.HasOne<Caja>().WithMany().HasForeignKey(uc => uc.CajaId).OnDelete(DeleteBehavior.Restrict);
    }
}
