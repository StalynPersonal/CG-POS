using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class MarcaSincronizacionConfiguracion : IEntityTypeConfiguration<MarcaSincronizacion>
{
    public void Configure(EntityTypeBuilder<MarcaSincronizacion> constructor)
    {
        constructor.ToTable("MarcasSincronizacion");
        constructor.HasKey(m => m.Clave);
        constructor.Property(m => m.Clave).HasMaxLength(MarcaSincronizacion.LargoMaximoClave).IsUnicode(false);
        constructor.Property(m => m.Texto).HasMaxLength(MarcaSincronizacion.LargoMaximoTexto).IsUnicode(false);
    }
}
