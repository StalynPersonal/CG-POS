using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CgPos.Central.Aplicacion.Organizacion;
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
        Assert.Equal("999000004", organizacion.Empresa.Rnc);
        // La terminal que baja es solo la suya; de las sucursales bajan todas, porque los almacenes de cualquiera de
        // ellas se pueden elegir al dejar mercancía para retirar (RF-138, RF-140).
        Assert.Equal(("01", "01"), (Assert.Single(organizacion.Cajas!).SucursalCodigo, Assert.Single(organizacion.Cajas!).Codigo));
        Assert.Contains(organizacion.Sucursales!, s => s.Codigo == "01");
        Assert.Contains(organizacion.Parametros!, p => p.Clave == "General.MonedaLocal");
        Assert.DoesNotContain(organizacion.Parametros!, p => p.Clave.StartsWith("Central.", StringComparison.Ordinal));

        // La clave baja solo como hash, con el formato que verifica la caja; el rol y las cajas, por código.
        var cajero = Assert.Single(organizacion.Usuarios!, u => u.Codigo == "C001");
        Assert.Null(cajero.Clave);
        Assert.True(new HashCredenciales().VerificarClave("Cajero.2026", cajero.ClaveHash!));
        Assert.Equal("CAJERO", cajero.RolCodigo);
        Assert.Contains(cajero.Cajas!, c => c == new CgPos.Contratos.CargaInicial.CajaReferencia("01", "01"));

        Assert.NotEmpty(paquete.Maestros!.Articulos!);
        Assert.NotEmpty(paquete.Maestros.SecuenciasEcf!);
        Assert.All(paquete.Maestros.SecuenciasEcf!, s => Assert.Equal(("01", "01"), (s.SucursalCodigo, s.CajaCodigo)));
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
        var secuencia = new SecuenciaEcfCarga("01", "02", TipoComprobante.FacturaConsumo, desde, desde + 999, new DateOnly(2027, 12, 31));
        await PublicarAsync(new PaqueteMaestros(SecuenciasEcf: [secuencia]));
        var clave = $"Pruebas.SoloCajaDos{Guid.NewGuid():N}";
        await central.UsarContextoAsync(async contexto =>
        {
            contexto.Parametros.Add(Parametro.Crear(clave, "solo para la caja 02", cajaId: CentralEnPruebas.CajaDos));
            return await contexto.SaveChangesAsync();
        });

        Assert.True((await BajarAsync(cliente, tokenUno, marcaUno)).SinCambios);

        var paraDos = await BajarAsync(cliente, tokenDos, marcaDos);
        Assert.Equal(desde, Assert.Single(paraDos.Maestros!.SecuenciasEcf!).Desde);
        var parametro = Assert.Single(paraDos.Organizacion!.Parametros!);
        Assert.Equal((clave, "01", "02"), (parametro.Clave, parametro.SucursalCodigo, parametro.CajaCodigo));
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
            articulo with { Codigo = "NUEVO-SIN-DEPARTAMENTO", DepartamentoCodigo = 999_999, CodigosBarras = null, CodigosProveedor = null },
            articulo with { Codigo = "NUEVO-PRECIO", PrecioDetalle = -1, CodigosBarras = null, CodigosProveedor = null },
            articulo with { Codigo = "NUEVO-BARRAS", CodigosProveedor = null },
            articulo with { Codigo = articulo.CodigosBarras![0], CodigosBarras = null, CodigosProveedor = null },
        ])));

        Assert.Contains(error.Errores, e => e.Contains("precio detalle", StringComparison.Ordinal));
        Assert.Contains(error.Errores, e => e.Contains($"El código '{articulo.CodigosBarras![0]}' ya lo usa el artículo", StringComparison.Ordinal));

        // El código interno de un artículo tampoco puede ser el código de barras de otro: la caja busca por cualquiera de los dos.
        Assert.Contains(error.Errores, e => e.Contains($"no puede estar también en '{articulo.CodigosBarras![0]}'", StringComparison.Ordinal));
        Assert.DoesNotContain(error.Errores, e => e.Contains($"El código '{articulo.Codigo}' ya lo usa", StringComparison.Ordinal));

        // Una referencia que no existe se informa por su código.
        var sinDepartamento = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() => PublicarAsync(new PaqueteMaestros(Articulos:
            [articulo with { Codigo = "NUEVO-SIN-DEPARTAMENTO", DepartamentoCodigo = 999_999, CodigosBarras = null, CodigosProveedor = null }])));
        Assert.Contains(sinDepartamento.Errores, e => e.Contains("No existe el departamento con código '999999'", StringComparison.Ordinal));

        Assert.False(await central.UsarContextoAsync(contexto => contexto.Articulos.AnyAsync(a => a.Codigo == "NUEVO-SIN-DEPARTAMENTO" || a.Codigo == "NUEVO-BARRAS")));
    }

    [SkippableFact]
    public async Task Departamentos_categorias_marcas_unidades_e_impuestos_viven_en_sus_tablas_y_bajan_igual_a_las_cajas()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var departamento = new DepartamentoCarga(Codigos.Siguiente(), "Departamento en tabla");
        var unidad = new UnidadMedidaCarga(Codigos.Siguiente(), $"U{sufijo}", "Unidad en tabla", PermiteDecimales: true, Decimales: 2);
        await PublicarAsync(new PaqueteMaestros(Departamentos: [departamento], UnidadesMedida: [unidad]));

        // Quedan en su tabla, con quién los publicó.
        await central.UsarContextoAsync(async contexto =>
        {
            Assert.True(await contexto.Departamentos.AnyAsync(d => d.Codigo == departamento.Codigo));
            Assert.Equal("Pruebas", await contexto.UnidadesMedida.Where(u => u.Codigo == unidad.Codigo)
                .Select(u => EF.Property<string>(u, "ModificadoPor")).SingleAsync());
            return 0;
        });

        // Publicar lo mismo no cambia nada; cambiar el nombre sí, y baja a la caja con el formato de siempre.
        Assert.Equal(0, (await PublicarAsync(new PaqueteMaestros(Departamentos: [departamento]))).Publicados);
        await PublicarAsync(new PaqueteMaestros(Departamentos: [departamento with { Nombre = "Departamento renombrado" }]));
        var bajada = await BajarAsync(cliente, tokenCaja, marca);
        Assert.Equal("Departamento renombrado", Assert.Single(bajada.Maestros!.Departamentos!, d => d.Codigo == departamento.Codigo).Nombre);
        Assert.Contains(bajada.Maestros.UnidadesMedida!, u => u.Codigo == unidad.Codigo && u.Abreviatura == $"U{sufijo}" && u.Decimales == 2);
    }

    [SkippableFact]
    public async Task Categorias_y_marcas_se_publican_y_el_articulo_solo_admite_una_categoria_de_su_departamento()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var inicial = await BajarAsync(cliente, tokenCaja, 0);
        var articulo = inicial.Maestros!.Articulos!.First();
        var otroDepartamento = inicial.Maestros.Departamentos!.First(d => d.Codigo != articulo.DepartamentoCodigo);

        var suya = new CategoriaCarga(Codigos.Siguiente(), "Categoría del departamento", articulo.DepartamentoCodigo);
        var ajena = new CategoriaCarga(Codigos.Siguiente(), "Categoría de otro departamento", otroDepartamento.Codigo);
        var marca = new MarcaCarga(Codigos.Siguiente(), "Marca de prueba");

        var sinDepartamento = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() =>
            PublicarAsync(new PaqueteMaestros(Categorias: [suya with { Codigo = Codigos.Siguiente(), DepartamentoCodigo = 999_999 }])));
        Assert.Contains(sinDepartamento.Errores, e => e.Contains("No existe el departamento", StringComparison.Ordinal));

        await PublicarAsync(new PaqueteMaestros(Categorias: [suya, ajena], Marcas: [marca]));

        var conAjena = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() =>
            PublicarAsync(new PaqueteMaestros(Articulos: [articulo with { CategoriaCodigo = ajena.Codigo }])));
        Assert.Contains(conAjena.Errores, e => e.Contains("no es de su departamento", StringComparison.Ordinal));

        await PublicarAsync(new PaqueteMaestros(Articulos: [articulo with { CategoriaCodigo = suya.Codigo, MarcaCodigo = marca.Codigo }]));

        // Todo baja a la caja: los catálogos nuevos y el artículo con su clasificación.
        var bajada = await BajarAsync(cliente, tokenCaja, inicial.Hasta);
        Assert.Contains(bajada.Maestros!.Categorias!, c => c.Codigo == suya.Codigo);
        Assert.Contains(bajada.Maestros.Marcas!, m => m.Codigo == marca.Codigo);
        var clasificado = Assert.Single(bajada.Maestros.Articulos!, a => a.Codigo == articulo.Codigo);
        Assert.Equal(((int?)suya.Codigo, (int?)marca.Codigo), (clasificado.CategoriaCodigo, clasificado.MarcaCodigo));

        // Mover la categoría a otro departamento dejaría al artículo inconsistente.
        var mover = await Assert.ThrowsAsync<PublicacionInvalidaExcepcion>(() =>
            PublicarAsync(new PaqueteMaestros(Categorias: [suya with { DepartamentoCodigo = otroDepartamento.Codigo }])));
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

        var cedula = CedulaValida();
        var nueva = Inscripcion(cedula);
        var repetida = Inscripcion("00113918205");
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, nueva.Mensaje));
        Assert.Equal(EstadoRecepcion.Recibido, await EnviarAsync(cliente, token, repetida.Mensaje));

        var bajada = await BajarAsync(cliente, token, marca);
        Assert.Equal(cedula, Assert.Single(bajada.Maestros!.MiembrosFidelidad!).Cedula);

        var conflicto = await central.UsarContextoAsync(contexto => contexto.ConflictosSincronizacion.SingleAsync(c => c.MensajeId == repetida.Mensaje.Id));
        Assert.Equal(TipoConflictoSincronizacion.MiembroDuplicado, conflicto.Tipo);
    }

    [SkippableFact]
    public async Task Resincronizar_una_caja_le_sirve_los_maestros_desde_cero_una_sola_vez()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);

        // La caja está al día: pedir desde su marca no trae nada.
        var alDia = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;
        Assert.True((await BajarAsync(cliente, tokenCaja, alDia)).SinCambios);

        using var pedido = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post,
            $"/api/monitor/cajas/{CentralEnPruebas.CajaUno}/resincronizar", admin));
        Assert.Equal(HttpStatusCode.OK, pedido.StatusCode);

        // Aunque la caja siga pidiendo desde su marca, el Central le sirve desde cero: es lo que la hace bajarlo todo.
        var completa = await BajarAsync(cliente, tokenCaja, alDia);
        Assert.Equal(0, completa.Desde);
        Assert.NotNull(completa.Maestros?.Articulos);

        // Y una vez que la caja arrancó, no se le vuelve a forzar: si no, se quedaría bajando lo mismo para siempre.
        var siguiente = await BajarAsync(cliente, tokenCaja, completa.Hasta);
        Assert.Equal(completa.Hasta, siguiente.Desde);

        // Una caja que no existe no se puede resincronizar.
        using var inexistente = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/monitor/cajas/999999/resincronizar", admin));
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [SkippableFact]
    public async Task La_bajada_viene_por_paginas_y_no_se_salta_ningun_maestro()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var tokenCaja = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var marca = (await BajarAsync(cliente, tokenCaja, 0)).Hasta;

        // Siete departamentos nuevos con páginas de dos: el aprovisionamiento de verdad son cientos de miles de filas y
        // en un solo viaje no llega, así que el Central corta el rango y la caja pide el resto.
        var departamentos = Enumerable.Range(0, 7).Select(_ => new DepartamentoCarga(Codigos.Siguiente(), "Departamento por páginas")).ToList();
        await PublicarAsync(new PaqueteMaestros(Departamentos: departamentos));

        var anterior = await central.CambiarParametroAsync(ClavesParametrosCentral.FilasPorPaginaBajada, "2");
        try
        {
            var recibidos = new List<int>();
            var paginas = 0;
            var desde = marca;

            // La primera página trae además cuántas filas hay que bajar en total, que es el denominador del conteo en pantalla.
            var totales = (await BajarAsync(cliente, tokenCaja, marca, conTotales: true)).Totales;
            Assert.NotNull(totales);
            Assert.Equal(departamentos.Count, Assert.Single(totales!, c => c.Tipo == TipoMaestro.Departamento).Cantidad);

            while (true)
            {
                var pagina = await BajarAsync(cliente, tokenCaja, desde);
                Assert.Null(pagina.Totales);
                paginas++;
                recibidos.AddRange((pagina.Maestros?.Departamentos ?? []).Select(d => d.Codigo));

                // Cada página avanza: si no, la caja se quedaría pidiendo lo mismo para siempre.
                Assert.True(pagina.Hasta > desde || pagina.Completo, $"La página {paginas} no avanzó de la versión {desde}.");
                desde = pagina.Hasta;

                if (pagina.Completo)
                    break;

                Assert.True(paginas < 50, "La bajada por páginas no terminó.");
            }

            Assert.True(paginas > 1, "Con siete departamentos y páginas de dos tenía que venir en varias partes.");
            Assert.Equal(departamentos.Select(d => d.Codigo).Order(), recibidos.Where(c => departamentos.Any(d => d.Codigo == c)).Order());

            // Y al terminar no queda nada pendiente: pedir otra vez no trae nada.
            Assert.True((await BajarAsync(cliente, tokenCaja, desde)).Completo);
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.FilasPorPaginaBajada, anterior);
        }
    }

    [SkippableFact]
    public async Task El_cliente_de_la_caja_descarga_los_maestros_del_central_real()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var http = central.CrearCliente();
        var secreto = await CentralEnPruebas.EmitirCredencialAsync(http, CentralEnPruebas.CajaUno);

        var resultado = await new ClienteCentralHttp(http, new ConfiguracionCajaEnMemoria(secreto, http.BaseAddress!.ToString()), TimeProvider.System).DescargarMaestrosAsync(0);

        Assert.True(resultado.CentralRespondio, resultado.Error);
        Assert.NotNull(resultado.Paquete!.Organizacion);
        Assert.NotEmpty(resultado.Paquete.Maestros!.Articulos!);
    }

    private static async Task<PaqueteBajadaMaestros> BajarAsync(HttpClient cliente, string token, long desde, bool conTotales = false)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get,
            $"/api/sincronizacion/maestros?desde={desde}{(conTotales ? "&totales=true" : string.Empty)}", token));
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

    private static (string Cedula, MensajeSincronizacion Mensaje) Inscripcion(string cedula)
    {
        var contenido = JsonSerializer.Serialize(new DocumentoInscripcionFidelidad(cedula, "Miembro de Prueba", null, null, "Cajero Desarrollo", DateTimeOffset.UtcNow),
            OpcionesJson.Predeterminadas);
        return (cedula, new MensajeSincronizacion(Guid.CreateVersion7(), TiposMensaje.InscripcionFidelidad, cedula, contenido, HashSincronizacion.Calcular(contenido),
            "01", "01", DateTimeOffset.UtcNow));
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
