using System.Diagnostics;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Pruebas.Infraestructura;
using CgPos.Pos.Pruebas.Soporte;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace CgPos.Pos.Pruebas.Catalogo;

/// <summary>Búsqueda con 50,000 artículos (criterio del plan: menos de 1 s). Base propia para no afectar otras pruebas.</summary>
public class RendimientoBusquedaPruebas(BaseDatosPruebas baseDatos, ITestOutputHelper salida) : IClassFixture<BaseDatosPruebas>
{
    private const int CantidadArticulos = 50_000;

    [SkippableFact]
    public async Task Buscar_entre_50_mil_articulos_toma_menos_de_un_segundo()
    {
        Skip.If(baseDatos.MotivoOmision is not null, baseDatos.MotivoOmision);
        var escenario = new EscenarioCatalogo();

        await using (var ambito = baseDatos.Servicios!.CreateAsyncScope())
        {
            await ambito.ServiceProvider.GetRequiredService<ICargaMaestros>().AplicarAsync(escenario.Paquete(), "Pruebas");
            var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosPos>();
            await escenario.ResolverIdsAsync(contexto);
            contexto.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

            var cronometroCarga = Stopwatch.StartNew();
            // Los Id salen de la secuencia HiLo de EF: se reserva un valor por artículo y cada valor cubre un bloque de Id que EF ya no entrega.
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @primero sql_variant;
                EXEC sys.sp_sequence_get_range @sequence_name = N'EntityFrameworkHiLoSequence', @range_size = {CantidadArticulos}, @range_first_value = @primero OUTPUT;
                DECLARE @incremento int = (SELECT CAST(increment AS int) FROM sys.sequences WHERE name = 'EntityFrameworkHiLoSequence');

                WITH numeros AS (
                    SELECT TOP ({CantidadArticulos}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
                    FROM sys.all_objects a CROSS JOIN sys.all_objects b)
                INSERT INTO Articulos (Id, Codigo, Descripcion, DepartamentoId, UnidadMedidaId, ImpuestoId, Tipo, MostrarEnCatalogo, VentaEnPos, Activo, EsServicio)
                SELECT CAST(@primero AS int) + (n - 1) * @incremento, CONCAT('R', {escenario.Sufijo}, '-', n), CONCAT('Tornillo acero inoxidable ', n, ' mm'),
                       {escenario.DepartamentoFerreteria}, {escenario.UnidadUnidad}, {escenario.ImpuestoItbis18}, 0, 0, 1, 1, 0
                FROM numeros;

                INSERT INTO CodigosArticulo (ArticuloId, Codigo, Tipo)
                SELECT Id, CONCAT('77', {escenario.Sufijo}, RIGHT(CONCAT('0000000', SUBSTRING(Codigo, 9, 10)), 7)), 0
                FROM Articulos WHERE Codigo LIKE CONCAT('R', {escenario.Sufijo}, '-%');

                INSERT INTO PreciosArticulo (Id, ArticuloId, Lista, Precio, VigenteDesde, RegistradoEn, Origen)
                SELECT Id + 1, Id, 0, 25.00, '2020-01-01T00:00:00+00:00', SYSDATETIMEOFFSET(), 'Rendimiento'
                FROM Articulos WHERE Codigo LIKE CONCAT('R', {escenario.Sufijo}, '-%');
                """);
            salida.WriteLine($"Carga de {CantidadArticulos} artículos: {cronometroCarga.ElapsedMilliseconds} ms");
        }

        await using var ambitoConsulta = baseDatos.Servicios!.CreateAsyncScope();
        var consulta = ambitoConsulta.ServiceProvider.GetRequiredService<IConsultaArticulos>();

        // Calentamiento (compilación de consultas y planes).
        await consulta.BuscarAsync("tornillo 49999");
        await consulta.BuscarPorCodigoAsync($"R{escenario.Sufijo}-49999");

        const int repeticiones = 10;
        var cronometro = Stopwatch.StartNew();
        for (var i = 0; i < repeticiones; i++)
        {
            // "tornillo 40009", "tornillo 40019"…: cada búsqueda coincide con un artículo existente.
            var resultados = await consulta.BuscarAsync($"tornillo {4_000 + i}9");
            Assert.NotEmpty(resultados);
        }
        var promedioTexto = cronometro.Elapsed.TotalMilliseconds / repeticiones;

        cronometro.Restart();
        for (var i = 0; i < repeticiones; i++)
            Assert.NotNull(await consulta.BuscarPorCodigoAsync($"R{escenario.Sufijo}-{30_000 + i}"));
        var promedioCodigo = cronometro.Elapsed.TotalMilliseconds / repeticiones;

        salida.WriteLine($"Búsqueda por texto: {promedioTexto:F1} ms promedio; por código: {promedioCodigo:F1} ms promedio");
        Assert.True(promedioTexto < 1000, $"Búsqueda por texto: {promedioTexto:F1} ms");
        Assert.True(promedioCodigo < 1000, $"Búsqueda por código: {promedioCodigo:F1} ms");
    }
}
