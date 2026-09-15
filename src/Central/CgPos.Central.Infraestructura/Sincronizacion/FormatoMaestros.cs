using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Sincronizacion;

namespace CgPos.Central.Infraestructura.Sincronizacion;

/// <summary>Registro de carga listo para publicar: su tipo, Id, código único (si tiene) y caja (si es de una sola).</summary>
internal sealed record FilaMaestro(TipoMaestro Tipo, Guid Id, string? Codigo, Guid? CajaId, object Dato)
{
    public string Contenido() => JsonSerializer.Serialize(Dato, Dato.GetType(), OpcionesJson.Predeterminadas);
}

/// <summary>Conversión entre el paquete de maestros de la caja y las filas publicadas del Central.</summary>
internal static class FormatoMaestros
{
    public static IEnumerable<FilaMaestro> Desglosar(PaqueteMaestros paquete)
    {
        foreach (var d in paquete.Monedas ?? []) yield return new(TipoMaestro.Moneda, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.Familias ?? []) yield return new(TipoMaestro.Familia, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.UnidadesMedida ?? []) yield return new(TipoMaestro.UnidadMedida, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.Impuestos ?? []) yield return new(TipoMaestro.Impuesto, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.Articulos ?? []) yield return new(TipoMaestro.Articulo, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.Clientes ?? []) yield return new(TipoMaestro.Cliente, d.Id, $"{d.TipoDocumento}:{d.Documento?.Trim()}", null, d);
        foreach (var d in paquete.FormasPago ?? []) yield return new(TipoMaestro.FormaPago, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.Bancos ?? []) yield return new(TipoMaestro.Banco, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.TiposTarjeta ?? []) yield return new(TipoMaestro.TipoTarjeta, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.Denominaciones ?? []) yield return new(TipoMaestro.Denominacion, d.Id, $"{d.Moneda?.Trim()}:{d.Valor:0.####}:{d.Tipo}", null, d);
        foreach (var d in paquete.Promociones ?? []) yield return new(TipoMaestro.Promocion, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.MotivosDescuento ?? []) yield return new(TipoMaestro.MotivoDescuento, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.TopesDescuento ?? []) yield return new(TipoMaestro.TopeDescuento, d.Id, null, null, d);
        foreach (var d in paquete.TasasCambio ?? []) yield return new(TipoMaestro.TasaCambio, d.Id, null, null, d);
        foreach (var d in paquete.SecuenciasEcf ?? []) yield return new(TipoMaestro.SecuenciaEcf, d.Id, null, d.CajaId, d);
        foreach (var d in paquete.MotivosDevolucion ?? []) yield return new(TipoMaestro.MotivoDevolucion, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.NivelesFidelidad ?? []) yield return new(TipoMaestro.NivelFidelidad, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.ReglasAcumulacion ?? []) yield return new(TipoMaestro.ReglaAcumulacion, d.Id, d.Codigo, null, d);
        foreach (var d in paquete.MiembrosFidelidad ?? []) yield return new(TipoMaestro.MiembroFidelidad, d.Id, CedulaNormalizada(d.Cedula), null, d);
        foreach (var d in paquete.Almacenes ?? []) yield return new(TipoMaestro.Almacen, d.Id, d.Codigo, null, d);
    }

    /// <summary>Arma el paquete de catálogo con las filas publicadas; nulo si no hay ninguna.</summary>
    public static PaqueteMaestros? Armar(IEnumerable<MaestroCentral> filas)
    {
        var porTipo = filas.Where(f => f.Tipo is not (TipoMaestro.RolCaja or TipoMaestro.UsuarioCaja)).ToLookup(f => f.Tipo);
        if (porTipo.Count == 0)
            return null;

        List<T>? Lista<T>(TipoMaestro tipo) => porTipo.Contains(tipo) ? porTipo[tipo].Select(Leer<T>).ToList() : null;

        return new PaqueteMaestros(
            Familias: Lista<FamiliaCarga>(TipoMaestro.Familia),
            UnidadesMedida: Lista<UnidadMedidaCarga>(TipoMaestro.UnidadMedida),
            Impuestos: Lista<ImpuestoCarga>(TipoMaestro.Impuesto),
            Articulos: Lista<ArticuloCarga>(TipoMaestro.Articulo),
            Clientes: Lista<ClienteCarga>(TipoMaestro.Cliente),
            FormasPago: Lista<FormaPagoCarga>(TipoMaestro.FormaPago),
            Bancos: Lista<BancoCarga>(TipoMaestro.Banco),
            TiposTarjeta: Lista<TipoTarjetaCarga>(TipoMaestro.TipoTarjeta),
            Denominaciones: Lista<DenominacionCarga>(TipoMaestro.Denominacion),
            Promociones: Lista<PromocionCarga>(TipoMaestro.Promocion),
            MotivosDescuento: Lista<MotivoDescuentoCarga>(TipoMaestro.MotivoDescuento),
            TopesDescuento: Lista<TopeDescuentoCarga>(TipoMaestro.TopeDescuento),
            TasasCambio: Lista<TasaCambioCarga>(TipoMaestro.TasaCambio),
            SecuenciasEcf: Lista<SecuenciaEcfCarga>(TipoMaestro.SecuenciaEcf),
            MotivosDevolucion: Lista<MotivoDevolucionCarga>(TipoMaestro.MotivoDevolucion),
            Monedas: Lista<MonedaCarga>(TipoMaestro.Moneda),
            NivelesFidelidad: Lista<NivelFidelidadCarga>(TipoMaestro.NivelFidelidad),
            ReglasAcumulacion: Lista<ReglaAcumulacionCarga>(TipoMaestro.ReglaAcumulacion),
            MiembrosFidelidad: Lista<MiembroFidelidadCarga>(TipoMaestro.MiembroFidelidad),
            Almacenes: Lista<AlmacenCarga>(TipoMaestro.Almacen));
    }

    public static List<T> Filtrar<T>(IEnumerable<MaestroCentral> filas, TipoMaestro tipo) => filas.Where(f => f.Tipo == tipo).Select(Leer<T>).ToList();

    public static T Leer<T>(MaestroCentral fila) =>
        JsonSerializer.Deserialize<T>(fila.Contenido, OpcionesJson.Predeterminadas)
        ?? throw new InvalidOperationException($"El maestro {fila.Tipo} {fila.Id} está vacío.");

    public static string? CedulaNormalizada(string? cedula)
    {
        try
        {
            return MiembroFidelidad.ValidarCedula(cedula ?? string.Empty);
        }
        catch (ArgumentException)
        {
            // La validación del dominio informa el error; aquí solo se evita romper el desglose.
            return cedula?.Trim();
        }
    }
}

/// <summary>
/// Hash de PIN y carné con el formato que verifica la caja (PBKDF2-SHA256 de 100,000 iteraciones y SHA-256 del carné). Los usuarios de caja se
/// administran en el Central y bajan ya con su hash: el PIN nunca viaja en claro.
/// </summary>
internal static class HashCredencialesCaja
{
    private const string Algoritmo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;
    private const int BytesSal = 16;
    private const int BytesHash = 32;

    public static bool EsPinValido(string? pin) => pin is { Length: >= 4 and <= 8 } && pin.All(char.IsAsciiDigit);

    public static string HashPin(string pin)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"{Algoritmo}${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerificarPin(string pin, string pinHash)
    {
        if (pinHash.Split('$') is not [Algoritmo, var textoIteraciones, var textoSal, var textoHash] || !int.TryParse(textoIteraciones, out var iteraciones))
            return false;

        try
        {
            var esperado = Convert.FromBase64String(textoHash);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(pin, Convert.FromBase64String(textoSal), iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string HashCredencialBarras(string codigoBarras) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigoBarras.Trim())));
}
