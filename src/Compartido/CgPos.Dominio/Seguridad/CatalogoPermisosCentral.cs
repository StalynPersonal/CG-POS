using System.Collections.Frozen;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Permisos de los usuarios del Central Manager. Son distintos de los de la caja (<see cref="CatalogoPermisos"/>):
/// un rol del Central administra y consulta, no opera una caja. Formato de código: "Central.Modulo.Accion".
/// </summary>
public static class CatalogoPermisosCentral
{
    public const string AdministrarSeguridad = "Central.Seguridad.Administrar";
    public const string ConsultarAuditoria = "Central.Auditoria.Consultar";
    public const string AdministrarOrganizacion = "Central.Organizacion.Administrar";
    public const string AdministrarDispositivos = "Central.Dispositivos.Administrar";

    /// <summary>Autoriza a montar una caja: es lo que el técnico escribe en la pantalla de configuración del equipo.</summary>
    public const string ConfigurarCajas = "Central.Cajas.Configurar";
    public const string AdministrarUsuariosCaja = "Central.UsuariosCaja.Administrar";
    public const string AdministrarMaestros = "Central.Maestros.Administrar";
    public const string CorregirDocumentoCliente = "Central.Clientes.CorregirDocumento";
    public const string AdministrarPrecios = "Central.Precios.Administrar";
    public const string AdministrarPromociones = "Central.Promociones.Administrar";
    public const string AdministrarFiscal = "Central.Fiscal.Administrar";
    public const string MonitorearSincronizacion = "Central.Sincronizacion.Monitorear";
    public const string AdministrarNotasCredito = "Central.NotasCredito.Administrar";
    public const string AdministrarFidelidad = "Central.Fidelidad.Administrar";
    public const string AdministrarListasBoda = "Central.ListasBoda.Administrar";
    public const string OperarDespacho = "Central.Despacho.Operar";
    public const string ConsultarReportes = "Central.Reportes.Consultar";
    public const string CerrarSucursal = "Central.CierresSucursal.Operar";

    /// <summary>Corregir el cuadre de un cierre de caja. La caja no puede: cerrar allá es definitivo.</summary>
    public const string AjustarCierres = "Central.Cierres.Ajustar";

    public static IReadOnlyList<DefinicionPermiso> Todos { get; } =
    [
        new(AdministrarSeguridad, "Seguridad", "Administrar usuarios y roles del Central"),
        new(ConsultarAuditoria, "Seguridad", "Consultar la auditoría del Central y de las cajas"),
        new(AdministrarOrganizacion, "Organización", "Administrar empresa, sucursales, cajas y parámetros"),
        new(AdministrarDispositivos, "Organización", "Emitir y revocar las credenciales con las que las cajas se conectan"),
        new(ConfigurarCajas, "Organización", "Autorizar la configuración de una caja desde el propio equipo"),
        new(AdministrarUsuariosCaja, "Seguridad", "Administrar usuarios y roles de las cajas"),
        new(AdministrarMaestros, "Maestros", "Administrar artículos, clientes y catálogos"),
        new(CorregirDocumentoCliente, "Maestros", "Corregir el tipo y el número de documento de un cliente (queda auditado con motivo)"),
        new(AdministrarPrecios, "Precios", "Administrar precios y topes de descuento"),
        new(AdministrarPromociones, "Promociones", "Crear, importar y distribuir promociones"),
        new(AdministrarFiscal, "Fiscal", "Administrar secuencias de e-CF y el envío a la DGII"),
        new(MonitorearSincronizacion, "Sincronización", "Monitorear la sincronización de las cajas y reenviar"),
        new(AdministrarNotasCredito, "Devoluciones", "Consultar y habilitar notas de crédito entre sucursales"),
        new(AdministrarFidelidad, "Fidelidad", "Administrar el programa de fidelidad"),
        new(AdministrarListasBoda, "Listas de boda", "Crear y administrar listas de boda y de regalos"),
        new(OperarDespacho, "Pendientes", "Operar el despacho central de pendientes y envíos"),
        new(ConsultarReportes, "Reportes", "Consultar y exportar reportes"),
        new(CerrarSucursal, "Reportes", "Hacer el cierre consolidado de sucursal y registrar sus depósitos"),
        new(AjustarCierres, "Reportes", "Corregir el cuadre de un cierre de caja, con motivo y auditoría"),
    ];

    private static readonly FrozenSet<string> Codigos = Todos.Select(p => p.Codigo).ToFrozenSet(StringComparer.Ordinal);

    public static bool Existe(string codigo) => Codigos.Contains(codigo);
}
