namespace CgPos.Contratos.Central;

public sealed record DatosEmpresa(int Id, string Rnc, string RazonSocial, string? NombreComercial, string? Direccion, string? Telefono);

/// <param name="Rnc">RNC con el que se factura; se valida su dígito verificador y el cambio queda en la auditoría.</param>
public sealed record SolicitudEmpresa(string RazonSocial, string? NombreComercial, string? Direccion, string? Telefono, string? Rnc = null);

public sealed record DatosSucursal(int Id, string Codigo, string Nombre, string? Direccion, string? Telefono, bool Activa, int Cajas);

/// <param name="Codigo">No cambia después de crear la sucursal.</param>
public sealed record SolicitudSucursal(string Codigo, string Nombre, string? Direccion, string? Telefono);

/// <param name="CredencialEmitidaEn">Cuándo se emitió la credencial activa; nulo si la caja no tiene.</param>
public sealed record DatosCaja(
    int Id,
    int SucursalId,
    string SucursalCodigo,
    string SucursalNombre,
    string Codigo,
    string Nombre,
    bool Habilitada,
    DateTimeOffset? CredencialEmitidaEn,
    DateTimeOffset? CredencialUltimoUsoEn,
    DateTimeOffset? UltimaRecepcionEn,
    DateTimeOffset? UltimaDescargaEn,
    /// <summary>Dirección de red fija de la caja; se comprueba en cada comunicación con el Central.</summary>
    string? DireccionIp = null);

public sealed record SolicitudCaja(int SucursalId, string Codigo, string Nombre, string? DireccionIp = null);

public sealed record SolicitudActualizarCaja(string Nombre, string? DireccionIp = null);

/// <param name="Ambito">Descripción del ámbito: general, una sucursal o una caja.</param>
public sealed record DatosParametro(int Id, string Clave, string Valor, string? Descripcion, int? SucursalId, int? CajaId, string Ambito);

/// <summary>Sin sucursal ni caja es general; con una de ellas aplica solo ahí (y prevalece sobre el general).</summary>
public sealed record SolicitudParametro(string Clave, string Valor, int? SucursalId = null, int? CajaId = null);

public sealed record SolicitudValorParametro(string Valor);

/// <summary>
/// Numeración de un documento que emite el Central. Cada documento tiene la suya y sin ella no se puede crear: es una
/// decisión del negocio, no algo que el sistema invente la primera vez que hace falta.
/// </summary>
/// <param name="Codigo">Con lo que el sistema la busca; no cambia.</param>
/// <param name="Prefijo">Lo que se ve delante del correlativo; esto sí se puede cambiar.</param>
/// <param name="Proximo">El número que se llevará el próximo documento, ya formateado.</param>
public sealed record DatosSecuenciaCentral(string Codigo, string Prefijo, string Documento, long Ultimo, int Digitos, bool Activa, string Proximo);

/// <param name="Codigo">Identifica la secuencia; con él la busca el sistema al numerar.</param>
/// <param name="Ultimo">Último número entregado; el próximo documento se lleva este más uno.</param>
public sealed record SolicitudSecuenciaCentral(string Codigo, string Prefijo, string Documento, long Ultimo, int Digitos, bool Activa);
