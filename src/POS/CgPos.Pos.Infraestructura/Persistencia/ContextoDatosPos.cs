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
    public DbSet<ConfiguracionCaja> ConfiguracionCaja => Set<ConfiguracionCaja>();
    public DbSet<MarcaSincronizacion> MarcasSincronizacion => Set<MarcaSincronizacion>();

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
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<Impuesto> Impuestos => Set<Impuesto>();
    public DbSet<Articulo> Articulos => Set<Articulo>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Moneda> Monedas => Set<Moneda>();
    public DbSet<FormaPago> FormasPago => Set<FormaPago>();
    public DbSet<Banco> Bancos => Set<Banco>();
    public DbSet<TipoTarjeta> TiposTarjeta => Set<TipoTarjeta>();
    public DbSet<Denominacion> Denominaciones => Set<Denominacion>();

    // Precios (M04)

    // Turnos y ventas (M13, M05)
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<CierreTurno> CierresTurno => Set<CierreTurno>();

    /// <summary>El cierre del lote del terminal de tarjetas, cuadrado contra lo aprobado en el turno.</summary>
    public DbSet<LoteTarjetas> LotesTarjetas => Set<LoteTarjetas>();
    /// <summary>La venta que el cajero está armando: todavía no tiene número de factura.</summary>
    public DbSet<VentaEnProceso> VentasEnProceso => Set<VentaEnProceso>();

    /// <summary>Las ventas que el cajero dejó en espera, nombradas con su referencia.</summary>
    public DbSet<VentaGuardada> VentasGuardadas => Set<VentaGuardada>();

    /// <summary>Las ventas cobradas: los documentos, con su número de factura.</summary>
    public DbSet<VentaCobrada> Ventas => Set<VentaCobrada>();
    public DbSet<AutorizacionOtorgada> AutorizacionesOtorgadas => Set<AutorizacionOtorgada>();

    // Devoluciones y notas de crédito (M10)
    public DbSet<CgPos.Dominio.Devoluciones.MotivoDevolucion> MotivosDevolucion => Set<CgPos.Dominio.Devoluciones.MotivoDevolucion>();
    public DbSet<CgPos.Dominio.Devoluciones.Devolucion> Devoluciones => Set<CgPos.Dominio.Devoluciones.Devolucion>();

    /// <summary>Copia temporal de las facturas de otras tiendas que se piden al Central para devolverlas; se borra al emitir la nota.</summary>
    public DbSet<CgPos.Dominio.Devoluciones.FacturaConsultada> FacturasConsultadas => Set<CgPos.Dominio.Devoluciones.FacturaConsultada>();

    // Descuentos y promociones (M06, M07)
    public DbSet<Promocion> Promociones => Set<Promocion>();

    /// <summary>Descuentos del banco por BIN de tarjeta (RF-98).</summary>
    public DbSet<DescuentoTarjeta> DescuentosTarjeta => Set<DescuentoTarjeta>();
    public DbSet<MotivoDescuento> MotivosDescuento => Set<MotivoDescuento>();
    public DbSet<TopeDescuento> TopesDescuento => Set<TopeDescuento>();

    // Fidelidad (M11)
    public DbSet<CgPos.Dominio.Fidelidad.NivelFidelidad> NivelesFidelidad => Set<CgPos.Dominio.Fidelidad.NivelFidelidad>();
    public DbSet<CgPos.Dominio.Fidelidad.ReglaAcumulacion> ReglasAcumulacion => Set<CgPos.Dominio.Fidelidad.ReglaAcumulacion>();
    public DbSet<CgPos.Dominio.Fidelidad.MiembroFidelidad> MiembrosFidelidad => Set<CgPos.Dominio.Fidelidad.MiembroFidelidad>();
    public DbSet<CgPos.Dominio.Fidelidad.MovimientoPuntos> MovimientosPuntos => Set<CgPos.Dominio.Fidelidad.MovimientoPuntos>();

    // Pendientes de entrega y envíos (M12)
    public DbSet<CgPos.Dominio.Entregas.PendienteEntrega> PendientesEntrega => Set<CgPos.Dominio.Entregas.PendienteEntrega>();

    // Cobro (M08)
    public DbSet<TasaCambio> TasasCambio => Set<TasaCambio>();
    public DbSet<OperacionTerminal> OperacionesTerminal => Set<OperacionTerminal>();

    // Facturación electrónica (M09)
    public DbSet<SecuenciaEcf> SecuenciasEcf => Set<SecuenciaEcf>();
    public DbSet<DocumentoElectronico> DocumentosElectronicos => Set<DocumentoElectronico>();

    protected override void OnModelCreating(ModelBuilder constructorModelo)
    {
        // Cada tabla tiene su propia secuencia de Id, así sus números empiezan en 1 y no se mezclan con los de otra tabla.
        // EF reserva bloques de la secuencia al agregar la entidad (HiLo), así que el Id está listo antes de guardar.
        // La venta, su línea y su destino son solo las reglas: lo que se guarda son sus tres variantes (en proceso,
        // guardada y cobrada), cada una en su tabla y con su propia numeración de Id. Sin esto EF las tomaría por herencia.
        constructorModelo.Ignore<Venta>();
        constructorModelo.Ignore<LineaVenta>();
        constructorModelo.Ignore<CgPos.Dominio.Entregas.DestinoEntrega>();
        constructorModelo.Ignore<CgPos.Dominio.Entregas.LineaDestinoEntrega>();

        // Entregar la mercancía pendiente es del Central: la caja crea el pendiente al cobrar y lo sube, nada más.
        constructorModelo.Ignore<CgPos.Dominio.Entregas.EntregaPendiente>();
        constructorModelo.Ignore<CgPos.Dominio.Entregas.LineaEntregaPendiente>();

        constructorModelo.ApplyConfigurationsFromAssembly(typeof(ContextoDatosPos).Assembly);

        foreach (var entidad in constructorModelo.Model.GetEntityTypes())
        {
            if (entidad.FindPrimaryKey()?.Properties is not [{ Name: "Id" } llave] || llave.ClrType != typeof(int))
                continue;

            var tabla = entidad.GetTableName() ?? entidad.ShortName();

            // La secuencia avanza de uno en uno: los Id quedan consecutivos, sin huecos. A cambio, cada alta le pide su
            // número a la base (una ida y vuelta más), lo que solo se nota en las cargas masivas de artículos.
            constructorModelo.HasSequence<int>($"Secuencia{tabla}").IncrementsBy(1);
            constructorModelo.Entity(entidad.ClrType).Property(llave.Name).UseHiLo($"Secuencia{tabla}");
        }

        // Quién y cuándo dejó así la organización de esta caja. Los maestros no las llevan: en la caja no se editan,
        // bajan del Central, y allá queda registrado quién los cambió.
        foreach (var tipo in new[] { typeof(CgPos.Dominio.Organizacion.Empresa), typeof(CgPos.Dominio.Organizacion.Sucursal),
                     typeof(CgPos.Dominio.Organizacion.Caja), typeof(CgPos.Dominio.Organizacion.Parametro) })
        {
            constructorModelo.Entity(tipo).Property<DateTimeOffset>(MarcasModificacion.ModificadoEn).HasPrecision(3);
            constructorModelo.Entity(tipo).Property<string>(MarcasModificacion.ModificadoPor)
                .HasMaxLength(MarcasModificacion.LargoMaximoUsuario).IsRequired();
        }
    }

    public override int SaveChanges(bool aceptarTodosLosCambios)
    {
        MarcasModificacion.Aplicar(this, TimeProvider.System);
        return base.SaveChanges(aceptarTodosLosCambios);
    }

    public override Task<int> SaveChangesAsync(bool aceptarTodosLosCambios, CancellationToken cancelacion = default)
    {
        MarcasModificacion.Aplicar(this, TimeProvider.System);
        return base.SaveChangesAsync(aceptarTodosLosCambios, cancelacion);
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
