using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Dispositivos;
using CgPos.Contratos.Central;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Seguridad;
using CgPos.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Dispositivos;

internal sealed class ServicioDispositivos(ContextoDatosCentral contexto, IAuditoriaCentral auditoria, TimeProvider reloj) : IServicioDispositivos
{
    private const string TipoEntidad = "Caja";

    public Task<CredencialEmitida?> EmitirCredencialAsync(int cajaId, UsuarioAuditoria emisor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        return EmitirCredencialAsync(cajaId, emisor.Nombre, null, null, cancelacion, emisor);
    }

    /// <summary>
    /// Emite la credencial y, cuando viene del enrolamiento, la deja atada al equipo que la pidió. Emitida a mano desde el
    /// Central se queda sin equipo: la ata la primera caja que la use.
    /// </summary>
    private async Task<CredencialEmitida?> EmitirCredencialAsync(int cajaId, string emitidaPor, string? huellaEquipo, string? nombreEquipo,
        CancellationToken cancelacion, UsuarioAuditoria? usuario = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emitidaPor);

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
        var credencial = CredencialDispositivo.Emitir(cajaId, TokensSeguros.Hash(secreto), ahora, emitidaPor);
        if (huellaEquipo is { Length: CredencialDispositivo.LargoHuella })
            credencial.FijarEquipo(huellaEquipo, nombreEquipo ?? "Equipo sin nombre", ahora);
        contexto.CredencialesDispositivo.Add(credencial);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.CredencialEmitida", TipoEntidad, cajaId.ToString(),
            new { Caja = caja.Codigo, Credencial = credencial.Id, Reemplazadas = anteriores.Count }, Usuario: usuario));
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

    public async Task<ResultadoDispositivo> AutenticarAsync(string sucursalCodigo, string cajaCodigo, string secreto, string? huellaEquipo,
        OrigenSolicitud origen, CancellationToken cancelacion = default)
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

        // Una credencial vale para un solo equipo. Las que se emitieron antes de existir la huella no tienen equipo atado:
        // la primera caja que las use se queda con ellas, para que nada deje de funcionar por haber actualizado el sistema.
        if (!credencial.CoincideEquipo(huellaEquipo))
            return await RechazarAsync(cajaId, MotivoRechazoDispositivo.EquipoNoAutorizado, origen, cancelacion);

        var ahora = reloj.Ahora();
        if (!credencial.TieneEquipo && huellaEquipo is { Length: CredencialDispositivo.LargoHuella })
        {
            credencial.FijarEquipo(huellaEquipo, "Fijado al conectarse", ahora);
            auditoria.Registrar(new EntradaAuditoria("Dispositivos.EquipoFijado", TipoEntidad, cajaId.ToString(),
                new { Caja = datos.Codigo, credencial.NombreEquipo, origen.DireccionIp }));
        }

        credencial.RegistrarUso(ahora, origen.DireccionIp);
        await contexto.SaveChangesAsync(cancelacion);

        return ResultadoDispositivo.Exito(new DispositivoAutenticado(cajaId, datos.Codigo, datos.Nombre, datos.SucursalId, datos.SucursalCodigo, credencial.Id));
    }


    public async Task<ResultadoEnrolamiento> SolicitarEnrolamientoAsync(SolicitudEnrolamientoCaja solicitud, OrigenSolicitud origen,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var sucursalCodigo = DosDigitos(solicitud.SucursalCodigo);
        var cajaCodigo = DosDigitos(solicitud.CajaCodigo);
        var huella = (solicitud.HuellaEquipo ?? string.Empty).Trim().ToUpperInvariant();
        var tokenHash = TokensSeguros.Hash(solicitud.Token ?? string.Empty);
        var ahora = reloj.Ahora();

        var caja = await (
                from c in contexto.Cajas
                join su in contexto.Sucursales on c.SucursalId equals su.Id
                where c.Codigo == cajaCodigo && su.Codigo == sucursalCodigo
                select new { c.Id, c.Codigo })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        var registrada = await contexto.SolicitudesEnrolamiento
            .SingleOrDefaultAsync(s => s.SucursalCodigo == sucursalCodigo && s.CajaCodigo == cajaCodigo && s.HuellaEquipo == huella, cancelacion);

        // Aprobada y todavía sin recoger: es el momento de entregarle la credencial, y solo a quien traiga el token de la solicitud.
        if (registrada is { Estado: EstadoSolicitudEnrolamiento.Aprobada } aprobada && caja is not null)
        {
            if (!aprobada.CoincideToken(tokenHash))
                return new ResultadoEnrolamiento(EstadoEnrolamientoCaja.NoDisponible, Mensaje: "La solicitud aprobada pertenece a otra instalación.");

            var emitida = await EmitirCredencialAsync(caja.Id, aprobada.ResueltaPor ?? "Enrolamiento", huella, aprobada.NombreEquipo, cancelacion);
            if (emitida is null)
                return new ResultadoEnrolamiento(EstadoEnrolamientoCaja.NoDisponible, Mensaje: "La caja ya no existe en el Central.");

            aprobada.MarcarEntregada(ahora);
            auditoria.Registrar(new EntradaAuditoria("Dispositivos.EnrolamientoEntregado", TipoEntidad, caja.Id.ToString(),
                new { Caja = cajaCodigo, aprobada.NombreEquipo, origen.DireccionIp }));
            await contexto.SaveChangesAsync(cancelacion);
            return new ResultadoEnrolamiento(EstadoEnrolamientoCaja.Entregada, emitida.Secreto);
        }

        // Ya recogió su credencial: no se entrega dos veces. Si el equipo la perdió, hay que liberarlo en el Central.
        if (registrada is { Estado: EstadoSolicitudEnrolamiento.Entregada })
            return new ResultadoEnrolamiento(EstadoEnrolamientoCaja.NoDisponible,
                Mensaje: "Este equipo ya recibió su credencial. Si la perdió, libere el equipo en el Central y vuelva a intentarlo.");

        if (registrada is { Estado: EstadoSolicitudEnrolamiento.Rechazada } rechazada)
        {
            // La rechazaron y el equipo vuelve a pedir: eso solo pasa si alguien reinició la caja, así que se le da otra
            // oportunidad de que la miren. El rechazo anterior queda en la auditoría.
            rechazada.Repetir(caja?.Id, Nombre(solicitud.NombreEquipo), tokenHash, origen.DireccionIp, ahora);
            auditoria.Registrar(new EntradaAuditoria("Dispositivos.EnrolamientoSolicitado", TipoEntidad, caja?.Id.ToString(),
                new { Sucursal = sucursalCodigo, Caja = cajaCodigo, Equipo = rechazada.NombreEquipo, Reintento = true, origen.DireccionIp }));
        }
        else if (registrada is not null)
        {
            registrada.Repetir(caja?.Id, Nombre(solicitud.NombreEquipo), tokenHash, origen.DireccionIp, ahora);
        }
        else
        {
            var nueva = SolicitudEnrolamiento.Crear(sucursalCodigo, cajaCodigo, caja?.Id, huella, Nombre(solicitud.NombreEquipo),
                tokenHash, origen.DireccionIp, ahora);
            contexto.SolicitudesEnrolamiento.Add(nueva);
            auditoria.Registrar(new EntradaAuditoria("Dispositivos.EnrolamientoSolicitado", TipoEntidad, caja?.Id.ToString(),
                new { Sucursal = sucursalCodigo, Caja = cajaCodigo, Equipo = nueva.NombreEquipo, origen.DireccionIp }));
        }

        await contexto.SaveChangesAsync(cancelacion);

        return caja is null
            ? new ResultadoEnrolamiento(EstadoEnrolamientoCaja.Pendiente,
                Mensaje: $"La caja {cajaCodigo} de la sucursal {sucursalCodigo} no existe en el Central: créela y luego acepte la solicitud.")
            : new ResultadoEnrolamiento(EstadoEnrolamientoCaja.Pendiente, Mensaje: "La solicitud quedó registrada: acéptela en el Central.");
    }

    public async Task<IReadOnlyList<DatosSolicitudEnrolamiento>> ListarSolicitudesAsync(bool soloPendientes, CancellationToken cancelacion = default)
    {
        var consulta = contexto.SolicitudesEnrolamiento.AsNoTracking();
        if (soloPendientes)
            consulta = consulta.Where(s => s.Estado == EstadoSolicitudEnrolamiento.Pendiente);

        // Las pendientes primero: son las únicas sobre las que hay algo que decidir.
        return await consulta
            .OrderBy(s => s.Estado == EstadoSolicitudEnrolamiento.Pendiente ? 0 : 1)
            .ThenByDescending(s => s.SolicitadaEn)
            .Select(s => new DatosSolicitudEnrolamiento(
                s.Id,
                s.SucursalCodigo,
                s.CajaCodigo,
                contexto.Cajas.Where(c => c.Id == s.CajaId).Select(c => c.Nombre).FirstOrDefault(),
                s.NombreEquipo,
                s.HuellaEquipo,
                s.DireccionIp,
                s.SolicitadaEn,
                s.Estado.ToString(),
                s.ResueltaEn,
                s.ResueltaPor,
                s.Motivo,
                s.CajaId != null,
                contexto.CredencialesDispositivo.Any(c => c.CajaId == s.CajaId && c.RevocadaEn == null && c.HuellaEquipo != null)))
            .ToListAsync(cancelacion);
    }

    public async Task<string?> AprobarSolicitudAsync(int solicitudId, UsuarioAuditoria usuario, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var solicitud = await contexto.SolicitudesEnrolamiento.SingleOrDefaultAsync(s => s.Id == solicitudId, cancelacion);
        if (solicitud is null)
            return "La solicitud ya no existe.";
        if (!solicitud.EsperaDecision)
            return "La solicitud ya fue resuelta.";

        var caja = await (
                from c in contexto.Cajas
                join su in contexto.Sucursales on c.SucursalId equals su.Id
                where c.Codigo == solicitud.CajaCodigo && su.Codigo == solicitud.SucursalCodigo
                select new { c.Id, c.Codigo })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);
        if (caja is null)
            return $"La caja {solicitud.CajaCodigo} de la sucursal {solicitud.SucursalCodigo} no existe en el Central: créela primero.";

        // Una caja cuya credencial ya está atada a un equipo no se enrola en otro sin liberarla antes: si no, bastaría
        // aceptar una solicitud para mudar una caja que está trabajando.
        var atada = await contexto.CredencialesDispositivo.AsNoTracking()
            .AnyAsync(c => c.CajaId == caja.Id && c.RevocadaEn == null && c.HuellaEquipo != null, cancelacion);
        if (atada)
            return "La credencial de esa caja está atada a otro equipo: libere el equipo antes de aceptar esta solicitud.";

        solicitud.Aprobar(caja.Id, reloj.Ahora(), usuario.Nombre);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.EnrolamientoAprobado", TipoEntidad, caja.Id.ToString(),
            new { Caja = caja.Codigo, solicitud.NombreEquipo, solicitud.DireccionIp }, Usuario: usuario));
        await contexto.SaveChangesAsync(cancelacion);
        return null;
    }

    public async Task<bool> RechazarSolicitudAsync(int solicitudId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentNullException.ThrowIfNull(usuario);

        var solicitud = await contexto.SolicitudesEnrolamiento.SingleOrDefaultAsync(s => s.Id == solicitudId, cancelacion);
        if (solicitud is null || !solicitud.EsperaDecision)
            return false;

        solicitud.Rechazar(reloj.Ahora(), usuario.Nombre, motivo);
        auditoria.Registrar(new EntradaAuditoria("Dispositivos.EnrolamientoRechazado", TipoEntidad, solicitud.CajaId?.ToString(),
            new { Sucursal = solicitud.SucursalCodigo, Caja = solicitud.CajaCodigo, solicitud.NombreEquipo }, motivo.Trim(), usuario));
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> LiberarEquipoAsync(int cajaId, string motivo, UsuarioAuditoria usuario, CancellationToken cancelacion = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ArgumentNullException.ThrowIfNull(usuario);

        var credencial = await contexto.CredencialesDispositivo.SingleOrDefaultAsync(c => c.CajaId == cajaId && c.RevocadaEn == null, cancelacion);
        if (credencial is null)
            return false;

        var equipo = credencial.NombreEquipo;
        credencial.LiberarEquipo();

        // Las solicitudes de esa caja se retiran: el equipo nuevo entra como una solicitud limpia y el viejo, si insiste,
        // vuelve a aparecer para que alguien decida.
        var previas = await contexto.SolicitudesEnrolamiento.Where(s => s.CajaId == cajaId).ToListAsync(cancelacion);
        contexto.SolicitudesEnrolamiento.RemoveRange(previas);

        auditoria.Registrar(new EntradaAuditoria("Dispositivos.EquipoLiberado", TipoEntidad, cajaId.ToString(),
            new { EquipoAnterior = equipo, SolicitudesRetiradas = previas.Count }, motivo.Trim(), usuario));
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    private static string Nombre(string? nombreEquipo) =>
        string.IsNullOrWhiteSpace(nombreEquipo) ? "Equipo sin nombre" : nombreEquipo.Trim();

    /// <summary>Los códigos de sucursal y caja son de dos dígitos: se acepta «1» y se usa «01», como en el resto del sistema.</summary>
    private static string DosDigitos(string? valor) =>
        int.TryParse((valor ?? string.Empty).Trim(), out var numero) && numero is >= 1 and <= CodigosCatalogo.MaximoSucursalCaja
            ? numero.ToString("00", System.Globalization.CultureInfo.InvariantCulture)
            : throw new ArgumentException($"El código debe ser de dos dígitos, entre 01 y {CodigosCatalogo.MaximoSucursalCaja}.", nameof(valor));

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
