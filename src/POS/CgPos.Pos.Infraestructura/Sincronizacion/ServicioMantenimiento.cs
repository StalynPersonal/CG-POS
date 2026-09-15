using System.Buffers.Binary;
using System.Data.Common;
using System.Globalization;
using System.Net.Sockets;
using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Ecf;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>Valores técnicos del mantenimiento; cada instalación los ajusta en appsettings.</summary>
/// <param name="HoraRespaldo">Nula si el respaldo automático está desactivado.</param>
/// <param name="ServidorHora">Nulo si no se verifica la hora.</param>
internal sealed record OpcionesMantenimiento(TimeSpan Intervalo, string? CarpetaRespaldo, int? HoraRespaldo, string? ServidorHora)
{
    public static OpcionesMantenimiento Leer(IConfiguration configuracion) => new(
        TimeSpan.FromMinutes(Math.Max(1, int.TryParse(configuracion[ClavesMantenimiento.IntervaloMinutos], out var minutos) ? minutos : 60)),
        Texto(configuracion[ClavesMantenimiento.CarpetaRespaldo]),
        int.TryParse(configuracion[ClavesMantenimiento.HoraRespaldo], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hora) && hora is >= 0 and <= 23 ? hora : null,
        Texto(configuracion[ClavesMantenimiento.ServidorHora]));

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

internal sealed class ServicioMantenimiento(
    ContextoDatosPos contexto,
    IParametros parametros,
    IContextoCaja contextoCaja,
    OpcionesSincronizacion opcionesSincronizacion,
    OpcionesMantenimiento opciones,
    EstadoMantenimiento estado,
    TimeProvider reloj,
    ILogger<ServicioMantenimiento> registro) : IServicioMantenimiento
{
    private string NombreBaseDatos => contexto.Database.GetDbConnection().Database;

    public bool CorrespondeRespaldo(DateTimeOffset ahoraLocal) =>
        opciones.HoraRespaldo is { } hora
        && ahoraLocal.Hour >= hora
        && (estado.UltimoRespaldoCorrecto is not { } ultimo || TimeZoneInfo.ConvertTime(ultimo, reloj.LocalTimeZone).Date < ahoraLocal.Date);

    public async Task<ResultadoRespaldo> RespaldarAsync(CancellationToken cancelacion = default)
    {
        var fecha = reloj.GetUtcNow();
        var carpeta = opciones.CarpetaRespaldo ?? await CarpetaRespaldoInstanciaAsync(cancelacion);
        if (carpeta is null)
            return Registrar(new ResultadoRespaldo(false, null, $"No hay carpeta de respaldos: configure {ClavesMantenimiento.CarpetaRespaldo}.", fecha));

        var baseDatos = NombreBaseDatos;
        var ruta = Path.Combine(carpeta, $"{baseDatos}_{TimeZoneInfo.ConvertTime(fecha, reloj.LocalTimeZone):yyyyMMdd_HHmmss}.bak");
        try
        {
            // BACKUP no admite transacción de usuario; el nombre de la base va escapado y la ruta como parámetro.
            contexto.Database.SetCommandTimeout(TimeSpan.FromMinutes(30));
            await contexto.Database.ExecuteSqlRawAsync(
                $"BACKUP DATABASE [{baseDatos.Replace("]", "]]", StringComparison.Ordinal)}] TO DISK = @ruta WITH INIT, CHECKSUM, NAME = @nombre",
                [new SqlParameter("@ruta", ruta), new SqlParameter("@nombre", $"CG-POS {baseDatos}")],
                cancelacion);

            estado.UltimoRespaldoCorrecto = fecha;
            registro.LogInformation("Respaldo de la base {BaseDatos} en {Ruta}", baseDatos, ruta);
            return Registrar(new ResultadoRespaldo(true, ruta, null, fecha));
        }
        catch (SqlException excepcion)
        {
            registro.LogWarning(excepcion, "Falló el respaldo de la base {BaseDatos} en {Ruta}", baseDatos, ruta);
            return Registrar(new ResultadoRespaldo(false, ruta, excepcion.Message, fecha));
        }
    }

    public async Task<ResultadoPurga> PurgarAsync(CancellationToken cancelacion = default)
    {
        var cajaId = contextoCaja.CajaId;
        var ahora = reloj.GetUtcNow();

        var xmlEliminados = 0;
        if (await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.DiasRetencionXmlEnviados, cajaId, cancelacion) is { } diasXml)
        {
            // Solo los XML ya confirmados por el Central y movidos a Enviados; lo pendiente nunca se purga (RN-19).
            var limite = ahora.AddDays(-(double)diasXml);
            var enviados = Path.GetFullPath(Path.Combine(opcionesSincronizacion.CarpetaXml, RutasXmlEcf.Enviados)) + Path.DirectorySeparatorChar;
            var rutas = await contexto.DocumentosElectronicos.AsNoTracking()
                .Where(d => d.Estado != EstadoDocumentoElectronico.Emitido && d.Estado != EstadoDocumentoElectronico.PendienteSincronizar && d.FechaFirma < limite)
                .Select(d => d.RutaXml)
                .ToListAsync(cancelacion);

            foreach (var ruta in rutas.Where(r => Path.GetFullPath(r).StartsWith(enviados, StringComparison.OrdinalIgnoreCase) && File.Exists(r)))
                xmlEliminados += Borrar(ruta) ? 1 : 0;
        }

        var mensajesEliminados = 0;
        if (await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.DiasRetencionMensajesConfirmados, cajaId, cancelacion) is { } diasMensajes)
        {
            var limite = ahora.AddDays(-(double)diasMensajes);
            mensajesEliminados = await contexto.BandejaSalida
                .Where(m => m.Estado == EstadoMensajeSalida.Confirmado && m.ConfirmadoEn < limite)
                .ExecuteDeleteAsync(cancelacion);
        }

        // Los respaldos solo se borran de una carpeta configurada y accesible desde la caja.
        var respaldosEliminados = 0;
        if (opciones.CarpetaRespaldo is { } carpeta && Directory.Exists(carpeta)
            && await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.DiasRetencionRespaldos, cajaId, cancelacion) is { } diasRespaldos)
        {
            var limite = ahora.AddDays(-(double)diasRespaldos).UtcDateTime;
            foreach (var archivo in Directory.EnumerateFiles(carpeta, $"{NombreBaseDatos}_*.bak").Where(a => File.GetLastWriteTimeUtc(a) < limite))
                respaldosEliminados += Borrar(archivo) ? 1 : 0;
        }

        if (xmlEliminados + mensajesEliminados + respaldosEliminados > 0)
            registro.LogInformation("Purga: {Xml} XML, {Mensajes} mensajes y {Respaldos} respaldos eliminados", xmlEliminados, mensajesEliminados, respaldosEliminados);

        return new ResultadoPurga(xmlEliminados, mensajesEliminados, respaldosEliminados);
    }

    public async Task<ResultadoHora> VerificarHoraAsync(CancellationToken cancelacion = default)
    {
        var fecha = reloj.GetUtcNow();
        if (opciones.ServidorHora is not { } servidor)
            return estado.UltimaHora = new ResultadoHora(false, null, null, "No hay servidor de hora configurado.", fecha);

        try
        {
            var desfase = await HoraNtp.ConsultarDesfaseAsync(servidor, TimeSpan.FromSeconds(5), TimeProvider.System, cancelacion);
            return estado.UltimaHora = new ResultadoHora(true, desfase, servidor, null, fecha);
        }
        catch (Exception excepcion) when (excepcion is SocketException or FormatException or OperationCanceledException && !cancelacion.IsCancellationRequested)
        {
            // Sin red la caja sigue operando; la hora se vuelve a verificar en el próximo ciclo.
            return estado.UltimaHora = new ResultadoHora(false, null, servidor, $"No se pudo consultar el servidor de hora {servidor}.", fecha);
        }
    }

    public async Task<IReadOnlyList<string>> ObtenerAlertasAsync(CancellationToken cancelacion = default)
    {
        var alertas = new List<string>();
        var cajaId = contextoCaja.CajaId;
        var ahora = reloj.GetUtcNow();

        if (await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.AlertaTamanoBaseDatosMb, cajaId, cancelacion) is { } limiteMb)
        {
            var tamanoMb = await TamanoBaseDatosMbAsync(cancelacion);
            if (tamanoMb >= limiteMb)
                alertas.Add($"La base de datos de la caja ocupa {tamanoMb:N0} MB (alerta desde {limiteMb:N0} MB): revise la purga y el espacio disponible.");
        }

        if (await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.HorasAlertaPendientes, cajaId, cancelacion) is { } horas)
        {
            var limite = ahora.AddHours(-(double)horas);
            var atrasados = await contexto.BandejaSalida.CountAsync(m => m.Estado != EstadoMensajeSalida.Confirmado && m.CreadoEn < limite, cancelacion);
            if (atrasados > 0)
                alertas.Add($"{atrasados:N0} documento(s) llevan más de {horas:0.#} horas sin sincronizar con el Central.");
        }

        if (estado.UltimaHora is { Verificada: true, Desfase: { } desfase }
            && await parametros.ObtenerDecimalOpcionalAsync(ClavesParametros.ToleranciaRelojSegundos, cajaId, cancelacion) is { } tolerancia
            && Math.Abs(desfase.TotalSeconds) > (double)tolerancia)
            alertas.Add($"La hora de la caja difiere {Math.Abs(desfase.TotalSeconds):N0} segundos del servidor de hora: ajústela, los e-CF registran la hora de firma.");

        if (estado.UltimoRespaldo is { Correcto: false } fallido)
            alertas.Add($"El último respaldo de la base de datos falló: {fallido.Error}");

        return alertas;
    }

    private ResultadoRespaldo Registrar(ResultadoRespaldo resultado) => estado.UltimoRespaldo = resultado;

    private async Task<decimal> TamanoBaseDatosMbAsync(CancellationToken cancelacion) =>
        await contexto.Database
            .SqlQueryRaw<decimal>("SELECT CAST(SUM(CAST(size AS bigint)) * 8 / 1024.0 AS decimal(18, 2)) AS [Value] FROM sys.database_files WHERE type = 0")
            .SingleAsync(cancelacion);

    /// <summary>Carpeta de respaldos de la instancia de SQL Server (registro de la instancia; SERVERPROPERTY solo existe desde 2016).</summary>
    private async Task<string?> CarpetaRespaldoInstanciaAsync(CancellationToken cancelacion)
    {
        var conexion = contexto.Database.GetDbConnection();
        await contexto.Database.OpenConnectionAsync(cancelacion);
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = @"EXEC master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'Software\Microsoft\MSSQLServer\MSSQLServer', N'BackupDirectory'";
            await using var lector = await comando.ExecuteReaderAsync(cancelacion);
            return await lector.ReadAsync(cancelacion) && !lector.IsDBNull(1) ? lector.GetString(1) : null;
        }
        catch (DbException excepcion)
        {
            registro.LogWarning(excepcion, "No se pudo leer la carpeta de respaldos de SQL Server.");
            return null;
        }
        finally
        {
            await contexto.Database.CloseConnectionAsync();
        }
    }

    private bool Borrar(string ruta)
    {
        try
        {
            File.Delete(ruta);
            return true;
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            registro.LogWarning(excepcion, "No se pudo borrar {Ruta} en la purga.", ruta);
            return false;
        }
    }
}

/// <summary>Consulta SNTP (RFC 4330) para verificar el reloj de la caja.</summary>
internal static class HoraNtp
{
    private static readonly DateTimeOffset EpocaNtp = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Las marcas de 32 bits reinician el 7 de febrero de 2036: las que tienen el bit alto en cero corresponden a la era siguiente.</summary>
    private static readonly DateTimeOffset EpocaNtpSiguiente = new(2036, 2, 7, 6, 28, 16, TimeSpan.Zero);

    public static async Task<TimeSpan> ConsultarDesfaseAsync(string servidor, TimeSpan tiempoEspera, TimeProvider reloj, CancellationToken cancelacion)
    {
        var solicitud = new byte[48];
        solicitud[0] = 0x1B; // Sin aviso de segundo intercalar, versión 3, modo cliente.

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
        limite.CancelAfter(tiempoEspera);
        using var udp = new UdpClient();
        udp.Connect(servidor, 123);

        var enviado = reloj.GetUtcNow();
        await udp.SendAsync(solicitud, limite.Token);
        var respuesta = await udp.ReceiveAsync(limite.Token);
        var recibido = reloj.GetUtcNow();

        return CalcularDesfase(enviado, LeerMarca(respuesta.Buffer, 32), LeerMarca(respuesta.Buffer, 40), recibido);
    }

    /// <summary>Desfase = ((T2 − T1) + (T3 − T4)) / 2: positivo si el servidor va adelantado respecto de la caja.</summary>
    public static TimeSpan CalcularDesfase(DateTimeOffset enviado, DateTimeOffset recibidoServidor, DateTimeOffset transmitidoServidor, DateTimeOffset recibido) =>
        ((recibidoServidor - enviado) + (transmitidoServidor - recibido)) / 2;

    public static DateTimeOffset LeerMarca(byte[] paquete, int posicion)
    {
        if (paquete.Length < posicion + 8)
            throw new FormatException("La respuesta del servidor de hora está incompleta.");

        var segundos = BinaryPrimitives.ReadUInt32BigEndian(paquete.AsSpan(posicion));
        var fraccion = BinaryPrimitives.ReadUInt32BigEndian(paquete.AsSpan(posicion + 4));
        var epoca = (segundos & 0x8000_0000) == 0 ? EpocaNtpSiguiente : EpocaNtp;
        return epoca.AddTicks((long)segundos * TimeSpan.TicksPerSecond + (long)(fraccion * (double)TimeSpan.TicksPerSecond / 4_294_967_296d));
    }

    public static void EscribirMarca(byte[] paquete, int posicion, DateTimeOffset fecha)
    {
        var epoca = fecha >= EpocaNtpSiguiente ? EpocaNtpSiguiente : EpocaNtp;
        var transcurrido = fecha - epoca;
        var segundos = (uint)Math.Floor(transcurrido.TotalSeconds);
        var fraccion = (uint)((transcurrido.Ticks % TimeSpan.TicksPerSecond) * 4_294_967_296d / TimeSpan.TicksPerSecond);
        BinaryPrimitives.WriteUInt32BigEndian(paquete.AsSpan(posicion), segundos);
        BinaryPrimitives.WriteUInt32BigEndian(paquete.AsSpan(posicion + 4), fraccion);
    }
}
