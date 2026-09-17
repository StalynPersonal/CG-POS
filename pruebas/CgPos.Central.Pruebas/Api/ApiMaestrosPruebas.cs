using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Fidelidad;
using CgPos.Contratos.Serializacion;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Sincronizacion;
using CgPos.Pos.Infraestructura.Seguridad;
using CgPos.Pos.Infraestructura.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiMaestrosPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task Aprovisionamiento_baja_organizacion_usuarios_con_hash_y_maestros_de_la_caja()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var paquete = await BajarAsync(cliente, token, 0);

        Assert.True(paquete.Hasta > 0);
        var organizacion = paquete.Organizacion!;
        Assert.Equal("999000001", organizacion.Empresa.Rnc);
        // Otras pruebas de la colección crean sucursales y cajas: basta con que estén las de desarrollo.
        Assert.Contains(organizacion.Cajas!, c => c.Id == CentralEnPruebas.CajaUno);
        Assert.Contains(organizacion.Cajas!, c => c.Id == CentralEnPruebas.CajaDos);
        Assert.Contains(organizacion.Parametros!, p => p.Clave == "General.MonedaLocal");
        Assert.DoesNotContain(organizacion.Parametros!, p => p.Clave.StartsWith("Central.", StringComparison.Ordinal));

        // La clave baja solo como hash, con el formato que verifica la caja.
        var cajero = Assert.Single(organizacion.Usuarios!, u => u.Codigo == "C001");
        Assert.Null(cajero.Clave);
        Assert.True(new HashCredenciales().VerificarClave("Cajero.2026", cajero.ClaveHash!));

        Assert.NotEmpty(paquete.Maestros!.Articulos!);
        Assert.NotEmpty(paquete.Maestros.SecuenciasEcf!);
        Assert.All(paquete.Maestros.SecuenciasEcf!, s => Assert.Equal(CentralEnPruebas.CajaUno, s.CajaId));
    }

    [SkippableFact]
    public async Task Bajada_incremental_entrega_solo_lo_cambiado_y_publicar_lo_mismo_no_genera_version()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        var inicial = await BajarAsync(cliente, token, 0);
        Assert.True((await BajarAsync(cliente, token, inicial.Hasta)).SinCambios);

        var articulo = inicial.Maestros!.Articulos!.First();
        var modificado = articulo with { Descripcion = articulo.Descripcion + " *" };
        Assert.Equal(new ResultadoPublicacion(1, 0), await PublicarAsync(new PaqueteMaestros(Articulos: [modificado])));

        var cambio = await BajarAsync(cliente, token, inicial.Hasta);
        Assert.Null(cambio.Organizacion);
        Assert.Equal(modificado.Descripcion, Assert.Single(cambio.Maestros!.Articulos!).Descripcion);

        Assert.Equal(new ResultadoPublicacion(0, 1), await PublicarAsync(new PaqueteMaestros(Articulos: [modificado])));
        Assert.True((await BajarAsync(cliente, token, cambio.Hasta)).SinCambios);

        var anterior = await central.CambiarParametroAsync("Tickets.MensajePie", "Mensaje nuevo del Central");
        try
        {
            var parametro = await BajarAsync(cliente, token, cambio.Hasta);
            Assert.Null(parametro.Maestros);
            Assert.Equal("Mensaje nuevo del Central", Assert.Single(parametro.Organizacion!.Parametros!).Valor);
        }
        finally
        {
            await central.CambiarParametroAsync("Tickets.MensajePie", anterior);
        }
    }

    [SkippableFact]
    public async Task A_cada_caja_solo_bajan_sus_parametros_y_sus_rangos_de_e_cf()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenUno = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var tokenDos = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaDos);
        var marcaUno = (await BajarAsync(cliente, tokenUno, 0)).Hasta;
        var marcaDos = (await BajarAsync(cliente, tokenDos, 0)).Hasta;

        // Los rangos del mismo tipo no se solapan en la empresa: uno alto y aleatorio no choca con los de desarrollo ni con otras pruebas.
        var desde = Random.Shared.NextInt64(1_000_000, 9_000_000_000);
        var secuencia = new SecuenciaEcfCarga(Guid.CreateVersion7(), CentralEnPruebas.CajaDos, TipoComprobante.FacturaConsumo, desde, desde + 999, new DateOnly(2027, 12, 31));
        await PublicarAsync(new PaqueteMaestros(SecuenciasEcf: [secuencia]));
        var clave = $"Pruebas.SoloCajaDos{Guid.NewGuid():N}";
        await central.UsarContextoAsync(async contexto =>
        {
            contexto.Parametros.Add(Parametro.Crear(clave, "solo para la caja 02", cajaId: CentralEnPruebas.CajaDos));
            return await contexto.SaveChangesAsync();
        });

        Assert.True((await BajarAsync(cliente, tokenUno, marcaUno)).SinCambios);

        var paraDos = await BajarAsync(cliente, tokenDos, marcaDos);
        Assert.Equal(secuencia.Id, Assert.Single(paraDos.Maestros!.SecuenciasEcf!).Id);
        Assert.Equal(clave, Assert.Single(paraDos.Organizacion!.Parametros!).Clave);
    }

    [SkippableFact]
    public async Task Publicacion_con_referencias_codigos_o_datos_invalidos_no_guarda_nada()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var inicial = await BajarAsync(cliente, await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno), 0);
        var articulo = inicial.Maestros!.Articulos!.First(a => a.CodigosBarras is { Count: > 0 });

        var error = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() => PublicarAsync(new PaqueteMaestros(Articulos:
        [
            articulo with { Id = Guid.CreateVersion7(), CodigosBarras = null, CodigosProveedor = null },
            articulo with { Id = Guid.CreateVersion7(), Codigo = "NUEVO-SIN-DEPARTAMENTO", DepartamentoId = Guid.CreateVersion7(), CodigosBarras = null, CodigosProveedor = null },
            articulo with { Id = Guid.CreateVersion7(), Codigo = "NUEVO-PRECIO", PrecioDetalle = -1, CodigosBarras = null, CodigosProveedor = null },
            articulo with { Id = Guid.CreateVersion7(), Codigo = "NUEVO-BARRAS", CodigosProveedor = null },
            articulo with { Id = Guid.CreateVersion7(), Codigo = articulo.CodigosBarras![0], CodigosBarras = null, CodigosProveedor = null },
        ])));

        Assert.Contains(error.Errores, e => e.Contains("ya existe con otro Id", StringComparison.Ordinal));
        Assert.Contains(error.Errores, e => e.Contains("departamento inexistente", StringComparison.Ordinal));
        Assert.Contains(error.Errores, e => e.Contains("precio detalle", StringComparison.Ordinal));
        Assert.Contains(error.Errores, e => e.Contains($"El código '{articulo.CodigosBarras![0]}' ya lo usa el artículo", StringComparison.Ordinal));

        // El código interno de un artículo tampoco puede ser el código de barras de otro: la caja busca por cualquiera de los dos.
        Assert.Contains(error.Errores, e => e.Contains($"no puede estar también en '{articulo.CodigosBarras![0]}'", StringComparison.Ordinal));
        Assert.DoesNotContain(error.Errores, e => e.Contains($"El código '{articulo.Codigo}' ya lo usa", StringComparison.Ordinal));
        Assert.False(await central.UsarContextoAsync(contexto => contexto.MaestrosCentral.AnyAsync(m => m.Codigo == "NUEVO-SIN-DEPARTAMENTO" || m.Codigo == "NUEVO-BARRAS")));
    }

    [SkippableFact]
    public async Task Departamentos_categorias_marcas_unidades_e_impuestos_viven_en_sus_tablas_y_bajan_igual_a_las_cajas()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var departamento = new DepartamentoCarga(Guid.CreateVersion7(), $"D{sufijo}", "Departamento en tabla");
        var unidad = new UnidadMedidaCarga(Guid.CreateVersion7(), $"U{sufijo}", "Unidad en tabla", PermiteDecimales: true, Decimales: 2);
        await PublicarAsync(new PaqueteMaestros(Departamentos: [departamento], UnidadesMedida: [unidad]));

        // Quedan en su tabla, con quién los publicó, y no en la tabla JSON.
        await central.UsarContextoAsync(async contexto =>
        {
            Assert.True(await contexto.Departamentos.AnyAsync(d => d.Id == departamento.Id));
            Assert.Equal("Pruebas", await contexto.UnidadesMedida.Where(u => u.Id == unidad.Id)
                .Select(u => EF.Property<string>(u, CgPos.Central.Infraestructura.Maestros.TablaMaestro.ColumnaModificadoPor)).SingleAsync());
            Assert.False(await contexto.MaestrosCentral.AnyAsync(m => m.Id == departamento.Id || m.Id == unidad.Id));
            return 0;
        });

        // Publicar lo mismo no cambia nada; cambiar el nombre sí, y baja a la caja con el formato de siempre.
        Assert.Equal(0, (await PublicarAsync(new PaqueteMaestros(Departamentos: [departamento]))).Publicados);
        await PublicarAsync(new PaqueteMaestros(Departamentos: [departamento with { Nombre = "Departamento renombrado" }]));
        var bajada = await BajarAsync(cliente, tokenCaja, marca);
        Assert.Equal("Departamento renombrado", Assert.Single(bajada.Maestros!.Departamentos!, d => d.Id == departamento.Id).Nombre);
        Assert.Contains(bajada.Maestros.UnidadesMedida!, u => u.Id == unidad.Id && u.Decimales == 2);

        // El código no cambia: la caja identifica el registro por él.
        var cambioCodigo = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() =>
            PublicarAsync(new PaqueteMaestros(Departamentos: [departamento with { Codigo = $"X{sufijo}" }])));
        Assert.Contains(cambioCodigo.Errores, e => e.Contains("No se puede cambiar el código del departamento", StringComparison.Ordinal));
    }

    [SkippableFact]
    public async Task Los_maestros_que_estaban_en_json_pasan_a_su_tabla_conservando_quien_los_cambio()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var marca = new MarcaCarga(Guid.CreateVersion7(), $"J{sufijo}", "Marca que estaba en JSON");
        var modificadoEn = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

        await central.UsarContextoAsync(async contexto =>
        {
            contexto.MaestrosCentral.Add(MaestroCentral.Publicar(TipoMaestro.Marca, marca.Id, marca.Codigo, null,
                System.Text.Json.JsonSerializer.Serialize(marca, OpcionesJson.Predeterminadas), modificadoEn, "Usuario anterior"));
            return await contexto.SaveChangesAsync();
        });

        await central.UsarContextoAsync(async contexto =>
        {
            await CgPos.Central.Infraestructura.Maestros.MigracionMaestrosATablas.EjecutarAsync(contexto, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                CancellationToken.None);
            return 0;
        });

        await central.UsarContextoAsync(async contexto =>
        {
            Assert.False(await contexto.MaestrosCentral.AnyAsync(m => m.Id == marca.Id));
            var enTabla = await contexto.Marcas.Where(m => m.Id == marca.Id)
                .Select(m => new
                {
                    m.Nombre,
                    Por = EF.Property<string>(m, CgPos.Central.Infraestructura.Maestros.TablaMaestro.ColumnaModificadoPor),
                    En = EF.Property<DateTimeOffset>(m, CgPos.Central.Infraestructura.Maestros.TablaMaestro.ColumnaModificadoEn),
                })
                .SingleAsync();
            Assert.Equal((marca.Nombre, "Usuario anterior", modificadoEn), (enTabla.Nombre, enTabla.Por, enTabla.En));
            return 0;
        });
    }

    [SkippableFact]
    public async Task Categorias_y_marcas_se_publican_y_el_articulo_solo_admite_una_categoria_de_su_departamento()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var inicial = await BajarAsync(cliente, tokenCaja, 0);
        var articulo = inicial.Maestros!.Articulos!.First();
        var otroDepartamento = inicial.Maestros.Departamentos!.First(d => d.Id != articulo.DepartamentoId);
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var suya = new CategoriaCarga(Guid.CreateVersion7(), $"C{sufijo}", "Categoría del departamento", articulo.DepartamentoId);
        var ajena = new CategoriaCarga(Guid.CreateVersion7(), $"X{sufijo}", "Categoría de otro departamento", otroDepartamento.Id);
        var marca = new MarcaCarga(Guid.CreateVersion7(), $"M{sufijo}", "Marca de prueba");

        var sinDepartamento = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() =>
            PublicarAsync(new PaqueteMaestros(Categorias: [suya with { Id = Guid.CreateVersion7(), Codigo = $"S{sufijo}", DepartamentoId = Guid.CreateVersion7() }])));
        Assert.Contains(sinDepartamento.Errores, e => e.Contains("departamento inexistente", StringComparison.Ordinal));

        await PublicarAsync(new PaqueteMaestros(Categorias: [suya, ajena], Marcas: [marca]));

        var conAjena = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() => PublicarAsync(new PaqueteMaestros(Articulos: [articulo with { CategoriaId = ajena.Id }])));
        Assert.Contains(conAjena.Errores, e => e.Contains("no es de su departamento", StringComparison.Ordinal));

        await PublicarAsync(new PaqueteMaestros(Articulos: [articulo with { CategoriaId = suya.Id, MarcaId = marca.Id }]));

        // Todo baja a la caja: los catálogos nuevos y el artículo con su clasificación.
        var bajada = await BajarAsync(cliente, tokenCaja, inicial.Hasta);
        Assert.Contains(bajada.Maestros!.Categorias!, c => c.Id == suya.Id);
        Assert.Contains(bajada.Maestros.Marcas!, m => m.Id == marca.Id);
        var clasificado = Assert.Single(bajada.Maestros.Articulos!, a => a.Id == articulo.Id);
        Assert.Equal((suya.Id, marca.Id), (clasificado.CategoriaId, clasificado.MarcaId));

        // Mover la categoría a otro departamento dejaría al artículo inconsistente.
        var mover = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() => PublicarAsync(new PaqueteMaestros(Categorias: [suya with { DepartamentoId = otroDepartamento.Id }])));
        Assert.Contains(mover.Errores, e => e.Contains("antes de mover la categoría", StringComparison.Ordinal));

        await PublicarAsync(new PaqueteMaestros(Articulos: [articulo]));
    }

    [SkippableFact]
    public async Task Inscripcion_de_fidelidad_en_caja_se_publica_y_una_cedula_ya_inscrita_queda_como_conflicto()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, token, 0)).Hasta;

        var nueva = Inscripcion(CedulaValida());
        var repetida = Inscripcion("00113918205");
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, nueva.Mensaje));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, repetida.Mensaje));

        var bajada = await BajarAsync(cliente, token, marca);
        Assert.Equal(nueva.MiembroId, Assert.Single(bajada.Maestros!.MiembrosFidelidad!).Id);

        var conflicto = await central.UsarContextoAsync(contexto => contexto.ConflictosSincronizacion.SingleAsync(c => c.MensajeId == repetida.Mensaje.Id));
        Assert.Equal(TipoConflictoSincronizacion.MiembroDuplicado, conflicto.Tipo);
    }

    [SkippableFact]
    public async Task El_cliente_de_la_caja_descarga_los_maestros_del_central_real()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var http = central.CrearCliente();
        var secreto = await CentralEnPruebas.EmitirCredencialAsync(http, CentralEnPruebas.CajaUno);

        var resultado = await new ClienteCentralHttp(http, CentralEnPruebas.CajaUno, secreto, TimeProvider.System).DescargarMaestrosAsync(0);

        Assert.True(resultado.CentralRespondio, resultado.Error);
        Assert.NotNull(resultado.Paquete!.Organizacion);
        Assert.NotEmpty(resultado.Paquete.Maestros!.Articulos!);
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, $"/api/sincronizacion/maestros?desde={desde}", token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<PaqueteBajadaMaestros>(OpcionesJson.Predeterminadas))!;
    }

    private async Task<ResultadoPublicacion> PublicarAsync(PaqueteMaestros paquete)
    {
        await using var ambito = central.Fabrica!.Services.CreateAsyncScope();
        return await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarAsync(paquete, "Pruebas");
    }

    private static async Task<EstadoRecepcion?> EnviarAsync(HttpClient cliente, string token, MensajeSincronizacion mensaje)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/sincronizacion/mensajes", token, mensaje));
        return (await respuesta.Content.ReadFromJsonAsync<RespuestaRecepcionCentral>(OpcionesJson.Predeterminadas))?.Estado;
    }

    private static (Guid MiembroId, MensajeSincronizacion Mensaje) Inscripcion(string cedula)
    {
        var miembroId = Guid.CreateVersion7();
        var contenido = JsonSerializer.Serialize(new DocumentoInscripcionFidelidad(miembroId, cedula, "Miembro de Prueba", null, null, CentralEnPruebas.CajaUno,
            CentralEnPruebas.Sucursal, "Cajero Desarrollo", DateTimeOffset.UtcNow), OpcionesJson.Predeterminadas);
        return (miembroId, new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.InscripcionFidelidad, miembroId, contenido, HashSincronizacion.Calcular(contenido),
            CentralEnPruebas.CajaUno, DateTimeOffset.UtcNow));
    }

    /// <summary>Una cédula nueva que pasa la validación del dominio (dígito verificador incluido).</summary>
    private static string CedulaValida()
    {
        for (var intento = 0; intento < 1000; intento++)
        {
            var base10 = Random.Shared.NextInt64(100_000_000, 9_999_999_999).ToString("D10");
            for (var digito = 0; digito <= 9; digito++)
            {
                try
                {
                    return MiembroFidelidad.ValidarCedula(base10 + digito);
                }
                catch (ArgumentException)
                {
                }
            }
        }

        throw new InvalidOperationException("No se pudo generar una cédula válida.");
    }
}
