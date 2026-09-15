using CgPos.Pos.Aplicacion.CargaInicial;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

internal sealed class DescargaMaestros(
    ContextoDatosPos contexto,
    IClienteCentral central,
    IEstadoConexionCentral conexion,
    ICargaInicial cargaInicial,
    ICargaMaestros cargaMaestros,
    TimeProvider reloj,
    ILogger<DescargaMaestros> registro) : IDescargaMaestros
{
    public async Task<ResultadoDescargaMaestros> DescargarAsync(CancellationToken cancelacion = default)
    {
        if (!central.Configurado)
            return new ResultadoDescargaMaestros(false, 0, 0, 0, null);

        var marca = await contexto.MarcasSincronizacion.SingleOrDefaultAsync(m => m.Clave == MarcaSincronizacion.VersionMaestros, cancelacion);
        var desde = marca?.Valor ?? 0;

        var resultado = await central.DescargarMaestrosAsync(desde, cancelacion);
        var ahora = reloj.GetUtcNow();
        if (resultado.Paquete is not { } paquete)
        {
            if (resultado.CentralRespondio)
                conexion.RegistrarContacto(ahora);
            else
                conexion.RegistrarFallo(ahora, resultado.Error);

            return new ResultadoDescargaMaestros(false, desde, 0, 0, resultado.Error);
        }

        conexion.RegistrarContacto(ahora);
        var creados = 0;
        var actualizados = 0;

        try
        {
            // Primero la organización y la seguridad: los maestros referencian cajas y sucursales.
            if (paquete.Organizacion is { } organizacion)
            {
                var carga = await cargaInicial.AplicarAsync(organizacion, cancelacion);
                creados += carga.Creados;
                actualizados += carga.Actualizados;
            }

            if (paquete.Maestros is { } maestros)
            {
                var carga = await cargaMaestros.AplicarAsync(maestros, "Central", cancelacion);
                creados += carga.Creados;
                actualizados += carga.Actualizados;
            }
        }
        catch (Exception excepcion) when (excepcion is CargaInicialInvalidaExcepcion or CargaMaestrosInvalidaExcepcion)
        {
            // La marca no avanza: el próximo ciclo vuelve a pedir lo mismo y lo aplica cuando el Central lo corrija.
            registro.LogError("Los maestros del Central (versión {Desde} a {Hasta}) no se pudieron aplicar: {Error}", paquete.Desde, paquete.Hasta, excepcion.Message);
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

        return new ResultadoDescargaMaestros(true, Math.Max(desde, paquete.Hasta), creados, actualizados, null);
    }
}
