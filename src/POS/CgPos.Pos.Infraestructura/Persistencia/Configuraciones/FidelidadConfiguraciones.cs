using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class NivelFidelidadConfiguracion : IEntityTypeConfiguration<NivelFidelidad>
{
    public void Configure(EntityTypeBuilder<NivelFidelidad> constructor)
    {
        constructor.ToTable("NivelesFidelidad");
        constructor.HasKey(n => n.Id);
        constructor.Property(n => n.Id).ValueGeneratedNever();
                constructor.Property(n => n.Nombre).HasMaxLength(NivelFidelidad.LargoMaximoNombre).IsRequired();
        constructor.Property(n => n.FactorAcumulacion).HasPrecision(9, 4);
        constructor.HasIndex(n => n.Codigo).IsUnique();
    }
}

internal sealed class ReglaAcumulacionConfiguracion : IEntityTypeConfiguration<ReglaAcumulacion>
{
    public void Configure(EntityTypeBuilder<ReglaAcumulacion> constructor)
    {
        constructor.ToTable("ReglasAcumulacion");
        constructor.HasKey(r => r.Id);
        constructor.Property(r => r.Id).ValueGeneratedNever();
                constructor.Property(r => r.Nombre).HasMaxLength(ReglaAcumulacion.LargoMaximoNombre).IsRequired();
        constructor.Property(r => r.MontoBase).HasPrecision(18, 2);
        constructor.Property(r => r.Puntos).HasPrecision(18, 4);
        constructor.HasIndex(r => r.Codigo).IsUnique();
    }
}

internal sealed class MiembroFidelidadConfiguracion : IEntityTypeConfiguration<MiembroFidelidad>
{
    public void Configure(EntityTypeBuilder<MiembroFidelidad> constructor)
    {
        constructor.ToTable("MiembrosFidelidad");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Id).ValueGeneratedNever();
        constructor.Property(m => m.Cedula).HasMaxLength(DocumentoIdentidad.LargoCedula).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Nombre).HasMaxLength(MiembroFidelidad.LargoMaximoNombre).IsRequired();
        constructor.Property(m => m.Telefono).HasMaxLength(MiembroFidelidad.LargoMaximoTelefono);
        constructor.Property(m => m.Correo).HasMaxLength(MiembroFidelidad.LargoMaximoCorreo);
        constructor.HasIndex(m => m.Cedula).IsUnique();
    }
}

internal sealed class MovimientoPuntosConfiguracion : IEntityTypeConfiguration<MovimientoPuntos>
{
    public void Configure(EntityTypeBuilder<MovimientoPuntos> constructor)
    {
        constructor.ToTable("MovimientosPuntos");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Id).ValueGeneratedNever();
        constructor.Property(m => m.Cedula).HasMaxLength(DocumentoIdentidad.LargoCedula).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Documento).HasMaxLength(MovimientoPuntos.LargoMaximoDocumento).IsUnicode(false).IsRequired();
        constructor.HasOne<MiembroFidelidad>().WithMany().HasForeignKey(m => m.MiembroId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(m => new { m.MiembroId, m.Fecha });
        constructor.HasIndex(m => m.VentaId);
    }
}

internal static class FidelidadVentaConfiguracion
{
    public static void ConfigurarFidelidad(this EntityTypeBuilder<Venta> constructor)
    {
        constructor.Property(v => v.FidelidadCedula).HasMaxLength(Venta.LargoMaximoDocumento).IsUnicode(false);
        constructor.Property(v => v.FidelidadNombre).HasMaxLength(Venta.LargoMaximoNombreCliente);
        constructor.Property(v => v.FidelidadNivel).HasMaxLength(Venta.LargoMaximoNombreCliente);
        constructor.Ignore(v => v.TieneFidelidad);
    }
}
