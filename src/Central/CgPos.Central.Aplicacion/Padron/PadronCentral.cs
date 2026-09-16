using CgPos.Contratos.Central;

namespace CgPos.Central.Aplicacion.Padron;

/// <summary>
/// Padrón de contribuyentes de la DGII distribuido desde el Central: se publica una vez y todas las cajas lo descargan,
/// en vez de llevar el archivo de la DGII caja por caja.
/// </summary>
public interface IServicioPadronCentral
{
    /// <returns>La versión publicada y su hash; nulo si no hay padrón publicado.</returns>
    Task<DatosPadronPublicado?> PublicadoAsync(CancellationToken cancelacion = default);

    /// <returns>El archivo del padrón para descargarlo; nulo si no hay ninguno publicado.</returns>
    Task<Stream?> AbrirArchivoAsync(CancellationToken cancelacion = default);
}
