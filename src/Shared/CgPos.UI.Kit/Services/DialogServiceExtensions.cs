using CgPos.UI.Kit.Components;
using MudBlazor;

namespace CgPos.UI.Kit.Services;

public static class DialogServiceExtensions
{
    /// <summary>Muestra <see cref="DialogoConfirmacion"/> y devuelve <c>true</c> si el usuario confirma.</summary>
    public static async Task<bool> ConfirmarAsync(
        this IDialogService dialogos,
        string titulo,
        string mensaje,
        string textoConfirmar = "Aceptar",
        bool esPeligrosa = false)
    {
        var parametros = new DialogParameters<DialogoConfirmacion>
        {
            { d => d.Mensaje, mensaje },
            { d => d.TextoConfirmar, textoConfirmar },
            { d => d.EsPeligrosa, esPeligrosa },
        };

        var referencia = await dialogos.ShowAsync<DialogoConfirmacion>(titulo, parametros);
        var resultado = await referencia.Result;
        return resultado is { Canceled: false };
    }
}
