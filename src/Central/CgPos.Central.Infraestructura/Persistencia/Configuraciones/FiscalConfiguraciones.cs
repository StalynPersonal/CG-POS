using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class AnulacionEcfCentralConfiguracion : IEntityTypeConfiguration<AnulacionEcfCentral>
{
    public void Configure(EntityTypeBuilder<AnulacionEcfCentral> constructor)
    {
        constructor.ToTable("AnulacionesEcf");
        constructor.HasKey(a => a.Id);
        constructor.Property(a => a.Id).ValueGeneratedNever();
        constructor.Ignore(a => a.Cantidad);
        constructor.Property(a => a.Motivo).HasMaxLength(AnulacionEcfCentral.LargoMaximoMotivo).IsRequired();
        constructor.Property(a => a.UsuarioNombre).HasMaxLength(AnulacionEcfCentral.LargoMaximoUsuario).IsRequired();
        constructor.Property(a => a.RespuestaDgii).HasMaxLength(AnulacionEcfCentral.LargoMaximoRespuesta);
        constructor.Property(a => a.XmlFirmado).Metadata.SetMaxLength(null);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(a => a.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(a => new { a.CajaId, a.TipoComprobante });
    }
}
