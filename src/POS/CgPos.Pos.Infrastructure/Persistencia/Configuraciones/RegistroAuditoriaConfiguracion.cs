using CgPos.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infrastructure.Persistencia.Configuraciones;

internal sealed class RegistroAuditoriaConfiguracion : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("Auditoria");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Accion).HasMaxLength(RegistroAuditoria.LargoMaximoAccion).IsRequired();
        builder.Property(r => r.TipoEntidad).HasMaxLength(RegistroAuditoria.LargoMaximoTipoEntidad).IsRequired();
        builder.Property(r => r.EntidadId).HasMaxLength(RegistroAuditoria.LargoMaximoEntidadId);
        // Sin largo máximo (nvarchar(max)): se anula la convención de 256.
        builder.Property(r => r.Detalle).Metadata.SetMaxLength(null);
        builder.Property(r => r.Motivo).HasMaxLength(RegistroAuditoria.LargoMaximoMotivo);
        builder.Property(r => r.UsuarioNombre).HasMaxLength(RegistroAuditoria.LargoMaximoNombre);
        builder.Property(r => r.AutorizadoPorNombre).HasMaxLength(RegistroAuditoria.LargoMaximoNombre);

        builder.HasIndex(r => r.OcurridoEn);
        builder.HasIndex(r => new { r.TipoEntidad, r.EntidadId });
        builder.HasIndex(r => r.Accion);
    }
}
