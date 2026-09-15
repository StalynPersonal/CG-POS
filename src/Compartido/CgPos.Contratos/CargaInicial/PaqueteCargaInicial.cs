namespace CgPos.Contratos.CargaInicial;

/// <summary>
/// Datos de organización y seguridad para una caja. Hoy se carga desde un archivo JSON;
/// más adelante lo enviará el Central por sincronización. Los Id son los del Central.
/// </summary>
public sealed record PaqueteCargaInicial(
    EmpresaCarga Empresa,
    IReadOnlyList<SucursalCarga>? Sucursales = null,
    IReadOnlyList<CajaCarga>? Cajas = null,
    IReadOnlyList<RolCarga>? Roles = null,
    IReadOnlyList<UsuarioCarga>? Usuarios = null,
    IReadOnlyList<ParametroCarga>? Parametros = null);

public sealed record EmpresaCarga(
    Guid Id,
    string Rnc,
    string RazonSocial,
    string? NombreComercial = null,
    string? Direccion = null,
    string? Telefono = null);

public sealed record SucursalCarga(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Direccion = null,
    string? Telefono = null,
    bool Activa = true);

public sealed record CajaCarga(
    Guid Id,
    Guid SucursalId,
    string Codigo,
    string Nombre,
    bool Habilitada = true);

/// <param name="Permisos">Códigos del catálogo de permisos; <c>"*"</c> asigna todos.</param>
public sealed record RolCarga(
    Guid Id,
    string Codigo,
    string Nombre,
    int Nivel,
    IReadOnlyList<string>? Permisos = null,
    bool Activo = true);

/// <param name="Pin">PIN en claro. Solo para archivos de desarrollo: se guarda como hash.</param>
/// <param name="PinHash">Hash del PIN ya calculado (lo que envía el Central).</param>
/// <param name="CredencialBarras">Código de barras del carné en claro. Solo para desarrollo.</param>
/// <param name="CredencialBarrasHash">SHA-256 hex del código de barras del carné.</param>
public sealed record UsuarioCarga(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid RolId,
    IReadOnlyList<Guid>? Cajas = null,
    string? Pin = null,
    string? PinHash = null,
    string? CredencialBarras = null,
    string? CredencialBarrasHash = null,
    bool Activo = true);

public sealed record ParametroCarga(
    Guid Id,
    string Clave,
    string Valor,
    string? Descripcion = null,
    Guid? SucursalId = null,
    Guid? CajaId = null);
