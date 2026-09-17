using System.Text.Json;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.CargaInicial;

internal sealed class ServicioCargaInicial(
    ContextoDatosPos contexto,
    IHashCredenciales hashCredenciales,
    IAuditoria auditoria,
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

        await ValidarAsync(paquete, sucursales, cajas, roles, usuarios, parametros, cancelacion);

        try
        {
            var permisosCatalogo = await SincronizarCatalogoPermisosAsync(cancelacion);
            await AplicarEmpresaAsync(paquete.Empresa, cancelacion);
            foreach (var sucursal in sucursales)
                await AplicarSucursalAsync(sucursal, paquete.Empresa.Id, cancelacion);
            foreach (var caja in cajas)
                await AplicarCajaAsync(caja, cancelacion);
            foreach (var rol in roles)
                await AplicarRolAsync(rol, cancelacion);
            foreach (var usuario in usuarios)
                await AplicarUsuarioAsync(usuario, cancelacion);
            foreach (var parametro in parametros)
                await AplicarParametroAsync(parametro, cancelacion);

            var resultado = new ResultadoCargaInicial(
                sucursales.Count, cajas.Count, roles.Count, usuarios.Count, parametros.Count, _creados, _actualizados, permisosCatalogo);

            auditoria.Registrar(new EntradaAuditoria("CargaInicial.Aplicada", "CargaInicial", paquete.Empresa.Id.ToString(), Detalle: resultado));
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

        var otraEmpresa = await contexto.Empresas.Where(e => e.Id != paquete.Empresa.Id).Select(e => e.RazonSocial).FirstOrDefaultAsync(cancelacion);
        if (otraEmpresa is not null)
            errores.Add($"La caja ya pertenece a otra empresa ({otraEmpresa}).");

        Duplicados(sucursales.Select(s => s.Id), "Id de sucursal", errores);
        Duplicados(sucursales.Select(s => s.Codigo.Trim()), "Código de sucursal", errores);
        Duplicados(cajas.Select(c => c.Id), "Id de caja", errores);
        Duplicados(cajas.Select(c => $"{c.SucursalId}/{c.Codigo.Trim()}"), "Código de caja en la sucursal", errores);
        Duplicados(roles.Select(r => r.Id), "Id de rol", errores);
        Duplicados(roles.Select(r => r.Codigo.Trim()), "Código de rol", errores);
        Duplicados(usuarios.Select(u => u.Id), "Id de usuario", errores);
        Duplicados(usuarios.Select(u => u.Codigo.Trim()), "Código de usuario", errores);
        Duplicados(parametros.Select(p => p.Id), "Id de parámetro", errores);

        var idsSucursales = sucursales.Select(s => s.Id).ToHashSet();
        idsSucursales.UnionWith(await contexto.Sucursales.Select(s => s.Id).ToListAsync(cancelacion));
        var idsCajas = cajas.Select(c => c.Id).ToHashSet();
        idsCajas.UnionWith(await contexto.Cajas.Select(c => c.Id).ToListAsync(cancelacion));
        var idsRoles = roles.Select(r => r.Id).ToHashSet();
        idsRoles.UnionWith(await contexto.Roles.Select(r => r.Id).ToListAsync(cancelacion));

        foreach (var caja in cajas.Where(c => !idsSucursales.Contains(c.SucursalId)))
            errores.Add($"La caja '{caja.Codigo}' referencia una sucursal inexistente ({caja.SucursalId}).");

        foreach (var rol in roles)
        {
            foreach (var codigo in (rol.Permisos ?? []).Where(p => p != TodosLosPermisos && !CatalogoPermisos.Existe(p)))
                errores.Add($"El rol '{rol.Codigo}' tiene un permiso inexistente: '{codigo}'.");

            var codigoRol = rol.Codigo.Trim();
            if (await contexto.Roles.AnyAsync(r => r.Codigo == codigoRol && r.Id != rol.Id, cancelacion))
                errores.Add($"El código de rol '{codigoRol}' ya existe con otro Id.");
        }

        var idsUsuariosExistentes = (await contexto.Usuarios.Select(u => u.Id).ToListAsync(cancelacion)).ToHashSet();
        foreach (var usuario in usuarios)
        {
            var etiqueta = $"El usuario '{usuario.Codigo}'";
            if (!idsRoles.Contains(usuario.RolId))
                errores.Add($"{etiqueta} referencia un rol inexistente ({usuario.RolId}).");
            foreach (var cajaId in (usuario.Cajas ?? []).Where(id => !idsCajas.Contains(id)))
                errores.Add($"{etiqueta} referencia una caja inexistente ({cajaId}).");

            if (usuario.Clave is not null && usuario.ClaveHash is not null)
                errores.Add($"{etiqueta} trae 'clave' y 'claveHash'; use solo uno.");
            if (usuario.Clave is { Length: 0 })
                errores.Add($"{etiqueta} tiene la clave vacía.");
            if (usuario.ClaveHash is not null && !hashCredenciales.EsHashClaveReconocido(usuario.ClaveHash))
                errores.Add($"{etiqueta} tiene un 'claveHash' con formato no reconocido.");
            if (usuario.Clave is null && usuario.ClaveHash is null && !idsUsuariosExistentes.Contains(usuario.Id))
                errores.Add($"{etiqueta} es nuevo y no tiene clave.");

            var codigoUsuario = usuario.Codigo.Trim();
            if (await contexto.Usuarios.AnyAsync(u => u.Codigo == codigoUsuario && u.Id != usuario.Id, cancelacion))
                errores.Add($"El código de usuario '{codigoUsuario}' ya existe con otro Id.");
        }

        foreach (var parametro in parametros)
        {
            if (parametro.SucursalId is { } sucursalId && !idsSucursales.Contains(sucursalId))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una sucursal inexistente ({sucursalId}).");
            if (parametro.CajaId is { } cajaId && !idsCajas.Contains(cajaId))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una caja inexistente ({cajaId}).");
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

    private async Task AplicarEmpresaAsync(EmpresaCarga dato, CancellationToken cancelacion)
    {
        var empresa = await contexto.Empresas.SingleOrDefaultAsync(e => e.Id == dato.Id, cancelacion);
        if (empresa is null)
        {
            contexto.Empresas.Add(Empresa.Crear(dato.Rnc, dato.RazonSocial, dato.NombreComercial, dato.Direccion, dato.Telefono, dato.Id));
            _creados++;
            return;
        }

        if (empresa.Rnc != dato.Rnc.Trim())
            throw new InvalidOperationException($"No se puede cambiar el RNC de la empresa ({empresa.Rnc} → {dato.Rnc}).");

        empresa.ActualizarDatos(dato.RazonSocial, dato.NombreComercial, dato.Direccion, dato.Telefono);
        _actualizados++;
    }

    private async Task AplicarSucursalAsync(SucursalCarga dato, Guid empresaId, CancellationToken cancelacion)
    {
        var sucursal = await contexto.Sucursales.SingleOrDefaultAsync(s => s.Id == dato.Id, cancelacion);
        if (sucursal is null)
        {
            sucursal = Sucursal.Crear(empresaId, dato.Codigo, dato.Nombre, dato.Direccion, dato.Telefono, dato.Id);
            contexto.Sucursales.Add(sucursal);
            _creados++;
        }
        else
        {
            ExigirMismoCodigo(sucursal.Codigo, dato.Codigo, "sucursal");
            sucursal.ActualizarDatos(dato.Nombre, dato.Direccion, dato.Telefono);
            _actualizados++;
        }

        if (dato.Activa) sucursal.Activar(); else sucursal.Desactivar();
    }

    private async Task AplicarCajaAsync(CajaCarga dato, CancellationToken cancelacion)
    {
        var caja = await contexto.Cajas.SingleOrDefaultAsync(c => c.Id == dato.Id, cancelacion);
        if (caja is null)
        {
            caja = Caja.Crear(dato.SucursalId, dato.Codigo, dato.Nombre, dato.Id);
            contexto.Cajas.Add(caja);
            _creados++;
        }
        else
        {
            ExigirMismoCodigo(caja.Codigo, dato.Codigo, "caja");
            if (caja.SucursalId != dato.SucursalId)
                throw new InvalidOperationException($"La caja '{caja.Codigo}' no se puede mover a otra sucursal.");
            caja.CambiarNombre(dato.Nombre);
            _actualizados++;
        }

        if (dato.Habilitada) caja.Habilitar(); else caja.Deshabilitar();
    }

    private async Task AplicarRolAsync(RolCarga dato, CancellationToken cancelacion)
    {
        var rol = await contexto.Roles.Include(r => r.PermisosAsignados).SingleOrDefaultAsync(r => r.Id == dato.Id, cancelacion);
        if (rol is null)
        {
            rol = Rol.Crear(dato.Codigo, dato.Nombre, dato.Nivel, dato.Id);
            contexto.Roles.Add(rol);
            _creados++;
        }
        else
        {
            ExigirMismoCodigo(rol.Codigo, dato.Codigo, "rol");
            rol.Actualizar(dato.Nombre, dato.Nivel);
            _actualizados++;
        }

        var permisos = dato.Permisos ?? [];
        var deseados = permisos.Contains(TodosLosPermisos)
            ? CatalogoPermisos.Todos.Select(p => p.Codigo).ToHashSet()
            : permisos.ToHashSet();

        foreach (var sobrante in rol.PermisosAsignados.Select(p => p.PermisoCodigo).Where(c => !deseados.Contains(c)).ToList())
            rol.QuitarPermiso(sobrante);
        foreach (var codigo in deseados)
            rol.AsignarPermiso(codigo);

        if (dato.Activo) rol.Activar(); else rol.Desactivar();
    }

    private async Task AplicarUsuarioAsync(UsuarioCarga dato, CancellationToken cancelacion)
    {
        var usuario = await contexto.Usuarios.Include(u => u.CajasAsignadas).SingleOrDefaultAsync(u => u.Id == dato.Id, cancelacion);
        if (usuario is null)
        {
            usuario = Usuario.Crear(dato.Codigo, dato.Nombre, dato.RolId, dato.Id);
            contexto.Usuarios.Add(usuario);
            _creados++;
        }
        else
        {
            ExigirMismoCodigo(usuario.Codigo, dato.Codigo, "usuario");
            usuario.CambiarNombre(dato.Nombre);
            usuario.CambiarRol(dato.RolId);
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

        var cajasDeseadas = (dato.Cajas ?? []).ToHashSet();
        foreach (var sobrante in usuario.CajasAsignadas.Select(c => c.CajaId).Where(id => !cajasDeseadas.Contains(id)).ToList())
            usuario.QuitarCaja(sobrante);
        foreach (var cajaId in cajasDeseadas)
            usuario.AsignarCaja(cajaId);

        if (dato.Activo) usuario.Activar(); else usuario.Desactivar();
    }

    private async Task AplicarParametroAsync(ParametroCarga dato, CancellationToken cancelacion)
    {
        var parametro = await contexto.Parametros.SingleOrDefaultAsync(p => p.Id == dato.Id, cancelacion);
        if (parametro is null)
        {
            contexto.Parametros.Add(Parametro.Crear(dato.Clave, dato.Valor, dato.Descripcion, dato.SucursalId, dato.CajaId, dato.Id));
            _creados++;
            return;
        }

        if (parametro.Clave != dato.Clave.Trim() || parametro.SucursalId != dato.SucursalId || parametro.CajaId != dato.CajaId)
            throw new InvalidOperationException($"El parámetro '{parametro.Clave}' no puede cambiar de clave ni de ámbito.");

        parametro.CambiarValor(dato.Valor);
        _actualizados++;
    }

    private static void ExigirMismoCodigo(string actual, string nuevo, string entidad)
    {
        if (actual != nuevo.Trim())
            throw new InvalidOperationException($"No se puede cambiar el código de {entidad} ({actual} → {nuevo.Trim()}).");
    }
}
