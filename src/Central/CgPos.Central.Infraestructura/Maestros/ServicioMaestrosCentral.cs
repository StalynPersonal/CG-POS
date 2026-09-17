using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Maestros;

internal sealed class ServicioMaestrosCentral(ContextoDatosCentral contexto, IPublicadorMaestros publicador, IAuditoriaCentral auditoria, TimeProvider reloj)
    : IServicioMaestrosCentral
{
    public async Task<IReadOnlyList<DatosMaestroCentral<T>>> ListarAsync<T>(TipoMaestro tipo, CancellationToken cancelacion = default) =>
        (await contexto.MaestrosAsync(tipo, cancelacion))
            .OrderBy(m => m.Codigo, StringComparer.Ordinal)
            .ThenByDescending(m => m.ModificadoEn)
            .Select(Datos<T>)
            .ToList();

    public async Task<PaginaMaestros<T>> BuscarAsync<T>(TipoMaestro tipo, string? texto, int pagina, int tamano, CancellationToken cancelacion = default)
    {
        tamano = Math.Clamp(tamano, 1, IServicioMaestrosCentral.TamanoMaximoPagina);
        pagina = Math.Max(pagina, 0);

        // Los catálogos con tabla propia son cortos: se filtran en memoria por código y nombre.
        if (TablasMaestros.TieneTabla(tipo))
        {
            var filtro = MaestroCentral.NormalizarBusqueda(texto);
            var todos = (await ListarAsync<T>(tipo, cancelacion))
                .Where(m => filtro is null || MaestroCentral.NormalizarBusqueda(System.Text.Json.JsonSerializer.Serialize(m.Dato, CgPos.Contratos.Serializacion.OpcionesJson.Predeterminadas))!.Contains(filtro, StringComparison.Ordinal))
                .ToList();
            return new PaginaMaestros<T>(todos.Skip(pagina * tamano).Take(tamano).ToList(), todos.Count);
        }

        var consulta = contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == tipo);
        if (MaestroCentral.NormalizarBusqueda(texto) is { } buscado)
        {
            var patron = Patron(buscado);
            consulta = consulta.Where(m => m.TextoBusqueda != null && EF.Functions.Like(m.TextoBusqueda, patron));
        }

        var total = await consulta.CountAsync(cancelacion);
        var filas = await consulta.OrderBy(m => m.Codigo).ThenBy(m => m.Id).Skip(pagina * tamano).Take(tamano).ToListAsync(cancelacion);
        return new PaginaMaestros<T>(filas.Select(Datos<T>).ToList(), total);
    }

    public async Task<ResultadoAdministracion> PublicarAsync(PaqueteMaestros paquete, Guid id, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (id == Guid.Empty)
            return ResultadoAdministracion.Error("El registro necesita un Id.");

        try
        {
            await publicador.PublicarAsync(paquete, actor.Nombre, cancelacion);
            return ResultadoAdministracion.Correcto(id);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }
    }

    public async Task<ResultadoAdministracion> GuardarArticuloAsync(Guid articuloId, ArticuloCarga articulo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (articulo.Id != articuloId)
            return ResultadoAdministracion.Error("El Id del registro no coincide con el de la ruta.");

        var anterior = await LeerAsync<ArticuloCarga>(TipoMaestro.Articulo, articuloId, cancelacion);
        var publicar = anterior is null
            ? articulo with { PreciosVigentesDesde = articulo.PreciosVigentesDesde ?? reloj.GetUtcNow() }
            : articulo with
            {
                PrecioDetalle = anterior.PrecioDetalle,
                PrecioMayor = anterior.PrecioMayor,
                CantidadMinimaMayor = anterior.CantidadMinimaMayor,
                PrecioMinimo = anterior.PrecioMinimo,
                Costo = anterior.Costo,
                PreciosVigentesDesde = anterior.PreciosVigentesDesde,
            };

        return await PublicarAsync(new PaqueteMaestros(Articulos: [publicar]), articuloId, actor, cancelacion);
    }

    public async Task<ResultadoAdministracion> CorregirDocumentoClienteAsync(Guid clienteId, SolicitudCorreccionDocumentoCliente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (await LeerAsync<ClienteCarga>(TipoMaestro.Cliente, clienteId, cancelacion) is not { } actual)
            return ResultadoAdministracion.Inexistente("El cliente no existe.");
        if (string.IsNullOrWhiteSpace(solicitud.Motivo))
            return ResultadoAdministracion.Error("Indique el motivo de la corrección.");

        var documento = CgPos.Dominio.Fiscal.DocumentoIdentidad.Normalizar(solicitud.Documento);
        if (solicitud.TipoDocumento == actual.TipoDocumento && documento == CgPos.Dominio.Fiscal.DocumentoIdentidad.Normalizar(actual.Documento))
            return ResultadoAdministracion.Error("El documento es el mismo que ya tiene el cliente.");

        // RNC y cédula deben tener formato y dígito verificador válidos; el pasaporte solo se valida por formato (en el dominio).
        if (solicitud.TipoDocumento != CgPos.Dominio.Fiscal.TipoDocumentoIdentidad.Pasaporte)
        {
            var validacion = CgPos.Dominio.Fiscal.DocumentoIdentidad.Validar(documento);
            if (validacion.Tipo != solicitud.TipoDocumento || !validacion.EsValido)
                return ResultadoAdministracion.Error($"El documento '{solicitud.Documento}' no es {(solicitud.TipoDocumento == CgPos.Dominio.Fiscal.TipoDocumentoIdentidad.Rnc ? "un RNC válido" : "una cédula válida")}.");
        }

        try
        {
            await publicador.PublicarAsync(new PaqueteMaestros(Clientes: [actual with { TipoDocumento = solicitud.TipoDocumento, Documento = documento }]), actor.Nombre,
                cancelacion, corregirDocumentoCliente: true);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }

        auditoria.Registrar(new EntradaAuditoria("Maestros.ClienteDocumentoCorregido", "Cliente", clienteId.ToString(),
            new { Anterior = new { actual.TipoDocumento, actual.Documento }, Nuevo = new { solicitud.TipoDocumento, Documento = documento } }, solicitud.Motivo.Trim(), actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(clienteId);
    }

    public async Task<ResultadoAdministracion> CambiarPreciosAsync(Guid articuloId, SolicitudPreciosArticulo solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        if (await LeerAsync<ArticuloCarga>(TipoMaestro.Articulo, articuloId, cancelacion) is not { } anterior)
            return ResultadoAdministracion.Inexistente("El artículo no existe.");

        // Todas las cajas registran el cambio con la misma vigencia, aunque lo reciban en momentos distintos.
        var articulo = anterior with
        {
            PrecioDetalle = solicitud.PrecioDetalle,
            PrecioMayor = solicitud.PrecioMayor,
            CantidadMinimaMayor = solicitud.CantidadMinimaMayor,
            PrecioMinimo = solicitud.PrecioMinimo,
            Costo = solicitud.Costo,
            PreciosVigentesDesde = solicitud.VigenteDesde ?? reloj.GetUtcNow(),
        };

        return await PublicarAsync(new PaqueteMaestros(Articulos: [articulo]), articuloId, actor, cancelacion);
    }

    public async Task<IReadOnlyList<DatosTopeDescuentoCentral>> ListarTopesAsync(CancellationToken cancelacion = default)
    {
        var topes = await ListarAsync<TopeDescuentoCarga>(TipoMaestro.TopeDescuento, cancelacion);
        var idsDepartamentos = topes.Select(t => t.Dato.DepartamentoId).OfType<Guid>().Distinct().ToList();
        var idsArticulos = topes.Select(t => t.Dato.ArticuloId).OfType<Guid>().Distinct().ToList();
        var categorias = (await ListarAsync<CategoriaCarga>(TipoMaestro.Categoria, cancelacion)).ToDictionary(c => c.Dato.Id, c => c.Dato);
        var marcas = (await ListarAsync<MarcaCarga>(TipoMaestro.Marca, cancelacion)).ToDictionary(m => m.Dato.Id, m => m.Dato);

        var departamentos = (await contexto.MaestrosAsync(TipoMaestro.Departamento, cancelacion, idsDepartamentos))
            .Select(FormatoMaestros.Leer<DepartamentoCarga>).ToDictionary(f => f.Id);
        var articulos = (await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.Articulo && idsArticulos.Contains(m.Id)).ToListAsync(cancelacion))
            .Select(FormatoMaestros.Leer<ArticuloCarga>).ToDictionary(a => a.Id);

        string Alcance(TopeDescuentoCarga tope) => tope switch
        {
            { ArticuloId: { } articuloId } => articulos.TryGetValue(articuloId, out var articulo) ? $"Artículo {articulo.Codigo} · {articulo.Descripcion}" : $"Artículo {articuloId}",
            { CategoriaId: { } categoriaId } => categorias.TryGetValue(categoriaId, out var categoria) ? $"Categoría {categoria.Codigo} · {categoria.Nombre}" : $"Categoría {categoriaId}",
            { MarcaId: { } marcaId } => marcas.TryGetValue(marcaId, out var marca) ? $"Marca {marca.Codigo} · {marca.Nombre}" : $"Marca {marcaId}",
            { DepartamentoId: { } departamentoId } => departamentos.TryGetValue(departamentoId, out var departamento) ? $"Departamento {departamento.Codigo} · {departamento.Nombre}" : $"Departamento {departamentoId}",
            _ => "General",
        };

        return topes
            .Select(t => new DatosTopeDescuentoCentral(t.Dato, Alcance(t.Dato), t.ModificadoEn, t.ModificadoPor))
            .OrderBy(t => t.Tope.ArticuloId is not null ? 4 : t.Tope.CategoriaId is not null ? 3 : t.Tope.MarcaId is not null ? 2 : t.Tope.DepartamentoId is not null ? 1 : 0)
            .ThenBy(t => t.Alcance, StringComparer.CurrentCulture)
            .ThenBy(t => t.Tope.Nivel)
            .ToList();
    }

    private async Task<T?> LeerAsync<T>(TipoMaestro tipo, Guid id, CancellationToken cancelacion) where T : class =>
        (await contexto.MaestrosAsync(tipo, cancelacion, [id])).SingleOrDefault() is { } fila
            ? FormatoMaestros.Leer<T>(fila)
            : null;

    private static DatosMaestroCentral<T> Datos<T>(MaestroCentral fila) => new(FormatoMaestros.Leer<T>(fila), fila.ModificadoEn, fila.ModificadoPor);

    private static string Patron(string texto) => $"%{texto.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]")}%";
}
