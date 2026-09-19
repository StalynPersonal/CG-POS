using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Central;
using CgPos.Dominio.Organizacion;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Organizacion;

internal sealed class ServicioOrganizacionCentral(ContextoDatosCentral contexto, IAuditoriaCentral auditoria) : IServicioOrganizacion
{
    public Task<DatosEmpresa?> ObtenerEmpresaAsync(CancellationToken cancelacion = default) =>
        contexto.Empresas.AsNoTracking()
            .Select(e => new DatosEmpresa(e.Id, e.Rnc, e.RazonSocial, e.NombreComercial, e.Direccion, e.Telefono))
            .FirstOrDefaultAsync(cancelacion);

    public async Task<ResultadoAdministracion> ActualizarEmpresaAsync(SolicitudEmpresa solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var empresa = await contexto.Empresas.FirstOrDefaultAsync(cancelacion);
        if (empresa is null)
            return ResultadoAdministracion.Inexistente("La empresa no está configurada; aplique la carga inicial del Central.");
        if (DatosObligatoriosOrganizacion.Empresa(solicitud.RazonSocial, solicitud.NombreComercial, solicitud.Direccion, solicitud.Telefono) is { } faltanEmpresa)
            return ResultadoAdministracion.Error(faltanEmpresa);

        var rncAnterior = empresa.Rnc;
        try
        {
            // El RNC se corrige si se registró mal al instalar; si no viene, se conserva el que hay.
            if (solicitud.Rnc is { Length: > 0 } rnc && CgPos.Dominio.Fiscal.DocumentoIdentidad.Normalizar(rnc) != rncAnterior)
                empresa.CambiarRnc(rnc);

            empresa.ActualizarDatos(solicitud.RazonSocial, solicitud.NombreComercial, solicitud.Direccion, solicitud.Telefono);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        if (empresa.Rnc != rncAnterior)
            auditoria.Registrar(new EntradaAuditoria("Organizacion.RncEmpresaCambiado", "Empresa", empresa.Rnc,
                Detalle: new { Anterior = rncAnterior, Nuevo = empresa.Rnc }, Usuario: actor));

        return await GuardarAsync("Organizacion.EmpresaActualizada", "Empresa", empresa.Id, actor, solicitud, cancelacion);
    }

    public async Task<IReadOnlyList<DatosSucursal>> ListarSucursalesAsync(CancellationToken cancelacion = default)
    {
        var cajasPorSucursal = await contexto.Cajas.AsNoTracking()
            .GroupBy(c => c.SucursalId)
            .Select(g => new { SucursalId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.SucursalId, g => g.Cantidad, cancelacion);

        return (await contexto.Sucursales.AsNoTracking().OrderBy(s => s.Codigo).ToListAsync(cancelacion))
            .Select(s => new DatosSucursal(s.Id, s.Codigo, s.Nombre, s.Direccion, s.Telefono, s.Activa, cajasPorSucursal.GetValueOrDefault(s.Id)))
            .ToList();
    }

    public async Task<ResultadoAdministracion> CrearSucursalAsync(SolicitudSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var empresaId = await contexto.Empresas.Select(e => (int?)e.Id).FirstOrDefaultAsync(cancelacion);
        if (empresaId is null)
            return ResultadoAdministracion.Error("Configure la empresa antes de crear sucursales.");

        var codigo = solicitud.Codigo;
        if (codigo is { Length: > 0 } && await contexto.Sucursales.AnyAsync(s => s.Codigo == codigo, cancelacion))
            return ResultadoAdministracion.Error($"Ya existe la sucursal con código {codigo}.");
        if (DatosObligatoriosOrganizacion.Sucursal(codigo, solicitud.Nombre, solicitud.Direccion, solicitud.Telefono) is { } faltanSucursal)
            return ResultadoAdministracion.Error(faltanSucursal);

        Sucursal sucursal;
        try
        {
            sucursal = Sucursal.Crear(empresaId.Value, codigo, solicitud.Nombre, solicitud.Direccion, solicitud.Telefono);
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        contexto.Sucursales.Add(sucursal);
        return await GuardarAsync("Organizacion.SucursalCreada", "Sucursal", sucursal.Id, actor, solicitud, cancelacion);
    }

    public async Task<ResultadoAdministracion> ActualizarSucursalAsync(int sucursalId, SolicitudSucursal solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var sucursal = await contexto.Sucursales.SingleOrDefaultAsync(s => s.Id == sucursalId, cancelacion);
        if (sucursal is null)
            return ResultadoAdministracion.Inexistente("La sucursal no existe.");
        if (sucursal.Codigo != solicitud.Codigo)
            return ResultadoAdministracion.Error("El código de la sucursal no se puede cambiar.");
        if (DatosObligatoriosOrganizacion.Sucursal(sucursal.Codigo, solicitud.Nombre, solicitud.Direccion, solicitud.Telefono) is { } faltanSucursal)
            return ResultadoAdministracion.Error(faltanSucursal);

        try
        {
            sucursal.ActualizarDatos(solicitud.Nombre, solicitud.Direccion, solicitud.Telefono);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        return await GuardarAsync("Organizacion.SucursalActualizada", "Sucursal", sucursal.Id, actor, solicitud, cancelacion);
    }

    public async Task<ResultadoAdministracion> CambiarEstadoSucursalAsync(int sucursalId, bool activa, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var sucursal = await contexto.Sucursales.SingleOrDefaultAsync(s => s.Id == sucursalId, cancelacion);
        if (sucursal is null)
            return ResultadoAdministracion.Inexistente("La sucursal no existe.");

        if (activa) sucursal.Activar(); else sucursal.Desactivar();
        return await GuardarAsync(activa ? "Organizacion.SucursalActivada" : "Organizacion.SucursalDesactivada", "Sucursal", sucursal.Id, actor,
            new { sucursal.Codigo }, cancelacion);
    }

    public async Task<IReadOnlyList<DatosCaja>> ListarCajasAsync(CancellationToken cancelacion = default)
    {
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, cancelacion);
        var credenciales = await contexto.CredencialesDispositivo.AsNoTracking().Where(c => c.RevocadaEn == null).ToDictionaryAsync(c => c.CajaId, cancelacion);
        var estados = await contexto.EstadosSincronizacionCaja.AsNoTracking().ToDictionaryAsync(e => e.CajaId, cancelacion);

        return (await contexto.Cajas.AsNoTracking().ToListAsync(cancelacion))
            .Select(caja =>
            {
                var sucursal = sucursales[caja.SucursalId];
                credenciales.TryGetValue(caja.Id, out var credencial);
                estados.TryGetValue(caja.Id, out var estado);
                return new DatosCaja(caja.Id, caja.SucursalId, sucursal.Codigo, sucursal.Nombre, caja.Codigo, caja.Nombre, caja.Habilitada,
                    credencial?.EmitidaEn, credencial?.UltimoUsoEn, estado?.UltimaRecepcionEn, estado?.UltimaDescargaEn,
                    credencial?.NombreEquipo, credencial?.EquipoFijadoEn);
            })
            .OrderBy(c => c.SucursalCodigo)
            .ThenBy(c => c.Codigo)
            .ToList();
    }

    public async Task<ResultadoAdministracion> CrearCajaAsync(SolicitudCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (!await contexto.Sucursales.AnyAsync(s => s.Id == solicitud.SucursalId, cancelacion))
            return ResultadoAdministracion.Error("Seleccione una sucursal existente.");

        var codigo = solicitud.Codigo;
        if (codigo is { Length: > 0 } && await contexto.Cajas.AnyAsync(c => c.SucursalId == solicitud.SucursalId && c.Codigo == codigo, cancelacion))
            return ResultadoAdministracion.Error($"La sucursal ya tiene la caja {codigo}.");

        Caja caja;
        try
        {
            caja = Caja.Crear(solicitud.SucursalId, codigo, solicitud.Nombre);
        }
        catch (ArgumentException excepcion)
        {
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        contexto.Cajas.Add(caja);
        return await GuardarAsync("Organizacion.CajaCreada", "Caja", caja.Id, actor, solicitud, cancelacion);
    }

    public async Task<ResultadoAdministracion> ActualizarCajaAsync(int cajaId, SolicitudActualizarCaja solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var caja = await contexto.Cajas.SingleOrDefaultAsync(c => c.Id == cajaId, cancelacion);
        if (caja is null)
            return ResultadoAdministracion.Inexistente("La caja no existe.");

        try
        {
            caja.CambiarNombre(solicitud.Nombre);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        return await GuardarAsync("Organizacion.CajaActualizada", "Caja", caja.Id, actor, solicitud, cancelacion);
    }

    public async Task<ResultadoAdministracion> CambiarEstadoCajaAsync(int cajaId, bool habilitada, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var caja = await contexto.Cajas.SingleOrDefaultAsync(c => c.Id == cajaId, cancelacion);
        if (caja is null)
            return ResultadoAdministracion.Inexistente("La caja no existe.");

        if (habilitada) caja.Habilitar(); else caja.Deshabilitar();
        return await GuardarAsync(habilitada ? "Organizacion.CajaHabilitada" : "Organizacion.CajaDeshabilitada", "Caja", caja.Id, actor, new { caja.Codigo }, cancelacion);
    }

    public async Task<IReadOnlyList<DatosParametro>> ListarParametrosAsync(CancellationToken cancelacion = default)
    {
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().ToDictionaryAsync(c => c.Id, cancelacion);

        string Ambito(Parametro parametro) => parametro switch
        {
            { CajaId: { } cajaId } when cajas.TryGetValue(cajaId, out var caja) =>
                $"Caja {caja.Codigo} · Sucursal {sucursales.GetValueOrDefault(caja.SucursalId)?.Codigo}",
            { SucursalId: { } sucursalId } when sucursales.TryGetValue(sucursalId, out var sucursal) => $"Sucursal {sucursal.Codigo} · {sucursal.Nombre}",
            _ => "General",
        };

        return (await contexto.Parametros.AsNoTracking().OrderBy(p => p.Clave).ToListAsync(cancelacion))
            .Select(p => new DatosParametro(p.Id, p.Clave, p.Valor, p.Descripcion, p.SucursalId, p.CajaId, Ambito(p)))
            .OrderBy(p => p.Clave, StringComparer.Ordinal)
            .ThenBy(p => p.CajaId is not null ? 2 : p.SucursalId is not null ? 1 : 0)
            .ToList();
    }

    public async Task<ResultadoAdministracion> CrearParametroAsync(SolicitudParametro solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var clave = solicitud.Clave?.Trim() ?? string.Empty;
        if (CatalogoParametros.Buscar(clave) is not { } definicion)
            return ResultadoAdministracion.Error($"El parámetro «{clave}» no existe en el catálogo.");
        if (definicion.Alcance == AlcanceParametro.Central && (solicitud.SucursalId is not null || solicitud.CajaId is not null))
            return ResultadoAdministracion.Error("Los parámetros del Central solo pueden ser generales.");
        if (solicitud.SucursalId is not null && solicitud.CajaId is not null)
            return ResultadoAdministracion.Error("Un parámetro aplica a una sucursal o a una caja, no a ambas.");
        if (solicitud.SucursalId is { } sucursalId && !await contexto.Sucursales.AnyAsync(s => s.Id == sucursalId, cancelacion))
            return ResultadoAdministracion.Error("La sucursal indicada no existe.");
        if (solicitud.CajaId is { } cajaId && !await contexto.Cajas.AnyAsync(c => c.Id == cajaId, cancelacion))
            return ResultadoAdministracion.Error("La caja indicada no existe.");
        if (await contexto.Parametros.AnyAsync(p => p.Clave == clave && p.SucursalId == solicitud.SucursalId && p.CajaId == solicitud.CajaId, cancelacion))
            return ResultadoAdministracion.Error("Ese parámetro ya existe en ese ámbito; edite su valor.");

        var valor = solicitud.Valor?.Trim() ?? string.Empty;
        if (await ValidarValorAsync(definicion, valor, cancelacion) is { } problema)
            return ResultadoAdministracion.Error(problema);

        var parametro = Parametro.Crear(clave, valor, definicion.Descripcion, solicitud.SucursalId, solicitud.CajaId);
        contexto.Parametros.Add(parametro);
        return await GuardarAsync("Organizacion.ParametroCreado", "Parametro", parametro.Id, actor, new { clave, valor, solicitud.SucursalId, solicitud.CajaId }, cancelacion);
    }

    public async Task<ResultadoAdministracion> CambiarValorParametroAsync(int parametroId, string valor, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        var parametro = await contexto.Parametros.SingleOrDefaultAsync(p => p.Id == parametroId, cancelacion);
        if (parametro is null)
            return ResultadoAdministracion.Inexistente("El parámetro no existe.");

        var nuevo = valor?.Trim() ?? string.Empty;
        if (CatalogoParametros.Buscar(parametro.Clave) is { } definicion && await ValidarValorAsync(definicion, nuevo, cancelacion) is { } problema)
            return ResultadoAdministracion.Error(problema);

        var anterior = parametro.Valor;
        try
        {
            parametro.CambiarValor(nuevo);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(excepcion.Message);
        }

        return await GuardarAsync("Organizacion.ParametroCambiado", "Parametro", parametro.Id, actor, new { parametro.Clave, Anterior = anterior, Nuevo = nuevo }, cancelacion);
    }

    /// <summary>El catálogo valida el tipo; la moneda local además debe estar publicada, o las cajas no podrían vender.</summary>
    private async Task<string?> ValidarValorAsync(DefinicionParametro definicion, string valor, CancellationToken cancelacion)
    {
        if (definicion.ValidarValor(valor) is { } problema)
            return problema;

        if (definicion.Clave == CatalogoParametros.MonedaLocal && valor.Length > 0)
        {
            var codigo = valor.ToUpperInvariant();
            if (!await contexto.Monedas.AnyAsync(m => m.Codigo == codigo, cancelacion))
                return $"La moneda «{codigo}» no está publicada en el maestro de monedas.";
        }

        return null;
    }

    private async Task<ResultadoAdministracion> GuardarAsync(string accion, string tipo, int entidadId, UsuarioAuditoria actor, object detalle, CancellationToken cancelacion)
    {
        auditoria.Registrar(new EntradaAuditoria(accion, tipo, entidadId.ToString(), detalle, Usuario: actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(entidadId);
    }
}
