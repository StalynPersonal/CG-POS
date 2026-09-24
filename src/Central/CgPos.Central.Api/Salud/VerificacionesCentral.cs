using CgPos.Central.Aplicacion.Catalogo;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CgPos.Central.Api.Salud;

/// <summary>
/// Verificaciones de /salud del Central.
/// </summary>
/// <remarks>
/// Esta dirección responde a cualquiera en la red, así que aquí solo entra lo que no sirve para atacar el sistema: si una
/// pantalla pública está encendida, no cuántas sucursales hay ni qué falta por configurar. Ese detalle vive en la
/// aplicación, con sesión y permiso.
/// </remarks>
internal static class VerificacionesCentral
{
    /// <summary>
    /// El chequeador de precios del pasillo. Viene apagado, y cuando lo está la pantalla no responde nada: es la primera
    /// pregunta del técnico que la instala y ve la pantalla en blanco.
    /// </summary>
    internal sealed class Chequeador(IServicioChequeadorPrecios servicio) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext contexto, CancellationToken cancelacion = default)
        {
            var habilitado = await servicio.HabilitadoAsync(cancelacion);
            var datos = new Dictionary<string, object> { ["habilitado"] = habilitado };

            // Apagado no es una falla: es cómo viene de fábrica y hay tiendas que no lo usan.
            return HealthCheckResult.Healthy(
                habilitado
                    ? "Chequeador encendido: la pantalla del pasillo responde en /chequeador/{sucursal}."
                    : "Chequeador apagado: la pantalla del pasillo no responde. Se enciende en Parámetros, con Central.Chequeador.Habilitado.",
                datos);
        }
    }
}
