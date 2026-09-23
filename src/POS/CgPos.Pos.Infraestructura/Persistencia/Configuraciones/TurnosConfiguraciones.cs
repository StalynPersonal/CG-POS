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
