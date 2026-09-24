using CgPos.Dominio.Pagos;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Turnos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class MovimientoCajaConfiguracion : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> constructor)
    {
        constructor.ToTable("MovimientosCaja");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Monto).HasPrecision(18, 2);
        constructor.Property(m => m.Moneda).HasMaxLength(Moneda.LargoCodigo).IsUnicode(false);
        constructor.Property(m => m.Motivo).HasMaxLength(MovimientoCaja.LargoMaximoMotivo);
        constructor.Property(m => m.UsuarioNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();
        constructor.Property(m => m.UsuarioAnteriorNombre).HasMaxLength(Turno.LargoMaximoUsuario);
        constructor.Property(m => m.AutorizadoPorNombre).HasMaxLength(Turno.LargoMaximoUsuario);

        constructor.HasOne<Turno>().WithMany().HasForeignKey(m => m.TurnoId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(m => new { m.TurnoId, m.Tipo, m.Numero }).IsUnique();
    }
}

/// <summary>Los motivos por los que una caja se queda sola; bajan del Central como los demás catálogos.</summary>
internal sealed class MotivoSuspensionConfiguracion : IEntityTypeConfiguration<MotivoSuspension>
{
    public void Configure(EntityTypeBuilder<MotivoSuspension> constructor)
    {
        constructor.ToTable("MotivosSuspension");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Nombre).HasMaxLength(MotivoSuspension.LargoMaximoNombre).IsRequired();
        constructor.HasIndex(m => m.Codigo).IsUnique();
    }
}

/// <summary>Cada rato que la caja estuvo parada, con su motivo y su duración.</summary>
internal sealed class SuspensionCajaConfiguracion : IEntityTypeConfiguration<SuspensionCaja>
{
    public void Configure(EntityTypeBuilder<SuspensionCaja> constructor)
    {
        constructor.ToTable("SuspensionesCaja");
        constructor.HasKey(s => s.Id);
        constructor.Property(s => s.UsuarioNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();
        constructor.Property(s => s.MotivoNombre).HasMaxLength(MotivoSuspension.LargoMaximoNombre).IsRequired();
        constructor.Property(s => s.Nota).HasMaxLength(SuspensionCaja.LargoMaximoNota);
        constructor.Ignore(s => s.Abierta);

        constructor.HasOne<Turno>().WithMany().HasForeignKey(s => s.TurnoId).OnDelete(DeleteBehavior.Restrict);

        // La que está abierta se busca en cada pantalla bloqueada; las del turno, al cerrarlo y para el reporte.
        constructor.HasIndex(s => new { s.TurnoId, s.ReanudadaEn });
    }
}

internal sealed class CierreTurnoConfiguracion : IEntityTypeConfiguration<CierreTurno>
{
    public void Configure(EntityTypeBuilder<CierreTurno> constructor)
    {
        constructor.ToTable("CierresTurno");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.SucursalCodigo).HasMaxLength(OrigenDocumento.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(c => c.SucursalNombre).HasMaxLength(OrigenDocumento.LargoMaximoNombreSucursal).IsRequired();
        constructor.Property(c => c.CajaCodigo).HasMaxLength(OrigenDocumento.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(c => c.FondoInicial).HasPrecision(18, 2);
        constructor.Property(c => c.Moneda).HasMaxLength(Moneda.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(c => c.TotalVentas).HasPrecision(18, 2);
        constructor.Property(c => c.TotalRetiros).HasPrecision(18, 2);
        constructor.Property(c => c.TotalEsperado).HasPrecision(18, 2);
        constructor.Property(c => c.UsuarioNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();

        constructor.HasOne<Turno>().WithMany().HasForeignKey(c => c.TurnoId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(c => new { c.TurnoId, c.Numero }).IsUnique();
        constructor.HasIndex(c => new { c.CajaId, c.CerradoEn });

        constructor.HasMany(c => c.FormasPago).WithOne().HasForeignKey(f => f.CierreTurnoId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.FormasPago).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>El cierre del lote del terminal de tarjetas cuadrado contra lo aprobado en la caja.</summary>
internal sealed class LoteTarjetasConfiguracion : IEntityTypeConfiguration<LoteTarjetas>
{
    public void Configure(EntityTypeBuilder<LoteTarjetas> constructor)
    {
        constructor.ToTable("LotesTarjetas");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.NumeroLote).HasMaxLength(LoteTarjetas.LargoMaximoLote).IsUnicode(false);
        constructor.Property(l => l.MontoCaja).HasPrecision(18, 2);
        constructor.Property(l => l.MontoTerminal).HasPrecision(18, 2);
        constructor.Property(l => l.Diferencia).HasPrecision(18, 2);
        constructor.Property(l => l.UsuarioNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();
        constructor.Ignore(l => l.Cuadrado);

        constructor.HasOne<Turno>().WithMany().HasForeignKey(l => l.TurnoId).OnDelete(DeleteBehavior.Restrict);

        // Un lote por turno: volver a cerrarlo reemplaza el anterior.
        constructor.HasIndex(l => l.TurnoId).IsUnique();

        constructor.HasMany(l => l.Descuadres).WithOne().HasForeignKey(a => a.LoteTarjetasId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(l => l.Descuadres).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class AprobacionLoteConfiguracion : IEntityTypeConfiguration<AprobacionLote>
{
    public void Configure(EntityTypeBuilder<AprobacionLote> constructor)
    {
        constructor.ToTable("LotesTarjetasAprobaciones");
        constructor.HasKey(a => a.Id);
        constructor.Property(a => a.Aprobacion).HasMaxLength(LoteTarjetas.LargoMaximoAprobacion).IsUnicode(false).IsRequired();
        constructor.HasIndex(a => a.LoteTarjetasId);
    }
}

internal sealed class CierreFormaPagoConfiguracion : IEntityTypeConfiguration<CierreFormaPago>
{
    public void Configure(EntityTypeBuilder<CierreFormaPago> constructor)
    {
        constructor.ToTable("CierresTurnoFormasPago");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Codigo).HasMaxLength(FormaPago.LargoMaximoCodigo).IsRequired();
        constructor.Property(f => f.Nombre).HasMaxLength(FormaPago.LargoMaximoNombre).IsRequired();
        constructor.Property(f => f.Moneda).HasMaxLength(3).IsUnicode(false).IsRequired();
        constructor.Property(f => f.Esperado).HasPrecision(18, 2);
    }
}
