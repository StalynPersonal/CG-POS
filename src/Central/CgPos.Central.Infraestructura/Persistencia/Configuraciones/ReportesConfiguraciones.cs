using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Reportes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CgPos.Central.Infraestructura.Persistencia.Configuraciones;

internal sealed class LineaVentaCentralConfiguracion : IEntityTypeConfiguration<LineaVentaCentral>
{
    public void Configure(EntityTypeBuilder<LineaVentaCentral> constructor)
    {
        constructor.ToTable("LineasVenta");
        constructor.HasKey(l => l.Id);
        constructor.Property(l => l.Codigo).HasMaxLength(ComprobanteVentaCentral.LargoMaximoCodigoArticulo).IsUnicode(false).IsRequired();
        constructor.Property(l => l.Descripcion).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto).IsRequired();
        constructor.Property(l => l.UnidadMedida).HasMaxLength(ComprobanteVentaCentral.LargoMaximoUnidad).IsUnicode(false);
        constructor.Property(l => l.Serial).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(l => l.PromocionCodigo).HasMaxLength(ComprobanteVentaCentral.LargoMaximoCodigoArticulo).IsUnicode(false);
        constructor.Property(l => l.Cantidad).HasPrecision(18, 3);
        constructor.Property(l => l.PrecioUnitario).HasPrecision(18, 4);
        constructor.Property(l => l.Descuento).HasPrecision(18, 2);
        constructor.Property(l => l.Impuesto).HasPrecision(18, 2);
        constructor.Property(l => l.Importe).HasPrecision(18, 2);
        constructor.HasIndex(l => l.Codigo);
    }
}

internal sealed class ComprobanteVentaCentralConfiguracion : IEntityTypeConfiguration<ComprobanteVentaCentral>
{
    public void Configure(EntityTypeBuilder<ComprobanteVentaCentral> constructor)
    {
        constructor.ToTable("VentasCentral");
        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Numero).HasMaxLength(ComprobanteVentaCentral.LargoMaximoNumero).IsRequired();
        constructor.Property(c => c.NumeroCentral).HasMaxLength(ComprobanteVentaCentral.LargoMaximoNumero).IsUnicode(false);
        // Dos documentos no pueden llevar el mismo número del Central; los que aún no lo tienen quedan fuera del índice.
        constructor.HasIndex(c => c.NumeroCentral).IsUnique().HasFilter("[NumeroCentral] IS NOT NULL");
        constructor.Property(c => c.Encf).HasMaxLength(ComprobanteVentaCentral.LargoMaximoEncf).IsFixedLength().IsUnicode(false);
        constructor.Property(c => c.EncfModificado).HasMaxLength(ComprobanteVentaCentral.LargoMaximoEncf).IsFixedLength().IsUnicode(false);
        constructor.Property(c => c.ClienteDocumento).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(c => c.ClienteNombre).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(c => c.UsuarioNombre).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(c => c.Moneda).HasMaxLength(ComprobanteVentaCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();

        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);

        constructor.HasMany(c => c.Impuestos).WithOne().HasForeignKey(i => i.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasMany(c => c.Pagos).WithOne().HasForeignKey(p => p.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasMany(c => c.Lineas).WithOne().HasForeignKey(l => l.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Impuestos).AutoInclude(false);
        constructor.Navigation(c => c.Pagos).AutoInclude(false);
        constructor.Navigation(c => c.Lineas).AutoInclude(false);

        // Todos los reportes filtran por día de operación, y varios por sucursal o caja.
        constructor.HasIndex(c => new { c.FechaOperacion, c.SucursalId, c.CajaId });
        constructor.HasIndex(c => c.Encf);

        // El número de factura o de nota de crédito identifica el documento en toda la empresa: un reenvío no duplica la fila.
        constructor.HasIndex(c => new { c.Tipo, c.Numero }).IsUnique();
    }
}

internal sealed class ImpuestoVentaCentralConfiguracion : IEntityTypeConfiguration<ImpuestoVentaCentral>
{
    public void Configure(EntityTypeBuilder<ImpuestoVentaCentral> constructor)
    {
        constructor.ToTable("ImpuestosVenta");
        constructor.HasKey(i => i.Id);

        constructor.HasIndex(i => i.ComprobanteId);
    }
}

internal sealed class PagoVentaCentralConfiguracion : IEntityTypeConfiguration<PagoVentaCentral>
{
    public void Configure(EntityTypeBuilder<PagoVentaCentral> constructor)
    {
        constructor.ToTable("PagosVenta");
        constructor.HasKey(p => p.Id);
        constructor.Property(p => p.FormaPagoNombre).HasMaxLength(ComprobanteVentaCentral.LargoMaximoTexto);
        constructor.Property(p => p.Moneda).HasMaxLength(ComprobanteVentaCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false);
        constructor.HasIndex(p => p.ComprobanteId);
    }
}

internal sealed class CierreTurnoCentralConfiguracion : IEntityTypeConfiguration<CierreTurnoCentral>
{
    public void Configure(EntityTypeBuilder<CierreTurnoCentral> constructor)
    {
        constructor.ToTable("CierresTurno");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.UsuarioNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(c => c.CuadradoPor).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(c => c.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Ignore(c => c.PendienteDeCuadre);
        constructor.Ignore(c => c.Ajustado);
        constructor.Ignore(c => c.ConDiferencia);
        constructor.Ignore(c => c.DeclaradoPorLaCaja);

        constructor.HasOne<Caja>().WithMany().HasForeignKey(c => c.CajaId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasMany(c => c.FormasPago).WithOne().HasForeignKey(f => f.CierreId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.FormasPago).AutoInclude(false);
        constructor.HasMany(c => c.Ajustes).WithOne().HasForeignKey(a => a.CierreId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Ajustes).AutoInclude(false);
        constructor.HasMany(c => c.Denominaciones).WithOne().HasForeignKey(d => d.CierreId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Denominaciones).AutoInclude(false);
        constructor.HasMany(c => c.Movimientos).WithOne().HasForeignKey(m => m.CierreId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.Movimientos).AutoInclude(false);

        // Pendientes de cuadre de una sucursal: es la consulta del módulo de cuadre.
        constructor.HasIndex(c => new { c.SucursalId, c.CuadradoEn });

        constructor.HasIndex(c => new { c.FechaOperacion, c.SucursalId, c.CajaId });

        // Un cierre por turno de cada caja; un reenvío del mismo mensaje actualiza la fila.
        constructor.HasIndex(c => new { c.CajaId, c.TurnoNumero }).IsUnique();
    }
}

/// <summary>El efectivo que contó el supervisor al cuadrar, denominación por denominación.</summary>
internal sealed class CierreDenominacionCentralConfiguracion : IEntityTypeConfiguration<CierreDenominacionCentral>
{
    public void Configure(EntityTypeBuilder<CierreDenominacionCentral> constructor)
    {
        constructor.ToTable("CierresTurnoDenominaciones");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(d => d.Valor).HasPrecision(18, 2);
        constructor.Property(d => d.Importe).HasPrecision(18, 2);
        constructor.HasIndex(d => d.CierreId);
    }
}

/// <summary>Los retiros, reembolsos y relevos del turno, como los informó la caja.</summary>
internal sealed class CierreMovimientoCentralConfiguracion : IEntityTypeConfiguration<CierreMovimientoCentral>
{
    public void Configure(EntityTypeBuilder<CierreMovimientoCentral> constructor)
    {
        constructor.ToTable("CierresTurnoMovimientos");
        constructor.HasKey(m => m.Id);
        constructor.Property(m => m.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(m => m.Monto).HasPrecision(18, 2);
        constructor.Property(m => m.Motivo).HasMaxLength(CierreTurnoCentral.LargoMaximoMotivo);
        constructor.Property(m => m.UsuarioNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto).IsRequired();
        constructor.Property(m => m.UsuarioAnteriorNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(m => m.AutorizadoPorNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.HasIndex(m => m.CierreId);
    }
}

internal sealed class AjusteCierreTurnoConfiguracion : IEntityTypeConfiguration<AjusteCierreTurno>
{
    public void Configure(EntityTypeBuilder<AjusteCierreTurno> constructor)
    {
        constructor.ToTable("AjustesCierreTurno");
        constructor.HasKey(a => a.Id);

        constructor.Property(a => a.FormaPagoNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto).IsRequired();
        constructor.Property(a => a.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(a => a.Motivo).HasMaxLength(CierreTurnoCentral.LargoMaximoMotivo).IsRequired();
        constructor.Property(a => a.AjustadoPorNombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto).IsRequired();

        // El movimiento se calcula al leerlo; no es una columna.
        constructor.Ignore(a => a.Movimiento);

        constructor.HasIndex(a => a.CierreId);
    }
}

internal sealed class CierreSucursalConfiguracion : IEntityTypeConfiguration<CierreSucursal>
{
    public void Configure(EntityTypeBuilder<CierreSucursal> constructor)
    {
        constructor.ToTable("CierresSucursal");
        constructor.HasKey(c => c.Id);
        constructor.Property(c => c.NumeroCentral).HasMaxLength(CierreSucursal.LargoMaximoTexto).IsUnicode(false);
        constructor.HasIndex(c => c.NumeroCentral).IsUnique().HasFilter("[NumeroCentral] IS NOT NULL");
        constructor.Property(c => c.Observacion).HasMaxLength(CierreSucursal.LargoMaximoObservacion);
        constructor.Property(c => c.CerradoPor).HasMaxLength(CierreSucursal.LargoMaximoTexto).IsRequired();
        constructor.Ignore(c => c.EfectivoADepositar);
        constructor.Ignore(c => c.DiferenciaDeposito);

        constructor.HasOne<Sucursal>().WithMany().HasForeignKey(c => c.SucursalId).OnDelete(DeleteBehavior.Restrict);
        constructor.HasMany(c => c.FormasPago).WithOne().HasForeignKey(f => f.CierreSucursalId).OnDelete(DeleteBehavior.Cascade);
        constructor.HasMany(c => c.Depositos).WithOne().HasForeignKey(d => d.CierreSucursalId).OnDelete(DeleteBehavior.Cascade);
        constructor.Navigation(c => c.FormasPago).AutoInclude(false);
        constructor.Navigation(c => c.Depositos).AutoInclude(false);

        // Un cierre por sucursal y día de operación.
        constructor.HasIndex(c => new { c.SucursalId, c.FechaOperacion }).IsUnique();
    }
}

internal sealed class CierreSucursalFormaPagoConfiguracion : IEntityTypeConfiguration<CierreSucursalFormaPago>
{
    public void Configure(EntityTypeBuilder<CierreSucursalFormaPago> constructor)
    {
        constructor.ToTable("CierresSucursalFormaPago");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Nombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(f => f.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false);
        constructor.HasIndex(f => f.CierreSucursalId);
    }
}

internal sealed class DepositoCierreSucursalConfiguracion : IEntityTypeConfiguration<DepositoCierreSucursal>
{
    public void Configure(EntityTypeBuilder<DepositoCierreSucursal> constructor)
    {
        constructor.ToTable("DepositosCierreSucursal");
        constructor.HasKey(d => d.Id);
        constructor.Property(d => d.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false).IsRequired();
        constructor.Property(d => d.BancoCodigo).HasMaxLength(CgPos.Dominio.Pagos.Banco.LargoMaximoCodigo).IsRequired();
        constructor.Property(d => d.BancoNombre).HasMaxLength(CgPos.Dominio.Pagos.Banco.LargoMaximoNombre).IsRequired();
        constructor.Property(d => d.NumeroBoleta).HasMaxLength(DepositoCierreSucursal.LargoMaximoBoleta).IsRequired();
        constructor.HasIndex(d => d.CierreSucursalId);
    }
}

internal sealed class CierreFormaPagoCentralConfiguracion : IEntityTypeConfiguration<CierreFormaPagoCentral>
{
    public void Configure(EntityTypeBuilder<CierreFormaPagoCentral> constructor)
    {
        constructor.ToTable("CierresFormaPago");
        constructor.HasKey(f => f.Id);
        constructor.Property(f => f.Nombre).HasMaxLength(CierreTurnoCentral.LargoMaximoTexto);
        constructor.Property(f => f.Moneda).HasMaxLength(CierreTurnoCentral.LargoMaximoMoneda).IsFixedLength().IsUnicode(false);
        constructor.HasIndex(f => f.CierreId);
    }
}
