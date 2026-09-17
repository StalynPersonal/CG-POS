using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class MensajeSalidaConfiguracion : IEntityTypeConfiguration<MensajeSalida>
{
    public void Configure(EntityTypeBuilder<MensajeSalida> constructor)
    {
        constructor.ToTable("BandejaSalida");

        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Id).ValueGeneratedNever();

        constructor.Property(m => m.TipoMensaje).HasMaxLength(MensajeSalida.LargoMaximoTipo).IsRequired();
        // Sin largo máximo (nvarchar(maximo)): se anula la convención de 256 para que EF no dimensione mal los parámetros.
        constructor.Property(m => m.Contenido).IsRequired().Metadata.SetMaxLength(null);
        constructor.Property(m => m.HashContenido).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.UltimoError).HasMaxLength(MensajeSalida.LargoMaximoError);

        // El proceso de sincronización busca por estado y fecha de próximo intento.
        constructor.HasIndex(m => new { m.Estado, m.ProximoIntentoEn })
            .HasDatabaseName("IX_BandejaSalida_Estado_ProximoIntentoEn");

        constructor.Property(m => m.Referencia).HasMaxLength(MensajeSalida.LargoMaximoReferencia).IsUnicode(false).IsRequired();
        constructor.HasIndex(m => m.Referencia);
    }
}
