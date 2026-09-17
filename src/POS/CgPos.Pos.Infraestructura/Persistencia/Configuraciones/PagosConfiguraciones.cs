using CgPos.Dominio.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class TasaCambioConfiguracion : IEntityTypeConfiguration<TasaCambio>
{
    public void Configure(EntityTypeBuilder<TasaCambio> constructor)
    {
        constructor.ToTable("TasasCambio");
        constructor.HasKey(t => t.Id);
        constructor.Property(t => t.Moneda).HasMaxLength(3).IsUnicode(false).IsRequired();
        constructor.Property(t => t.Tasa).HasPrecision(18, 4);
        constructor.HasIndex(t => new { t.Moneda, t.VigenteDesde }).IsUnique();
    }
}

internal sealed class OperacionTerminalConfiguracion : IEntityTypeConfiguration<OperacionTerminal>
{
    public void Configure(EntityTypeBuilder<OperacionTerminal> constructor)
    {
        constructor.ToTable("OperacionesTerminal");
        constructor.HasKey(o => o.Id);
        constructor.Property(o => o.Monto).HasPrecision(18, 2);
        constructor.Property(o => o.Aprobacion).HasMaxLength(OperacionTerminal.LargoMaximoAprobacion);
        constructor.Property(o => o.UltimosDigitos).HasMaxLength(4).IsUnicode(false);
        constructor.Property(o => o.Marca).HasMaxLength(OperacionTerminal.LargoMaximoMarca);
        constructor.Property(o => o.Mensaje).HasMaxLength(OperacionTerminal.LargoMaximoMensaje);
        constructor.Ignore(o => o.DisponibleParaCobro);
        constructor.HasIndex(o => o.VentaId);
        constructor.HasIndex(o => new { o.CajaId, o.TurnoId, o.Fecha });
    }
}
