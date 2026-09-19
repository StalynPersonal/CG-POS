using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Seguridad;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Dispositivos;

internal sealed class ServicioDispositivos(ContextoDatosCentral contexto, IAuditoriaCentral auditoria, TimeProvider reloj) : IServicioDispositivos
{
    private const string TipoEntidad = "Caja";

    public async Task<CredencialEmitida?> EmitirCredencialAsync(int cajaId, UsuarioAuditoria emisor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(emisor);

        var caja = await contexto.Cajas.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cajaId, cancelacion);
        if (caja is null)
            return null;

        var ahora = reloj.Ahora();
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        // Primero se revoca la anterior: el índice único solo admite una credencial activa por caja.
        var anteriores = await contexto.CredencialesDispositivo.Where(c => c.CajaId == cajaId && c.RevocadaEn == null).ToListAsync(cancelacion);
        foreach (var anterior in anteriores)
            anterior.Revocar(ahora, "Reemplazada por una credencial nueva");
        await contexto.SaveChangesAsync(cancelacion);

        var secreto = TokensSeguros.Generar();
        var credencial = CredencialDispositivo.Emitir(cajaId, TokensSeguros.Hash(secreto), ahora, emisor.Nombre);
        contexto.CredencialesDispositivo.Add(credencial);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.CredencialEmitida", TipoEntidad, cajaId.ToString(),
            new { Caja = caja.Codigo, Credencial = credencial.Id, Reemplazadas = anteriores.Count }, Usuario: emisor));
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        var sucursalCodigo = await contexto.Sucursales.AsNoTracking().Where(s => s.Id == caja.SucursalId).Select(s => s.Codigo).SingleAsync(cancelacion);
        return new CredencialEmitida(caja.Id, sucursalCodigo, caja.Codigo, secreto, ahora);
    }

    public async Task<bool> RevocarCredencialAsync(int cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentNullException.ThrowIfNull(usuario);

        var activa = await contexto.CredencialesDispositivo.SingleOrDefaultAsync(c => c.CajaId == cajaId && c.RevocadaEn == null, cancelacion);
        if (activa is null)
            return false;

        activa.Revocar(reloj.Ahora(), motivo);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.CredencialRevocada", TipoEntidad, cajaId.ToString(), new { Credencial = activa.Id },
            motivo.Trim(), usuario));
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<ResultadoDispositivo> AutenticarAsync(string sucursalCodigo, string cajaCodigo, string secreto, OrigenSolicitud origen, CancellationToken cancelacion = default)
    {
        var cajaId = await contexto.Cajas.AsNoTracking()
            .Where(c => c.Codigo == cajaCodigo && contexto.Sucursales.Any(s => s.Id == c.SucursalId && s.Codigo == sucursalCodigo))
            .Select(c => c.Id)
            .FirstOrDefaultAsync(cancelacion);
        var credencial = cajaId <= 0 || string.IsNullOrWhiteSpace(secreto)
            ? null
            : await contexto.CredencialesDispositivo.SingleOrDefaultAsync(c => c.CajaId == cajaId && c.RevocadaEn == null, cancelacion);

        if (credencial is null || !TokensSeguros.Coincide(secreto.Trim(), credencial.SecretoHash))
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.CredencialInvalida, origen, cancelacion);

        var datos = await (
                from caja in contexto.Cajas
                join sucursal in contexto.Sucursales on caja.SucursalId equals sucursal.Id
                where caja.Id == cajaId
                select new { caja.Codigo, caja.Nombre, caja.Habilitada, SucursalId = sucursal.Id, SucursalCodigo = sucursal.Codigo, SucursalActiva = sucursal.Activa })
            .AsNoTracking()
            .SingleAsync(cancelacion);

        if (!datos.Habilitada)
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.CajaDeshabilitada, origen, cancelacion);
        if (!datos.SucursalActiva)
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.SucursalInactiva, origen, cancelacion);

        credencial.RegistrarUso(reloj.Ahora(), origen.DireccionIp);
        await contexto.SaveChangesAsync(cancelacion);

        return ResultadoDispositivo.Exito(new DispositivoAutenticado(cajaId, datos.Codigo, datos.Nombre, datos.SucursalId, datos.SucursalCodigo, credencial.Id));
    }

    public Task<bool> EsCredencialActivaAsync(int credencialId, int cajaId, CancellationToken cancelacion = default) =>
        contexto.CredencialesDispositivo.AnyAsync(c => c.Id == credencialId && c.CajaId == cajaId && c.RevocadaEn == null
            && contexto.Cajas.Any(caja => caja.Id == cajaId && caja.Habilitada), cancelacion);

    private async Task<ResultadoDispositivo> RechazarAsync(int cajaId, MotivoRechazoDispositivo motivo, OrigenSolicitud origen, CancellationToken cancelacion)
    {
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.AutenticacionRechazada", TipoEntidad, cajaId <= 0 ? null : cajaId.ToString(),
            new { Motivo = motivo.ToString(), origen.DireccionIp, origen.AgenteUsuario }));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoDispositivo.Rechazo(motivo);
    }
}
