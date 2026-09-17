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
        constructor.Property(m => m.Id).ValueGeneratedNever();

        constructor.Property(m => m.Cedula).HasMaxLength(MovimientoPuntosCentral.LargoMaximoCedula).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Documento).HasMaxLength(MovimientoPuntosCentral.LargoMaximoDocumento).IsRequired();
        constructor.Property(m => m.Usuario).HasMaxLength(MovimientoPuntosCentral.LargoMaximoUsuario);
        constructor.Property(m => m.Motivo).HasMaxLength(MovimientoPuntosCentral.LargoMaximoMotivo);

        // Sin clave foránea al miembro: es un maestro publicado, y un movimiento puede llegar antes que la inscripción de otra caja.
        constructor.HasOne<Caja>().WithMany().HasForeignKey(m => m.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(m => m.SucursalId).OnDelete(DeleteBehavior.Restrict);

        // El saldo se recalcula con todos los movimientos de la cédula, en orden.
        constructor.HasIndex(m => new { m.Cedula, m.Fecha });

        // Un documento de caja genera un solo movimiento de cada tipo: reenviar el mensaje no acumula dos veces.
        constructor.HasIndex(m => new { m.Documento, m.Tipo }).IsUnique().HasFilter($"[Origen] = {(int)OrigenMovimientoPuntos.Caja}");
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
