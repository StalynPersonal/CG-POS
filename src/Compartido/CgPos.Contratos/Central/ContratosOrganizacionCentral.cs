namespace CgPos.Contratos.Central;

public sealed record DatosEmpresa(int Id, string Rnc, string RazonSocial, string? NombreComercial, string? Direccion, string? Telefono);

/// <param name="Rnc">RNC con el que se factura; se valida su dígito verificador y el cambio queda en la auditoría.</param>
public sealed record SolicitudEmpresa(string RazonSocial, string? NombreComercial, string? Direccion, string? Telefono, string? Rnc = null);

public sealed record DatosSucursal(int Id, int Codigo, string Nombre, string? Direccion, string? Telefono, bool Activa, int Cajas);

/// <param name="Codigo">No cambia después de crear la sucursal.</param>
public sealed record SolicitudSucursal(int Codigo, string Nombre, string? Direccion, string? Telefono);

/// <param name="CredencialEmitidaEn">Cuándo se emitió la credencial activa; nulo si la caja no tiene.</param>
public sealed record DatosCaja(
    int Id,
    int SucursalId,
    int SucursalCodigo,
    string SucursalNombre,
    int Codigo,
    string Nombre,
    bool Habilitada,
    DateTimeOffset? CredencialEmitidaEn,
    DateTimeOffset? CredencialUltimoUsoEn,
    DateTimeOffset? UltimaRecepcionEn,
    DateTimeOffset? UltimaDescargaEn);

public sealed record SolicitudCaja(int SucursalId, int Codigo, string Nombre);

public sealed record SolicitudActualizarCaja(string Nombre);

/// <param name="Ambito">Descripción del ámbito: general, una sucursal o una caja.</param>
public sealed record DatosParametro(int Id, string Clave, string Valor, string? Descripcion, int? SucursalId, int? CajaId, string Ambito);

/// <summary>Sin sucursal ni caja es general; con una de ellas aplica solo ahí (y prevalece sobre el general).</summary>
public sealed record SolicitudParametro(string Clave, string Valor, int? SucursalId = null, int? CajaId = null);

public sealed record SolicitudValorParametro(string Valor);
