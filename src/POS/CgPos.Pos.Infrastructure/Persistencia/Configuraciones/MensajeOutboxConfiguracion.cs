using CgPos.Pos.Application.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infrastructure.Persistencia.Configuraciones;

internal sealed class MensajeOutboxConfiguracion : IEntityTypeConfiguration<MensajeOutbox>
{
    public void Configure(EntityTypeBuilder<MensajeOutbox> builder)
    {
        builder.ToTable("OutboxMensajes");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.TipoMensaje).HasMaxLength(MensajeOutbox.LargoMaximoTipo).IsRequired();
        // Sin largo máximo (nvarchar(max)): se anula la convención de 256 para que EF no dimensione mal los parámetros.
        builder.Property(m => m.Contenido).IsRequired().Metadata.SetMaxLength(null);
        builder.Property(m => m.HashContenido).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.Property(m => m.UltimoError).HasMaxLength(MensajeOutbox.LargoMaximoError);

        // El proceso de sincronización busca por estado y fecha de próximo intento.
        builder.HasIndex(m => new { m.Estado, m.ProximoIntentoEn })
            .HasDatabaseName("IX_OutboxMensajes_Estado_ProximoIntentoEn");

        builder.HasIndex(m => m.AgregadoId);
    }
}
