using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CgPos.Pos.Infraestructura.Perifericos;

/// <summary>
/// Manda los bytes tal cual a una impresora instalada en Windows (USB, serie o compartida), sin que el controlador los
/// reinterprete. Es lo que hace falta para las térmicas de mostrador: los comandos ESC/POS —el corte del papel, el QR del
/// timbre y el pulso de la gaveta— tienen que llegar sin que nadie los toque.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ColaImpresionWindows
{
    private const string Winspool = "winspool.drv";

    /// <summary>«RAW» le dice al sistema que no convierta nada: lo que se envía es lo que sale por el cable.</summary>
    private const string TipoCrudo = "RAW";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct InformacionDocumento
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string Nombre;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Salida;
        [MarshalAs(UnmanagedType.LPWStr)] public string TipoDatos;
    }

    [DllImport(Winspool, EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AbrirImpresora(string nombre, out nint manejador, nint valoresPredeterminados);

    [DllImport(Winspool, EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int IniciarDocumento(nint impresora, int nivel, ref InformacionDocumento documento);

    [DllImport(Winspool, EntryPoint = "StartPagePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IniciarPagina(nint impresora);

    [DllImport(Winspool, EntryPoint = "WritePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Escribir(nint impresora, nint datos, int cantidad, out int escritos);

    [DllImport(Winspool, EntryPoint = "EndPagePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminarPagina(nint impresora);

    [DllImport(Winspool, EntryPoint = "EndDocPrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminarDocumento(nint impresora);

    [DllImport(Winspool, EntryPoint = "ClosePrinter", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CerrarImpresora(nint impresora);

    /// <exception cref="IOException">Windows rechazó el trabajo: la impresora no existe, está sin permisos o no responde.</exception>
    public static void Enviar(string impresora, string documento, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(impresora);

        if (!AbrirImpresora(impresora, out var manejador, nint.Zero))
            throw Fallo($"No se pudo abrir la impresora «{impresora}». Revise que el nombre sea exactamente el de Windows.");

        try
        {
            var informacion = new InformacionDocumento { Nombre = documento, Salida = null, TipoDatos = TipoCrudo };
            if (IniciarDocumento(manejador, 1, ref informacion) == 0)
                throw Fallo($"La impresora «{impresora}» no aceptó el trabajo.");

            try
            {
                if (!IniciarPagina(manejador))
                    throw Fallo($"La impresora «{impresora}» no aceptó la página.");

                // Los bytes se copian a memoria no administrada porque así los pide la API de impresión de Windows.
                var memoria = Marshal.AllocHGlobal(bytes.Length);
                try
                {
                    Marshal.Copy(bytes, 0, memoria, bytes.Length);
                    if (!Escribir(manejador, memoria, bytes.Length, out var escritos) || escritos != bytes.Length)
                        throw Fallo($"La impresora «{impresora}» recibió el trabajo incompleto.");
                }
                finally
                {
                    Marshal.FreeHGlobal(memoria);
                }

                TerminarPagina(manejador);
            }
            finally
            {
                TerminarDocumento(manejador);
            }
        }
        finally
        {
            CerrarImpresora(manejador);
        }
    }

    private static IOException Fallo(string mensaje) =>
        new($"{mensaje} ({Marshal.GetLastPInvokeErrorMessage()})");
}
