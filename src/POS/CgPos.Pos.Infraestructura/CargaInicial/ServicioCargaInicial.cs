using System.Text.Json;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.CargaInicial;

internal sealed class ServicioCargaInicial(
    ContextoDatosPos contexto,
    IHashCredenciales hashCredenciales,
    IAuditoria auditoria,
    IConfiguracionCaja configuracionCaja,
    ILogger<ServicioCargaInicial> logger) : ICargaInicial
{
    private const string TodosLosPermisos = "*";

    private int _creados;
    private int _actualizados;

    public async Task<ResultadoCargaInicial> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo de carga inicial: {ruta}", ruta);

        PaqueteCargaInicial? paquete;
        try
        {
            await using var archivo = File.OpenRead(ruta);
            paquete = await JsonSerializer.DeserializeAsync<PaqueteCargaInicial>(archivo, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException excepcion)
        {
            throw new CargaInicialInvalidaExcepcion([$"JSON inválido en {Path.GetFileName(ruta)}: {excepcion.Message}"]);
        }

        return await AplicarAsync(paquete ?? throw new CargaInicialInvalidaExcepcion(["El archivo está vacío."]), cancelacion);
    }

    public async Task<ResultadoCargaInicial> AplicarAsync(PaqueteCargaInicial paquete, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        _creados = 0;
        _actualizados = 0;

        var sucursales = paquete.Sucursales ?? [];
        var cajas = paquete.Cajas ?? [];
        var roles = paquete.Roles ?? [];
        var usuarios = paquete.Usuarios ?? [];
        var parametros = paquete.Parametros ?? [];

        // La caja solo guarda lo suyo: su sucursal y su terminal. De las demás no necesita nada, y lo que haga falta de otra
        // sucursal o caja (una factura para una devolución, por ejemplo) se le pregunta al Central.
        if (await configuracionCaja.ObtenerAsync(cancelacion) is { } propia)
        {
            sucursales = [.. sucursales.Where(s => EsPropia(s.Codigo, propia.SucursalCodigo))];
            cajas = [.. cajas.Where(c => EsPropia(c.SucursalCodigo, propia.SucursalCodigo) && EsPropia(c.Codigo, propia.CajaCodigo))];
            usuarios = [.. usuarios.Select(u => u.Cajas is null ? u : u with
            {
                Cajas = [.. u.Cajas.Where(c => EsPropia(c.SucursalCodigo, propia.SucursalCodigo) && EsPropia(c.CajaCodigo, propia.CajaCodigo))],
            })];
            parametros = [.. parametros.Where(p => p.SucursalCodigo is null
                || EsPropia(p.SucursalCodigo, propia.SucursalCodigo) && (p.CajaCodigo is null || EsPropia(p.CajaCodigo, propia.CajaCodigo)))];
        }

        await ValidarAsync(paquete, sucursales, cajas, roles, usuarios, parametros, cancelacion);

        try
        {
            var permisosCatalogo = await SincronizarCatalogoPermisosAsync(cancelacion);
            var empresaId = await AplicarEmpresaAsync(paquete.Empresa, cancelacion);

            // Códigos -> Id de esta caja, con lo que ya existe y lo que se crea en este paquete.
            var idsSucursales = await contexto.Sucursales.ToDictionaryAsync(x => x.Codigo, x => x.Id, cancelacion);
            foreach (var sucursal in sucursales)
                idsSucursales[sucursal.Codigo] = await AplicarSucursalAsync(sucursal, empresaId, cancelacion);

            var idsCajas = await IdsCajasAsync(contexto, cancelacion);
            foreach (var caja in cajas)
                idsCajas[(caja.SucursalCodigo, caja.Codigo)] = await AplicarCajaAsync(caja, idsSucursales, cancelacion);

            var idsRoles = await contexto.Roles.ToDictionaryAsync(x => x.Codigo, x => x.Id, StringComparer.OrdinalIgnoreCase, cancelacion);
            foreach (var rol in roles)
                idsRoles[rol.Codigo.Trim()] = await AplicarRolAsync(rol, cancelacion);

            foreach (var usuario in usuarios)
                await AplicarUsuarioAsync(usuario, idsRoles, idsCajas, cancelacion);
            foreach (var parametro in parametros)
                await AplicarParametroAsync(parametro, idsSucursales, idsCajas, cancelacion);

            var resultado = new ResultadoCargaInicial(
                sucursales.Count, cajas.Count, roles.Count, usuarios.Count, parametros.Count, _creados, _actualizados, permisosCatalogo);

            auditoria.Registrar(new EntradaAuditoria("CargaInicial.Aplicada", "CargaInicial", paquete.Empresa.Rnc, Detalle: resultado));
            await contexto.SaveChangesAsync(cancelacion);

            logger.LogInformation(
                "Carga inicial aplicada: {Creados} creados, {Actualizados} actualizados ({Sucursales} sucursales, {Cajas} cajas, {Roles} roles, {Usuarios} usuarios, {Parametros} parámetros)",
                resultado.Creados, resultado.Actualizados, resultado.Sucursales, resultado.Cajas, resultado.Roles, resultado.Usuarios, resultado.Parametros);

            return resultado;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            // Nada se guardó: se descartan los cambios en memoria para no contaminar el contexto.
            contexto.ChangeTracker.Clear();
            var detalle = excepcion is DbUpdateException { InnerException: { } interna } ? interna.Message : excepcion.Message;
            throw new CargaInicialInvalidaExcepcion([detalle]);
        }
    }

    /// <summary>Los códigos vienen de dos dígitos y se comparan tal cual, sin espacios.</summary>
    private static bool EsPropia(string? codigo, string propio) =>
        string.Equals(codigo?.Trim(), propio.Trim(), StringComparison.Ordinal);

    /// <summary>Id local de cada caja por el código de su sucursal y el suyo.</summary>
    internal static async Task<Dictionary<(string Sucursal, string Caja), int>> IdsCajasAsync(ContextoDatosPos contexto, CancellationToken cancelacion) =>
        (await contexto.Cajas.Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, c.Codigo, c.Id }).ToListAsync(cancelacion))
        .ToDictionary(c => (c.Sucursal, c.Codigo), c => c.Id);

    private async Task ValidarAsync(
        PaqueteCargaInicial paquete,
        IReadOnlyList<SucursalCarga> sucursales,
        IReadOnlyList<CajaCarga> cajas,
        IReadOnlyList<RolCarga> roles,
        IReadOnlyList<UsuarioCarga> usuarios,
        IReadOnlyList<ParametroCarga> parametros,
        CancellationToken cancelacion)
    {
        var errores = new List<string>();

        if (paquete.Empresa is null)
            throw new CargaInicialInvalidaExcepcion(["Falta la empresa."]);

        var rnc = paquete.Empresa.Rnc?.Trim();
        var otraEmpresa = await contexto.Empresas.Where(e => e.Rnc != rnc).Select(e => e.RazonSocial).FirstOrDefaultAsync(cancelacion);
        if (otraEmpresa is not null)
            errores.Add($"La caja ya pertenece a otra empresa ({otraEmpresa}).");

        Duplicados(sucursales.Select(s => s.Codigo), "Código de sucursal", errores);
        Duplicados(cajas.Select(c => $"{c.SucursalCodigo}-{c.Codigo:00}"), "Caja (sucursal-caja)", errores);
        Duplicados(roles.Select(r => r.Codigo.Trim().ToUpperInvariant()), "Código de rol", errores);
        Duplicados(usuarios.Select(u => u.Codigo.Trim().ToUpperInvariant()), "Código de usuario", errores);
        Duplicados(parametros.Select(p => $"{p.Clave.Trim()} ({p.SucursalCodigo}/{p.CajaCodigo})"), "Parámetro", errores);

        var codigosSucursales = sucursales.Select(s => s.Codigo).ToHashSet();
        codigosSucursales.UnionWith(await contexto.Sucursales.Select(s => s.Codigo).ToListAsync(cancelacion));
        var codigosCajas = cajas.Select(c => (c.SucursalCodigo, c.Codigo)).ToHashSet();
        codigosCajas.UnionWith((await IdsCajasAsync(contexto, cancelacion)).Keys);
        var codigosRoles = roles.Select(r => r.Codigo.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        codigosRoles.UnionWith(await contexto.Roles.Select(r => r.Codigo).ToListAsync(cancelacion));

        foreach (var caja in cajas.Where(c => !codigosSucursales.Contains(c.SucursalCodigo)))
            errores.Add($"La caja {caja.Codigo} referencia una sucursal inexistente ({caja.SucursalCodigo}).");

        foreach (var rol in roles)
            foreach (var codigo in (rol.Permisos ?? []).Where(p => p != TodosLosPermisos && !CatalogoPermisos.Existe(p)))
                errores.Add($"El rol '{rol.Codigo}' tiene un permiso inexistente: '{codigo}'.");

        var codigosUsuariosExistentes = (await contexto.Usuarios.Select(u => u.Codigo).ToListAsync(cancelacion)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var usuario in usuarios)
        {
            var etiqueta = $"El usuario '{usuario.Codigo}'";
            if (!codigosRoles.Contains(usuario.RolCodigo?.Trim() ?? string.Empty))
                errores.Add($"{etiqueta} referencia un rol inexistente ({usuario.RolCodigo}).");
            foreach (var caja in (usuario.Cajas ?? []).Where(c => !codigosCajas.Contains((c.SucursalCodigo, c.CajaCodigo))))
                errores.Add($"{etiqueta} referencia una caja inexistente ({caja.SucursalCodigo}-{caja.CajaCodigo}).");

            if (usuario.Clave is not null && usuario.ClaveHash is not null)
                errores.Add($"{etiqueta} trae 'clave' y 'claveHash'; use solo uno.");
            if (usuario.Clave is { Length: 0 })
                errores.Add($"{etiqueta} tiene la clave vacía.");
            if (usuario.ClaveHash is not null && !hashCredenciales.EsHashClaveReconocido(usuario.ClaveHash))
                errores.Add($"{etiqueta} tiene un 'claveHash' con formato no reconocido.");
            if (usuario.Clave is null && usuario.ClaveHash is null && !codigosUsuariosExistentes.Contains(usuario.Codigo.Trim()))
                errores.Add($"{etiqueta} es nuevo y no tiene clave.");
        }

        foreach (var parametro in parametros)
        {
            if (parametro.SucursalCodigo is { } sucursal && !codigosSucursales.Contains(sucursal))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una sucursal inexistente ({sucursal}).");
            if (parametro.CajaCodigo is not null && parametro.SucursalCodigo is null)
                errores.Add($"El parámetro '{parametro.Clave}' de caja debe indicar también la sucursal de la caja.");
            if (parametro is { SucursalCodigo: { } s, CajaCodigo: { } c } && !codigosCajas.Contains((s, c)))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una caja inexistente ({s:00}-{c:00}).");
        }

        if (errores.Count > 0)
            throw new CargaInicialInvalidaExcepcion(errores);
    }

    private static void Duplicados<T>(IEnumerable<T> valores, string campo, List<string> errores)
    {
        foreach (var repetido in valores.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key))
            errores.Add($"{campo} repetido en el paquete: {repetido}.");
    }

    private async Task<int> SincronizarCatalogoPermisosAsync(CancellationToken cancelacion)
    {
        var existentes = await contexto.Permisos.ToDictionaryAsync(p => p.Codigo, cancelacion);

        foreach (var definicion in CatalogoPermisos.Todos)
        {
            if (existentes.TryGetValue(definicion.Codigo, out var permiso))
                permiso.Actualizar(definicion);
            else
                contexto.Permisos.Add(Permiso.Crear(definicion));
        }

        // Permisos retirados del catálogo: se quitan de los roles y de la tabla.
        var obsoletos = existentes.Keys.Where(codigo => !CatalogoPermisos.Existe(codigo)).ToList();
        if (obsoletos.Count > 0)
        {
            contexto.RemoveRange(await contexto.Set<RolPermiso>().Where(rp => obsoletos.Contains(rp.PermisoCodigo)).ToListAsync(cancelacion));
            contexto.Permisos.RemoveRange(obsoletos.Select(codigo => existentes[codigo]));
            logger.LogWarning("Permisos retirados del catálogo: {Permisos}", string.Join(", ", obsoletos));
        }

        return CatalogoPermisos.Todos.Count;
    }

    /// <summary>La caja pertenece a una sola empresa, identificada por su RNC.</summary>
    private async Task<int> AplicarEmpresaAsync(EmpresaCarga dato, CancellationToken cancelacion)
    {
        var empresa = await contexto.Empresas.SingleOrDefaultAsync(cancelacion);
        if (empresa is null)
        {
            empresa = Empresa.Crear(dato.Rnc, dato.RazonSocial, dato.NombreComercial, dato.Direccion, dato.Telefono);
            contexto.Empresas.Add(empresa);
            _creados++;
            return empresa.Id;
        }

        if (empresa.Rnc != dato.Rnc.Trim())
            throw new InvalidOperationException($"No se puede cambiar el RNC de la empresa ({empresa.Rnc} → {dato.Rnc}).");

        empresa.ActualizarDatos(dato.RazonSocial, dato.NombreComercial, dato.Direccion, dato.Telefono);
        _actualizados++;
        return empresa.Id;
    }

    private async Task<int> AplicarSucursalAsync(SucursalCarga dato, int empresaId, CancellationToken cancelacion)
    {
        var sucursal = await contexto.Sucursales.SingleOrDefaultAsync(s => s.Codigo == dato.Codigo, cancelacion);
        if (sucursal is null)
        {
            sucursal = Sucursal.Crear(empresaId, dato.Codigo, dato.Nombre, dato.Direccion, dato.Telefono);
            contexto.Sucursales.Add(sucursal);
            _creados++;
        }
        else
        {
            sucursal.ActualizarDatos(dato.Nombre, dato.Direccion, dato.Telefono);
            _actualizados++;
        }

        if (dato.Activa) sucursal.Activar(); else sucursal.Desactivar();
        return sucursal.Id;
    }

    private async Task<int> AplicarCajaAsync(CajaCarga dato, IReadOnlyDictionary<string, int> idsSucursales, CancellationToken cancelacion)
    {
        var sucursalId = idsSucursales[dato.SucursalCodigo];
        var caja = await contexto.Cajas.SingleOrDefaultAsync(c => c.SucursalId == sucursalId && c.Codigo == dato.Codigo, cancelacion);
        if (caja is null)
        {
            caja = Caja.Crear(sucursalId, dato.Codigo, dato.Nombre, dato.DireccionIp ?? string.Empty, dato.SucursalCodigo);
            contexto.Cajas.Add(caja);
            _creados++;
        }
        else
        {
            caja.CambiarNombre(dato.Nombre);
            caja.CambiarSucursalCodigo(dato.SucursalCodigo);
            if (dato.DireccionIp is { Length: > 0 } direccion)
                caja.CambiarDireccionIp(direccion);
            _actualizados++;
        }

        if (dato.Habilitada) caja.Habilitar(); else caja.Deshabilitar();
        return caja.Id;
    }

    private async Task<int> AplicarRolAsync(RolCarga dato, CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim();
        var rol = await contexto.Roles.Include(r => r.PermisosAsignados).SingleOrDefaultAsync(r => r.Codigo == codigo, cancelacion);
        if (rol is null)
        {
            rol = Rol.Crear(dato.Codigo, dato.Nombre, dato.Nivel);
            contexto.Roles.Add(rol);
            _creados++;
        }
        else
        {
            rol.Actualizar(dato.Nombre, dato.Nivel);
            _actualizados++;
        }

        var permisos = dato.Permisos ?? [];
        var deseados = permisos.Contains(TodosLosPermisos)
            ? CatalogoPermisos.Todos.Select(p => p.Codigo).ToHashSet()
            : permisos.ToHashSet();

        foreach (var sobrante in rol.PermisosAsignados.Select(p => p.PermisoCodigo).Where(c => !deseados.Contains(c)).ToList())
            rol.QuitarPermiso(sobrante);
        foreach (var permiso in deseados)
            rol.AsignarPermiso(permiso);

        if (dato.Activo) rol.Activar(); else rol.Desactivar();
        return rol.Id;
    }

    private async Task AplicarUsuarioAsync(UsuarioCarga dato, IReadOnlyDictionary<string, int> idsRoles, IReadOnlyDictionary<(string Sucursal, string Caja), int> idsCajas,
        CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim();
        var rolId = idsRoles[dato.RolCodigo.Trim()];
        var usuario = await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.Codigo == codigo, cancelacion);
        if (usuario is null)
        {
            usuario = Usuario.Crear(dato.Codigo, dato.Nombre, rolId);
            contexto.Usuarios.Add(usuario);
            _creados++;
        }
        else
        {
            usuario.CambiarNombre(dato.Nombre);
            usuario.CambiarRol(rolId);
            _actualizados++;
        }

        if (dato.ClaveHash is not null)
        {
            if (usuario.ClaveHash != dato.ClaveHash)
                usuario.EstablecerClaveHash(dato.ClaveHash);
        }
        else if (dato.Clave is not null && (usuario.ClaveHash is null || !hashCredenciales.VerificarClave(dato.Clave, usuario.ClaveHash)))
        {
            // Solo se recalcula si la clave cambió: el hash lleva sal aleatoria.
            usuario.EstablecerClaveHash(hashCredenciales.HashClave(dato.Clave));
        }

        var cajasDeseadas = (dato.Cajas ?? []).Select(c => idsCajas[(c.SucursalCodigo, c.CajaCodigo)]).ToHashSet();
        foreach (var sobrante in usuario.CajasAsignadas.Select(c => c.CajaId).Where(id => !cajasDeseadas.Contains(id)).ToList())
            usuario.QuitarCaja(sobrante);
        foreach (var cajaId in cajasDeseadas)
            usuario.AsignarCaja(cajaId);

        if (dato.Activo) usuario.Activar(); else usuario.Desactivar();
    }

    private async Task AplicarParametroAsync(ParametroCarga dato, IReadOnlyDictionary<string, int> idsSucursales,
        IReadOnlyDictionary<(string Sucursal, string Caja), int> idsCajas, CancellationToken cancelacion)
    {
        var (sucursalId, cajaId) = AmbitoLocal(dato.SucursalCodigo, dato.CajaCodigo, idsSucursales, idsCajas);
        var clave = dato.Clave.Trim();
        var parametro = await contexto.Parametros.SingleOrDefaultAsync(p => p.Clave == clave && p.SucursalId == sucursalId && p.CajaId == cajaId, cancelacion);
        if (parametro is null)
        {
            contexto.Parametros.Add(Parametro.Crear(dato.Clave, dato.Valor, dato.Descripcion, sucursalId, cajaId));
            _creados++;
            return;
        }

        parametro.CambiarValor(dato.Valor);
        _actualizados++;
    }

    /// <summary>Sucursal y caja locales de un parámetro: el de caja se guarda solo con la caja (la sucursal va implícita en ella).</summary>
    internal static (int? SucursalId, int? CajaId) AmbitoLocal(string? sucursalCodigo, string? cajaCodigo, IReadOnlyDictionary<string, int> idsSucursales,
        IReadOnlyDictionary<(string Sucursal, string Caja), int> idsCajas) =>
        (sucursalCodigo, cajaCodigo) switch
        {
            ({ } s, { } c) => (null, idsCajas[(s, c)]),
            ({ } s, null) => (idsSucursales[s], null),
            _ => (null, null),
        };
}
