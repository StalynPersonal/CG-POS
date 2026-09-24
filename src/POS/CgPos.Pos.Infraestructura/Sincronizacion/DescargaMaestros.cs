using CgPos.Contratos.Sincronizacion;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

internal sealed class DescargaMaestros(
    ContextoDatosPos contexto,
    IClienteCentral central,
    IEstadoConexionCentral conexion,
    ICargaInicial cargaInicial,
    ICargaMaestros cargaMaestros,
    IProgresoActualizacion progreso,
    TimeProvider reloj,
    ILogger<DescargaMaestros> registro) : IDescargaMaestros
{
    /// <summary>
    /// Trae los maestros del Central. Vienen por páginas: cada una se aplica y se guarda su marca antes de pedir la
    /// siguiente, así que si se corta la red en el medio no se pierde lo que ya entró y la próxima vez se retoma desde ahí.
    /// </summary>
    public async Task<ResultadoDescargaMaestros> DescargarAsync(CancellationToken cancelacion = default)
    {
        if (!central.Configurado)
            return new ResultadoDescargaMaestros(false, 0, 0, 0, null);

        // Desde aquí hasta el final la pantalla muestra que se está actualizando: una caja nueva tarda, y sin este aviso
        // parece que está rota.
        using var enCurso = progreso.Comenzar("Actualizando la caja con los datos del Central");

        var desde = await MarcaAsync(cancelacion);
        var (creados, actualizados, paginas) = (0, 0, 0);

        // La cuenta se pide una sola vez, en la primera tanda, y se arrastra por todas: así la pantalla dice «Clientes
        // 2,000 de 705,706» y el número sube, en vez de reiniciarse con cada tanda.
        var cuenta = new AvanceMaestros();

        while (true)
        {
            var pagina = await PaginaAsync(desde, paginas == 0, cuenta, cancelacion);
            creados += pagina.Creados;
            actualizados += pagina.Actualizados;

            if (!pagina.Exitosa)
                return pagina with { Creados = creados, Actualizados = actualizados };

            paginas++;
            if (pagina.Completo)
            {
                progreso.Terminar(null);
                if (paginas > 1)
                    registro.LogInformation("Aprovisionamiento terminado en {Paginas} páginas: {Creados} creados, {Actualizados} actualizados",
                        paginas, creados, actualizados);

                return pagina with { Creados = creados, Actualizados = actualizados };
            }

            // El Central cortó el rango porque no cabía: se sigue desde donde quedó. Si no avanzó, se corta aquí en vez de
            // quedarse pidiendo lo mismo para siempre; el próximo ciclo lo vuelve a intentar.
            if (pagina.Hasta <= desde)
            {
                registro.LogWarning("El Central dice que falta más pero no avanzó de la versión {Desde}: se deja para el próximo ciclo.", desde);
                progreso.Terminar(null);
                return pagina with { Creados = creados, Actualizados = actualizados };
            }

            desde = pagina.Hasta;
            progreso.Etapa($"Actualizando la caja con los datos del Central (parte {paginas + 1})");
        }
    }

    private async Task<long> MarcaAsync(CancellationToken cancelacion) =>
        (await contexto.MarcasSincronizacion.AsNoTracking()
            .Where(m => m.Clave == MarcaSincronizacion.VersionMaestros)
            .Select(m => (long?)m.Valor)
            .FirstOrDefaultAsync(cancelacion)) ?? 0;

    private async Task<ResultadoDescargaMaestros> PaginaAsync(long desde, bool pedirTotales, AvanceMaestros cuenta, CancellationToken cancelacion)
    {
        var marca = await contexto.MarcasSincronizacion.SingleOrDefaultAsync(m => m.Clave == MarcaSincronizacion.VersionMaestros, cancelacion);
        var resultado = await central.DescargarMaestrosAsync(desde, pedirTotales, cancelacion);
        var ahora = reloj.Ahora();
        if (resultado.Paquete is not { } paquete)
        {
            if (resultado.CentralRespondio)
                conexion.RegistrarContacto(ahora);
            else
                conexion.RegistrarFallo(ahora, resultado.Error);

            progreso.Terminar(resultado.Error);
            return new ResultadoDescargaMaestros(false, desde, 0, 0, resultado.Error);
        }

        conexion.RegistrarContacto(ahora);
        if (paquete.Totales is { Count: > 0 } totales)
            cuenta.FijarTotales(totales);

        // Con el registro en Information se ve desde el arranque qué trae cada bajada, sin tener que subir el nivel.
        registro.LogInformation("Bajando maestros del Central (versión {Desde} a {Hasta}): {Articulos} artículos, {Clientes} clientes, {Promociones} promociones.",
            paquete.Desde, paquete.Hasta, paquete.Maestros?.Articulos?.Count ?? 0, paquete.Maestros?.Clientes?.Count ?? 0, paquete.Maestros?.Promociones?.Count ?? 0);

        var creados = 0;
        var actualizados = 0;

        try
        {
            // Primero la organización y la seguridad: los maestros referencian cajas y sucursales.
            if (paquete.Organizacion is { } organizacion)
            {
                progreso.Etapa("Actualizando la caja con la empresa, la sucursal y los usuarios");
                var carga = await cargaInicial.AplicarAsync(organizacion, cancelacion);
                creados += carga.Creados;
                actualizados += carga.Actualizados;
            }

            if (paquete.Maestros is { } maestros)
            {
                progreso.Etapa("Actualizando la caja con artículos y precios");
                var carga = await cargaMaestros.AplicarAsync(maestros, "Central", cuenta, cancelacion);
                creados += carga.Creados;
                actualizados += carga.Actualizados;
            }

            if (paquete.EstadosDgii is { Count: > 0 } estados)
                actualizados += await AplicarEstadosDgiiAsync(estados, ahora, cancelacion);

            if (paquete.Completo && paquete.ParametrosVigentes is { Count: > 0 } vigentes)
                actualizados += await BorrarParametrosEliminadosAsync(vigentes, cancelacion);
        }
        catch (Exception excepcion) when (excepcion is CargaInicialInvalidaExcepcion or CargaMaestrosInvalidaExcepcion)
        {
            // La marca no avanza: el próximo ciclo vuelve a pedir lo mismo y lo aplica cuando el Central lo corrija.
            registro.LogError("Los maestros del Central (versión {Desde} a {Hasta}) no se pudieron aplicar: {Error}", paquete.Desde, paquete.Hasta, excepcion.Message);
            progreso.Terminar(excepcion.Message);
            return new ResultadoDescargaMaestros(false, desde, 0, 0, excepcion.Message);
        }

        if (paquete.Hasta > desde)
        {
            if (marca is null)
                contexto.MarcasSincronizacion.Add(MarcaSincronizacion.Crear(MarcaSincronizacion.VersionMaestros, paquete.Hasta, ahora));
            else
                marca.Actualizar(paquete.Hasta, ahora);

            await contexto.SaveChangesAsync(cancelacion);
        }

        if (creados + actualizados > 0)
            registro.LogInformation("Maestros del Central aplicados hasta la versión {Hasta}: {Creados} creados, {Actualizados} actualizados", paquete.Hasta, creados, actualizados);

        return new ResultadoDescargaMaestros(true, Math.Max(desde, paquete.Hasta), creados, actualizados, null, paquete.Completo);
    }

    /// <summary>
    /// Resultado que la DGII dio a los e-CF ya emitidos por esta caja (RF-223). Solo cambia los documentos cuyo estado es distinto,
    /// para no repetir el historial en cada descarga.
    /// </summary>
    /// <summary>
    /// Borra los parámetros que ya no existen en el Central (RN-24): una baja no viaja en el rango de versiones, así que el Central
    /// manda en cada descarga todos los que hoy aplican a esta caja y lo que sobre aquí se elimina.
    /// </summary>
    private async Task<int> BorrarParametrosEliminadosAsync(IReadOnlyList<Contratos.CargaInicial.ParametroReferencia> vigentes, CancellationToken cancelacion)
    {
        var idsSucursales = await contexto.Sucursales.ToDictionaryAsync(s => s.Codigo, s => s.Id, cancelacion);
        var idsCajas = await CargaInicial.ServicioCargaInicial.IdsCajasAsync(contexto, cancelacion);
        var claves = vigentes
            .Where(v => (v.SucursalCodigo is null || idsSucursales.ContainsKey(v.SucursalCodigo))
                        && (v.CajaCodigo is null || (v.SucursalCodigo is { } s && idsCajas.ContainsKey((s, v.CajaCodigo)))))
            .Select(v => (v.Clave.Trim(), CargaInicial.ServicioCargaInicial.AmbitoLocal(v.SucursalCodigo, v.CajaCodigo, idsSucursales, idsCajas)))
            .ToHashSet();

        var sobrantes = (await contexto.Parametros.ToListAsync(cancelacion))
            .Where(p => !claves.Contains((p.Clave, (p.SucursalId, p.CajaId))))
            .ToList();
        if (sobrantes.Count == 0)
            return 0;

        contexto.Parametros.RemoveRange(sobrantes);
        await contexto.SaveChangesAsync(cancelacion);
        registro.LogInformation("Se borraron {Cantidad} parámetros que ya no existen en el Central: {Claves}", sobrantes.Count,
            string.Join(", ", sobrantes.Select(p => p.Clave)));
        return sobrantes.Count;
    }

    private async Task<int> AplicarEstadosDgiiAsync(IReadOnlyList<EstadoDgiiCarga> estados, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var encfs = estados.Select(e => e.Encf).ToList();
        var documentos = await contexto.DocumentosElectronicos.Where(d => encfs.Contains(d.Encf)).ToDictionaryAsync(d => d.Encf, cancelacion);
        var cambiados = 0;

        foreach (var estado in estados)
        {
            if (!documentos.TryGetValue(estado.Encf, out var documento))
                continue;

            var nuevo = estado.Estado switch
            {
                EstadoEnvioDgii.Aceptado => EstadoDocumentoElectronico.Aceptado,
                EstadoEnvioDgii.AceptadoCondicional => EstadoDocumentoElectronico.AceptadoCondicional,
                EstadoEnvioDgii.Rechazado => EstadoDocumentoElectronico.Rechazado,
                _ => EstadoDocumentoElectronico.EnProceso,
            };
            if (documento.Estado == nuevo)
                continue;

            documento.CambiarEstado(nuevo, estado.Mensaje, estado.EstadoEn ?? ahora);
            cambiados++;
        }

        if (cambiados > 0)
        {
            await contexto.SaveChangesAsync(cancelacion);
            registro.LogInformation("Resultados de la DGII aplicados a {Cantidad} e-CF de la caja", cambiados);
        }

        return cambiados;
    }
}
