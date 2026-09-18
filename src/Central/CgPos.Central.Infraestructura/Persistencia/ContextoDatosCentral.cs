using CgPos.Dominio.Auditoria;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Persistencia;

/// <summary>Base de datos del Central (SQL Server Standard en producción).</summary>
public sealed class ContextoDatosCentral(DbContextOptions<ContextoDatosCentral> opciones) : DbContext(opciones)
{
    public DbSet<RegistroAuditoria> Auditoria => Set<RegistroAuditoria>();

    public DbSet<CgPos.Central.Infraestructura.CargaInicial.ArchivoArranqueAplicado> ArchivosArranqueAplicados => Set<CgPos.Central.Infraestructura.CargaInicial.ArchivoArranqueAplicado>();

    // Organización (M01)
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<CredencialDispositivo> CredencialesDispositivo => Set<CredencialDispositivo>();

    // Seguridad del Central Manager (M02)
    public DbSet<RolCentral> RolesCentral => Set<RolCentral>();
    public DbSet<UsuarioCentral> UsuariosCentral => Set<UsuarioCentral>();
    public DbSet<SesionCentral> SesionesCentral => Set<SesionCentral>();

    // Sincronización con las cajas (M14)
    public DbSet<CgPos.Dominio.Sincronizacion.DocumentoRecibido> DocumentosRecibidos => Set<CgPos.Dominio.Sincronizacion.DocumentoRecibido>();
    public DbSet<CgPos.Dominio.Sincronizacion.ComprobanteRecibido> ComprobantesRecibidos => Set<CgPos.Dominio.Sincronizacion.ComprobanteRecibido>();
    public DbSet<CgPos.Dominio.Sincronizacion.ConflictoSincronizacion> ConflictosSincronizacion => Set<CgPos.Dominio.Sincronizacion.ConflictoSincronizacion>();
    public DbSet<CgPos.Dominio.Sincronizacion.EstadoSincronizacionCaja> EstadosSincronizacionCaja => Set<CgPos.Dominio.Sincronizacion.EstadoSincronizacionCaja>();

    // Notas de crédito de todas las sucursales (M10)
    public DbSet<CgPos.Dominio.Devoluciones.NotaCreditoCentral> NotasCredito => Set<CgPos.Dominio.Devoluciones.NotaCreditoCentral>();
    public DbSet<CgPos.Dominio.ListasBoda.ListaBoda> ListasBoda => Set<CgPos.Dominio.ListasBoda.ListaBoda>();
    public DbSet<CgPos.Dominio.Devoluciones.ConsumoNotaCreditoCentral> ConsumosNotaCredito => Set<CgPos.Dominio.Devoluciones.ConsumoNotaCreditoCentral>();
    public DbSet<CgPos.Dominio.Devoluciones.ReservaNotaCreditoCentral> ReservasNotaCredito => Set<CgPos.Dominio.Devoluciones.ReservaNotaCreditoCentral>();

    // Saldo oficial de puntos del programa de fidelidad (M11)
    public DbSet<CgPos.Dominio.Fidelidad.MovimientoPuntosCentral> MovimientosPuntos => Set<CgPos.Dominio.Fidelidad.MovimientoPuntosCentral>();
    public DbSet<CgPos.Dominio.Fidelidad.SaldoPuntosCentral> SaldosPuntos => Set<CgPos.Dominio.Fidelidad.SaldoPuntosCentral>();

    // Pendientes de entrega y envíos de todas las sucursales (M12)
    public DbSet<CgPos.Dominio.Entregas.PendienteCentral> PendientesEntrega => Set<CgPos.Dominio.Entregas.PendienteCentral>();

    // Modelo de lectura para los reportes (M16)
    public DbSet<CgPos.Dominio.Reportes.ComprobanteVentaCentral> VentasCentral => Set<CgPos.Dominio.Reportes.ComprobanteVentaCentral>();
    public DbSet<CgPos.Dominio.Reportes.ImpuestoVentaCentral> ImpuestosVenta => Set<CgPos.Dominio.Reportes.ImpuestoVentaCentral>();
    public DbSet<CgPos.Dominio.Reportes.PagoVentaCentral> PagosVenta => Set<CgPos.Dominio.Reportes.PagoVentaCentral>();
    public DbSet<CgPos.Dominio.Reportes.CierreTurnoCentral> CierresTurno => Set<CgPos.Dominio.Reportes.CierreTurnoCentral>();

    // Maestros que el Central publica para las cajas, cada uno en su tabla (bajan por código)
    public DbSet<CgPos.Dominio.Pagos.Moneda> Monedas => Set<CgPos.Dominio.Pagos.Moneda>();
    public DbSet<CgPos.Dominio.Catalogo.Departamento> Departamentos => Set<CgPos.Dominio.Catalogo.Departamento>();
    public DbSet<CgPos.Dominio.Catalogo.Categoria> Categorias => Set<CgPos.Dominio.Catalogo.Categoria>();
    public DbSet<CgPos.Dominio.Catalogo.Marca> Marcas => Set<CgPos.Dominio.Catalogo.Marca>();
    public DbSet<CgPos.Dominio.Catalogo.UnidadMedida> UnidadesMedida => Set<CgPos.Dominio.Catalogo.UnidadMedida>();
    public DbSet<CgPos.Dominio.Catalogo.Impuesto> Impuestos => Set<CgPos.Dominio.Catalogo.Impuesto>();
    public DbSet<CgPos.Dominio.Catalogo.Articulo> Articulos => Set<CgPos.Dominio.Catalogo.Articulo>();
    public DbSet<CgPos.Dominio.Clientes.Cliente> Clientes => Set<CgPos.Dominio.Clientes.Cliente>();
    public DbSet<CgPos.Dominio.Pagos.FormaPago> FormasPago => Set<CgPos.Dominio.Pagos.FormaPago>();
    public DbSet<CgPos.Dominio.Pagos.Banco> Bancos => Set<CgPos.Dominio.Pagos.Banco>();
    public DbSet<CgPos.Dominio.Reportes.CierreSucursal> CierresSucursal => Set<CgPos.Dominio.Reportes.CierreSucursal>();
    public DbSet<CgPos.Dominio.Pagos.TipoTarjeta> TiposTarjeta => Set<CgPos.Dominio.Pagos.TipoTarjeta>();
    public DbSet<CgPos.Dominio.Pagos.Denominacion> Denominaciones => Set<CgPos.Dominio.Pagos.Denominacion>();
    public DbSet<CgPos.Dominio.Pagos.TasaCambio> TasasCambio => Set<CgPos.Dominio.Pagos.TasaCambio>();
    public DbSet<CgPos.Dominio.Promociones.Promocion> Promociones => Set<CgPos.Dominio.Promociones.Promocion>();
    public DbSet<CgPos.Dominio.Promociones.DescuentoTarjeta> DescuentosTarjeta => Set<CgPos.Dominio.Promociones.DescuentoTarjeta>();
    public DbSet<CgPos.Dominio.Promociones.MotivoDescuento> MotivosDescuento => Set<CgPos.Dominio.Promociones.MotivoDescuento>();
    public DbSet<CgPos.Dominio.Promociones.TopeDescuento> TopesDescuento => Set<CgPos.Dominio.Promociones.TopeDescuento>();
    public DbSet<CgPos.Dominio.Fiscal.SecuenciaEcf> SecuenciasEcf => Set<CgPos.Dominio.Fiscal.SecuenciaEcf>();
    public DbSet<CgPos.Dominio.Devoluciones.MotivoDevolucion> MotivosDevolucion => Set<CgPos.Dominio.Devoluciones.MotivoDevolucion>();
    public DbSet<CgPos.Dominio.Fidelidad.NivelFidelidad> NivelesFidelidad => Set<CgPos.Dominio.Fidelidad.NivelFidelidad>();
    public DbSet<CgPos.Dominio.Fidelidad.ReglaAcumulacion> ReglasAcumulacion => Set<CgPos.Dominio.Fidelidad.ReglaAcumulacion>();
    public DbSet<CgPos.Dominio.Fidelidad.MiembroFidelidad> MiembrosFidelidad => Set<CgPos.Dominio.Fidelidad.MiembroFidelidad>();
    public DbSet<CgPos.Dominio.Entregas.Almacen> Almacenes => Set<CgPos.Dominio.Entregas.Almacen>();
    public DbSet<Rol> RolesCaja => Set<Rol>();
    public DbSet<Usuario> UsuariosCaja => Set<Usuario>();

    // Anulaciones de e-NCF informadas a la DGII (ANECF)
    public DbSet<CgPos.Dominio.Fiscal.AnulacionEcfCentral> AnulacionesEcf => Set<CgPos.Dominio.Fiscal.AnulacionEcfCentral>();

    /// <summary>Versión de fila (rowversion) de lo que baja a las cajas: permite entregar solo lo cambiado (RF-273).</summary>
    public const string ColumnaVersion = "Version";

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        // Cada tabla tiene su propia secuencia de Id, así sus números empiezan en 1 y no se mezclan con los de otra tabla.
        // EF reserva bloques de la secuencia al agregar la entidad (HiLo), así que el Id está listo antes de guardar.
        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoDatosCentral).Assembly);

        foreach (var entidad in constructorModelo.Model.GetEntityTypes())
        {
            if (entidad.FindPrimaryKey()?.Properties is not [{ Name: "Id" } llave] || llave.ClrType != typeof(int))
                continue;

            var tabla = entidad.GetTableName() ?? entidad.ShortName();
            constructorModelo.Entity(entidad.ClrType).Property(llave.Name).UseHiLo($"Secuencia{tabla}");
        }

        // La de los maestros publicados se define en su configuración, junto con su índice.
        foreach (var tipo in new[] { typeof(Empresa), typeof(Sucursal), typeof(Caja), typeof(Parametro) })
            constructorModelo.Entity(tipo).Property<long>(ColumnaVersion).IsRowVersion().HasConversion<byte[]>();
    }

    /// <summary>
    /// Antes de guardar, cada registro de auditoría de esta operación recibe el antes y el después de lo que cambió en la
    /// base, para que la pantalla de Auditoría lo muestre sin que cada servicio tenga que armarlo a mano.
    /// </summary>
    public override int SaveChanges(bool aceptarTodosLosCambios)
    {
        CgPos.Central.Infraestructura.Auditoria.CambiosAuditoria.Adjuntar(this);
        return base.SaveChanges(aceptarTodosLosCambios);
    }

    public override Task<int> SaveChangesAsync(bool aceptarTodosLosCambios, CancellationToken cancelacion = default)
    {
        CgPos.Central.Infraestructura.Auditoria.CambiosAuditoria.Adjuntar(this);
        return base.SaveChangesAsync(aceptarTodosLosCambios, cancelacion);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder constructorConvenciones)
    {
        // Mismas convenciones que la base de la caja: los documentos viajan entre ambas sin perder precisión.
        constructorConvenciones.Properties<decimal>().HavePrecision(18, 4);
        constructorConvenciones.Properties<string>().HaveMaxLength(256);
        constructorConvenciones.Properties<DateTime>().HavePrecision(3);
        constructorConvenciones.Properties<DateTimeOffset>().HavePrecision(3);
    }
}
