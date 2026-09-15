using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Turnos;
using CgPos.Dominio.Ventas;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Persistencia;

/// <summary>Base de datos local de la caja (SQL Server Express en producción).</summary>
public sealed class ContextoDatosPos(DbContextOptions<ContextoDatosPos> opciones) : DbContext(opciones)
{
    // Sincronización y auditoría
    public DbSet<MensajeSalida> BandejaSalida => Set<MensajeSalida>();
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    // Organización (M01)
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Parametro> Parametros => Set<Parametro>();

    // Seguridad (M02)
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    // Maestros y catálogos (M03)
    public DbSet<Familia> Familias => Set<Familia>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ContribuyenteDgii> ContribuyentesDgii => Set<ContribuyenteDgii>();
    public DbSet<Moneda> Monedas => Set<Moneda>();
    public DbSet<FormaPago> FormasPago => Set<FormaPago>();
    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<TipoTarjeta> TiposTarjeta => Set<TipoTarjeta>();
    public DbSet<Denominacion> Denominaciones => Set<Denominacion>();

    // Precios (M04)
    public DbSet<PrecioArticulo> PreciosArticulo => Set<PrecioArticulo>();

    // Turnos y ventas (M13, M05)
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<CierreTurno> CierresTurno => Set<CierreTurno>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<AutorizacionOtorgada> AutorizacionesOtorgadas => Set<AutorizacionOtorgada>();

    // Devoluciones y notas de crédito (M10)
    public DbSet<CgPos.Dominio.Devoluciones.MotivoDevolucion> MotivosDevolucion => Set<CgPos.Dominio.Devoluciones.MotivoDevolucion>();
    public DbSet<CgPos.Dominio.Devoluciones.Devolucion> Devoluciones => Set<CgPos.Dominio.Devoluciones.Devolucion>();

    // Descuentos y promociones (M06, M07)
    public DbSet<Promocion> Promociones => Set<Promocion>();
    public DbSet<MotivoDescuento> MotivosDescuento => Set<MotivoDescuento>();
    public DbSet<TopeDescuento> TopesDescuento => Set<TopeDescuento>();

    // Fidelidad (M11)
    public DbSet<CgPos.Dominio.Fidelidad.NivelFidelidad> NivelesFidelidad => Set<CgPos.Dominio.Fidelidad.NivelFidelidad>();
    public DbSet<CgPos.Dominio.Fidelidad.ReglaAcumulacion> ReglasAcumulacion => Set<CgPos.Dominio.Fidelidad.ReglaAcumulacion>();
    public DbSet<CgPos.Dominio.Fidelidad.MiembroFidelidad> MiembrosFidelidad => Set<CgPos.Dominio.Fidelidad.MiembroFidelidad>();
    public DbSet<CgPos.Dominio.Fidelidad.MovimientoPuntos> MovimientosPuntos => Set<CgPos.Dominio.Fidelidad.MovimientoPuntos>();

    // Pendientes de entrega y envíos (M12)
    public DbSet<CgPos.Dominio.Entregas.Almacen> Almacenes => Set<CgPos.Dominio.Entregas.Almacen>();
    public DbSet<CgPos.Dominio.Entregas.PendienteEntrega> PendientesEntrega => Set<CgPos.Dominio.Entregas.PendienteEntrega>();

    // Cobro (M08)
    public DbSet<TasaCambio> TasasCambio => Set<TasaCambio>();
    public DbSet<OperacionTerminal> OperacionesTerminal => Set<OperacionTerminal>();

    // Facturación electrónica (M09)
    public DbSet<SecuenciaEcf> SecuenciasEcf => Set<SecuenciaEcf>();
    public DbSet<DocumentoElectronico> DocumentosElectronicos => Set<DocumentoElectronico>();

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoDatosPos).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder constructorConvenciones)
    {
        // Montos y cantidades: 4 decimales en almacenamiento; el redondeo fiscal a 2 lo hace el dominio.
        constructorConvenciones.Properties<decimal>().HavePrecision(18, 4);

        // Evita nvarchar(maximo) por defecto (no indexable); cada configuración ajusta su largo si hace falta.
        constructorConvenciones.Properties<string>().HaveMaxLength(256);

        // Precisión de milisegundos, suficiente para auditoría y orden de eventos.
        constructorConvenciones.Properties<DateTime>().HavePrecision(3);
        constructorConvenciones.Properties<DateTimeOffset>().HavePrecision(3);
    }
}
