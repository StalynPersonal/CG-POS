using CgPos.Central.Aplicacion.Notificaciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Central.Infraestructura.Notificaciones;

internal sealed class AvisosDespacho(
    ContextoDatosCentral contexto,
    IServicioCorreo correo,
    IParametrosCentral parametros,
    TimeProvider reloj,
    ILogger<AvisosDespacho> registro) : IAvisosDespacho
{
    public async Task<int> AvisarPreparadosAsync(int maximo, CancellationToken cancelacion = default)
    {
        if (!await parametros.ObtenerBooleanoOpcionalAsync(ClavesParametrosCentral.DespachoAvisarPreparado, cancelacion)
            || !await correo.ConfiguradoAsync(cancelacion))
            return 0;

        var pendientes = await contexto.PendientesEntrega
            .Where(p => p.AvisoEnviadoEn == null && p.Estado == EstadoPendiente.Preparado && p.ClienteDocumento != null)
            .OrderBy(p => p.ActualizadoEn)
            .Take(Math.Max(maximo, 1))
            .ToListAsync(cancelacion);

        if (pendientes.Count == 0)
            return 0;

        var correos = await CorreosAsync(pendientes.Select(p => p.ClienteDocumento!), cancelacion);
        var empresa = await contexto.Empresas.AsNoTracking().SingleOrDefaultAsync(cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nombre, cancelacion);
        var ahora = reloj.GetUtcNow();
        var enviados = 0;

        foreach (var pendiente in pendientes)
        {
            var documento = DocumentoIdentidad.Normalizar(pendiente.ClienteDocumento);
            if (documento is null || !correos.TryGetValue(documento, out var destinatario))
            {
                // Sin correo registrado se marca como avisado igual: la sucursal lo llama por teléfono y no se reintenta cada ciclo.
                pendiente.MarcarAvisado(ahora);
                continue;
            }

            var lugar = pendiente.Metodo == MetodoEntrega.Envio
                ? $"Su pedido va en camino a {pendiente.Ciudad}."
                : $"Puede retirarlo en {pendiente.AlmacenNombre ?? sucursales.GetValueOrDefault(pendiente.SucursalId) ?? "la tienda"}.";

            var cuerpo = $"""
                Hola {pendiente.ClienteNombre},

                Su pedido {pendiente.Numero} de la factura {pendiente.VentaNumero} ya está preparado. {lugar}

                Lleve su comprobante o el código del pedido al retirarlo.

                {empresa?.NombreComercial ?? empresa?.RazonSocial ?? "CG-POS"}
                """;

            var resultado = await correo.EnviarAsync(new MensajeCorreo(destinatario, $"Su pedido {pendiente.Numero} está listo", cuerpo), cancelacion);
            if (!resultado.Enviado)
            {
                // Un fallo del servidor de correo no marca el aviso: se reintenta en el próximo ciclo.
                registro.LogWarning("No se avisó al cliente del pendiente {Numero}: {Error}", pendiente.Numero, resultado.Error);
                continue;
            }

            pendiente.MarcarAvisado(ahora);
            enviados++;
        }

        await contexto.SaveChangesAsync(cancelacion);
        return enviados;
    }

    /// <summary>Correo del cliente registrado, por documento normalizado; los que no están en el maestro no tienen a dónde escribirles.</summary>
    private async Task<Dictionary<string, string>> CorreosAsync(IEnumerable<string> documentos, CancellationToken cancelacion)
    {
        var buscados = documentos.Select(DocumentoIdentidad.Normalizar).OfType<string>().Distinct().ToHashSet(StringComparer.Ordinal);
        if (buscados.Count == 0)
            return [];

        // El maestro de clientes se identifica por "tipo de documento:documento" (ej. "RNC:401007551"): se prueban los tipos posibles.
        var codigos = buscados
            .SelectMany(documento => Enum.GetValues<TipoDocumentoIdentidad>().Select(tipo => $"{tipo}:{documento}".ToUpperInvariant()))
            .ToList();

        var clientes = await contexto.MaestrosCentral.AsNoTracking()
            .Where(m => m.Tipo == TipoMaestro.Cliente && m.Codigo != null && codigos.Contains(m.Codigo))
            .ToListAsync(cancelacion);

        return clientes
            .Select(FormatoMaestros.Leer<ClienteCarga>)
            .Where(c => c.Activo && c.Correo is { Length: > 0 } && c.Correo.Contains('@'))
            .GroupBy(c => DocumentoIdentidad.Normalizar(c.Documento) ?? c.Documento, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Correo!, StringComparer.Ordinal);
    }
}
