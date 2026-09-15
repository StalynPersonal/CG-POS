namespace CgPos.Contratos.Central;

public sealed record DatosEmpresa(Guid Id, string Rnc, string RazonSocial, string? NombreComercial, string? Direccion, string? Telefono);

/// <summary>El RNC no cambia desde el Central Manager.</summary>
public sealed record SolicitudEmpresa(string RazonSocial, string? NombreComercial, string? Direccion, string? Telefono);

public sealed record DatosSucursal(Guid Id, string Codigo, string Nombre, string? Direccion, string? Telefono, bool Activa, int Cajas);

/// <param name="Codigo">No cambia después de crear la sucursal.</param>
public sealed record SolicitudSucursal(string Codigo, string Nombre, string? Direccion, string? Telefono);

/// <param name="CredencialEmitidaEn">Cuándo se emitió la credencial activa; nulo si la caja no tiene.</param>
public sealed record DatosCaja(
    Guid Id,
    Guid SucursalId,
    string SucursalCodigo,
    string SucursalNombre,
    string Codigo,
    string Nombre,
    bool Habilitada,
    DateTimeOffset? CredencialEmitidaEn,
    DateTimeOffset? CredencialUltimoUsoEn,
    DateTimeOffset? UltimaRecepcionEn,
    DateTimeOffset? UltimaDescargaEn);

public sealed record SolicitudCaja(Guid SucursalId, string Codigo, string Nombre);

public sealed record SolicitudActualizarCaja(string Nombre);

/// <param name="Ambito">Descripción del ámbito: general, una sucursal o una caja.</param>
public sealed record DatosParametro(Guid Id, string Clave, string Valor, string? Descripcion, Guid? SucursalId, Guid? CajaId, string Ambito);

/// <summary>Sin sucursal ni caja es general; con una de ellas aplica solo ahí (y prevalece sobre el general).</summary>
public sealed record SolicitudParametro(string Clave, string Valor, Guid? SucursalId = null, Guid? CajaId = null);

public sealed record SolicitudValorParametro(string Valor);
