using System.Globalization;
using System.Text.Json.Nodes;

namespace CgPos.Central.Web.Maestros;

/// <summary>Lectura y escritura de los campos de un registro de carga en JSON.</summary>
public static class ValoresCatalogo
{
    public const string SegunTipo = "segun-tipo";

    /// <returns>El valor tipado del campo: string, decimal, int, bool, DateTimeOffset o, en Sí/No según el tipo, "true", "false" o <see cref="SegunTipo"/>.</returns>
    public static object? Leer(JsonObject dato, CampoCatalogo campo)
    {
        if (dato[campo.Nombre] is not JsonValue nodo)
            return campo.Tipo == TipoCampoCatalogo.SiNoSegunTipo ? SegunTipo : campo.Predeterminado;

        return campo.Tipo switch
        {
            TipoCampoCatalogo.Decimal => nodo.GetValue<decimal>(),
            TipoCampoCatalogo.Entero => nodo.GetValue<int>(),
            TipoCampoCatalogo.Departamento or TipoCampoCatalogo.Sucursal => nodo.GetValue<int>().ToString(CultureInfo.InvariantCulture),
            TipoCampoCatalogo.Booleano => nodo.GetValue<bool>(),
            TipoCampoCatalogo.SiNoSegunTipo => nodo.GetValue<bool>() ? "true" : "false",
            TipoCampoCatalogo.FechaHora => DateTimeOffset.Parse(nodo.GetValue<string>(), CultureInfo.InvariantCulture),
            _ => nodo.GetValue<string>(),
        };
    }

    /// <summary>Escribe el valor en el registro; un valor vacío quita la propiedad (la caja toma su valor predeterminado).</summary>
    public static void Escribir(JsonObject dato, CampoCatalogo campo, object? valor)
    {
        JsonNode? nodo = valor switch
        {
            null => null,
            string texto when string.IsNullOrWhiteSpace(texto) || texto == SegunTipo => null,
            string texto when campo.Tipo == TipoCampoCatalogo.SiNoSegunTipo => JsonValue.Create(texto == "true"),
            string texto when campo.Tipo is TipoCampoCatalogo.Departamento or TipoCampoCatalogo.Sucursal =>
                JsonValue.Create(int.Parse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture)),
            string texto => JsonValue.Create(texto.Trim()),
            decimal numero => JsonValue.Create(numero),
            int entero => JsonValue.Create(entero),
            bool logico => JsonValue.Create(logico),
            DateTimeOffset fecha => JsonValue.Create(fecha.ToString("O", CultureInfo.InvariantCulture)),
            _ => throw new ArgumentException($"Valor no soportado para {campo.Nombre}.", nameof(valor)),
        };

        if (nodo is null)
            dato.Remove(campo.Nombre);
        else
            dato[campo.Nombre] = nodo;
    }

    /// <summary>Vacío en un campo obligatorio (el Sí/No según el tipo nunca lo es).</summary>
    public static bool EstaVacio(object? valor) => valor is null || valor is string texto && string.IsNullOrWhiteSpace(texto);
}
