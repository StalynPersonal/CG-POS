using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Sincronizacion;
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

    public async Task<ResultadoPublicacion> PublicarAsync(PaqueteMaestros paquete, string usuario, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuario);

        var errores = ValidacionMaestros.Validar(paquete).ToList();
        var filas = FormatoMaestros.Desglosar(paquete).ToList();
        var existentes = await CargarExistentesAsync(filas.Select(f => f.Tipo), cancelacion);

        ValidarUnicos(filas, existentes.Values, errores);
        await ValidarReferenciasAsync(paquete, existentes.Values, errores, cancelacion);
        if (errores.Count > 0)
            throw new PublicacionInvalidaExcepcion(errores);

        var resultado = Guardar(filas, existentes, usuario);
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
                errores.Add($"Rol de caja '{rol.Codigo}': {excepcion.Message}");
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
                errores.Add($"{etiqueta}: {excepcion.Message}");
                continue;
            }

            if (!idsRoles.Contains(dato.RolId))
                errores.Add($"{etiqueta} referencia un rol inexistente ({dato.RolId}).");
            foreach (var cajaId in (dato.Cajas ?? []).Where(id => !idsCajas.Contains(id)))
                errores.Add($"{etiqueta} referencia una caja inexistente ({cajaId}).");

            var anterior = existentes.TryGetValue((TipoMaestro.UsuarioCaja, dato.Id), out var fila) ? FormatoMaestros.Leer<UsuarioCarga>(fila) : null;
            if (anterior is not null && anterior.Codigo != dato.Codigo.Trim())
                errores.Add($"{etiqueta}: el código del usuario no se puede cambiar.");
            var pinHash = dato.PinHash;
            if (dato.Pin is not null)
            {
                if (!HashCredencialesCaja.EsPinValido(dato.Pin))
                    errores.Add($"{etiqueta} tiene un PIN inválido (debe tener entre 4 y 8 dígitos).");
                else
                    pinHash = anterior?.PinHash is { } hashAnterior && HashCredencialesCaja.VerificarPin(dato.Pin, hashAnterior)
                        ? hashAnterior
                        : HashCredencialesCaja.HashPin(dato.Pin);
            }

            pinHash ??= anterior?.PinHash;
            if (pinHash is null)
                errores.Add($"{etiqueta} es nuevo y no tiene PIN.");

            var barras = dato.CredencialBarrasHash ?? (dato.CredencialBarras is null ? null : HashCredencialesCaja.HashCredencialBarras(dato.CredencialBarras));
            filas.Add(new FilaMaestro(TipoMaestro.UsuarioCaja, dato.Id, dato.Codigo, null,
                dato with { Pin = null, PinHash = pinHash, CredencialBarras = null, CredencialBarrasHash = barras }));
        }

        // Un carné identifica a un solo usuario: la caja tiene un índice único sobre su hash.
        var usuariosPublicados = filas.Where(f => f.Tipo == TipoMaestro.UsuarioCaja).Select(f => (UsuarioCarga)f.Dato).ToList();
        var idsPublicados = usuariosPublicados.Select(u => u.Id).ToHashSet();
        foreach (var repetido in existentes.Values
                     .Where(m => m.Tipo == TipoMaestro.UsuarioCaja && !idsPublicados.Contains(m.Id))
                     .Select(FormatoMaestros.Leer<UsuarioCarga>)
                     .Concat(usuariosPublicados)
                     .Where(u => u.CredencialBarrasHash is not null)
                     .GroupBy(u => u.CredencialBarrasHash, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
            errores.Add($"El carné está asignado a más de un usuario de caja ({string.Join(", ", repetido.Select(u => u.Codigo))}).");

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
            var resultado = Guardar(filas, existentes, usuario);
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
            throw new PublicacionInvalidaExcepcion([excepcion.Message]);
        }
    }

    private async Task<Dictionary<(TipoMaestro Tipo, Guid Id), MaestroCentral>> CargarExistentesAsync(IEnumerable<TipoMaestro> tipos, CancellationToken cancelacion)
    {
        var lista = tipos.Distinct().ToList();
        return lista.Count == 0
            ? []
            : await contexto.MaestrosCentral.Where(m => lista.Contains(m.Tipo)).ToDictionaryAsync(m => (m.Tipo, m.Id), cancelacion);
    }

    private ResultadoPublicacion Guardar(IEnumerable<FilaMaestro> filas, Dictionary<(TipoMaestro Tipo, Guid Id), MaestroCentral> existentes, string usuario)
    {
        var ahora = reloj.GetUtcNow();
        var publicados = 0;
        var sinCambios = 0;

        foreach (var fila in filas)
        {
            var contenido = fila.Contenido();
            if (existentes.TryGetValue((fila.Tipo, fila.Id), out var maestro))
            {
                if (maestro.Actualizar(fila.Codigo, fila.CajaId, contenido, ahora, usuario)) publicados++; else sinCambios++;
                continue;
            }

            maestro = MaestroCentral.Publicar(fila.Tipo, fila.Id, fila.Codigo, fila.CajaId, contenido, ahora, usuario);
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
                : await contexto.MaestrosCentral.Where(m => m.Tipo == tipo).Select(m => m.Id).ToListAsync(cancelacion));
            return ids;
        }

        if (paquete.Articulos is { Count: > 0 } articulos)
        {
            var familias = await IdsAsync(TipoMaestro.Familia, (paquete.Familias ?? []).Select(f => f.Id));
            var unidades = await IdsAsync(TipoMaestro.UnidadMedida, (paquete.UnidadesMedida ?? []).Select(u => u.Id));
            var impuestos = await IdsAsync(TipoMaestro.Impuesto, (paquete.Impuestos ?? []).Select(i => i.Id));

            foreach (var articulo in articulos)
            {
                var etiqueta = $"El artículo '{articulo.Codigo}'";
                if (!familias.Contains(articulo.FamiliaId)) errores.Add($"{etiqueta} referencia una familia inexistente ({articulo.FamiliaId}).");
                if (!unidades.Contains(articulo.UnidadMedidaId)) errores.Add($"{etiqueta} referencia una unidad de medida inexistente ({articulo.UnidadMedidaId}).");
                if (!impuestos.Contains(articulo.ImpuestoId)) errores.Add($"{etiqueta} referencia un impuesto inexistente ({articulo.ImpuestoId}).");
            }

            // Un código de barras o de proveedor identifica a un solo artículo en toda la empresa.
            var idsDelPaquete = articulos.Select(a => a.Id).ToHashSet();
            var duenoPorCodigo = new Dictionary<string, (Guid Id, string Articulo)>(StringComparer.OrdinalIgnoreCase);
            foreach (var publicado in publicados.Where(m => m.Tipo == TipoMaestro.Articulo && !idsDelPaquete.Contains(m.Id)).Select(FormatoMaestros.Leer<ArticuloCarga>))
                foreach (var codigo in (publicado.CodigosBarras ?? []).Concat(publicado.CodigosProveedor ?? []))
                    duenoPorCodigo[codigo.Trim()] = (publicado.Id, publicado.Codigo);

            foreach (var articulo in articulos)
                foreach (var codigo in (articulo.CodigosBarras ?? []).Concat(articulo.CodigosProveedor ?? []).Select(c => c.Trim()))
                {
                    if (duenoPorCodigo.TryGetValue(codigo, out var dueno) && dueno.Id != articulo.Id)
                        errores.Add($"El código '{codigo}' está en los artículos '{dueno.Articulo}' y '{articulo.Codigo}'.");
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
    /// <summary>Publica un archivo de maestros (formato de la carga de la caja). Es idempotente: lo que no cambió no baja otra vez.</summary>
    public static async Task PublicarMaestrosDesdeArchivoAsync(this IServiceProvider servicios, string ruta, CancellationToken cancelacion = default)
    {
        var paquete = await LeerAsync<PaqueteMaestros>(ruta, cancelacion);
        await using var ambito = servicios.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>().PublicarAsync(paquete, "Carga inicial", cancelacion);
    }

    /// <summary>Publica roles, usuarios y parámetros de un archivo de carga inicial de caja.</summary>
    public static async Task PublicarSeguridadCajasDesdeArchivoAsync(this IServiceProvider servicios, string ruta, CancellationToken cancelacion = default)
    {
        var paquete = await LeerAsync<PaqueteCargaInicial>(ruta, cancelacion);
        await using var ambito = servicios.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<IPublicadorMaestros>()
            .PublicarSeguridadCajasAsync(paquete.Roles ?? [], paquete.Usuarios ?? [], paquete.Parametros ?? [], "Carga inicial", cancelacion);
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
