using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Pos.Infraestructura.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Pos.Infraestructura.Persistencia.Configuraciones;

internal sealed class TurnoConfiguracion : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> constructor)
    {
        constructor.ToTable("Turnos");
        constructor.HasKey(t => t.Id);
        constructor.Property(t => t.Id).ValueGeneratedNever();
        constructor.Property(t => t.UsuarioAperturaNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();
        constructor.Property(t => t.UsuarioActualNombre).HasMaxLength(Turno.LargoMaximoUsuario).IsRequired();
        constructor.Property(t => t.FondoInicial).HasPrecision(18, 2);
        constructor.Ignore(t => t.EstaAbierto);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(t => t.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasIndex(t => new { t.CajaId, t.Numero }).IsUnique();

        // Un solo turno abierto por caja: lo garantiza la base aunque dos aperturas lleguen a la vez.
        constructor.HasIndex(t => t.CajaId)
            .IsUnique()
            .HasFilter($"[Estado] = {(int)EstadoTurno.Abierto}")
            .HasDatabaseName("IX_Turnos_CajaId_Abierto");
    }
}

internal sealed class VentaConfiguracion : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> constructor)
    {
        constructor.ToTable("Ventas");
        constructor.HasKey(v => v.Id);
        constructor.Property(v => v.Id).ValueGeneratedNever();
        constructor.Property(v => v.NumeroTransaccion).HasMaxLength(Venta.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(v => v.UsuarioNombre).HasMaxLength(Venta.LargoMaximoUsuario).IsRequired();
        constructor.Property(v => v.MotivoAnulacion).HasMaxLength(Venta.LargoMaximoMotivo);
        constructor.Property(v => v.AnuladaPorNombre).HasMaxLength(Venta.LargoMaximoUsuario);
        constructor.Property(v => v.ClienteDocumento).HasMaxLength(Venta.LargoMaximoDocumento).IsUnicode(false);
        constructor.Property(v => v.ClienteNombre).HasMaxLength(Venta.LargoMaximoNombreCliente);
        constructor.Property(v => v.LimiteCompra).HasPrecision(18, 2);
        constructor.Property(v => v.DescuentoFacturaLineas).HasMaxLength(2000).IsUnicode(false);
        constructor.Property(v => v.MotivoDescuentoFactura).HasMaxLength(Venta.LargoMaximoMotivo);
        constructor.Property(v => v.DescuentoFacturaAutorizadoPorNombre).HasMaxLength(Venta.LargoMaximoUsuario);
        constructor.Ignore(v => v.TieneLineasActivas);

        constructor.HasIndex(v => v.NumeroTransaccion).IsUnique();
        constructor.HasIndex(v => new { v.CajaId, v.Secuencia }).IsUnique();
        constructor.HasIndex(v => new { v.TurnoId, v.UsuarioId, v.Estado });

        constructor.HasOne<Turno>().WithMany().HasForeignKey(v => v.TurnoId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasMany(v => v.Lineas).WithOne().HasForeignKey(l => l.VentaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(v => v.Lineas).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class LineaVentaConfiguracion : IEntityTypeConfiguration<LineaVenta>
{
    public void Configure(EntityTypeBuilder<LineaVenta> constructor)
    {
        constructor.ToTable("LineasVenta");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Id).ValueGeneratedNever();
        constructor.Property(l => l.CodigoInterno).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(l => l.CodigoLeido).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(l => l.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(l => l.UnidadMedidaCodigo).HasMaxLength(UnidadMedida.LargoMaximoCodigo).IsRequired();
        constructor.Property(l => l.PorcentajeImpuesto).HasPrecision(5, 2);
        constructor.Property(l => l.Serial).HasMaxLength(LineaVenta.LargoMaximoSerial);
        constructor.Property(l => l.PromocionCodigo).HasMaxLength(Promocion.LargoMaximoCodigo);
        constructor.Property(l => l.PromocionNombre).HasMaxLength(Promocion.LargoMaximoNombre);
        constructor.Property(l => l.PromocionDescripcion).HasMaxLength(60);
        constructor.Property(l => l.DescuentoPromocion).HasPrecision(18, 2);
        constructor.Property(l => l.DescuentoManual).HasPrecision(18, 2);
        constructor.Property(l => l.DescuentoFactura).HasPrecision(18, 2);
        constructor.Property(l => l.MotivoDescuento).HasMaxLength(Venta.LargoMaximoMotivo);
        constructor.Property(l => l.DescuentoAutorizadoPorNombre).HasMaxLength(Venta.LargoMaximoUsuario);
        constructor.Ignore(l => l.EstaActiva);
        constructor.Ignore(l => l.ImporteBruto);
        constructor.Ignore(l => l.ImporteConImpuesto);
        constructor.Ignore(l => l.DescuentoTotal);
        constructor.Ignore(l => l.TienePromocionActiva);
        constructor.HasIndex(l => l.PromocionId);

        constructor.HasIndex(l => new { l.VentaId, l.NumeroLinea }).IsUnique();
    }
}

internal sealed class SecuenciaCajaConfiguracion : IEntityTypeConfiguration<SecuenciaCaja>
{
    public void Configure(EntityTypeBuilder<SecuenciaCaja> constructor)
    {
        constructor.ToTable("SecuenciasCaja");
        constructor.HasKey(s => new { s.CajaId, s.Tipo });
        constructor.Property(s => s.Tipo).HasMaxLength(SecuenciaCaja.LargoMaximoTipo).IsUnicode(false);
    }
}

internal sealed class AutorizacionOtorgadaConfiguracion : IEntityTypeConfiguration<AutorizacionOtorgada>
{
    public void Configure(EntityTypeBuilder<AutorizacionOtorgada> constructor)
    {
        constructor.ToTable("AutorizacionesOtorgadas");
        constructor.HasKey(a => a.Id);
        constructor.Property(a => a.Id).ValueGeneratedNever();
        constructor.Property(a => a.Permiso).HasMaxLength(Permiso.LargoMaximoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(a => a.SolicitanteNombre).HasMaxLength(AutorizacionOtorgada.LargoMaximoNombre).IsRequired();
        constructor.Property(a => a.SupervisorNombre).HasMaxLength(AutorizacionOtorgada.LargoMaximoNombre).IsRequired();
        constructor.Property(a => a.Motivo).HasMaxLength(AutorizacionOtorgada.LargoMaximoMotivo).IsRequired();
        constructor.Property(a => a.UsadaEnTipoEntidad).HasMaxLength(AutorizacionOtorgada.LargoMaximoEntidad);
        constructor.Property(a => a.UsadaEnEntidadId).HasMaxLength(AutorizacionOtorgada.LargoMaximoEntidadId);
        constructor.HasIndex(a => new { a.SolicitanteId, a.ConcedidaEn });
    }
}
