using CgPos.Interfaz.Componentes;
using MudBlazor;

namespace CgPos.Interfaz.Servicios;

public static class ExtensionesServicioDialogos
{
    /// <summary>Muestra <see cref="DialogoConfirmacion"/> y devuelve <c>true</c> si el usuario confirma.</summary>
    public static async Task<bool> ConfirmarAsync(
        this IDialogService dialogos,
        string titulo,
        string mensaje,
        string textoConfirmar = "Aceptar",
        bool esPeligrosa = false,
        string textoCancelar = "Cancelar")
    {
        var parametros = new DialogParameters<DialogoConfirmacion>
        {
            { d => d.Mensaje, mensaje },
            { d => d.TextoConfirmar, textoConfirmar },
            { d => d.TextoCancelar, textoCancelar },
            { d => d.EsPeligrosa, esPeligrosa },
        };

        var referencia = await dialogos.ShowAsync<DialogoConfirmacion>(titulo, parametros);
        var resultado = await referencia.Result;
        return resultado is { Canceled: false };
    }
}
