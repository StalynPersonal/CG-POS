using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Maestros;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Sincronizacion;

/// <summary>
/// Publica maestros para las cajas: cada registro se guarda en su tabla por su código (o llave natural) con las reglas del dominio. Todo o nada:
/// con un error no se guarda nada, porque un maestro inválido detendría la sincronización de las cajas.
/// </summary>
internal sealed class PublicadorMaestros(
    ContextoDatosCentral contexto,
    IAuditoriaCentral auditoria,
    TimeProvider reloj,
    ILogger<PublicadorMaestros> registro) : IPublicadorMaestros
{
    /// <summary>Los parámetros con este prefijo rigen al propio Central y no bajan a las cajas.</summary>
    public const string PrefijoParametrosCentral = "Central.";

    public async Task<ResultadoPublicacion> PublicarAsync(PaqueteMaestros paquete, string usuario, CancellationToken cancelacion = default, bool corregirDocumentoCliente = false)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var errores = ValidacionMaestros.Validar(paquete).ToList();
        ValidarRepetidos(paquete, errores);
        await ValidarCodigosArticulosAsync(paquete.Articulos ?? [], errores, cancelacion);
        await ValidarMonedasAsync(paquete, errores, cancelacion);
        if (errores.Count > 0)
            throw new PublicacionInvalidaExcepcion(errores);

        var resolutor = new ResolutorCodigosCentral(contexto);
        await resolutor.PrepararAsync(cancelacion);
        await resolutor.CargarArticulosAsync(
            (paquete.Promociones ?? []).SelectMany(p => p.Articulos ?? [])
                .Concat((paquete.TopesDescuento ?? []).Select(t => t.ArticuloCodigo).OfType<string>())
                .Concat((paquete.ReglasAcumulacion ?? []).Where(r => r.Tipo == Dominio.Fidelidad.TipoReglaAcumulacion.Articulo).Select(r => r.Referencia).OfType<string>()),
            null, cancelacion);

        var opciones = new OpcionesPublicacion(reloj.Ahora(), usuario, corregirDocumentoCliente);
        var (publicados, sinCambios) = await AplicarAsync(Registros(paquete), resolutor, opciones, errores, cancelacion);

        if (errores.Count == 0)
            await ValidarDespuesDeAplicarAsync(paquete, errores, cancelacion);
        if (errores.Count > 0)
        {
            contexto.ChangeTracker.Clear();
            throw new PublicacionInvalidaExcepcion(errores);
        }

        auditoria.Registrar(new EntradaAuditoria("Maestros.Publicados", "Maestros", Detalle: new { Usuario = usuario, Publicados = publicados, SinCambios = sinCambios }));
        await GuardarAsync(cancelacion);

        registro.LogInformation("Maestros publicados por {Usuario}: {Publicados} nuevos o cambiados, {SinCambios} sin cambios", usuario, publicados, sinCambios);
        return new ResultadoPublicacion(publicados, sinCambios);
    }

    public async Task<ResultadoPublicacion> PublicarSeguridadCajasAsync(IReadOnlyList<RolCarga> roles, IReadOnlyList<UsuarioCarga> usuarios,
        IReadOnlyList<ParametroCarga> parametros, string usuario, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var errores = new List<string>();
        foreach (var repetido in roles.GroupBy(r => r.Codigo?.Trim().ToUpperInvariant()).Where(g => g.Count() > 1))
            errores.Add($"Rol de caja '{repetido.Key}' repetido.");
        foreach (var repetido in usuarios.GroupBy(u => u.Codigo?.Trim().ToUpperInvariant()).Where(g => g.Count() > 1))
            errores.Add($"Usuario de caja '{repetido.Key}' repetido.");

        foreach (var rol in roles)
        {
            try
            {
                var entidad = Rol.Crear(rol.Codigo, rol.Nombre, rol.Nivel);
                foreach (var permiso in (rol.Permisos ?? []).Where(p => p != "*"))
                    entidad.AsignarPermiso(permiso);
            }
            catch (ArgumentException excepcion)
            {
                errores.Add($"Rol de caja '{rol.Codigo}': {ValidacionMaestros.MensajeError(excepcion)}");
            }
        }

        var resolutor = new ResolutorCodigosCentral(contexto);
        await resolutor.PrepararAsync(cancelacion);

        // La clave solo baja como hash; una clave que no cambió conserva su hash (lleva sal aleatoria).
        var usuariosConHash = new List<UsuarioCarga>();
        foreach (var dato in usuarios)
        {
            var etiqueta = $"Usuario de caja '{dato.Codigo}'";
            try
            {
                Usuario.Crear(dato.Codigo, dato.Nombre, ResolutorValidacion.Id("Rol", dato.RolCodigo));
            }
            catch (ArgumentException excepcion)
            {
                errores.Add($"{etiqueta}: {ValidacionMaestros.MensajeError(excepcion)}");
                continue;
            }

            var anterior = await TablasMaestros.UsuariosCaja.BuscarAsync(contexto, dato, cancelacion);
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

            usuariosConHash.Add(dato with { Clave = null, ClaveHash = claveHash });
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
        }

        if (errores.Count > 0)
            throw new PublicacionInvalidaExcepcion(errores);

        var opciones = new OpcionesPublicacion(reloj.Ahora(), usuario);
        var registros = roles.Select(r => ((TablaMaestro)TablasMaestros.RolesCaja, (object)r, $"Rol de caja '{r.Codigo}'"))
            .Concat(usuariosConHash.Select(u => ((TablaMaestro)TablasMaestros.UsuariosCaja, (object)u, $"Usuario de caja '{u.Codigo}'")));
        var (publicados, sinCambios) = await AplicarAsync(registros, resolutor, opciones, errores, cancelacion);

        var parametrosCambiados = 0;
        foreach (var parametro in parametros)
        {
            try
            {
                if (await AplicarParametroAsync(parametro, resolutor, cancelacion))
                    parametrosCambiados++;
            }
            catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
            {
                errores.Add($"El parámetro '{parametro.Clave}': {ValidacionMaestros.MensajeError(excepcion)}");
            }
        }

        if (errores.Count > 0)
        {
            contexto.ChangeTracker.Clear();
            throw new PublicacionInvalidaExcepcion(errores);
        }

        var resultado = new ResultadoPublicacion(publicados + parametrosCambiados, sinCambios + parametros.Count - parametrosCambiados);
        auditoria.Registrar(new EntradaAuditoria("Maestros.SeguridadCajasPublicada", "Maestros",
            Detalle: new { Usuario = usuario, Roles = roles.Count, Usuarios = usuarios.Count, Parametros = parametros.Count, resultado.Publicados }));
        await GuardarAsync(cancelacion);
        return resultado;
    }

    /// <summary>Registros del paquete con su tabla, en el orden en que se aplican.</summary>
    internal static IEnumerable<(TablaMaestro Tabla, object Dato, string Etiqueta)> Registros(PaqueteMaestros paquete)
    {
        IEnumerable<(TablaMaestro, object, string)> De<T>(TablaMaestro tabla, IReadOnlyList<T>? lista, Func<T, string> etiqueta) where T : class =>
            (lista ?? []).Select(d => (tabla, (object)d, etiqueta(d)));

        return De(TablasMaestros.Monedas, paquete.Monedas, d => $"Moneda '{d.Codigo}'")
            .Concat(De(TablasMaestros.Departamentos, paquete.Departamentos, d => $"Departamento {d.Codigo}"))
            .Concat(De(TablasMaestros.Categorias, paquete.Categorias, d => $"Categoría {d.Codigo}"))
            .Concat(De(TablasMaestros.Marcas, paquete.Marcas, d => $"Marca {d.Codigo}"))
            .Concat(De(TablasMaestros.UnidadesMedida, paquete.UnidadesMedida, d => $"Unidad de medida {d.Codigo}"))
            .Concat(De(TablasMaestros.Impuestos, paquete.Impuestos, d => $"Impuesto '{d.Codigo}'"))
            .Concat(De(TablasMaestros.Articulos, paquete.Articulos, d => $"Artículo '{d.Codigo}'"))
            .Concat(De(TablasMaestros.Clientes, paquete.Clientes, d => $"Cliente '{d.Codigo}'"))
            .Concat(De(TablasMaestros.FormasPago, paquete.FormasPago, d => $"Forma de pago '{d.Codigo}'"))
            .Concat(De(TablasMaestros.Bancos, paquete.Bancos, d => $"Banco '{d.Codigo}'"))
            .Concat(De(TablasMaestros.TiposTarjeta, paquete.TiposTarjeta, d => $"Tipo de tarjeta {d.Codigo}"))
            .Concat(De(TablasMaestros.Denominaciones, paquete.Denominaciones, d => $"Denominación {d.Moneda} {d.Valor}"))
            .Concat(De(TablasMaestros.Promociones, paquete.Promociones, d => $"Promoción '{d.Codigo}'"))
            .Concat(De(TablasMaestros.MotivosDescuento, paquete.MotivosDescuento, d => $"Motivo de descuento {d.Codigo}"))
            .Concat(De(TablasMaestros.TopesDescuento, paquete.TopesDescuento, d => $"Tope de descuento {d.Codigo}"))
            .Concat(De(TablasMaestros.TasasCambio, paquete.TasasCambio, d => $"Tasa de cambio {d.Moneda}"))
            .Concat(De(TablasMaestros.SecuenciasEcf, paquete.SecuenciasEcf, EtiquetaRango))
            .Concat(De(TablasMaestros.MotivosDevolucion, paquete.MotivosDevolucion, d => $"Motivo de devolución {d.Codigo}"))
            .Concat(De(TablasMaestros.NivelesFidelidad, paquete.NivelesFidelidad, d => $"Nivel de fidelidad {d.Codigo}"))
            .Concat(De(TablasMaestros.ReglasAcumulacion, paquete.ReglasAcumulacion, d => $"Regla de acumulación {d.Codigo}"))
            .Concat(De(TablasMaestros.MiembrosFidelidad, paquete.MiembrosFidelidad, d => $"Miembro de fidelidad '{d.Cedula}'"))
            .Concat(De(TablasMaestros.DescuentosTarjeta, paquete.DescuentosTarjeta, d => $"Descuento por tarjeta '{d.Codigo}'"));
    }

    private async Task<(int Publicados, int SinCambios)> AplicarAsync(IEnumerable<(TablaMaestro Tabla, object Dato, string Etiqueta)> registros,
        ResolutorCodigosCentral resolutor, OpcionesPublicacion opciones, List<string> errores, CancellationToken cancelacion)
    {
        var publicados = 0;
        var sinCambios = 0;
        foreach (var (tabla, dato, etiqueta) in registros)
        {
            try
            {
                if (await tabla.AplicarAsync(contexto, dato, resolutor, opciones, cancelacion)) publicados++; else sinCambios++;
            }
            catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
            {
                errores.Add($"{etiqueta}: {ValidacionMaestros.MensajeError(excepcion)}");
            }
        }

        return (publicados, sinCambios);
    }

    private async Task GuardarAsync(CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion)
        {
            contexto.ChangeTracker.Clear();
            throw new PublicacionInvalidaExcepcion([$"No se pudo guardar: {excepcion.InnerException?.Message ?? excepcion.Message}"]);
        }
    }

    /// <returns><c>true</c> si el parámetro es nuevo o cambió de valor.</returns>
    private async Task<bool> AplicarParametroAsync(ParametroCarga dato, ResolutorCodigosCentral resolutor, CancellationToken cancelacion)
    {
        int? sucursalId = null;
        int? cajaId = null;
        if (dato is { SucursalCodigo: { } s, CajaCodigo: { } c })
            cajaId = resolutor.Caja(s, c);
        else if (dato.CajaCodigo is not null)
            throw new ArgumentException("Un parámetro de caja debe indicar también la sucursal de la caja.");
        else if (dato.SucursalCodigo is { } sucursal)
            sucursalId = resolutor.Sucursal(sucursal);

        var clave = dato.Clave.Trim();
        var parametro = await contexto.Parametros.SingleOrDefaultAsync(p => p.Clave == clave && p.SucursalId == sucursalId && p.CajaId == cajaId, cancelacion);
        if (parametro is null)
        {
            contexto.Parametros.Add(Parametro.Crear(dato.Clave, dato.Valor, dato.Descripcion, sucursalId, cajaId));
            return true;
        }

        if (parametro.Valor == dato.Valor)
            return false;

        parametro.CambiarValor(dato.Valor);
        return true;
    }

    private static string EtiquetaRango(SecuenciaEcfCarga rango) => $"Rango de e-CF E{(int)rango.TipoComprobante} {rango.Desde}–{rango.Hasta}";

    private static void ValidarRepetidos(PaqueteMaestros paquete, List<string> errores)
    {
        void Repetidos<T>(IEnumerable<T> llaves, string nombre)
        {
            foreach (var repetido in llaves.GroupBy(l => l).Where(g => g.Count() > 1))
                errores.Add($"{nombre} {repetido.Key} repetido en el paquete.");
        }

        Repetidos((paquete.Articulos ?? []).Select(a => a.Codigo?.Trim()), "Artículo");
        Repetidos((paquete.Clientes ?? []).Select(a => a.Codigo?.Trim().ToUpperInvariant()), "Cliente");
        Repetidos((paquete.Departamentos ?? []).Select(a => a.Codigo), "Departamento");
        Repetidos((paquete.Categorias ?? []).Select(a => a.Codigo), "Categoría");
        Repetidos((paquete.Marcas ?? []).Select(a => a.Codigo), "Marca");
        Repetidos((paquete.UnidadesMedida ?? []).Select(a => a.Codigo), "Unidad de medida");
        Repetidos((paquete.Promociones ?? []).Select(a => a.Codigo?.Trim().ToUpperInvariant()), "Promoción");
        Repetidos((paquete.TopesDescuento ?? []).Select(a => a.Codigo), "Tope de descuento");
        Repetidos((paquete.SecuenciasEcf ?? []).Select(a => $"E{(int)a.TipoComprobante} desde {a.Desde}"), "Rango de e-CF");
    }

    /// <summary>El código interno, los de barras y los de proveedor identifican a un solo artículo en toda la empresa: la caja busca por cualquiera.</summary>
    private async Task ValidarCodigosArticulosAsync(IReadOnlyList<ArticuloCarga> articulos, List<string> errores, CancellationToken cancelacion)
    {
        if (articulos.Count == 0)
            return;

        IEnumerable<string> Codigos(ArticuloCarga articulo) =>
            new[] { articulo.Codigo }.Concat(articulo.CodigosBarras ?? []).Concat(articulo.CodigosProveedor ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase);

        var duenoPorCodigo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var articulo in articulos)
            foreach (var codigo in Codigos(articulo))
            {
                if (duenoPorCodigo.TryGetValue(codigo, out var otro) && otro != articulo.Codigo.Trim())
                    errores.Add($"El código '{codigo}' está en los artículos '{otro}' y '{articulo.Codigo}'.");
                duenoPorCodigo[codigo] = articulo.Codigo.Trim();
            }

        foreach (var bloque in duenoPorCodigo.Keys.Chunk(500))
        {
            var codigos = bloque.ToList();
            var internos = await contexto.Articulos.AsNoTracking().Where(a => codigos.Contains(a.Codigo)).Select(a => new { Codigo = a.Codigo, Articulo = a.Codigo })
                .ToListAsync(cancelacion);
            var secundarios = await contexto.Set<CodigoArticulo>().AsNoTracking().Where(c => codigos.Contains(c.Codigo))
                .Join(contexto.Articulos, c => c.ArticuloId, a => a.Id, (c, a) => new { c.Codigo, Articulo = a.Codigo })
                .ToListAsync(cancelacion);

            foreach (var usado in internos.Concat(secundarios).Where(u => !string.Equals(duenoPorCodigo[u.Codigo], u.Articulo, StringComparison.Ordinal)))
                errores.Add($"El código '{usado.Codigo}' ya lo usa el artículo '{usado.Articulo}' (como código interno, de barras o de proveedor); no puede estar también en '{duenoPorCodigo[usado.Codigo]}'.");
        }
    }

    /// <summary>Formas de pago, denominaciones y tasas solo pueden usar monedas publicadas (las del paquete o las ya guardadas).</summary>
    private async Task ValidarMonedasAsync(PaqueteMaestros paquete, List<string> errores, CancellationToken cancelacion)
    {
        var usanMoneda = (paquete.FormasPago ?? []).Select(f => (f.Moneda, $"La forma de pago '{f.Codigo}'"))
            .Concat((paquete.Denominaciones ?? []).Select(d => (d.Moneda, $"La denominación {d.Valor}")))
            .Concat((paquete.TasasCambio ?? []).Select(t => (t.Moneda, "La tasa de cambio")))
            .ToList();
        if (usanMoneda.Count == 0)
            return;

        var monedas = (paquete.Monedas ?? []).Select(m => m.Codigo.Trim().ToUpperInvariant()).ToHashSet();
        monedas.UnionWith(await contexto.Monedas.AsNoTracking().Select(m => m.Codigo).ToListAsync(cancelacion));
        foreach (var (moneda, referencia) in usanMoneda.Where(u => !monedas.Contains(u.Moneda?.Trim().ToUpperInvariant() ?? string.Empty)))
            errores.Add($"{referencia} usa la moneda '{moneda}', que no está publicada.");
    }

    /// <summary>Reglas entre registros que se validan con todo ya aplicado (lo del paquete junto con lo guardado).</summary>
    private async Task ValidarDespuesDeAplicarAsync(PaqueteMaestros paquete, List<string> errores, CancellationToken cancelacion)
    {
        // La categoría de un artículo es de su departamento (también si se mueve la categoría a otro departamento).
        if (paquete.Articulos is { Count: > 0 } || paquete.Categorias is { Count: > 0 })
        {
            var categorias = (await contexto.Categorias.AsNoTracking().Select(c => new { c.Id, c.DepartamentoId }).ToListAsync(cancelacion))
                .ToDictionary(c => c.Id, c => c.DepartamentoId);
            foreach (var categoria in contexto.Categorias.Local)
                categorias[categoria.Id] = categoria.DepartamentoId;

            foreach (var articulo in contexto.Articulos.Local.Where(a => a.CategoriaId is { } id && categorias.GetValueOrDefault(id) != a.DepartamentoId))
                errores.Add($"El artículo '{articulo.Codigo}' tiene una categoría que no es de su departamento.");

            var movidas = contexto.Categorias.Local.Select(c => (c.Id, c.DepartamentoId)).ToList();
            var locales = contexto.Articulos.Local.Select(a => a.Id).ToHashSet();
            foreach (var (id, departamentoId) in movidas)
                foreach (var codigo in await contexto.Articulos.AsNoTracking().Where(a => a.CategoriaId == id && a.DepartamentoId != departamentoId && !locales.Contains(a.Id))
                             .Select(a => a.Codigo).Take(5).ToListAsync(cancelacion))
                    errores.Add($"El artículo '{codigo}' tiene esa categoría en otro departamento; cámbielo antes de mover la categoría.");
        }

        // La caja rechaza reducir un rango por debajo de lo que pudo emitir, y un e-NCF es único: los rangos del mismo tipo no se solapan.
        foreach (var secuencia in contexto.SecuenciasEcf.Local)
        {
            var entrada = contexto.Entry(secuencia);
            if (entrada.State == EntityState.Modified && secuencia.Hasta < (long)entrada.Property(nameof(Dominio.Fiscal.SecuenciaEcf.Hasta)).OriginalValue!)
                errores.Add($"El rango de e-CF E{(int)secuencia.TipoComprobante} {secuencia.Desde} no puede reducirse; la caja pudo haber emitido hasta su final.");

            // Se compara contra toda la empresa, no contra la caja: lo normal es repartir un rango entre varias (1–5 a la
            // uno, 6–10 a la dos), y el choque que importa es justo el de dos cajas con los mismos números.
            var solapado = await contexto.SecuenciasEcf.AsNoTracking()
                .Where(s => s.Id != secuencia.Id && s.TipoComprobante == secuencia.TipoComprobante && s.Desde <= secuencia.Hasta && s.Hasta >= secuencia.Desde)
                .Select(s => new { s.Desde, s.Hasta, s.CajaId })
                .FirstOrDefaultAsync(cancelacion);
            var solapadoLocal = contexto.SecuenciasEcf.Local.FirstOrDefault(s => s.Id != secuencia.Id && s.TipoComprobante == secuencia.TipoComprobante
                                                                                  && s.Desde <= secuencia.Hasta && s.Hasta >= secuencia.Desde);
            if (solapado is not null || solapadoLocal is not null)
            {
                var (otroDesde, otroHasta, otraCaja) = solapado is not null
                    ? (solapado.Desde, solapado.Hasta, solapado.CajaId)
                    : (solapadoLocal!.Desde, solapadoLocal.Hasta, solapadoLocal.CajaId);
                var codigoCaja = await contexto.Cajas.AsNoTracking().Where(c => c.Id == otraCaja)
                    .Join(contexto.Sucursales.AsNoTracking(), c => c.SucursalId, s => s.Id, (c, s) => $"{s.Codigo}-{c.Codigo}")
                    .FirstOrDefaultAsync(cancelacion);

                errores.Add($"Los rangos de e-CF E{(int)secuencia.TipoComprobante} {secuencia.Desde}–{secuencia.Hasta} y {otroDesde}–{otroHasta}"
                            + (codigoCaja is null ? string.Empty : $" (caja {codigoCaja})") + " se solapan.");
            }
        }

        // Un documento de identidad es de un solo cliente: la caja busca al cliente por él.
        foreach (var cliente in contexto.Clientes.Local)
        {
            var otro = contexto.Clientes.Local.FirstOrDefault(c => c.Id != cliente.Id && c.TipoDocumento == cliente.TipoDocumento && c.Documento == cliente.Documento)?.Codigo
                       ?? await contexto.Clientes.AsNoTracking()
                           .Where(c => c.Id != cliente.Id && c.TipoDocumento == cliente.TipoDocumento && c.Documento == cliente.Documento)
                           .Select(c => c.Codigo).FirstOrDefaultAsync(cancelacion);
            if (otro is not null)
                errores.Add($"El documento '{cliente.Documento}' del cliente '{cliente.Codigo}' ya existe en el cliente '{otro}'.");
        }

        // Dentro de un alcance, la caja toma el tope del nivel del autorizador: dos topes del mismo nivel y alcance serían ambiguos.
        foreach (var tope in contexto.TopesDescuento.Local)
        {
            var repetido = contexto.TopesDescuento.Local.Any(t => t.Id != tope.Id && MismoAlcance(t, tope))
                           || await contexto.TopesDescuento.AsNoTracking().AnyAsync(t => t.Id != tope.Id && t.Nivel == tope.Nivel && t.DepartamentoId == tope.DepartamentoId
                               && t.ArticuloId == tope.ArticuloId && t.CategoriaId == tope.CategoriaId && t.MarcaId == tope.MarcaId, cancelacion);
            if (repetido)
            {
                var alcance = tope.ArticuloId is not null ? "ese artículo"
                    : tope.CategoriaId is not null ? "esa categoría"
                    : tope.MarcaId is not null ? "esa marca"
                    : tope.DepartamentoId is not null ? "ese departamento"
                    : "el alcance general";
                errores.Add($"Ya hay un tope de descuento de nivel {tope.Nivel} para {alcance}; cambie ese tope.");
            }
        }
    }

    private static bool MismoAlcance(Dominio.Promociones.TopeDescuento a, Dominio.Promociones.TopeDescuento b) =>
        a.Nivel == b.Nivel && a.DepartamentoId == b.DepartamentoId && a.ArticuloId == b.ArticuloId && a.CategoriaId == b.CategoriaId && a.MarcaId == b.MarcaId;
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

        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarAsync(await SoloNuevosAsync(contexto, paquete, cancelacion), "Carga inicial", cancelacion);
    }

    /// <summary>Publica roles, usuarios y parámetros de un archivo de carga inicial de caja; como los maestros, solo lo que no existe.</summary>
    public static async Task PublicarSeguridadCajasDesdeArchivoAsync(this IServiceProvider servicios, string ruta, CancellationToken cancelacion = default)
    {
        var paquete = await LeerAsync<PaqueteCargaInicial>(ruta, cancelacion);
        await using var ambito = servicios.CreateAsyncScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<ContextoDatosCentral>();

        var roles = await contexto.RolesCaja.Select(r => r.Codigo).ToListAsync(cancelacion);
        var usuarios = await contexto.UsuariosCaja.Select(u => u.Codigo).ToListAsync(cancelacion);
        var parametros = await contexto.Parametros.Select(p => new { p.Clave, p.SucursalId, p.CajaId }).ToListAsync(cancelacion);
        var resolutor = new ResolutorCodigosCentral(contexto);
        await resolutor.PrepararAsync(cancelacion);

        bool ParametroExiste(ParametroCarga dato)
        {
            try
            {
                var cajaId = dato is { SucursalCodigo: { } s, CajaCodigo: { } c } ? resolutor.Caja(s, c) : (int?)null;
                var sucursalId = cajaId is null && dato.SucursalCodigo is { } sucursal ? resolutor.Sucursal(sucursal) : (int?)null;
                return parametros.Any(p => p.Clave == dato.Clave.Trim() && p.SucursalId == sucursalId && p.CajaId == cajaId);
            }
            catch (InvalidOperationException)
            {
                // La sucursal o la caja no existen: el publicador informa el error.
                return false;
            }
        }

        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarSeguridadCajasAsync(
            (paquete.Roles ?? []).Where(r => !roles.Contains(r.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)).ToList(),
            (paquete.Usuarios ?? []).Where(u => !usuarios.Contains(u.Codigo.Trim(), StringComparer.OrdinalIgnoreCase)).ToList(),
            (paquete.Parametros ?? []).Where(p => !ParametroExiste(p)).ToList(),
            "Carga inicial", cancelacion);
    }

    /// <summary>Copia del paquete sin los registros que ya existen (mismo código o llave natural).</summary>
    internal static async Task<PaqueteMaestros> SoloNuevosAsync(ContextoDatosCentral contexto, PaqueteMaestros paquete, CancellationToken cancelacion)
    {
        async Task<List<T>?> Nuevos<TEntidad, T>(TablaMaestro<TEntidad, T> tabla, IReadOnlyList<T>? lista)
            where TEntidad : Dominio.Comun.Entidad where T : class
        {
            if (lista is null)
                return null;

            var nuevos = new List<T>();
            foreach (var dato in lista)
                if (await tabla.BuscarAsync(contexto, dato, cancelacion) is null)
                    nuevos.Add(dato);
            return nuevos;
        }

        return new PaqueteMaestros(
            await Nuevos(TablasMaestros.Departamentos, paquete.Departamentos),
            await Nuevos(TablasMaestros.UnidadesMedida, paquete.UnidadesMedida),
            await Nuevos(TablasMaestros.Impuestos, paquete.Impuestos),
            await Nuevos(TablasMaestros.Articulos, paquete.Articulos),
            await Nuevos(TablasMaestros.Clientes, paquete.Clientes),
            await Nuevos(TablasMaestros.FormasPago, paquete.FormasPago),
            await Nuevos(TablasMaestros.Bancos, paquete.Bancos),
            await Nuevos(TablasMaestros.TiposTarjeta, paquete.TiposTarjeta),
            await Nuevos(TablasMaestros.Denominaciones, paquete.Denominaciones),
            await Nuevos(TablasMaestros.Promociones, paquete.Promociones),
            await Nuevos(TablasMaestros.MotivosDescuento, paquete.MotivosDescuento),
            await Nuevos(TablasMaestros.TopesDescuento, paquete.TopesDescuento),
            await Nuevos(TablasMaestros.TasasCambio, paquete.TasasCambio),
            await Nuevos(TablasMaestros.SecuenciasEcf, paquete.SecuenciasEcf),
            await Nuevos(TablasMaestros.MotivosDevolucion, paquete.MotivosDevolucion),
            await Nuevos(TablasMaestros.Monedas, paquete.Monedas),
            await Nuevos(TablasMaestros.NivelesFidelidad, paquete.NivelesFidelidad),
            await Nuevos(TablasMaestros.ReglasAcumulacion, paquete.ReglasAcumulacion),
            await Nuevos(TablasMaestros.MiembrosFidelidad, paquete.MiembrosFidelidad),
            await Nuevos(TablasMaestros.DescuentosTarjeta, paquete.DescuentosTarjeta),
            await Nuevos(TablasMaestros.Categorias, paquete.Categorias),
            await Nuevos(TablasMaestros.Marcas, paquete.Marcas));
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
