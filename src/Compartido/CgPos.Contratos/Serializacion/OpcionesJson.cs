using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CgPos.Contratos.Serializacion;

/// <summary>Formato JSON único entre caja y Central (camelCase, enums como texto).</summary>
public static class OpcionesJson
{
    public static readonly JsonSerializerOptions Predeterminadas = Crear();

    private static JsonSerializerOptions Crear()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Obligatorio antes de MakeReadOnly(); instancia compartida e inmutable (thread-safe).
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        opciones.Converters.Add(new JsonStringEnumConverter());
        opciones.MakeReadOnly();
        return opciones;
    }
}
