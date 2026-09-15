using Microsoft.JSInterop;

namespace CgPos.Interfaz.Servicios;

/// <summary>
/// Escucha F1–F12 y Escape a nivel de ventana y los publica a los componentes suscritos.
/// Llamar <see cref="IniciarAsync"/> una vez (en el layout) después del primer render.
/// </summary>
public sealed class ServicioAtajosTeclado(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _modulo;
    private DotNetObjectReference<ServicioAtajosTeclado>? _referencia;

    /// <summary>Nombre de la tecla presionada, ej. "F2" o "Escape".</summary>
    public event Func<string, Task>? TeclaPresionada;

    public async Task IniciarAsync()
    {
        if (_modulo is not null)
            return;

        _modulo = await js.InvokeAsync<IJSObjectReference>("import", "./_content/CgPos.Interfaz/atajosTeclado.js");
        _referencia = DotNetObjectReference.Create(this);
        await _modulo.InvokeVoidAsync("registrar", _referencia);
    }

    [JSInvokable]
    public async Task AlPresionarTecla(string tecla)
    {
        if (TeclaPresionada is null)
            return;

        foreach (var suscriptor in TeclaPresionada.GetInvocationList().Cast<Func<string, Task>>())
            await suscriptor(tecla);
    }

    public async ValueTask DisposeAsync()
    {
        if (_modulo is not null)
        {
            try
            {
                await _modulo.InvokeVoidAsync("quitar");
                await _modulo.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _referencia?.Dispose();
    }
}
