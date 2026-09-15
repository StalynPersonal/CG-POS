using System.Text.Json;
using CgPos.Contracts.Serializacion;
using CgPos.Contracts.Sincronizacion;

namespace CgPos.Pos.Tests.Sincronizacion;

public class OpcionesJsonTests
{
    private sealed record Ejemplo(string Numero, decimal Total, EstadoMensajeOutbox Estado, string? Comentario = null);

    [Fact]
    public void Serializa_en_camelCase_con_enums_como_texto_y_sin_nulos()
    {
        var json = JsonSerializer.Serialize(new Ejemplo("E320000000001", 850.00m, EstadoMensajeOutbox.Pendiente), OpcionesJson.Predeterminadas);

        Assert.Equal("""{"numero":"E320000000001","total":850.00,"estado":"Pendiente"}""", json);
    }

    [Fact]
    public void Ida_y_vuelta_conserva_los_datos()
    {
        var original = new Ejemplo("E320000000001", 2175.34m, EstadoMensajeOutbox.Confirmado, "Prueba");

        var json = JsonSerializer.Serialize(original, OpcionesJson.Predeterminadas);
        var copia = JsonSerializer.Deserialize<Ejemplo>(json, OpcionesJson.Predeterminadas);

        Assert.Equal(original, copia);
    }

    [Fact]
    public void Las_opciones_compartidas_son_inmutables()
    {
        Assert.True(OpcionesJson.Predeterminadas.IsReadOnly);
    }
}
