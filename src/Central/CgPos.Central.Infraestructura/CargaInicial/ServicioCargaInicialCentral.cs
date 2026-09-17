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
    ILogger<ServicioCargaInicialCentral> logger) : ICargaInicialCentral
{
    private const string TodosLosPermisos = "*";

    private int _creados;
    private int _existentes;

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
        _existentes = 0;

        var sucursales = paquete.Sucursales ?? [];
        var cajas = paquete.Cajas ?? [];
        var parametros = paquete.Parametros ?? [];
        var roles = paquete.RolesCentral ?? [];
        var usuarios = paquete.UsuariosCentral ?? [];

        await ValidarAsync(paquete, sucursales, cajas, parametros, roles, usuarios, cancelacion);

        try
        {
            var empresaId = await AplicarEmpresaAsync(paquete.Empresa, cancelacion);

            // Códigos -> Id del Central, con lo que ya existe y lo que se crea en esta carga.
            var idsSucursales = await contexto.Sucursales.ToDictionaryAsync(s => s.Codigo, s => s.Id, cancelacion);
            foreach (var sucursal in sucursales)
                idsSucursales[sucursal.Codigo] = await AplicarSucursalAsync(sucursal, empresaId, cancelacion);

            var idsCajas = (await contexto.Cajas.Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, c.Codigo, c.Id })
                    .ToListAsync(cancelacion))
                .ToDictionary(c => (c.Sucursal, c.Codigo), c => c.Id);
            foreach (var caja in cajas)
                idsCajas[(caja.SucursalCodigo, caja.Codigo)] = await AplicarCajaAsync(caja, idsSucursales[caja.SucursalCodigo], cancelacion);

            foreach (var parametro in parametros)
                await AplicarParametroAsync(parametro, idsSucursales, idsCajas, cancelacion);

            var idsRoles = await contexto.RolesCentral.ToDictionaryAsync(r => r.Codigo, r => r.Id, StringComparer.OrdinalIgnoreCase, cancelacion);
            foreach (var rol in roles)
                idsRoles[rol.Codigo.Trim()] = await AplicarRolAsync(rol, cancelacion);
            foreach (var usuario in usuarios)
                await AplicarUsuarioAsync(usuario, idsRoles[usuario.RolCodigo.Trim()], cancelacion);

            var resultado = new ResultadoCargaCentral(sucursales.Count, cajas.Count, parametros.Count, roles.Count, usuarios.Count, _creados, _existentes);
            auditoria.Registrar(new EntradaAuditoria("CargaInicial.Aplicada", "CargaInicial", paquete.Empresa.Rnc, resultado));
            await contexto.SaveChangesAsync(cancelacion);

            logger.LogInformation(
                "Carga inicial del Central aplicada: {Creados} creados, {Existentes} ya existían y se conservaron ({Sucursales} sucursales, {Cajas} cajas, {Parametros} parámetros, {Roles} roles, {Usuarios} usuarios)",
                resultado.Creados, resultado.Existentes, resultado.Sucursales, resultado.Cajas, resultado.Parametros, resultado.Roles, resultado.Usuarios);
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
        var rnc = paquete.Empresa.Rnc?.Trim();
        var otraEmpresa = await contexto.Empresas.Where(e => e.Rnc != rnc).Select(e => e.RazonSocial).FirstOrDefaultAsync(cancelacion);
        if (otraEmpresa is not null)
            errores.Add($"El Central ya pertenece a otra empresa ({otraEmpresa}).");

        // Los datos obligatorios se exigen a lo que se va a crear; lo existente se conserva y se completa en el Manager.
        if (!await contexto.Empresas.AnyAsync(cancelacion)
            && Organizacion.DatosObligatoriosOrganizacion.Empresa(paquete.Empresa.RazonSocial, paquete.Empresa.NombreComercial, paquete.Empresa.Direccion,
                paquete.Empresa.Telefono) is { } faltanEmpresa)
            errores.Add(faltanEmpresa);

        var codigosSucursales = (await contexto.Sucursales.Select(s => s.Codigo).ToListAsync(cancelacion)).ToHashSet();
        foreach (var sucursal in sucursales.Where(s => !codigosSucursales.Contains(s.Codigo)))
            if (Organizacion.DatosObligatoriosOrganizacion.Sucursal(sucursal.Codigo, sucursal.Nombre, sucursal.Direccion, sucursal.Telefono) is { } faltanSucursal)
                errores.Add($"{faltanSucursal} ({sucursal.Codigo:00})");

        Duplicados(sucursales.Select(s => s.Codigo), "Código de sucursal", errores);
        Duplicados(cajas.Select(c => $"{c.SucursalCodigo:00}-{c.Codigo:00}"), "Caja (sucursal-caja)", errores);
        Duplicados(parametros.Select(p => $"{p.Clave.Trim()} ({p.SucursalCodigo}/{p.CajaCodigo})"), "Parámetro", errores);
        Duplicados(roles.Select(r => r.Codigo.Trim().ToUpperInvariant()), "Código de rol", errores);
        Duplicados(usuarios.Select(u => u.Codigo.Trim().ToUpperInvariant()), "Usuario", errores);

        codigosSucursales.UnionWith(sucursales.Select(s => s.Codigo));
        var codigosCajas = (await contexto.Cajas.Join(contexto.Sucursales, c => c.SucursalId, s => s.Id, (c, s) => new { Sucursal = s.Codigo, c.Codigo })
                .ToListAsync(cancelacion))
            .Select(c => (c.Sucursal, c.Codigo))
            .ToHashSet();
        codigosCajas.UnionWith(cajas.Select(c => (c.SucursalCodigo, c.Codigo)));
        var codigosRoles = (await contexto.RolesCentral.Select(r => r.Codigo).ToListAsync(cancelacion)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        codigosRoles.UnionWith(roles.Select(r => r.Codigo.Trim()));
        var codigosUsuarios = (await contexto.UsuariosCentral.Select(u => u.Codigo).ToListAsync(cancelacion)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var caja in cajas.Where(c => !codigosSucursales.Contains(c.SucursalCodigo)))
            errores.Add($"La caja {caja.Codigo:00} referencia una sucursal inexistente ({caja.SucursalCodigo:00}).");

        foreach (var parametro in parametros)
        {
            if (CatalogoParametros.Buscar(parametro.Clave) is not { } definicion)
                errores.Add($"El parámetro '{parametro.Clave}' no está en el catálogo de parámetros.");
            else if (definicion.ValidarValor(parametro.Valor) is { } problema)
                errores.Add($"El parámetro '{parametro.Clave}': {problema}");
            else if (definicion.Alcance == AlcanceParametro.Central && (parametro.SucursalCodigo is not null || parametro.CajaCodigo is not null))
                errores.Add($"El parámetro '{parametro.Clave}' es del Central y solo puede ser general.");

            if (parametro.SucursalCodigo is { } sucursal && !codigosSucursales.Contains(sucursal))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una sucursal inexistente ({sucursal:00}).");
            if (parametro.CajaCodigo is not null && parametro.SucursalCodigo is null)
                errores.Add($"El parámetro '{parametro.Clave}' de caja debe indicar también la sucursal de la caja.");
            if (parametro is { SucursalCodigo: { } s, CajaCodigo: { } c } && !codigosCajas.Contains((s, c)))
                errores.Add($"El parámetro '{parametro.Clave}' referencia una caja inexistente ({s:00}-{c:00}).");
        }

        foreach (var rol in roles)
            foreach (var codigo in (rol.Permisos ?? []).Where(p => p != TodosLosPermisos && !CatalogoPermisosCentral.Existe(p)))
                errores.Add($"El rol '{rol.Codigo}' tiene un permiso inexistente en el Central: '{codigo}'.");

        foreach (var usuario in usuarios)
        {
            var etiqueta = $"El usuario '{usuario.Codigo}'";
            if (!codigosRoles.Contains(usuario.RolCodigo?.Trim() ?? string.Empty))
                errores.Add($"{etiqueta} referencia un rol inexistente ({usuario.RolCodigo}).");
            if (usuario.Contrasena is not null && usuario.ContrasenaHash is not null)
                errores.Add($"{etiqueta} trae 'contrasena' y 'contrasenaHash'; use solo uno.");
            if (usuario.ContrasenaHash is not null && !hashContrasenas.EsHashReconocido(usuario.ContrasenaHash))
                errores.Add($"{etiqueta} tiene un 'contrasenaHash' con formato no reconocido.");
            if (string.IsNullOrEmpty(usuario.Contrasena) && usuario.ContrasenaHash is null && !codigosUsuarios.Contains(usuario.Codigo.Trim()))
                errores.Add($"{etiqueta} es nuevo y no tiene contraseña.");
        }

        if (errores.Count > 0)
            throw new CargaCentralInvalidaExcepcion(errores);
    }

    private static void Duplicados<T>(IEnumerable<T> valores, string campo, List<string> errores)
    {
        foreach (var repetido in valores.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key))
            errores.Add($"{campo} repetido en el paquete: {repetido}.");
    }

    /// <summary>El Central pertenece a una sola empresa, identificada por su RNC.</summary>
    private async Task<Guid> AplicarEmpresaAsync(EmpresaCarga dato, CancellationToken cancelacion)
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

        _existentes++;
        return empresa.Id;
    }

    private async Task<Guid> AplicarSucursalAsync(SucursalCarga dato, Guid empresaId, CancellationToken cancelacion)
    {
        var sucursal = await contexto.Sucursales.SingleOrDefaultAsync(s => s.Codigo == dato.Codigo, cancelacion);
        if (sucursal is not null)
        {
            _existentes++;
            return sucursal.Id;
        }

        sucursal = Sucursal.Crear(empresaId, dato.Codigo, dato.Nombre, dato.Direccion, dato.Telefono);
        if (dato.Activa) sucursal.Activar(); else sucursal.Desactivar();
        contexto.Sucursales.Add(sucursal);
        _creados++;
        return sucursal.Id;
    }

    private async Task<Guid> AplicarCajaAsync(CajaCarga dato, Guid sucursalId, CancellationToken cancelacion)
    {
        var caja = await contexto.Cajas.SingleOrDefaultAsync(c => c.SucursalId == sucursalId && c.Codigo == dato.Codigo, cancelacion);
        if (caja is not null)
        {
            _existentes++;
            return caja.Id;
        }

        caja = Caja.Crear(sucursalId, dato.Codigo, dato.Nombre);
        if (dato.Habilitada) caja.Habilitar(); else caja.Deshabilitar();
        contexto.Cajas.Add(caja);
        _creados++;
        return caja.Id;
    }

    private async Task AplicarParametroAsync(ParametroCarga dato, IReadOnlyDictionary<int, Guid> idsSucursales, IReadOnlyDictionary<(int Sucursal, int Caja), Guid> idsCajas,
        CancellationToken cancelacion)
    {
        Guid? sucursalId = null;
        Guid? cajaId = null;
        if (dato is { SucursalCodigo: { } s, CajaCodigo: { } c })
            cajaId = idsCajas[(s, c)];
        else if (dato.SucursalCodigo is { } sucursal)
            sucursalId = idsSucursales[sucursal];

        var clave = dato.Clave.Trim();
        if (await contexto.Parametros.AnyAsync(p => p.Clave == clave && p.SucursalId == sucursalId && p.CajaId == cajaId, cancelacion))
        {
            _existentes++;
            return;
        }

        contexto.Parametros.Add(Parametro.Crear(dato.Clave, dato.Valor, dato.Descripcion, sucursalId, cajaId));
        _creados++;
    }

    private async Task<Guid> AplicarRolAsync(RolCentralCarga dato, CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim();
        var rol = await contexto.RolesCentral.Include(r => r.PermisosAsignados).SingleOrDefaultAsync(r => r.Codigo == codigo, cancelacion);
        if (rol is not null)
        {
            _existentes++;
            return rol.Id;
        }

        rol = RolCentral.Crear(dato.Codigo, dato.Nombre);
        contexto.RolesCentral.Add(rol);
        _creados++;

        var permisos = dato.Permisos ?? [];
        var deseados = permisos.Contains(TodosLosPermisos)
            ? CatalogoPermisosCentral.Todos.Select(p => p.Codigo)
            : permisos;
        foreach (var permiso in deseados.Distinct())
            rol.AsignarPermiso(permiso);

        if (dato.Activo) rol.Activar(); else rol.Desactivar();
        return rol.Id;
    }

    private async Task AplicarUsuarioAsync(UsuarioCentralCarga dato, Guid rolId, CancellationToken cancelacion)
    {
        var codigo = dato.Codigo.Trim();
        if (await contexto.UsuariosCentral.AnyAsync(u => u.Codigo == codigo, cancelacion))
        {
            _existentes++;
            return;
        }

        var hash = dato.ContrasenaHash ?? hashContrasenas.Hash(dato.Contrasena!);
        var usuario = UsuarioCentral.Crear(dato.Codigo, dato.Nombre, dato.Correo, rolId, hash, dato.DebeCambiarContrasena);
        contexto.UsuariosCentral.Add(usuario);
        _creados++;

        if (dato.Activo) usuario.Activar(); else usuario.Desactivar();
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
