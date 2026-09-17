using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class EmpresaConfiguracion : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> constructor)
    {
        constructor.ToTable("Empresas");
        constructor.HasKey(e => e.Id);
        constructor.Property(e => e.Id).ValueGeneratedNever();

        // En el Central todos los datos de la empresa y de las sucursales son obligatorios (salen en e-CF, tickets y reportes).
        constructor.Property(e => e.Rnc).HasMaxLength(Empresa.LargoRnc).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(e => e.RazonSocial).HasMaxLength(Empresa.LargoMaximoNombre).IsRequired();
        constructor.Property(e => e.NombreComercial).HasMaxLength(Empresa.LargoMaximoNombre).IsRequired();
        constructor.Property(e => e.Direccion).HasMaxLength(Empresa.LargoMaximoDireccion).IsRequired();
        constructor.Property(e => e.Telefono).HasMaxLength(Empresa.LargoMaximoTelefono).IsRequired();

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

        constructor.Property(s => s.Nombre).HasMaxLength(Empresa.LargoMaximoNombre).IsRequired();
        constructor.Property(s => s.Direccion).HasMaxLength(Empresa.LargoMaximoDireccion).IsRequired();
        constructor.Property(s => s.Telefono).HasMaxLength(Empresa.LargoMaximoTelefono).IsRequired();

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

        // Una clave por ámbito; SQL Server trata los NULL como iguales en índices únicos.
        constructor.HasIndex(p => new { p.Clave, p.SucursalId, p.CajaId }).IsUnique().HasFilter(null);
    }
}

internal sealed class CredencialDispositivoConfiguracion : IEntityTypeConfiguration<CredencialDispositivo>
{
    public void Configure(EntityTypeBuilder<CredencialDispositivo> constructor)
    {
        constructor.ToTable("CredencialesDispositivo");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();

        constructor.Property(c => c.SecretoHash).HasMaxLength(CredencialDispositivo.LargoHashSecreto).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(c => c.EmitidaPor).HasMaxLength(CredencialDispositivo.LargoMaximoNombre).IsRequired();
        constructor.Property(c => c.MotivoRevocacion).HasMaxLength(CredencialDispositivo.LargoMaximoMotivo);
        constructor.Property(c => c.UltimaIp).HasMaxLength(CredencialDispositivo.LargoMaximoIp).IsUnicode(false);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);

        // Una sola credencial activa por caja.
        constructor.HasIndex(c => c.CajaId).IsUnique().HasFilter("[RevocadaEn] IS NULL").HasDatabaseName("IX_CredencialesDispositivo_CajaActiva");
    }
}

internal sealed class RegistroAuditoriaConfiguracion : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> constructor)
    {
        constructor.ToTable("Auditoria");

        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Id).ValueGeneratedNever();

        constructor.Property(r => r.Accion).HasMaxLength(RegistroAuditoria.LargoMaximoAccion).IsRequired();
        constructor.Property(r => r.TipoEntidad).HasMaxLength(RegistroAuditoria.LargoMaximoTipoEntidad).IsRequired();
        constructor.Property(r => r.EntidadId).HasMaxLength(RegistroAuditoria.LargoMaximoEntidadId);
        // Sin largo máximo (nvarchar(max)): se anula la convención de 256.
        constructor.Property(r => r.Detalle).Metadata.SetMaxLength(null);
        constructor.Property(r => r.Motivo).HasMaxLength(RegistroAuditoria.LargoMaximoMotivo);
        constructor.Property(r => r.UsuarioNombre).HasMaxLength(RegistroAuditoria.LargoMaximoNombre);
        constructor.Property(r => r.AutorizadoPorNombre).HasMaxLength(RegistroAuditoria.LargoMaximoNombre);

        constructor.HasIndex(r => r.OcurridoEn);
        constructor.HasIndex(r => new { r.TipoEntidad, r.EntidadId });
        constructor.HasIndex(r => r.Accion);
    }
}
