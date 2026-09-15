using CgPos.Dominio.Pagos;
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
        constructor.Property(m => m.Id).ValueGeneratedNever();
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

internal sealed class CierreTurnoConfiguracion : IEntityTypeConfiguration<CierreTurno>
{
    public void Configure(EntityTypeBuilder<CierreTurno> constructor)
    {
        constructor.ToTable("CierresTurno");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.Id).ValueGeneratedNever();
        constructor.Property(c => c.FondoInicial).HasPrecision(18, 2);
        constructor.Property(c => c.Moneda).HasMaxLength(Moneda.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(c => c.TotalVentas).HasPrecision(18, 2);
        constructor.Property(c => c.TotalRetiros).HasPrecision(18, 2);
        constructor.Property(c => c.TotalEsperado).HasPrecision(18, 2);
        constructor.Property(c => c.TotalDeclarado).HasPrecision(18, 2);
        constructor.Property(c => c.Diferencia).HasPrecision(18, 2);
        constructor.Property(c => c.UsuarioNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();
        constructor.Property(c => c.ReabiertoPorNombre).HasMaxLength(Turno.LargoMaximoUsuario);
        constructor.Property(c => c.MotivoReapertura).HasMaxLength(CierreTurno.LargoMaximoMotivo);

        constructor.HasOne<Turno>().WithMany().HasForeignKey(c => c.TurnoId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(c => new { c.TurnoId, c.Numero }).IsUnique();
        constructor.HasIndex(c => new { c.CajaId, c.CerradoEn });

        constructor.HasMany(c => c.FormasPago).WithOne().HasForeignKey(f => f.CierreTurnoId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.FormasPago).UsePropertyAccessMode(PropertyAccessMode.Field);
        constructor.HasMany(c => c.Denominaciones).WithOne().HasForeignKey(d => d.CierreTurnoId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Denominaciones).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CierreFormaPagoConfiguracion : IEntityTypeConfiguration<CierreFormaPago>
{
    public void Configure(EntityTypeBuilder<CierreFormaPago> constructor)
    {
        constructor.ToTable("CierresTurnoFormasPago");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Id).ValueGeneratedNever();
        constructor.Property(f => f.Codigo).HasMaxLength(FormaPago.LargoMaximoCodigo).IsRequired();
        constructor.Property(f => f.Nombre).HasMaxLength(FormaPago.LargoMaximoNombre).IsRequired();
        constructor.Property(f => f.Moneda).HasMaxLength(3).IsUnicode(false).IsRequired();
        constructor.Property(f => f.Esperado).HasPrecision(18, 2);
        constructor.Property(f => f.Declarado).HasPrecision(18, 2);
        constructor.Property(f => f.Diferencia).HasPrecision(18, 2);
    }
}

internal sealed class CierreDenominacionConfiguracion : IEntityTypeConfiguration<CierreDenominacion>
{
    public void Configure(EntityTypeBuilder<CierreDenominacion> constructor)
    {
        constructor.ToTable("CierresTurnoDenominaciones");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Id).ValueGeneratedNever();
        constructor.Property(d => d.Moneda).HasMaxLength(3).IsUnicode(false).IsRequired();
        constructor.Property(d => d.Valor).HasPrecision(18, 2);
        constructor.Property(d => d.Importe).HasPrecision(18, 2);
    }
}
