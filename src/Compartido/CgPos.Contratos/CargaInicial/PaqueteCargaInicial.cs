namespace CgPos.Contratos.CargaInicial;

/// <summary>
/// Datos de organización y seguridad para una caja: los envía el Central por sincronización (o un archivo en desarrollo).
/// No llevan Id: la empresa se identifica por su RNC, las sucursales y cajas por su código, los roles y usuarios por su código y los parámetros
/// por su clave y ámbito.
/// </summary>
public sealed record PaqueteCargaInicial(
    EmpresaCarga Empresa,
    IReadOnlyList<SucursalCarga>? Sucursales = null,
    IReadOnlyList<CajaCarga>? Cajas = null,
    IReadOnlyList<RolCarga>? Roles = null,
    IReadOnlyList<UsuarioCarga>? Usuarios = null,
    IReadOnlyList<ParametroCarga>? Parametros = null);

public sealed record EmpresaCarga(
    string Rnc,
    string RazonSocial,
    string? NombreComercial = null,
    string? Direccion = null,
    string? Telefono = null);

public sealed record SucursalCarga(
    int Codigo,
    string Nombre,
    string? Direccion = null,
    string? Telefono = null,
    bool Activa = true);

/// <param name="Codigo">Único dentro de la sucursal.</param>
public sealed record CajaCarga(
    int SucursalCodigo,
    int Codigo,
    string Nombre,
    bool Habilitada = true);

/// <summary>Una caja se identifica por el código de su sucursal y el suyo.</summary>
public sealed record CajaReferencia(int SucursalCodigo, int CajaCodigo);

/// <param name="Permisos">Códigos del catálogo de permisos; <c>"*"</c> asigna todos.</param>
public sealed record RolCarga(
    string Codigo,
    string Nombre,
    int Nivel,
    IReadOnlyList<string>? Permisos = null,
    bool Activo = true);

/// <param name="Clave">Clave en claro: solo en archivos de desarrollo o al asignarla en el Central; se guarda como hash y nunca baja a la caja.</param>
/// <param name="ClaveHash">Hash de la clave ya calculado (lo que envía el Central).</param>
public sealed record UsuarioCarga(
    string Codigo,
    string Nombre,
    string RolCodigo,
    IReadOnlyList<CajaReferencia>? Cajas = null,
    string? Clave = null,
    string? ClaveHash = null,
    bool Activo = true);

/// <summary>Parámetro general (sin sucursal ni caja), de una sucursal o de una caja; se identifica por su clave y su ámbito.</summary>
public sealed record ParametroCarga(
    string Clave,
    string Valor,
    string? Descripcion = null,
    int? SucursalCodigo = null,
    int? CajaCodigo = null);

/// <summary>Clave y ámbito de un parámetro, para saber cuáles siguen vigentes.</summary>
public sealed record ParametroReferencia(string Clave, int? SucursalCodigo = null, int? CajaCodigo = null);
