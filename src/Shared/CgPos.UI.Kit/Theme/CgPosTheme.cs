using MudBlazor;

namespace CgPos.UI.Kit.Theme;

/// <summary>Paleta del Layout Verde Institucional.</summary>
public static class CgPosColores
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
public static class CgPosTheme
{
    public static readonly string[] FuentePrincipal = ["Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static MudTheme Crear() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = CgPosColores.VerdeInstitucional,
            PrimaryContrastText = "#ffffff",
            Secondary = CgPosColores.VerdeAccion,
            SecondaryContrastText = "#ffffff",
            Tertiary = "#5f7d72",
            TertiaryContrastText = "#ffffff",
            Success = CgPosColores.VerdeAccion,
            SuccessContrastText = "#ffffff",
            Info = CgPosColores.Informacion,
            InfoContrastText = "#ffffff",
            Warning = CgPosColores.Advertencia,
            WarningContrastText = CgPosColores.Texto,
            Error = CgPosColores.Peligro,
            ErrorContrastText = "#ffffff",
            Background = CgPosColores.Fondo,
            BackgroundGray = CgPosColores.VerdeSuave,
            Surface = "#ffffff",
            AppbarBackground = CgPosColores.VerdeInstitucional,
            AppbarText = "#ffffff",
            DrawerBackground = CgPosColores.VerdeInstitucional,
            DrawerText = CgPosColores.VerdeSuave,
            DrawerIcon = CgPosColores.VerdeSuave,
            TextPrimary = CgPosColores.Texto,
            TextSecondary = CgPosColores.TextoSecundario,
            LinesDefault = CgPosColores.Linea,
            Divider = CgPosColores.Linea,
            TableLines = "#dfe6e2",
            TableStriped = "#f4f9f5",
            TableHover = CgPosColores.VerdeSuave,
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
