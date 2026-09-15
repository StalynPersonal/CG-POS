using System.Reflection;
using System.Text.Json;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Pruebas.Soporte;

namespace CgPos.Pos.Pruebas.Organizacion;

/// <summary>El Central solo deja configurar lo que está en el catálogo: toda clave que lee la caja debe estar ahí.</summary>
public class CatalogoParametrosCajaPruebas
{
    [Fact]
    public void Todas_las_claves_que_lee_la_caja_estan_en_el_catalogo_como_parametros_de_caja()
    {
        var claves = typeof(ClavesParametros).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(campo => campo.IsLiteral && campo.FieldType == typeof(string))
            .Select(campo => (string)campo.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(claves);
        Assert.All(claves, clave => Assert.Equal(AlcanceParametro.Caja, CatalogoParametros.Buscar(clave)?.Alcance));
    }

    [Fact]
    public async Task Los_parametros_de_la_carga_inicial_de_desarrollo_son_validos_para_el_catalogo()
    {
        await using var archivo = File.OpenRead(Path.Combine(RutasPrueba.RaizRepositorio(), "datos", "carga-inicial.desarrollo.json"));
        var paquete = await JsonSerializer.DeserializeAsync<PaqueteCargaInicial>(archivo, OpcionesJson.Predeterminadas);

        Assert.NotEmpty(paquete!.Parametros!);
        Assert.All(paquete.Parametros!, parametro =>
        {
            var definicion = CatalogoParametros.Buscar(parametro.Clave);
            Assert.NotNull(definicion);
            Assert.Null(definicion.ValidarValor(parametro.Valor));
        });
    }
}
