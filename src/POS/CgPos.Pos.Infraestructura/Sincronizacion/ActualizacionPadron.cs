using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Sincronizacion;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

internal sealed class ActualizacionPadron(
    ContextoDatosPos contexto,
    IClienteCentral central,
    IImportadorPadronDgii importador,
    TimeProvider reloj,
    ILogger<ActualizacionPadron> registro) : IActualizacionPadron
{
    public async Task<int> ActualizarAsync(CancellationToken cancelacion = default)
    {
        if (!central.Configurado || await central.ConsultarPadronAsync(cancelacion) is not { } publicado)
            return 0;

        var marca = await contexto.MarcasSincronizacion.SingleOrDefaultAsync(m => m.Clave == MarcaSincronizacion.PadronDgii, cancelacion);
        if (string.Equals(marca?.Texto, publicado.Hash, StringComparison.OrdinalIgnoreCase))
            return 0;

        registro.LogInformation("Importando el padrón de la DGII {Version} publicado en el Central ({Tamano:N0} bytes)", publicado.Version, publicado.Tamano);
        var archivo = await central.DescargarPadronAsync(cancelacion);
        if (archivo is null)
            return 0;

        ResultadoImportacionPadron resultado;
        await using (archivo)
        {
            resultado = await importador.ImportarAsync(archivo, cancelacion);
        }

        var ahora = reloj.GetUtcNow();
        if (marca is null)
        {
            marca = MarcaSincronizacion.Crear(MarcaSincronizacion.PadronDgii, 0, ahora);
            contexto.MarcasSincronizacion.Add(marca);
        }

        // El hash queda marcado solo después de importar: si falla, el próximo ciclo lo vuelve a intentar.
        marca.ActualizarTexto(publicado.Hash, ahora);
        await contexto.SaveChangesAsync(cancelacion);

        registro.LogInformation("Padrón de la DGII actualizado: {Cantidad:N0} contribuyentes ({Descartadas:N0} líneas descartadas)", resultado.RegistrosValidos,
            resultado.LineasDescartadas);
        return resultado.RegistrosValidos;
    }
}
