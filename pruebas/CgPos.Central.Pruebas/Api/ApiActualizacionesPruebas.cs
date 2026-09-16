using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Pruebas.Soporte;
using CgPos.Contratos.Central;
using CgPos.Contratos.Serializacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Pruebas.Api;

[Collection(ColeccionCentral.Nombre)]
public class ApiActualizacionesPruebas(CentralEnPruebas central)
{
    [SkippableFact]
    public async Task La_caja_descarga_la_version_publicada_verifica_su_hash_y_reporta_la_que_instalo()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();
        var token = await CentralEnPruebas.TokenCajaAsync(cliente, CentralEnPruebas.CajaUno);
        var admin = await CentralEnPruebas.TokenAdministradorAsync(cliente);

        // Sin paquete publicado no hay nada que instalar.
        using (var sinPublicar = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/actualizaciones/caja", token)))
            Assert.Equal(HttpStatusCode.NoContent, sinPublicar.StatusCode);

        var carpeta = Path.Combine(Path.GetTempPath(), $"cgpos-paquetes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(carpeta);
        try
        {
            var contenido = Encoding.UTF8.GetBytes("paquete de prueba del Agente");
            var archivo = Path.Combine(carpeta, "cgpos-agente-1.2.3.zip");
            await File.WriteAllBytesAsync(archivo, contenido);

            await central.CambiarParametroAsync(ClavesParametrosCentral.ActualizacionesCarpetaPaquetes, carpeta);
            await central.CambiarParametroAsync(ClavesParametrosCentral.ActualizacionesVersionPublicada, "1.2.3");

            var publicada = await ObtenerAsync<DatosActualizacionCaja>(cliente, token, "/api/actualizaciones/caja");
            Assert.Equal(("1.2.3", "cgpos-agente-1.2.3.zip", contenido.LongLength), (publicada.Version, publicada.Archivo, publicada.Tamano));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(contenido)), publicada.Hash);

            using (var paquete = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/actualizaciones/caja/paquete", token)))
            {
                paquete.EnsureSuccessStatusCode();
                Assert.Equal(contenido, await paquete.Content.ReadAsByteArrayAsync());
            }

            using (var reporte = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Post, "/api/actualizaciones/caja/version", token,
                new SolicitudVersionCaja("1.2.3"))))
                Assert.Equal(HttpStatusCode.NoContent, reporte.StatusCode);

            // El Central ve el avance del despliegue: esta caja al día, las demás no.
            var versiones = await ObtenerAsync<List<DatosVersionCaja>>(cliente, admin, "/api/manager/actualizaciones/cajas");
            var actualizada = Assert.Single(versiones, v => v.CajaId == CentralEnPruebas.CajaUno);
            Assert.Equal(("1.2.3", true), (actualizada.Version, actualizada.AlDia));
            Assert.Contains(versiones, v => v.CajaId != CentralEnPruebas.CajaUno && !v.AlDia);

            var auditoria = await central.UsarContextoAsync(contexto => contexto.Auditoria
                .Where(r => r.Accion == "Actualizaciones.VersionCaja")
                .CountAsync());
            Assert.True(auditoria >= 1);
        }
        finally
        {
            await central.CambiarParametroAsync(ClavesParametrosCentral.ActualizacionesCarpetaPaquetes, null);
            await central.CambiarParametroAsync(ClavesParametrosCentral.ActualizacionesVersionPublicada, null);
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [SkippableFact]
    public async Task El_paquete_solo_se_entrega_a_una_caja_autenticada()
    {
        Skip.If(central.MotivoOmision is not null, central.MotivoOmision);
        using var cliente = central.CrearCliente();

        using var sinToken = await cliente.GetAsync("/api/actualizaciones/caja");
        using var conTokenDeUsuario = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, "/api/actualizaciones/caja",
            await CentralEnPruebas.TokenAdministradorAsync(cliente)));

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, conTokenDeUsuario.StatusCode);
    }

    private static async Task<T> ObtenerAsync<T>(HttpClient cliente, string token, string ruta)
    {
        using var respuesta = await cliente.SendAsync(CentralEnPruebas.Solicitud(HttpMethod.Get, ruta, token));
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson.Predeterminadas))!;
    }
}
