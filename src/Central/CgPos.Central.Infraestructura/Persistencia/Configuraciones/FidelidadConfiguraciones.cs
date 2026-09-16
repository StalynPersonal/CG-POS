using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class MovimientoPuntosCentralConfiguracion : IEntityTypeConfiguration<MovimientoPuntosCentral>
{
    public void Configure(EntityTypeBuilder<MovimientoPuntosCentral> constructor)
    {
        constructor.ToTable("MovimientosPuntos");
        constructor.HasKey(m => m.Id);

        // El Id lo genera la caja: reenviar el mismo movimiento no acumula dos veces.
        constructor.Property(m => m.Id).ValueGeneratedNever();

        constructor.Property(m => m.Cedula).HasMaxLength(MovimientoPuntosCentral.LargoMaximoCedula).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Documento).HasMaxLength(MovimientoPuntosCentral.LargoMaximoDocumento);
        constructor.Property(m => m.Usuario).HasMaxLength(MovimientoPuntosCentral.LargoMaximoUsuario);
        constructor.Property(m => m.Motivo).HasMaxLength(MovimientoPuntosCentral.LargoMaximoMotivo);

        // Sin clave foránea al miembro: es un maestro publicado, y un movimiento puede llegar antes que la inscripción de otra caja.
        constructor.HasOne<Caja>().WithMany().HasForeignKey(m => m.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(m => m.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // El saldo se recalcula con todos los movimientos del miembro, en orden.
        constructor.HasIndex(m => new { m.MiembroId, m.Fecha });
        constructor.HasIndex(m => m.Cedula);
    }
}

internal sealed class SaldoPuntosCentralConfiguracion : IEntityTypeConfiguration<SaldoPuntosCentral>
{
    public void Configure(EntityTypeBuilder<SaldoPuntosCentral> constructor)
    {
        constructor.ToTable("SaldosPuntos");
        constructor.HasKey(s => s.MiembroId);
        constructor.Property(s => s.MiembroId).ValueGeneratedNever();
        constructor.Property(s => s.Cedula).HasMaxLength(MovimientoPuntosCentral.LargoMaximoCedula).IsFixedLength().IsUnicode(false).IsRequired();

        // El trabajo de vencimiento busca los saldos con puntos ya vencidos.
        constructor.HasIndex(s => s.ProximoVencimiento);
    }
}
