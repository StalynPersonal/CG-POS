using CgPos.Pos.Aplicacion.Ecf;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Aplicacion.Ventas;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CgPos.Pos.Agente.Salud;

/// <summary>
/// Diagnóstico de la caja para /salud. Está pensado para el técnico que acaba de instalarla: en vez de decir solo si vive,
/// dice qué le falta y qué hacer con cada cosa, sin tener que entrar a la pantalla ni leer el registro.
/// </summary>
/// <remarks>
/// El Agente solo escucha en localhost, así que este detalle no sale del equipo. Aun así, nunca se publica el valor de un
/// secreto: de la credencial se dice si la tiene, no cuál es.
/// </remarks>
internal static class VerificacionesCaja
{
    /// <summary>Lo que no impide vender se informa como «degradado»: la caja arranca igual y el técnico ve qué le falta.</summary>
    private static HealthCheckResult Aviso(string descripcion, IReadOnlyDictionary<string, object> datos) =>
        new(HealthStatus.Degraded, descripcion, data: datos);

    private static HealthCheckResult Bien(string descripcion, IReadOnlyDictionary<string, object> datos) =>
        HealthCheckResult.Healthy(descripcion, datos);

    /// <summary>Qué caja es este equipo y si el Central la reconoce.</summary>
    internal sealed class Caja(IEstadoCaja estado, IContextoCaja contexto) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto_, CancellationToken cancelacion = default)
        {
            var datos = new Dictionary<string, object>
            {
                ["sucursal"] = contexto.SucursalCodigo ?? "(sin configurar)",
                ["caja"] = contexto.CajaCodigo ?? "(sin configurar)",
            };

            var caja = await estado.ObtenerAsync(cancelacion);
            if (caja.Problema is { Length: > 0 })
            {
                // Sin códigos configurados es un archivo que llenar; con ellos puestos, es que la caja todavía no bajó.
                var sinCodigos = contexto.SucursalCodigo is not { Length: > 0 } || contexto.CajaCodigo is not { Length: > 0 };
                return Aviso(sinCodigos
                    ? "Este equipo no sabe qué caja es: configure Caja:Sucursal y Caja:Codigo."
                    : $"La caja {contexto.SucursalCodigo}-{contexto.CajaCodigo} todavía no bajó del Central. Compruebe que exista allá y que ya la hayan aceptado.",
                    datos);
            }

            datos["empresa"] = caja.EmpresaNombre ?? string.Empty;
            datos["sucursalNombre"] = caja.SucursalNombre ?? string.Empty;
            datos["cajaNombre"] = caja.CajaNombre ?? string.Empty;

            return caja.Habilitada
                ? Bien($"Caja {caja.CajaCodigo} de {caja.SucursalNombre}, habilitada.", datos)
                : Aviso("La caja está deshabilitada en el Central: habilítela en Organización > Cajas.", datos);
        }
    }

    /// <summary>Qué caja es este equipo, dónde está su Central y si su configuración sirve.</summary>
    internal sealed class Configuracion(IConfiguracionCaja configuracion) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken cancelacion = default)
        {
            if (await configuracion.ObtenerAsync(cancelacion) is not { } datos)
                return Aviso("Esta caja todavía no está configurada: ábrala y llene sus datos.", new Dictionary<string, object>());

            var valores = new Dictionary<string, object>
            {
                ["sucursal"] = datos.SucursalCodigo,
                ["caja"] = datos.CajaCodigo,
                ["direccionIp"] = datos.DireccionIp,
                ["central"] = datos.UrlCentral,
                ["configuradaEn"] = datos.ConfiguradaEn,
            };

            return datos.Sirve
                ? Bien($"Caja {datos.CajaCodigo} de la sucursal {datos.SucursalCodigo}, en {datos.DireccionIp}.", valores)
                : Aviso($"{datos.Problema} Vuelva a configurar la caja en su pantalla.", valores);
        }
    }

    /// <summary>Comunicación con el Central: si contesta, qué falta por subir y desde cuándo.</summary>
    internal sealed class Central(IEstadoSincronizacion sincronizacion) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken cancelacion = default)
        {
            var estado = await sincronizacion.ObtenerAsync(cancelacion);
            var datos = new Dictionary<string, object>
            {
                ["configurado"] = estado.CentralConfigurado,
                ["enLinea"] = estado.EnLinea,
                ["documentosPendientes"] = estado.DocumentosPendientes,
            };

            if (estado.UltimaSincronizacion is { } ultima)
                datos["ultimaSincronizacion"] = ultima;
            if (estado.UltimoRespaldo is { } respaldo)
                datos["ultimoRespaldo"] = respaldo;
            if (estado.UltimoError is { Length: > 0 } error)
                datos["ultimoError"] = error;
            if (estado.Alertas is { Count: > 0 } alertas)
                datos["alertas"] = alertas;

            if (!estado.CentralConfigurado)
                return Aviso("Sin Central configurado: la caja vende, pero nada sube.", datos);

            // Sin comunicación la caja sigue vendiendo: es un aviso, no una falla.
            if (!estado.EnLinea)
                return Aviso($"Sin comunicación con el Central. {estado.DocumentosPendientes} documento(s) esperando subir.", datos);

            var pendientes = estado.DocumentosPendientes == 0
                ? "sin documentos pendientes"
                : $"{estado.DocumentosPendientes} documento(s) pendientes";
            return estado.Alertas is { Count: > 0 }
                ? Aviso($"Comunicada, {pendientes}, con avisos de mantenimiento.", datos)
                : Bien($"Comunicada con el Central, {pendientes}.", datos);
        }
    }

    /// <summary>Certificado digital: sin él la caja no puede firmar los comprobantes fiscales.</summary>
    internal sealed class Certificado(IConfiguration configuracion) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken cancelacion = default)
        {
            var ruta = configuracion[ClavesEcf.RutaCertificado];
            var datos = new Dictionary<string, object>
            {
                ["carpetaXml"] = configuracion[ClavesEcf.CarpetaXml] is { Length: > 0 } carpeta ? carpeta : ClavesEcf.CarpetaXmlPredeterminada,
            };

            if (ruta is not { Length: > 0 })
                return Task.FromResult(Aviso("No hay certificado configurado (Ecf:Certificado:Ruta): la caja no podrá facturar.", datos));

            datos["ruta"] = ruta;
            if (!File.Exists(ruta))
                return Task.FromResult(Aviso($"El certificado configurado no está en {ruta}: cópielo al equipo.", datos));

            // El PIN nunca se guarda: lo digita un supervisor en la caja y el certificado queda cargado solo en memoria.
            return Task.FromResult(Bien("Certificado presente. Un supervisor debe cargarlo con su PIN desde la caja.", datos));
        }
    }
}
