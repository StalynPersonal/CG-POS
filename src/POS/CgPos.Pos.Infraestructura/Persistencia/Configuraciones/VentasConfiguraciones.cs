using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Entregas;
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
        constructor.Property(t => t.SucursalCodigo).HasMaxLength(OrigenDocumento.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(t => t.SucursalNombre).HasMaxLength(OrigenDocumento.LargoMaximoNombreSucursal).IsRequired();
        constructor.Property(t => t.CajaCodigo).HasMaxLength(OrigenDocumento.LargoCodigo).IsUnicode(false).IsRequired();
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

internal sealed class VentaEnProcesoConfiguracion : IEntityTypeConfiguration<VentaEnProceso>
{
    public void Configure(EntityTypeBuilder<VentaEnProceso> constructor)
    {
        constructor.ConfigurarVenta<VentaEnProceso, LineaVentaEnProceso, DestinoEntregaEnProceso>("VentasEnProceso");
    }
}

internal sealed class VentaGuardadaConfiguracion : IEntityTypeConfiguration<VentaGuardada>
{
    public void Configure(EntityTypeBuilder<VentaGuardada> constructor)
    {
        constructor.ConfigurarVenta<VentaGuardada, LineaVentaGuardada, DestinoEntregaGuardada>("VentasGuardadas");
        constructor.Property(v => v.Referencia).HasMaxLength(VentaGuardada.LargoMaximoReferencia).IsRequired();

        // El cajero la encuentra por su referencia: dos guardadas del mismo turno no pueden llamarse igual.
        constructor.HasIndex(v => new { v.TurnoId, v.Referencia }).IsUnique();
    }
}

internal sealed class VentaCobradaConfiguracion : IEntityTypeConfiguration<VentaCobrada>
{
    public void Configure(EntityTypeBuilder<VentaCobrada> constructor)
    {
        constructor.ConfigurarVenta<VentaCobrada, LineaVentaCobrada, DestinoEntregaCobrada>("Ventas");

        // El número de factura solo existe aquí: en las tablas de trabajo todas lo tienen vacío.
        constructor.HasIndex(v => v.NumeroTransaccion).IsUnique();
        constructor.HasIndex(v => new { v.CajaId, v.Secuencia }).IsUnique();

        // Solo la venta cobrada tiene pagos.
        constructor.Ignore(v => v.Pagos);
        constructor.HasMany<PagoVenta>("_pagos").WithOne().HasForeignKey(p => p.VentaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation("_pagos").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
    }
}

internal sealed class PagoVentaConfiguracion : IEntityTypeConfiguration<PagoVenta>
{
    public void Configure(EntityTypeBuilder<PagoVenta> constructor)
    {
        constructor.ToTable("PagosVenta");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.FormaPagoCodigo).HasMaxLength(Dominio.Pagos.FormaPago.LargoMaximoCodigo).IsRequired();
        constructor.Property(p => p.FormaPagoNombre).HasMaxLength(Dominio.Pagos.FormaPago.LargoMaximoNombre).IsRequired();
        constructor.Property(p => p.Moneda).HasMaxLength(3).IsUnicode(false).IsRequired();
        constructor.Property(p => p.MontoRecibido).HasPrecision(18, 2);
        constructor.Property(p => p.TasaCambio).HasPrecision(18, 4);
        constructor.Property(p => p.MontoAplicado).HasPrecision(18, 2);
        constructor.Property(p => p.Referencia).HasMaxLength(PagoVenta.LargoMaximoReferencia);
        constructor.Property(p => p.BancoNombre).HasMaxLength(PagoVenta.LargoMaximoNombre);
        constructor.Property(p => p.TipoTarjetaNombre).HasMaxLength(PagoVenta.LargoMaximoNombre);
        constructor.Property(p => p.UltimosDigitos).HasMaxLength(4).IsUnicode(false);
        constructor.HasIndex(p => new { p.VentaId, p.Numero }).IsUnique();
        constructor.HasIndex(p => p.OperacionTerminalId);
    }
}

internal sealed class LineaVentaEnProcesoConfiguracion : IEntityTypeConfiguration<LineaVentaEnProceso>
{
    public void Configure(EntityTypeBuilder<LineaVentaEnProceso> constructor) => constructor.ConfigurarLinea("LineasVentaEnProceso");
}

internal sealed class LineaVentaGuardadaConfiguracion : IEntityTypeConfiguration<LineaVentaGuardada>
{
    public void Configure(EntityTypeBuilder<LineaVentaGuardada> constructor) => constructor.ConfigurarLinea("LineasVentaGuardadas");
}

internal sealed class LineaVentaCobradaConfiguracion : IEntityTypeConfiguration<LineaVentaCobrada>
{
    public void Configure(EntityTypeBuilder<LineaVentaCobrada> constructor) => constructor.ConfigurarLinea("LineasVenta");
}

/// <summary>
/// Lo que las tres tablas de la venta tienen en común: la que se arma, la que está en espera y la cobrada. Cada una es
/// una entidad independiente, con su propia numeración de Id y sus propias llaves foráneas; las columnas son las mismas.
/// </summary>
internal static class ConfiguracionComunVenta
{
    public static void ConfigurarVenta<TVenta, TLinea, TDestino>(this EntityTypeBuilder<TVenta> constructor, string tabla)
        where TVenta : Venta
        where TLinea : LineaVenta
        where TDestino : DestinoEntrega
    {
        constructor.ToTable(tabla);
        constructor.HasKey(v => v.Id);
        constructor.Property(v => v.NumeroTransaccion).HasMaxLength(Venta.LargoMaximoNumero).IsUnicode(false).IsRequired();
        constructor.Property(v => v.SucursalCodigo).HasMaxLength(OrigenDocumento.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(v => v.SucursalNombre).HasMaxLength(OrigenDocumento.LargoMaximoNombreSucursal).IsRequired();
        constructor.Property(v => v.CajaCodigo).HasMaxLength(OrigenDocumento.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(v => v.UsuarioNombre).HasMaxLength(Venta.LargoMaximoUsuario).IsRequired();
        constructor.Property(v => v.Moneda).HasMaxLength(CgPos.Dominio.Pagos.Moneda.LargoCodigo).IsUnicode(false).IsRequired();
        constructor.Property(v => v.SimboloMoneda).HasMaxLength(CgPos.Dominio.Pagos.Moneda.LargoMaximoSimbolo).IsRequired();
        constructor.Property(v => v.MotivoAnulacion).HasMaxLength(Venta.LargoMaximoMotivo);
        constructor.Property(v => v.AnuladaPorNombre).HasMaxLength(Venta.LargoMaximoUsuario);
        constructor.Property(v => v.ClienteDocumento).HasMaxLength(Venta.LargoMaximoDocumento).IsUnicode(false);
        constructor.Property(v => v.ClienteNombre).HasMaxLength(Venta.LargoMaximoNombreCliente);
        constructor.Property(v => v.LimiteCompra).HasPrecision(18, 2);
        constructor.Property(v => v.PorcentajeRetencion).HasPrecision(5, 2);
        constructor.Property(v => v.CertificacionExencion).HasMaxLength(Venta.LargoMaximoCertificacion);
        constructor.Property(v => v.CotizacionNumero).HasMaxLength(Venta.LargoMaximoNumeroCotizacion).IsUnicode(false);
        constructor.Property(v => v.ListaBodaNumero).HasMaxLength(CgPos.Dominio.ListasBoda.ListaBoda.LargoMaximoNumero).IsUnicode(false);
        constructor.Property(v => v.ListaBodaEvento).HasMaxLength(CgPos.Dominio.ListasBoda.ListaBoda.LargoMaximoNombre);
        constructor.Property(v => v.DescuentoFacturaLineas).HasMaxLength(2000).IsUnicode(false);
        constructor.Property(v => v.MotivoDescuentoFactura).HasMaxLength(Venta.LargoMaximoMotivo);
        constructor.Property(v => v.DescuentoFacturaAutorizadoPorNombre).HasMaxLength(Venta.LargoMaximoUsuario);
        constructor.Ignore(v => v.TieneLineasActivas);
        constructor.Ignore(v => v.ExentaDeImpuesto);
        constructor.Ignore(v => v.Identificacion);
        constructor.ConfigurarFidelidad();

        constructor.HasIndex(v => new { v.TurnoId, v.UsuarioId, v.Estado });

        constructor.HasOne<Turno>().WithMany().HasForeignKey(v => v.TurnoId).OnDelete(DeleteBehavior.Restrict);

        // Cobro (M08)
        constructor.Property(v => v.CobradaPorNombre).HasMaxLength(Venta.LargoMaximoUsuario);
        constructor.Property(v => v.TotalCobrado).HasPrecision(18, 2);
        constructor.Property(v => v.Devuelta).HasPrecision(18, 2);
        constructor.Property(v => v.RedondeoEfectivo).HasPrecision(18, 2);
        constructor.HasIndex(v => new { v.TurnoId, v.Estado });

        // Las líneas y los destinos viven en la lista de su tipo que guarda cada variante, y se cargan siempre con la
        // venta: sin ellos no se calculan los totales, y los destinos condicionan los cambios de línea y el cobro.
        constructor.Ignore(v => v.Lineas);
        constructor.Ignore(v => v.DestinosEntrega);
        constructor.HasMany<TLinea>("_lineas").WithOne().HasForeignKey(l => l.VentaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation("_lineas").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();
        constructor.HasMany<TDestino>("_destinos").WithOne().HasForeignKey(d => d.VentaId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation("_destinos").UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        // La temporal y la guardada no tienen pagos: se cobra la copia que va a la tabla de ventas.
        if (typeof(TVenta) != typeof(VentaCobrada))
            constructor.Ignore(v => v.Pagos);
    }

    public static void ConfigurarLinea<TLinea>(this EntityTypeBuilder<TLinea> constructor, string tabla) where TLinea : LineaVenta
    {
        constructor.ToTable(tabla);
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.CodigoInterno).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(l => l.CodigoLeido).HasMaxLength(Articulo.LargoMaximoCodigo).IsRequired();
        constructor.Property(l => l.Descripcion).HasMaxLength(Articulo.LargoMaximoDescripcion).IsRequired();
        constructor.Property(l => l.UnidadMedidaCodigo).HasMaxLength(UnidadMedida.LargoMaximoAbreviatura).IsRequired();
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
        constructor.Ignore(l => l.Exenta);
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
        constructor.Property(s => s.SucursalCodigo).HasMaxLength(CgPos.Dominio.Comun.CodigosCatalogo.LargoSucursalCaja).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(s => s.CajaCodigo).HasMaxLength(CgPos.Dominio.Comun.CodigosCatalogo.LargoSucursalCaja).IsFixedLength().IsUnicode(false).IsRequired();
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
