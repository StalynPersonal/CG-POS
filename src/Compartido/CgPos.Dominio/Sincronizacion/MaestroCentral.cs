using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Sincronizacion;

public enum TipoMaestro
{
    Moneda,
    Familia,
    UnidadMedida,
    Impuesto,
    Articulo,
    Cliente,
    FormaPago,
    Banco,
    TipoTarjeta,
    Denominacion,
    Promocion,
    MotivoDescuento,
    TopeDescuento,
    TasaCambio,
    SecuenciaEcf,
    MotivoDevolucion,
    NivelFidelidad,
    ReglaAcumulacion,
    MiembroFidelidad,
    Almacen,

    /// <summary>Descuento del banco por BIN de tarjeta (RF-98).</summary>
    DescuentoTarjeta,

    /// <summary>Rol de los usuarios de caja (con su nivel y permisos del catálogo de la caja).</summary>
    RolCaja,

    /// <summary>Usuario de caja con el hash de su PIN y carné y las cajas que opera.</summary>
    UsuarioCaja,
}

/// <summary>
/// Maestro que el Central publica para las cajas (RF-269). El Central es la autoridad sobre maestros (RN-24): se guarda en el mismo formato de carga
/// que aplica la caja, y su versión de fila (rowversion) permite bajar solo lo cambiado (RF-273).
/// </summary>
public sealed class MaestroCentral
{
    public const int LargoMaximoCodigo = 100;
    public const int LargoMaximoUsuario = 150;
    public const int LargoMaximoTextoBusqueda = 1000;

    private MaestroCentral()
    {
    }

    public TipoMaestro Tipo { get; private set; }

    /// <summary>Id del maestro: el mismo con el que queda en las cajas.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código que no se repite dentro del tipo (artículo, cédula del miembro…); nulo si el tipo no tiene uno.</summary>
    public string? Codigo { get; private set; }

    /// <summary>Caja a la que pertenece (ej. un rango de e-CF); nulo si es para todas.</summary>
    public Guid? CajaId { get; private set; }

    /// <summary>Registro de carga en JSON, tal como lo aplica la caja.</summary>
    public string Contenido { get; private set; } = string.Empty;

    public DateTimeOffset ModificadoEn { get; private set; }
    public string ModificadoPor { get; private set; } = string.Empty;

    /// <summary>Texto donde se busca (código, descripción, códigos de barras…) en minúsculas y sin acentos; nulo si el tipo no se busca.</summary>
    public string? TextoBusqueda { get; private set; }

    public static MaestroCentral Publicar(TipoMaestro tipo, Guid id, string? codigo, Guid? cajaId, string contenido, DateTimeOffset ahora, string usuario,
        string? textoBusqueda = null)
    {
        var maestro = new MaestroCentral { Tipo = tipo, Id = Validar.Id(id, "Maestro") };
        maestro.Asignar(codigo, cajaId, contenido, ahora, usuario, textoBusqueda);
        return maestro;
    }

    /// <returns><c>true</c> si algo cambió: solo entonces el maestro recibe una versión nueva y baja otra vez a las cajas.</returns>
    public bool Actualizar(string? codigo, Guid? cajaId, string contenido, DateTimeOffset ahora, string usuario, string? textoBusqueda = null)
    {
        if (Contenido == contenido && Codigo == Normalizar(codigo) && CajaId == cajaId && TextoBusqueda == NormalizarBusqueda(textoBusqueda))
            return false;

        Asignar(codigo, cajaId, contenido, ahora, usuario, textoBusqueda);
        return true;
    }

    /// <summary>Minúsculas y sin acentos, para que "jabon" encuentre "Jabón".</summary>
    public static string? NormalizarBusqueda(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        var descompuesto = texto.Trim().Normalize(System.Text.NormalizationForm.FormD);
        var limpio = new System.Text.StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark))
            limpio.Append(char.ToLowerInvariant(caracter));

        var normalizado = limpio.ToString().Normalize(System.Text.NormalizationForm.FormC);
        return normalizado.Length > LargoMaximoTextoBusqueda ? normalizado[..LargoMaximoTextoBusqueda] : normalizado;
    }

    private void Asignar(string? codigo, Guid? cajaId, string contenido, DateTimeOffset ahora, string usuario, string? textoBusqueda)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);
        if (cajaId == Guid.Empty)
            throw new ArgumentException("La caja del maestro no puede ser un Id vacío.", nameof(cajaId));

        Codigo = Normalizar(codigo) is { Length: > LargoMaximoCodigo } largo
            ? throw new ArgumentException($"El código no puede exceder {LargoMaximoCodigo} caracteres ({largo}).", nameof(codigo))
            : Normalizar(codigo);
        CajaId = cajaId;
        Contenido = contenido;
        TextoBusqueda = NormalizarBusqueda(textoBusqueda);
        ModificadoEn = ahora;
        ModificadoPor = Validar.Texto(usuario, "Modificado por", LargoMaximoUsuario);
    }

    private static string? Normalizar(string? codigo) => string.IsNullOrWhiteSpace(codigo) ? null : codigo.Trim().ToUpperInvariant();
}
