using CgPos.Dominio.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

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
        // Sin largo máximo (nvarchar(maximo)): se anula la convención de 256.
        constructor.Property(r => r.Detalle).Metadata.SetMaxLength(null);
        constructor.Property(r => r.Motivo).HasMaxLength(RegistroAuditoria.LargoMaximoMotivo);
        constructor.Property(r => r.UsuarioNombre).HasMaxLength(RegistroAuditoria.LargoMaximoNombre);
        constructor.Property(r => r.AutorizadoPorNombre).HasMaxLength(RegistroAuditoria.LargoMaximoNombre);

        constructor.HasIndex(r => r.OcurridoEn);
        constructor.HasIndex(r => new { r.TipoEntidad, r.EntidadId });
        constructor.HasIndex(r => r.Accion);
    }
}
