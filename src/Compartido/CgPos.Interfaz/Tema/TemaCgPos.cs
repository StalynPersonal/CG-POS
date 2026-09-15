using MudBlazor;

namespace CgPos.Interfaz.Tema;

/// <summary>Paleta del diseño verde institucional.</summary>
public static class ColoresCgPos
{
    public const string VerdeInstitucional = "#1b4d3e";
    public const string VerdeInstitucionalOscuro = "#123629";
    public const string VerdeAccion = "#2e7d32";
    public const string VerdeSuave = "#e8f5e9";
    public const string Fondo = "#f4f7f5";
    public const string Texto = "#1f2a26";
    public const string TextoSecundario = "#51605a";
    public const string Linea = "#cfd8d3";
    public const string Peligro = "#b3261e";
    public const string Advertencia = "#b7791f";
    public const string Informacion = "#2f6f8f";
}

/// <summary>Tema MudBlazor compartido por la caja y el Central.</summary>
public static class TemaCgPos
{
    public static readonly string[] FuentePrincipal = ["Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static MudTheme Crear() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = ColoresCgPos.VerdeInstitucional,
            PrimaryContrastText = "#ffffff",
            Secondary = ColoresCgPos.VerdeAccion,
            SecondaryContrastText = "#ffffff",
            Tertiary = "#5f7d72",
            TertiaryContrastText = "#ffffff",
            Success = ColoresCgPos.VerdeAccion,
            SuccessContrastText = "#ffffff",
            Info = ColoresCgPos.Informacion,
            InfoContrastText = "#ffffff",
            Warning = ColoresCgPos.Advertencia,
            WarningContrastText = ColoresCgPos.Texto,
            Error = ColoresCgPos.Peligro,
            ErrorContrastText = "#ffffff",
            Background = ColoresCgPos.Fondo,
            BackgroundGray = ColoresCgPos.VerdeSuave,
            Surface = "#ffffff",
            AppbarBackground = ColoresCgPos.VerdeInstitucional,
            AppbarText = "#ffffff",
            DrawerBackground = ColoresCgPos.VerdeInstitucional,
            DrawerText = ColoresCgPos.VerdeSuave,
            DrawerIcon = ColoresCgPos.VerdeSuave,
            TextPrimary = ColoresCgPos.Texto,
            TextSecondary = ColoresCgPos.TextoSecundario,
            LinesDefault = ColoresCgPos.Linea,
            Divider = ColoresCgPos.Linea,
            TableLines = "#dfe6e2",
            TableStriped = "#f4f9f5",
            TableHover = ColoresCgPos.VerdeSuave,
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FuentePrincipal, FontSize = "1rem" },
            Button = new ButtonTypography { FontFamily = FuentePrincipal, FontWeight = "600", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            AppbarHeight = "64px",
        },
    };
}
