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

/// <param name="Clave">Clave en claro: solo en archivos de desarrollo o al asignarla en el Central; se guarda como hash y nunca baja a la caja.</param>
/// <param name="ClaveHash">Hash de la clave ya calculado (lo que envía el Central).</param>
public sealed record UsuarioCarga(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid RolId,
    IReadOnlyList<Guid>? Cajas = null,
    string? Clave = null,
    string? ClaveHash = null,
    bool Activo = true);

public sealed record ParametroCarga(
    Guid Id,
    string Clave,
    string Valor,
    string? Descripcion = null,
    Guid? SucursalId = null,
    Guid? CajaId = null);
