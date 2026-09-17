using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Maestros;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Sincronizacion;

internal sealed class PublicadorMaestros(
    ContextoDatosCentral contexto,
    IAuditoriaCentral auditoria,
    TimeProvider reloj,
    ILogger<PublicadorMaestros> registro) : IPublicadorMaestros
{
    /// <summary>Los parámetros con este prefijo rigen al propio Central y no bajan a las cajas.</summary>
    public const string PrefijoParametrosCentral = "Central.";

    private const string TodosLosPermisos = "*";

    public async Task<ResultadoPublicacion> PublicarAsync(PaqueteMaestros paquete, string usuario, CancellationToken cancelacion = default, bool corregirDocumentoCliente = false)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var errores = ValidacionMaestros.Validar(paquete).ToList();
        var filas = FormatoMaestros.Desglosar(paquete).ToList();
        var existentes = await CargarExistentesAsync(filas.Select(f => f.Tipo), cancelacion);

        ValidarUnicos(filas, existentes.Values, errores);
        ValidarInmutables(filas, existentes, errores, corregirDocumentoCliente);
        await ValidarReferenciasAsync(paquete, existentes.Values, errores, cancelacion);
        if (errores.Count > 0)
            throw new PublicacionInvalidaExcepcion(errores);

        ResultadoPublicacion resultado;
        try
        {
            resultado = await GuardarAsync(filas, existentes, usuario, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
        {
            contexto.ChangeTracker.Clear();
            throw new PublicacionInvalidaExcepcion([ValidacionMaestros.MensajeError(excepcion)]);
        }

        auditoria.Registrar(new EntradaAuditoria("Maestros.Publicados", "Maestros", Detalle: new { Usuario = usuario, resultado.Publicados, resultado.SinCambios }));
        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation("Maestros publicados por {Usuario}: {Publicados} nuevos o cambiados, {SinCambios} sin cambios", usuario, resultado.Publicados, resultado.SinCambios);
        return resultado;
    }

    public async Task<ResultadoPublicacion> PublicarSeguridadCajasAsync(IReadOnlyList<RolCarga> roles, IReadOnlyList<UsuarioCarga> usuarios, IReadOnlyList<ParametroCarga> parametros,
        string usuario, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var errores = new List<string>();
        var existentes = await CargarExistentesAsync([TipoMaestro.RolCaja, TipoMaestro.UsuarioCaja], cancelacion);
        var idsCajas = (await contexto.Cajas.Select(c => c.Id).ToListAsync(cancelacion)).ToHashSet();
        var idsSucursales = (await contexto.Sucursales.Select(s => s.Id).ToListAsync(cancelacion)).ToHashSet();
        var idsRoles = roles.Select(r => r.Id).Concat(existentes.Keys.Where(k => k.Tipo == TipoMaestro.RolCaja).Select(k => k.Id)).ToHashSet();
        var filas = new List<FilaMaestro>();

        foreach (var rol in roles)
        {
            try
            {
                var entidad = Rol.Crear(rol.Codigo, rol.Nombre, rol.Nivel, rol.Id);
                foreach (var permiso in (rol.Permisos ?? []).Where(p => p != TodosLosPermisos))
                    entidad.AsignarPermiso(permiso);

                if (existentes.TryGetValue((TipoMaestro.RolCaja, rol.Id), out var filaRol) && FormatoMaestros.Leer<RolCarga>(filaRol).Codigo != rol.Codigo.Trim())
                    errores.Add($"Rol de caja '{rol.Codigo}': el código del rol no se puede cambiar.");

                filas.Add(new FilaMaestro(TipoMaestro.RolCaja, rol.Id, rol.Codigo, null, rol));
            }
            catch (ArgumentException excepcion)
            {
                errores.Add($"Rol de caja '{rol.Codigo}': {ValidacionMaestros.MensajeError(excepcion)}");
            }
        }

        foreach (var dato in usuarios)
        {
            var etiqueta = $"Usuario de caja '{dato.Codigo}'";
            try
            {
                Usuario.Crear(dato.Codigo, dato.Nombre, dato.RolId, dato.Id);
            }
            catch (ArgumentException excepcion)
            {
                errores.Add($"{etiqueta}: {ValidacionMaestros.MensajeError(excepcion)}");
                continue;
            }

            if (!idsRoles.Contains(dato.RolId))
                errores.Add($"{etiqueta} referencia un rol inexistente ({dato.RolId}).");
            foreach (var cajaId in (dato.Cajas ?? []).Where(id => !idsCajas.Contains(id)))
                errores.Add($"{etiqueta} referencia una caja inexistente ({cajaId}).");

            var anterior = existentes.TryGetValue((TipoMaestro.UsuarioCaja, dato.Id), out var fila) ? FormatoMaestros.Leer<UsuarioCarga>(fila) : null;
            if (anterior is not null && anterior.Codigo != dato.Codigo.Trim())
                errores.Add($"{etiqueta}: el código del usuario no se puede cambiar.");
            // La clave solo baja como hash; una clave que no cambió conserva su hash (lleva sal aleatoria).
            var claveHash = dato.ClaveHash;
            if (dato.Clave is not null)
                claveHash = dato.Clave.Length == 0
                    ? null
                    : anterior?.ClaveHash is { } hashAnterior && HashCredencialesCaja.VerificarClave(dato.Clave, hashAnterior)
                        ? hashAnterior
                        : HashCredencialesCaja.HashClave(dato.Clave);

            claveHash ??= anterior?.ClaveHash;
            if (claveHash is null)
                errores.Add($"{etiqueta} no tiene clave.");

            filas.Add(new FilaMaestro(TipoMaestro.UsuarioCaja, dato.Id, dato.Codigo, null, dato with { Clave = null, ClaveHash = claveHash }));
        }

        foreach (var parametro in parametros)
        {
            var clave = parametro.Clave?.Trim() ?? string.Empty;
            if (clave.StartsWith(PrefijoParametrosCentral, StringComparison.OrdinalIgnoreCase))
                errores.Add($"El parámetro '{clave}' es del Central y no se publica para las cajas.");
            else if (CatalogoParametros.Buscar(clave) is not { Alcance: AlcanceParametro.Caja } definicion)
                errores.Add($"El parámetro '{clave}' no está en el catálogo de parámetros de caja.");
            else if (definicion.ValidarValor(parametro.Valor) is { } problema)
                errores.Add($"El parámetro '{clave}': {problema}");
            if (parametro.SucursalId is { } sucursalId && !idsSucursales.Contains(sucursalId))
                errores.Add($"El parámetro '{clave}' referencia una sucursal inexistente ({sucursalId}).");
            if (parametro.CajaId is { } cajaId && !idsCajas.Contains(cajaId))
                errores.Add($"El parámetro '{clave}' referencia una caja inexistente ({cajaId}).");
        }

        ValidarUnicos(filas, existentes.Values, errores);
        if (errores.Count > 0)
            throw new PublicacionInvalidaExcepcion(errores);

        try
        {
            var resultado = await GuardarAsync(filas, existentes, usuario, cancelacion);
            var parametrosCambiados = await AplicarParametrosAsync(parametros, cancelacion);
            resultado = resultado with { Publicados = resultado.Publicados + parametrosCambiados, SinCambios = resultado.SinCambios + parametros.Count - parametrosCambiados };

            auditoria.Registrar(new EntradaAuditoria("Maestros.SeguridadCajasPublicada", "Maestros",
                Detalle: new { Usuario = usuario, Roles = roles.Count, Usuarios = usuarios.Count, Parametros = parametros.Count, resultado.Publicados }));
            await contexto.SaveChangesAsync(cancelacion);
            return resultado;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
        {
            contexto.ChangeTracker.Clear();
            throw new PublicacionInvalidaExcepcion([ValidacionMaestros.MensajeError(excepcion)]);
        }
    }

    private async Task<Dictionary<(TipoMaestro Tipo, Guid Id), MaestroCentral>> CargarExistentesAsync(IEnumerable<TipoMaestro> tipos, CancellationToken cancelacion)
    {
        var lista = tipos.Distinct().ToList();
        var enJson = lista.Where(t => !Maestros.TablasMaestros.TieneTabla(t)).ToList();
        var existentes = enJson.Count == 0
            ? []
            : await contexto.MaestrosCentral.Where(m => enJson.Contains(m.Tipo)).ToDictionaryAsync(m => (m.Tipo, m.Id), cancelacion);

        // Los que ya tienen su tabla se leen como filas publicadas solo para validar; se guardan en su tabla.
        foreach (var tipo in lista.Where(Maestros.TablasMaestros.TieneTabla))
            foreach (var fila in await contexto.MaestrosAsync(tipo, cancelacion))
                existentes[(tipo, fila.Id)] = fila;

        return existentes;
    }

    private async Task<ResultadoPublicacion> GuardarAsync(IEnumerable<FilaMaestro> filas, Dictionary<(TipoMaestro Tipo, Guid Id), MaestroCentral> existentes, string usuario,
        CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();
        var publicados = 0;
        var sinCambios = 0;

        foreach (var fila in filas)
        {
            if (Maestros.TablasMaestros.Buscar(fila.Tipo) is { } tabla)
            {
                if (await tabla.AplicarAsync(contexto, fila.Dato, ahora, usuario, cancelacion)) publicados++; else sinCambios++;
                continue;
            }

            var contenido = fila.Contenido();
            if (existentes.TryGetValue((fila.Tipo, fila.Id), out var maestro))
            {
                if (maestro.Actualizar(fila.Codigo, fila.CajaId, contenido, ahora, usuario, fila.TextoBusqueda())) publicados++; else sinCambios++;
                continue;
            }

            maestro = MaestroCentral.Publicar(fila.Tipo, fila.Id, fila.Codigo, fila.CajaId, contenido, ahora, usuario, fila.TextoBusqueda());
            contexto.MaestrosCentral.Add(maestro);
            existentes[(fila.Tipo, fila.Id)] = maestro;
            publicados++;
        }

        return new ResultadoPublicacion(publicados, sinCambios);
    }

    /// <returns>Cantidad de parámetros nuevos o con valor distinto.</returns>
    private async Task<int> AplicarParametrosAsync(IReadOnlyList<ParametroCarga> parametros, CancellationToken cancelacion)
    {
        var cambiados = 0;
        foreach (var dato in parametros)
        {
            var parametro = await contexto.Parametros.SingleOrDefaultAsync(p => p.Id == dato.Id, cancelacion);
            if (parametro is null)
            {
                contexto.Parametros.Add(Parametro.Crear(dato.Clave, dato.Valor, dato.Descripcion, dato.SucursalId, dato.CajaId, dato.Id));
                cambiados++;
                continue;
            }

            if (parametro.Clave != dato.Clave.Trim() || parametro.SucursalId != dato.SucursalId || parametro.CajaId != dato.CajaId)
                throw new InvalidOperationException($"El parámetro '{parametro.Clave}' no puede cambiar de clave ni de ámbito.");

            if (parametro.Valor == dato.Valor)
                continue;

            parametro.CambiarValor(dato.Valor);
            cambiados++;
        }

        return cambiados;
    }

    /// <summary>Maestros cuyo código la caja no deja cambiar: se identifica con él en sus documentos.</summary>
    private static readonly Dictionary<TipoMaestro, string> CodigoInmutable = new()
    {
        [TipoMaestro.Articulo] = "del artículo",
        [TipoMaestro.Departamento] = "del departamento",
        [TipoMaestro.Categoria] = "de la categoría",
        [TipoMaestro.Marca] = "de la marca",
        [TipoMaestro.UnidadMedida] = "de la unidad de medida",
        [TipoMaestro.Impuesto] = "del impuesto",
        [TipoMaestro.Moneda] = "de la moneda",
        [TipoMaestro.Promocion] = "de la promoción",
        [TipoMaestro.NivelFidelidad] = "del nivel de fidelidad",
        [TipoMaestro.ReglaAcumulacion] = "de la regla de acumulación",
        [TipoMaestro.Almacen] = "del almacén",
        [TipoMaestro.MiembroFidelidad] = "(cédula) del miembro de fidelidad",
        [TipoMaestro.DescuentoTarjeta] = "del descuento por tarjeta",
    };

    /// <summary>Lo que la caja rechaza cambiar se valida antes de publicar: un maestro así detendría su sincronización.</summary>
    private static void ValidarInmutables(IEnumerable<FilaMaestro> filas, Dictionary<(TipoMaestro Tipo, Guid Id), MaestroCentral> existentes, List<string> errores,
        bool corregirDocumentoCliente)
    {
        foreach (var fila in filas)
        {
            if (!existentes.TryGetValue((fila.Tipo, fila.Id), out var publicado))
                continue;

            // El código del artículo se compara exacto, como en la caja; los demás se guardan en mayúsculas.
            var cambiaCodigo = fila.Tipo == TipoMaestro.Articulo
                ? FormatoMaestros.Leer<ArticuloCarga>(publicado).Codigo.Trim() != ((ArticuloCarga)fila.Dato).Codigo?.Trim()
                : !string.Equals(publicado.Codigo, fila.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase);

            if (CodigoInmutable.TryGetValue(fila.Tipo, out var descripcion) && cambiaCodigo)
                errores.Add($"No se puede cambiar el código {descripcion} '{publicado.Codigo}'; cree uno nuevo.");
            else if (fila.Tipo == TipoMaestro.Denominacion && cambiaCodigo)
                errores.Add($"La denominación {publicado.Codigo} no puede cambiar de moneda, valor ni tipo; cree una nueva.");
            else if (fila.Tipo == TipoMaestro.FormaPago && FormatoMaestros.Leer<FormaPagoCarga>(publicado).Tipo != ((FormaPagoCarga)fila.Dato).Tipo)
                errores.Add($"No se puede cambiar el tipo de la forma de pago '{publicado.Codigo}'; cree una nueva.");
            // El documento solo cambia con la corrección auditada (con motivo), no al guardar los datos del cliente.
            else if (fila.Tipo == TipoMaestro.Cliente && cambiaCodigo && !corregirDocumentoCliente)
                errores.Add($"El documento del cliente '{FormatoMaestros.Leer<ClienteCarga>(publicado).Documento}' se cambia con «Corregir documento».");
        }
    }

    /// <summary>Ids que existen entre los referidos: los del paquete y los ya publicados, consultados por bloques.</summary>
    private async Task<HashSet<Guid>> IdsExistentesAsync(TipoMaestro tipo, IEnumerable<Guid> referidos, IEnumerable<Guid> delPaquete, CancellationToken cancelacion)
    {
        var ids = delPaquete.ToHashSet();
        foreach (var bloque in referidos.Where(id => id != Guid.Empty && !ids.Contains(id)).Distinct().Chunk(1000))
        {
            var buscar = bloque.ToList();
            ids.UnionWith((await contexto.MaestrosAsync(tipo, cancelacion, buscar)).Select(m => m.Id));
        }

        return ids;
    }

    private static string EtiquetaRango(SecuenciaEcfCarga rango) => $"E{(int)rango.TipoComprobante} {rango.Desde}–{rango.Hasta}";

    private static void ValidarUnicos(IReadOnlyList<FilaMaestro> filas, IEnumerable<MaestroCentral> existentes, List<string> errores)
    {
        foreach (var repetido in filas.GroupBy(f => (f.Tipo, f.Id)).Where(g => g.Count() > 1))
            errores.Add($"{repetido.Key.Tipo} {repetido.Key.Id} repetido en el paquete.");

        var codigos = existentes.Where(m => m.Codigo is not null).ToDictionary(m => (m.Tipo, m.Codigo!), m => m.Id);
        foreach (var fila in filas.Where(f => !string.IsNullOrWhiteSpace(f.Codigo)))
        {
            var clave = (fila.Tipo, fila.Codigo!.Trim().ToUpperInvariant());
            if (codigos.TryGetValue(clave, out var otroId) && otroId != fila.Id)
                errores.Add($"{fila.Tipo} con código '{fila.Codigo!.Trim()}' ya existe con otro Id ({otroId}).");
            else
                codigos[clave] = fila.Id;
        }
    }

    private async Task ValidarReferenciasAsync(PaqueteMaestros paquete, IEnumerable<MaestroCentral> existentes, List<string> errores, CancellationToken cancelacion)
    {
        var publicados = existentes.ToList();

        async Task<HashSet<Guid>> IdsAsync(TipoMaestro tipo, IEnumerable<Guid> delPaquete)
        {
            var ids = delPaquete.ToHashSet();
            ids.UnionWith(publicados.Any(m => m.Tipo == tipo)
                ? publicados.Where(m => m.Tipo == tipo).Select(m => m.Id)
                : await contexto.IdsMaestrosAsync(tipo, cancelacion));
            return ids;
        }

        // Departamento de cada categoría, con las del paquete sobre las publicadas.
        async Task<Dictionary<Guid, Guid>> DepartamentoDeCategoriaAsync()
        {
            var filas = publicados.Any(m => m.Tipo == TipoMaestro.Categoria)
                ? publicados.Where(m => m.Tipo == TipoMaestro.Categoria).ToList()
                : await contexto.MaestrosAsync(TipoMaestro.Categoria, cancelacion);
            var mapa = filas.Select(FormatoMaestros.Leer<CategoriaCarga>).ToDictionary(c => c.Id, c => c.DepartamentoId);
            foreach (var categoria in paquete.Categorias ?? [])
                mapa[categoria.Id] = categoria.DepartamentoId;
            return mapa;
        }

        if (paquete.Categorias is { Count: > 0 } categorias)
        {
            var departamentos = await IdsAsync(TipoMaestro.Departamento, (paquete.Departamentos ?? []).Select(d => d.Id));
            foreach (var categoria in categorias.Where(c => !departamentos.Contains(c.DepartamentoId)))
                errores.Add($"La categoría '{categoria.Codigo}' referencia un departamento inexistente ({categoria.DepartamentoId}).");

            // Cambiar una categoría de departamento dejaría artículos con una categoría de otro departamento.
            var idsCategorias = categorias.Select(c => c.Id).ToHashSet();
            var nuevoDepartamento = categorias.ToDictionary(c => c.Id, c => c.DepartamentoId);
            var articulosPublicados = await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.Articulo).ToListAsync(cancelacion);
            var idsArticulosPaquete = (paquete.Articulos ?? []).Select(a => a.Id).ToHashSet();
            foreach (var articulo in articulosPublicados.Where(m => !idsArticulosPaquete.Contains(m.Id)).Select(FormatoMaestros.Leer<ArticuloCarga>)
                         .Where(a => a.CategoriaId is { } id && idsCategorias.Contains(id) && nuevoDepartamento[id] != a.DepartamentoId))
                errores.Add($"El artículo '{articulo.Codigo}' tiene esa categoría en otro departamento; cámbielo antes de mover la categoría.");
        }

        if (paquete.Articulos is { Count: > 0 } articulos)
        {
            var departamentos = await IdsAsync(TipoMaestro.Departamento, (paquete.Departamentos ?? []).Select(f => f.Id));
            var unidades = await IdsAsync(TipoMaestro.UnidadMedida, (paquete.UnidadesMedida ?? []).Select(u => u.Id));
            var impuestos = await IdsAsync(TipoMaestro.Impuesto, (paquete.Impuestos ?? []).Select(i => i.Id));
            var marcas = await IdsAsync(TipoMaestro.Marca, (paquete.Marcas ?? []).Select(m => m.Id));
            var departamentoDeCategoria = await DepartamentoDeCategoriaAsync();

            foreach (var articulo in articulos)
            {
                var etiqueta = $"El artículo '{articulo.Codigo}'";
                if (!departamentos.Contains(articulo.DepartamentoId)) errores.Add($"{etiqueta} referencia un departamento inexistente ({articulo.DepartamentoId}).");
                if (!unidades.Contains(articulo.UnidadMedidaId)) errores.Add($"{etiqueta} referencia una unidad de medida inexistente ({articulo.UnidadMedidaId}).");
                if (!impuestos.Contains(articulo.ImpuestoId)) errores.Add($"{etiqueta} referencia un impuesto inexistente ({articulo.ImpuestoId}).");
                if (articulo.MarcaId is { } marcaId && !marcas.Contains(marcaId)) errores.Add($"{etiqueta} referencia una marca inexistente ({marcaId}).");
                if (articulo.CategoriaId is { } categoriaId)
                {
                    if (!departamentoDeCategoria.TryGetValue(categoriaId, out var departamentoCategoria))
                        errores.Add($"{etiqueta} referencia una categoría inexistente ({categoriaId}).");
                    else if (departamentoCategoria != articulo.DepartamentoId)
                        errores.Add($"{etiqueta} tiene una categoría que no es de su departamento.");
                }
            }

            // El código interno, los de barras y los de proveedor identifican a un solo artículo en toda la empresa: la caja busca por cualquiera de ellos.
            var idsDelPaquete = articulos.Select(a => a.Id).ToHashSet();
            var duenoPorCodigo = new Dictionary<string, (Guid Id, string Articulo)>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> Codigos(ArticuloCarga articulo) =>
                new[] { articulo.Codigo }.Concat(articulo.CodigosBarras ?? []).Concat(articulo.CodigosProveedor ?? []).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);

            var articulosPublicados = publicados.Any(m => m.Tipo == TipoMaestro.Articulo && !idsDelPaquete.Contains(m.Id))
                ? publicados.Where(m => m.Tipo == TipoMaestro.Articulo && !idsDelPaquete.Contains(m.Id)).ToList()
                : await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.Articulo && !idsDelPaquete.Contains(m.Id)).ToListAsync(cancelacion);
            foreach (var publicado in articulosPublicados.Select(FormatoMaestros.Leer<ArticuloCarga>))
                foreach (var codigo in Codigos(publicado))
                    duenoPorCodigo[codigo] = (publicado.Id, publicado.Codigo);

            foreach (var articulo in articulos)
                foreach (var codigo in Codigos(articulo))
                {
                    // Dos artículos con el mismo código interno ya se informan como código repetido con otro Id.
                    var mismoInterno = string.Equals(codigo, articulo.Codigo.Trim(), StringComparison.OrdinalIgnoreCase)
                                       && duenoPorCodigo.TryGetValue(codigo, out var otroInterno)
                                       && string.Equals(codigo, otroInterno.Articulo, StringComparison.OrdinalIgnoreCase);
                    if (!mismoInterno && duenoPorCodigo.TryGetValue(codigo, out var dueno) && dueno.Id != articulo.Id)
                        errores.Add($"El código '{codigo}' ya lo usa el artículo '{dueno.Articulo}' (como código interno, de barras o de proveedor); no puede estar también en '{articulo.Codigo}'.");
                    duenoPorCodigo[codigo] = (articulo.Id, articulo.Codigo);
                }
        }

        var usanMoneda = (paquete.FormasPago ?? []).Select(f => (f.Moneda, $"La forma de pago '{f.Codigo}'"))
            .Concat((paquete.Denominaciones ?? []).Select(d => (d.Moneda, $"La denominación {d.Valor}")))
            .Concat((paquete.TasasCambio ?? []).Select(t => (t.Moneda, "La tasa de cambio")))
            .ToList();
        if (usanMoneda.Count > 0)
        {
            var monedas = (paquete.Monedas ?? []).Select(m => m.Codigo.Trim().ToUpperInvariant()).ToHashSet();
            monedas.UnionWith((await contexto.MaestrosCentral.Where(m => m.Tipo == TipoMaestro.Moneda && m.Codigo != null).Select(m => m.Codigo!).ToListAsync(cancelacion))
                .Select(c => c.ToUpperInvariant()));
            foreach (var (moneda, referencia) in usanMoneda.Where(u => !monedas.Contains(u.Moneda?.Trim().ToUpperInvariant() ?? string.Empty)))
                errores.Add($"{referencia} usa la moneda '{moneda}', que no está publicada.");
        }

        if (paquete.SecuenciasEcf is { Count: > 0 } secuencias)
        {
            var cajas = (await contexto.Cajas.Select(c => c.Id).ToListAsync(cancelacion)).ToHashSet();
            foreach (var secuencia in secuencias.Where(s => !cajas.Contains(s.CajaId)))
                errores.Add($"El rango de e-CF {secuencia.Id} referencia una caja inexistente ({secuencia.CajaId}).");

            // La caja rechaza cambiar un rango de caja, tipo o inicio, o dejarlo por debajo de lo emitido: se valida aquí para no detener su sincronización.
            var actuales = publicados.Where(m => m.Tipo == TipoMaestro.SecuenciaEcf).Select(FormatoMaestros.Leer<SecuenciaEcfCarga>).ToDictionary(s => s.Id);
            foreach (var secuencia in secuencias)
            {
                if (!actuales.TryGetValue(secuencia.Id, out var anterior))
                    continue;

                if (anterior.CajaId != secuencia.CajaId || anterior.TipoComprobante != secuencia.TipoComprobante || anterior.Desde != secuencia.Desde)
                    errores.Add($"El rango de e-CF {EtiquetaRango(anterior)} no puede cambiar de caja, tipo ni inicio; asigne un rango nuevo.");
                if (secuencia.Hasta < anterior.Hasta)
                    errores.Add($"El rango de e-CF {EtiquetaRango(anterior)} no puede reducirse; la caja pudo haber emitido hasta su final.");
            }

            // Un e-NCF es único en toda la empresa: los rangos del mismo tipo no se solapan entre cajas.
            var idsDelPaquete = secuencias.Select(s => s.Id).ToHashSet();
            foreach (var grupo in actuales.Values.Where(a => !idsDelPaquete.Contains(a.Id)).Concat(secuencias).GroupBy(s => s.TipoComprobante))
            {
                SecuenciaEcfCarga? mayor = null;
                foreach (var rango in grupo.OrderBy(s => s.Desde))
                {
                    if (mayor is not null && rango.Desde <= mayor.Hasta)
                        errores.Add($"Los rangos de e-CF {EtiquetaRango(mayor)} y {EtiquetaRango(rango)} se solapan.");
                    if (mayor is null || rango.Hasta > mayor.Hasta)
                        mayor = rango;
                }
            }
        }

        if (paquete.Promociones is { Count: > 0 } promociones)
        {
            var departamentosPromocion = await IdsExistentesAsync(TipoMaestro.Departamento, promociones.SelectMany(p => p.Departamentos ?? []), (paquete.Departamentos ?? []).Select(f => f.Id), cancelacion);
            var categoriasPromocion = await IdsExistentesAsync(TipoMaestro.Categoria, promociones.SelectMany(p => p.Categorias ?? []), (paquete.Categorias ?? []).Select(c => c.Id), cancelacion);
            var marcasPromocion = await IdsExistentesAsync(TipoMaestro.Marca, promociones.SelectMany(p => p.Marcas ?? []), (paquete.Marcas ?? []).Select(m => m.Id), cancelacion);
            var articulosPromocion = await IdsExistentesAsync(TipoMaestro.Articulo, promociones.SelectMany(p => p.Articulos ?? []), (paquete.Articulos ?? []).Select(a => a.Id), cancelacion);
            var sucursalesExistentes = (await contexto.Sucursales.Select(s => s.Id).ToListAsync(cancelacion)).ToHashSet();
            foreach (var promocion in promociones)
            {
                var etiqueta = $"La promoción '{promocion.Codigo}'";
                if ((promocion.Articulos ?? []).Count(id => id != Guid.Empty && !articulosPromocion.Contains(id)) is var sinArticulo and > 0)
                    errores.Add($"{etiqueta} referencia {sinArticulo} artículo(s) inexistente(s).");
                if ((promocion.Departamentos ?? []).Count(id => id != Guid.Empty && !departamentosPromocion.Contains(id)) is var sinDepartamento and > 0)
                    errores.Add($"{etiqueta} referencia {sinDepartamento} departamento(s) inexistente(s).");
                if ((promocion.Categorias ?? []).Count(id => id != Guid.Empty && !categoriasPromocion.Contains(id)) is var sinCategoria and > 0)
                    errores.Add($"{etiqueta} referencia {sinCategoria} categoría(s) inexistente(s).");
                if ((promocion.Marcas ?? []).Count(id => id != Guid.Empty && !marcasPromocion.Contains(id)) is var sinMarca and > 0)
                    errores.Add($"{etiqueta} referencia {sinMarca} marca(s) inexistente(s).");
                if ((promocion.Sucursales ?? []).Count(id => id != Guid.Empty && !sucursalesExistentes.Contains(id)) is var sinSucursal and > 0)
                    errores.Add($"{etiqueta} referencia {sinSucursal} sucursal(es) inexistente(s).");
            }
        }

        if (paquete.TopesDescuento is { Count: > 0 } topes)
        {
            var departamentos = await IdsExistentesAsync(TipoMaestro.Departamento, topes.Select(t => t.DepartamentoId).OfType<Guid>(), (paquete.Departamentos ?? []).Select(f => f.Id), cancelacion);
            var articulosTope = await IdsExistentesAsync(TipoMaestro.Articulo, topes.Select(t => t.ArticuloId).OfType<Guid>(), (paquete.Articulos ?? []).Select(a => a.Id), cancelacion);
            var categoriasTope = await IdsExistentesAsync(TipoMaestro.Categoria, topes.Select(t => t.CategoriaId).OfType<Guid>(), (paquete.Categorias ?? []).Select(c => c.Id), cancelacion);
            var marcasTope = await IdsExistentesAsync(TipoMaestro.Marca, topes.Select(t => t.MarcaId).OfType<Guid>(), (paquete.Marcas ?? []).Select(m => m.Id), cancelacion);
            foreach (var tope in topes)
            {
                if (tope.DepartamentoId is { } departamentoId && !departamentos.Contains(departamentoId))
                    errores.Add($"El tope de descuento de nivel {tope.Nivel} referencia un departamento inexistente ({departamentoId}).");
                if (tope.ArticuloId is { } articuloId && !articulosTope.Contains(articuloId))
                    errores.Add($"El tope de descuento de nivel {tope.Nivel} referencia un artículo inexistente ({articuloId}).");
                if (tope.CategoriaId is { } categoriaId && !categoriasTope.Contains(categoriaId))
                    errores.Add($"El tope de descuento de nivel {tope.Nivel} referencia una categoría inexistente ({categoriaId}).");
                if (tope.MarcaId is { } marcaId && !marcasTope.Contains(marcaId))
                    errores.Add($"El tope de descuento de nivel {tope.Nivel} referencia una marca inexistente ({marcaId}).");
            }

            // Dentro de un alcance, la caja toma el tope del nivel del autorizador: dos topes del mismo nivel y alcance serían ambiguos.
            var idsTopes = topes.Select(t => t.Id).ToHashSet();
            foreach (var repetido in publicados.Where(m => m.Tipo == TipoMaestro.TopeDescuento && !idsTopes.Contains(m.Id))
                         .Select(FormatoMaestros.Leer<TopeDescuentoCarga>)
                         .Concat(topes)
                         .GroupBy(t => (t.Nivel, t.DepartamentoId, t.ArticuloId, t.CategoriaId, t.MarcaId))
                         .Where(g => g.Count() > 1))
            {
                var alcance = repetido.Key.ArticuloId is not null ? "ese artículo"
                    : repetido.Key.CategoriaId is not null ? "esa categoría"
                    : repetido.Key.MarcaId is not null ? "esa marca"
                    : repetido.Key.DepartamentoId is not null ? "ese departamento"
                    : "el alcance general";
                errores.Add($"Ya hay un tope de descuento de nivel {repetido.Key.Nivel} para {alcance}; cambie ese tope.");
            }
        }

        if (paquete.Almacenes is { Count: > 0 } almacenes)
        {
            var sucursales = (await contexto.Sucursales.Select(s => s.Id).ToListAsync(cancelacion)).ToHashSet();
            foreach (var almacen in almacenes.Where(a => !sucursales.Contains(a.SucursalId)))
                errores.Add($"El almacén '{almacen.Codigo}' referencia una sucursal inexistente ({almacen.SucursalId}).");
        }

        if (paquete.MiembrosFidelidad?.Where(m => m.NivelId is not null).ToList() is { Count: > 0 } conNivel)
        {
            var niveles = await IdsAsync(TipoMaestro.NivelFidelidad, (paquete.NivelesFidelidad ?? []).Select(n => n.Id));
            foreach (var miembro in conNivel.Where(m => !niveles.Contains(m.NivelId!.Value)))
                errores.Add($"El miembro '{miembro.Cedula}' referencia un nivel de fidelidad inexistente ({miembro.NivelId}).");
        }
    }
}

public static class ExtensionesPublicacionMaestros
{
    /// <summary>
    /// Publica un archivo de maestros (formato de la carga de la caja) al arrancar. Solo publica lo que no existe en el Central: lo ya publicado
    /// se administra en el Manager y volver a aplicar el archivo no debe deshacer esos cambios.
    /// </summary>
    public static async Task PublicarMaestrosDesdeArchivoAsync(this IServiceProvider servicios, string ruta, CancellationToken cancelacion = default)
    {
        var paquete = await LeerAsync<PaqueteMaestros>(ruta, cancelacion);
        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();
        var publicados = await contexto.IdsTodosLosMaestrosAsync(cancelacion);

        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarAsync(SoloNuevos(paquete, publicados), "Carga inicial", cancelacion);
    }

    /// <summary>Publica roles, usuarios y parámetros de un archivo de carga inicial de caja; como los maestros, solo lo que no existe.</summary>
    public static async Task PublicarSeguridadCajasDesdeArchivoAsync(this IServiceProvider servicios, string ruta, CancellationToken cancelacion = default)
    {
        var paquete = await LeerAsync<PaqueteCargaInicial>(ruta, cancelacion);
        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();
        var existentes = (await contexto.MaestrosCentral.Where(m => m.Tipo == TipoMaestro.RolCaja || m.Tipo == TipoMaestro.UsuarioCaja).Select(m => m.Id)
            .ToListAsync(cancelacion)).ToHashSet();
        existentes.UnionWith(await contexto.Parametros.Select(p => p.Id).ToListAsync(cancelacion));

        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarSeguridadCajasAsync(
            (paquete.Roles ?? []).Where(r => !existentes.Contains(r.Id)).ToList(),
            (paquete.Usuarios ?? []).Where(u => !existentes.Contains(u.Id)).ToList(),
            (paquete.Parametros ?? []).Where(p => !existentes.Contains(p.Id)).ToList(),
            "Carga inicial", cancelacion);
    }

    /// <summary>Copia del paquete sin los maestros cuyo Id ya está publicado.</summary>
    internal static PaqueteMaestros SoloNuevos(PaqueteMaestros paquete, IReadOnlySet<Guid> publicados)
    {
        var copia = paquete with { };
        foreach (var propiedad in typeof(PaqueteMaestros).GetProperties().Where(p => p.CanWrite && p.PropertyType.IsGenericType
                     && p.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
        {
            if (propiedad.GetValue(copia) is not System.Collections.IEnumerable lista)
                continue;

            var tipo = propiedad.PropertyType.GetGenericArguments()[0];
            var id = tipo.GetProperty("Id");
            if (id?.PropertyType != typeof(Guid))
                continue;

            var filtrados = lista.Cast<object>().Where(elemento => !publicados.Contains((Guid)id.GetValue(elemento)!)).ToArray();
            var arreglo = Array.CreateInstance(tipo, filtrados.Length);
            Array.Copy(filtrados, arreglo, filtrados.Length);
            propiedad.SetValue(copia, arreglo);
        }

        return copia;
    }

    private static async Task<T> LeerAsync<T>(string ruta, CancellationToken cancelacion)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo: {ruta}", ruta);

        await using var archivo = File.OpenRead(ruta);
        return await JsonSerializer.DeserializeAsync<T>(archivo, OpcionesJson.Predeterminadas, cancelacion)
            ?? throw new PublicacionInvalidaExcepcion([$"El archivo {Path.GetFileName(ruta)} está vacío."]);
    }
}
