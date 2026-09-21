using CgPos.Contratos.Seguridad;
using CgPos.Dominio.Comun;
using CgPos.Pos.Aplicacion.Sincronizacion;

namespace CgPos.Pos.Infraestructura.Sincronizacion;

/// <summary>
/// Lleva la cuenta de lo que la sincronización está haciendo. Es del Agente entero (singleton) porque quien actualiza es
/// el servicio de fondo y quien pregunta es la pantalla, cada uno en su propio ámbito.
/// </summary>
internal sealed class ProgresoActualizacionEnMemoria(TimeProvider reloj) : IProgresoActualizacion
{
    private readonly Lock _candado = new();
    private string? _etapa;
    private DateTimeOffset? _desde;
    private string? _ultimoError;
    private int? _hechos;
    private int? _total;

    public DatosActualizacionCaja? Actual
    {
        get
        {
            lock (_candado)
                return _etapa is null && _ultimoError is null
                    ? null
                    : new DatosActualizacionCaja(_etapa is not null, _etapa, _desde, _ultimoError, _hechos, _total);
        }
    }

    public IDisposable Comenzar(string etapa)
    {
        lock (_candado)
        {
            _etapa = etapa;
            _desde = reloj.Ahora();
            _hechos = null;
            _total = null;
        }

        return new Fin(this);
    }

    public void Etapa(string etapa)
    {
        lock (_candado)
        {
            // Sin actualización abierta no hay etapa que cambiar: el aviso llegó tarde y se ignora.
            if (_etapa is null)
                return;

            _etapa = etapa;
            _hechos = null;
            _total = null;
        }
    }

    public void Avance(int hechos, int total)
    {
        lock (_candado)
        {
            if (_etapa is null)
                return;

            _hechos = hechos;
            _total = total;
        }
    }

    public void Terminar(string? error)
    {
        lock (_candado)
        {
            _etapa = null;
            _desde = null;
            _hechos = null;
            _total = null;
            _ultimoError = error;
        }
    }

    /// <summary>Cierra la actualización aunque el trabajo falle con una excepción.</summary>
    private sealed class Fin(ProgresoActualizacionEnMemoria progreso) : IDisposable
    {
        public void Dispose()
        {
            lock (progreso._candado)
            {
                progreso._etapa = null;
                progreso._desde = null;
                progreso._hechos = null;
                progreso._total = null;
            }
        }
    }
}
