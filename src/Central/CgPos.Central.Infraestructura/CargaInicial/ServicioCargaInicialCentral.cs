using System.Text.Json;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.CargaInicial;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.CargaInicial;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.CargaInicial;

internal sealed class ServicioCargaInicialCentral(
    ContextoDatosCentral contexto,
    IHashContrasenas hashContrasenas,
    IAuditoriaCentral auditoria,
    TimeProvider reloj,
    ILogger<ServicioCargaInicialCentral> logger) : ICargaInicialCentral
{
    private const string TodosLosPermisos = "*";

    private int _creados;
    private int _actualizados;

    public async Task<ResultadoCargaCentral> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo de carga inicial del Central: {ruta}", ruta);

        PaqueteCargaCentral? paquete;
        try
        {
            await using var archivo = File.OpenRead(ruta);
            paquete = await JsonSerializer.DeserializeAsync<PaqueteCargaCentral>(archivo, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException excepcion)
        {
            throw new CargaCentralInvalidaExcepcion([$"JSON inválido en {Path.GetFileName(ruta)}: {excepcion.Message}"]);
        }

        return await AplicarAsync(paquete ?? throw new CargaCentralInvalidaExcepcion(["El archivo está vacío."]), cancelacion);
    }

    public async Task<ResultadoCargaCentral> AplicarAsync(PaqueteCargaCentral paquete, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        _creados = 0;
        _actualizados = 0;

        var sucursales = paquete.Sucursales ?? [];
        var cajas = paquete.Cajas ?? [];
        var parametros = paquete.Parametros ?? [];
        var roles = paquete.RolesCentral ?? [];
        var usuarios = paquete.UsuariosCentral ?? [];

        await ValidarAsync(paquete, sucursales, cajas, parametros, roles, usuarios, cancelacion);

        try
        {
            await AplicarEmpresaAsync(paquete.Empresa, cancelacion);
            foreach (var sucursal in sucursales)
                await AplicarSucursalAsync(sucursal, paquete.Empresa.Id, cancelacion);
            foreach (var caja in cajas)
                await AplicarCajaAsync(caja, cancelacion);
            foreach (var parametro in parametros)
                await AplicarParametroAsync(parametro, cancelacion);
            foreach (var rol in roles)
                await AplicarRolAsync(rol, cancelacion);
            foreach (var usuario in usuarios)
                await AplicarUsuarioAsync(usuario, cancelacion);

            var resultado = new ResultadoCargaCentral(sucursales.Count, cajas.Count, parametros.Count, roles.Count, usuarios.Count, _creados, _actualizados);
            auditoria.Registrar(new EntradaAuditoria("CargaInicial.Aplicada", "CargaInicial", paquete.Empresa.Id.ToString(), resultado));
            await contexto.SaveChangesAsync(cancelacion);

            logger.LogInformation(
                "Carga inicial del Central aplicada: {Creados} creados, {Actualizados} actualizados ({Sucursales} sucursales, {Cajas} cajas, {Parametros} parámetros, {Roles} roles, {Usuarios} usuarios)",
                resultado.Creados, resultado.Actualizados, resultado.Sucursales, resultado.Cajas, resultado.Parametros, resultado.Roles, resultado.Usuarios);
            return resultado;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            contexto.ChangeTracker.Clear();
            var detalle = excepcion is DbUpdateException { InnerException: { } interna } ? interna.Message : excepcion.Message;
            throw new CargaCentralInvalidaExcepcion([detalle]);
        }
    }

    private async Task ValidarAsync(PaqueteCargaCentral paquete, IReadOnlyList<SucursalCarga> sucursales, IReadOnlyList<CajaCarga> cajas,
        IReadOnlyList<ParametroCarga> parametros, IReadOnlyList<RolCentralCarga> roles, IReadOnlyList<UsuarioCentralCarga> usuarios, CancellationToken cancelacion)
    {
        if (paquete.Empresa is null)
            throw new CargaCentralInvalidaExcepcion(["Falta la empresa."]);

        var errores = new List<string>();
        var otraEmpresa = await contexto.Empresas.Where(e => e.Id != paquete.Empresa.Id).Select(e => e.RazonSocial).FirstOrDefaultAsync(cancelacion);
        if (otraEmpresa is not null)
            errores.Add($"El Central ya pertenece a otra empresa ({otraEmpresa}).");

        Duplicados(sucursales.Select(s => s.Id), "Id de sucursal", errores);
        Duplicados(sucursales.Select(s => s.Codigo.Trim()), "Código de sucursal", errores);
        Duplicados(cajas.Select(c => c.Id), "Id de caja", errores);
        Duplicados(cajas.Select(c => $"{c.SucursalId}/{c.Codigo.Trim()}"), "Código de caja en la sucursal", errores);
        Duplicados(parametros.Select(p => p.Id), "Id de parámetro", errores);
        Duplicados(roles.Select(r => r.Id), "Id de rol", errores);
        Duplicados(roles.Select(r => r.Codigo.Trim()), "Código de rol", errores);
        Duplicados(usuarios.Select(u => u.Id), "Id de usuario", errores);
        Duplicados(usuarios.Select(u => u.Codigo.Trim()), "Usuario", errores);

        var idsSucursales = sucursales.Select(s => s.Id).ToHashSet();
        idsSucursales.UnionWith(await contexto.Sucursales.Select(s => s.Id).ToListAsync(cancelacion));
        var idsCajas = cajas.Select(c => c.Id).ToHashSet();
        idsCajas.UnionWith(await contexto.Cajas.Select(c => c.Id).ToListAsync(cancelacion));
        var idsRoles = roles.Select(r => r.Id).ToHashSet();
        idsRoles.UnionWith(await contexto.RolesCentral.Select(r => r.Id).ToListAsync(cancelacion));
        var idsUsuarios = (await contexto.UsuariosCentral.Select(u => u.Id).ToListAsync(cancelacion)).ToHashSet();

        foreach (var caja in cajas.Where(c => !idsSucursales.Contains(c.SucursalId)))
            errores.Add($"La caja '{caja.Codigo}' referencia una sucursal inexistente ({caja.SucursalId}).");

        foreach (var parametro in parametros)
        {
            if (parametro.SucursalId is { } sucursalId && !idsSucursales.Contains(sucursalId))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una sucursal inexistente ({sucursalId}).");
            if (parametro.CajaId is { } cajaId && !idsCajas.Contains(cajaId))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una caja inexistente ({cajaId}).");
        }

        foreach (var rol in roles)
        {
            foreach (var codigo in (rol.Permisos ?? []).Where(p => p != TodosLosPermisos && !CatalogoPermisosCentral.Existe(p)))
                errores.Add($"El rol '{rol.Codigo}' tiene un permiso inexistente en el Central: '{codigo}'.");

            var codigoRol = rol.Codigo.Trim();
            if (await contexto.RolesCentral.AnyAsync(r => r.Codigo == codigoRol && r.Id != rol.Id, cancelacion))
                errores.Add($"El código de rol '{codigoRol}' ya existe con otro Id.");
        }

        foreach (var usuario in usuarios)
        {
            var etiqueta = $"El usuario '{usuario.Codigo}'";
            if (!idsRoles.Contains(usuario.RolId))
                errores.Add($"{etiqueta} referencia un rol inexistente ({usuario.RolId}).");
            if (usuario.Contrasena is not null && usuario.ContrasenaHash is not null)
                errores.Add($"{etiqueta} trae 'contrasena' y 'contrasenaHash'; use solo uno.");
            if (usuario.ContrasenaHash is not null && !hashContrasenas.EsHashReconocido(usuario.ContrasenaHash))
                errores.Add($"{etiqueta} tiene un 'contrasenaHash' con formato no reconocido.");
            if (string.IsNullOrEmpty(usuario.Contrasena) && usuario.ContrasenaHash is null && !idsUsuarios.Contains(usuario.Id))
                errores.Add($"{etiqueta} es nuevo y no tiene contraseña.");

            var codigoUsuario = usuario.Codigo.Trim();
            if (await contexto.UsuariosCentral.AnyAsync(u => u.Codigo == codigoUsuario && u.Id != usuario.Id, cancelacion))
                errores.Add($"El usuario '{codigoUsuario}' ya existe con otro Id.");
        }

        if (errores.Count > 0)
            throw new CargaCentralInvalidaExcepcion(errores);
    }

    private static void Duplicados<T>(IEnumerable<T> valores, string campo, List<string> errores)
    {
        foreach (var repetido in valores.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key))
            errores.Add($"{campo} repetido en el paquete: {repetido}.");
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

    private async Task AplicarRolAsync(RolCentralCarga dato, CancellationToken cancelacion)
    {
        var rol = await contexto.RolesCentral.Include(r => r.PermisosAsignados).SingleOrDefaultAsync(r => r.Id == dato.Id, cancelacion);
        if (rol is null)
        {
            rol = RolCentral.Crear(dato.Codigo, dato.Nombre, dato.Id);
            contexto.RolesCentral.Add(rol);
            _creados++;
        }
        else
        {
            ExigirMismoCodigo(rol.Codigo, dato.Codigo, "rol");
            rol.CambiarNombre(dato.Nombre);
            _actualizados++;
        }

        var permisos = dato.Permisos ?? [];
        var deseados = permisos.Contains(TodosLosPermisos)
            ? CatalogoPermisosCentral.Todos.Select(p => p.Codigo).ToHashSet()
            : permisos.ToHashSet();

        foreach (var sobrante in rol.PermisosAsignados.Select(p => p.PermisoCodigo).Where(c => !deseados.Contains(c)).ToList())
            rol.QuitarPermiso(sobrante);
        foreach (var codigo in deseados)
            rol.AsignarPermiso(codigo);

        if (dato.Activo) rol.Activar(); else rol.Desactivar();
    }

    private async Task AplicarUsuarioAsync(UsuarioCentralCarga dato, CancellationToken cancelacion)
    {
        var usuario = await contexto.UsuariosCentral.SingleOrDefaultAsync(u => u.Id == dato.Id, cancelacion);
        if (usuario is null)
        {
            var hash = dato.ContrasenaHash ?? hashContrasenas.Hash(dato.Contrasena!);
            usuario = UsuarioCentral.Crear(dato.Codigo, dato.Nombre, dato.Correo, dato.RolId, hash, dato.DebeCambiarContrasena, dato.Id);
            contexto.UsuariosCentral.Add(usuario);
            _creados++;
        }
        else
        {
            ExigirMismoCodigo(usuario.Codigo, dato.Codigo, "usuario");
            usuario.ActualizarDatos(dato.Nombre, dato.Correo);
            usuario.CambiarRol(dato.RolId);

            // Una contraseña en texto solo se usa al crear el usuario: volver a aplicar la carga no deshace el cambio que hizo el usuario.
            if (dato.ContrasenaHash is not null && dato.ContrasenaHash != usuario.ContrasenaHash)
                usuario.CambiarContrasena(dato.ContrasenaHash, dato.DebeCambiarContrasena, reloj.GetUtcNow());

            _actualizados++;
        }

        if (dato.Activo) usuario.Activar(); else usuario.Desactivar();
    }

    private static void ExigirMismoCodigo(string actual, string nuevo, string entidad)
    {
        if (actual != nuevo.Trim())
            throw new InvalidOperationException($"No se puede cambiar el código de {entidad} ({actual} → {nuevo.Trim()}).");
    }
}

public static class ExtensionesCargaInicialCentral
{
    /// <summary>Aplica el archivo de carga inicial al arrancar el Central (instalación o desarrollo). Es idempotente.</summary>
    public static async Task AplicarCargaInicialCentralAsync(this IServiceProvider servicios, string ruta, CancellationToken cancelacion = default)
    {
        await using var ambito = servicios.CreateAsyncScope();
        await ambito.ServiceProvider.GetRequiredService<ICargaInicialCentral>().AplicarDesdeArchivoAsync(ruta, cancelacion);
    }
}
