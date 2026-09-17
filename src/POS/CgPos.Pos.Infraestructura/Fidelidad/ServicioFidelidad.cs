using CgPos.Contratos.Sincronizacion;
using CgPos.Contratos.Fidelidad;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Fidelidad;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Fidelidad;

internal static class ConsultasFidelidad
{
    /// <summary>Saldo disponible del miembro con los movimientos de la caja posteriores a su última sincronización.</summary>
    public static async Task<int> SaldoPuntosAsync(this ContextoDatosPos contexto, MiembroFidelidad miembro, DateOnly hoy, CancellationToken cancelacion)
    {
        var desde = miembro.SaldoSincronizadoEn;
        var movimientos = await contexto.MovimientosPuntos.AsNoTracking()
            .Where(m => m.MiembroId == miembro.Id && (desde == null || m.Fecha > desde))
            .ToListAsync(cancelacion);
        return miembro.SaldoDisponible(movimientos, hoy);
    }

    public static async Task<DatosMiembroFidelidad> DatosMiembroAsync(this ContextoDatosPos contexto, IParametros parametros, MiembroFidelidad miembro, int cajaId,
        DateOnly hoy, CancellationToken cancelacion)
    {
        var saldo = await contexto.SaldoPuntosAsync(miembro, hoy, cancelacion);
        var valorPunto = await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.ValorPuntoFidelidad, cajaId, cancelacion);
        return new DatosMiembroFidelidad(miembro.Id, miembro.Cedula, miembro.Nombre, miembro.Telefono, miembro.Correo,
            await contexto.NombreNivelAsync(miembro.NivelId, cancelacion), saldo, valorPunto is { } valor ? Math.Max(0, saldo) * valor : null,
            miembro.PuntosPorVencer, miembro.ProximoVencimiento, miembro.SaldoSincronizadoEn, miembro.InscritoEnCaja);
    }

    public static async Task<string?> NombreNivelAsync(this ContextoDatosPos contexto, int? nivelId, CancellationToken cancelacion) =>
        nivelId is { } id
            ? await contexto.NivelesFidelidad.AsNoTracking().Where(n => n.Id == id).Select(n => n.Nombre).FirstOrDefaultAsync(cancelacion)
            : null;
}

internal sealed class ServicioFidelidad(
    ContextoDatosPos contexto,
    IParametros parametros,
    IConsultaDocumentos consultaDocumentos,
    IBandejaSalida bandejaSalida,
    IAuditoria auditoria,
    TimeProvider reloj) : IServicioFidelidad
{
    private DateOnly Hoy => DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

    public async Task<RespuestaFidelidad> ConsultarAsync(SesionUsuario sesion, string cedula, CancellationToken cancelacion = default)
    {
        if (Normalizar(cedula) is not { } normalizada)
            return new RespuestaFidelidad(CodigoResultadoFidelidad.CedulaInvalida, "Digite la cédula del cliente (11 dígitos).", null);

        var miembro = await contexto.MiembrosFidelidad.AsNoTracking().SingleOrDefaultAsync(m => m.Cedula == normalizada, cancelacion);
        if (miembro is not { Activo: true })
            return new RespuestaFidelidad(CodigoResultadoFidelidad.NoInscrito, $"La cédula {normalizada} no está inscrita en el programa de fidelidad.", null);

        return new RespuestaFidelidad(CodigoResultadoFidelidad.Correcto, null, await contexto.DatosMiembroAsync(parametros, miembro, sesion.CajaId, Hoy, cancelacion));
    }

    public async Task<RespuestaFidelidad> InscribirAsync(SesionUsuario sesion, SolicitudInscripcionFidelidad solicitud, CancellationToken cancelacion = default)
    {
        var validacion = DocumentoIdentidad.Validar(solicitud.Cedula);
        if (validacion is not { FormatoValido: true, Tipo: TipoDocumentoIdentidad.Cedula })
            return new RespuestaFidelidad(CodigoResultadoFidelidad.CedulaInvalida, "El programa de fidelidad se identifica con la cédula (11 dígitos).", null);

        var existente = await contexto.MiembrosFidelidad.AsNoTracking().SingleOrDefaultAsync(m => m.Cedula == validacion.Documento, cancelacion);
        if (existente is not null)
            return new RespuestaFidelidad(CodigoResultadoFidelidad.YaInscrito, $"La cédula {validacion.Documento} ya está inscrita a nombre de {existente.Nombre}.",
                await contexto.DatosMiembroAsync(parametros, existente, sesion.CajaId, Hoy, cancelacion));

        // Hay cédulas antiguas que no cumplen el dígito verificador: se aceptan si el padrón DGII las conoce.
        var consulta = await consultaDocumentos.ConsultarAsync(validacion.Documento, cancelacion);
        if (!validacion.DigitoVerificadorValido && !consulta.EnPadron)
            return new RespuestaFidelidad(CodigoResultadoFidelidad.CedulaInvalida, $"La cédula {validacion.Documento} no es válida (dígito verificador).", null);

        var nombre = string.IsNullOrWhiteSpace(solicitud.Nombre) ? consulta.Cliente?.Nombre ?? consulta.RazonSocial : solicitud.Nombre.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
            return new RespuestaFidelidad(CodigoResultadoFidelidad.NombreRequerido, "La cédula no está en el padrón DGII: indique el nombre del cliente.", null);

        MiembroFidelidad miembro;
        try
        {
            miembro = MiembroFidelidad.Inscribir(validacion.Documento, nombre, solicitud.Telefono, solicitud.Correo, reloj.GetUtcNow());
        }
        catch (ArgumentException excepcion)
        {
            return new RespuestaFidelidad(CodigoResultadoFidelidad.DatosInvalidos, excepcion.Message, null);
        }

        contexto.MiembrosFidelidad.Add(miembro);
        bandejaSalida.Encolar("Fidelidad.Inscripcion", miembro.Cedula,
            new DocumentoInscripcionFidelidad(miembro.Cedula, miembro.Nombre, miembro.Telefono, miembro.Correo, sesion.Nombre, miembro.InscritoEn));
        auditoria.Registrar(new EntradaAuditoria("Fidelidad.Inscripcion", "MiembroFidelidad", miembro.Cedula,
            Detalle: new { miembro.Nombre, miembro.Telefono, miembro.Correo },
            Usuario: new UsuarioAuditoria(sesion.UsuarioId, sesion.Nombre)));

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException)
        {
            // Otra caja u otra pantalla inscribió la misma cédula al mismo tiempo.
            contexto.ChangeTracker.Clear();
            return new RespuestaFidelidad(CodigoResultadoFidelidad.YaInscrito, $"La cédula {miembro.Cedula} ya está inscrita.", null);
        }

        return new RespuestaFidelidad(CodigoResultadoFidelidad.Correcto, $"{miembro.Nombre} quedó inscrito en el programa de fidelidad.",
            await contexto.DatosMiembroAsync(parametros, miembro, sesion.CajaId, Hoy, cancelacion));
    }

    private static string? Normalizar(string? cedula)
    {
        try
        {
            return MiembroFidelidad.ValidarCedula(cedula);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
