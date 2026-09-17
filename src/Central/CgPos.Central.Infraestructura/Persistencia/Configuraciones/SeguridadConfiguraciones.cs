using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class RolCentralConfiguracion : IEntityTypeConfiguration<RolCentral>
{
    public void Configure(EntityTypeBuilder<RolCentral> constructor)
    {
        constructor.ToTable("RolesCentral");
        constructor.HasKey(r => r.Id);

        constructor.Property(r => r.Codigo).HasMaxLength(RolCentral.LargoMaximoCodigo).IsRequired();
        constructor.Property(r => r.Nombre).HasMaxLength(RolCentral.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(r => r.Codigo).IsUnique();

        constructor.HasMany(r => r.PermisosAsignados).WithOne().HasForeignKey(rp => rp.RolId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(r => r.PermisosAsignados).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RolCentralPermisoConfiguracion : IEntityTypeConfiguration<RolCentralPermiso>
{
    public void Configure(EntityTypeBuilder<RolCentralPermiso> constructor)
    {
        // Los permisos son los del catálogo en código (CatalogoPermisosCentral): no hay tabla de permisos.
        constructor.ToTable("RolesCentralPermisos");
        constructor.HasKey(rp => new { rp.RolId, rp.PermisoCodigo });
        constructor.Property(rp => rp.PermisoCodigo).HasMaxLength(Permiso.LargoMaximoCodigo).IsUnicode(false);
    }
}

internal sealed class UsuarioCentralConfiguracion : IEntityTypeConfiguration<UsuarioCentral>
{
    public void Configure(EntityTypeBuilder<UsuarioCentral> constructor)
    {
        constructor.ToTable("UsuariosCentral");
        constructor.HasKey(u => u.Id);

        constructor.Property(u => u.Codigo).HasMaxLength(UsuarioCentral.LargoMaximoCodigo).IsRequired();
        constructor.Property(u => u.Nombre).HasMaxLength(UsuarioCentral.LargoMaximoNombre).IsRequired();
        constructor.Property(u => u.Correo).HasMaxLength(UsuarioCentral.LargoMaximoCorreo);
        constructor.Property(u => u.ContrasenaHash).HasMaxLength(UsuarioCentral.LargoMaximoHashContrasena).IsUnicode(false).IsRequired();

        constructor.HasIndex(u => u.Codigo).IsUnique();
        constructor.HasOne<RolCentral>().WithMany().HasForeignKey(u => u.RolId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SesionCentralConfiguracion : IEntityTypeConfiguration<SesionCentral>
{
    public void Configure(EntityTypeBuilder<SesionCentral> constructor)
    {
        constructor.ToTable("SesionesCentral");
        constructor.HasKey(s => s.Id);
        constructor.Property(s => s.Id).ValueGeneratedNever();

        constructor.Property(s => s.TokenHash).HasMaxLength(SesionCentral.LargoHashToken).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(s => s.MotivoRevocacion).HasMaxLength(SesionCentral.LargoMaximoMotivo);
        constructor.Property(s => s.DireccionIp).HasMaxLength(SesionCentral.LargoMaximoIp).IsUnicode(false);
        constructor.Property(s => s.AgenteUsuario).HasMaxLength(SesionCentral.LargoMaximoAgenteUsuario);

        // Dos renovaciones simultáneas con el mismo token: solo una lo marca como usado.
        constructor.Property(s => s.UsadaEn).IsConcurrencyToken();

        constructor.HasIndex(s => s.TokenHash).IsUnique();
        constructor.HasIndex(s => s.Familia);
        constructor.HasIndex(s => new { s.UsuarioId, s.RevocadaEn });

        constructor.HasOne<UsuarioCentral>().WithMany().HasForeignKey(s => s.UsuarioId).OnDelete(DeleteBehavior.Restrict);
    }
}
