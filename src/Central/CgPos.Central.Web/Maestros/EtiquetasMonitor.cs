using CgPos.Dominio.Sincronizacion;
using MudBlazor;

namespace CgPos.Central.Web.Maestros;

public static class EtiquetasMonitor
{
    public static IReadOnlyList<EstadoEnvioDgii> EstadosDgii { get; } = Enum.GetValues<EstadoEnvioDgii>();

    public static string Estado(EstadoEnvioDgii estado) => estado switch
    {
        EstadoEnvioDgii.Pendiente => "Pendiente de envío",
        EstadoEnvioDgii.Enviado => "En proceso en la DGII",
        EstadoEnvioDgii.Aceptado => "Aceptado",
        EstadoEnvioDgii.AceptadoCondicional => "Aceptado condicional",
        _ => "Rechazado",
    };

    public static Color Color(EstadoEnvioDgii estado) => estado switch
    {
        EstadoEnvioDgii.Pendiente => MudBlazor.Color.Warning,
        EstadoEnvioDgii.Enviado => MudBlazor.Color.Info,
        EstadoEnvioDgii.Aceptado => MudBlazor.Color.Success,
        EstadoEnvioDgii.AceptadoCondicional => MudBlazor.Color.Success,
        _ => MudBlazor.Color.Error,
    };

    public static bool PuedeReenviarse(EstadoEnvioDgii estado) => estado is EstadoEnvioDgii.Pendiente or EstadoEnvioDgii.Rechazado;

    public static string Conflicto(TipoConflictoSincronizacion tipo) => tipo switch
    {
        TipoConflictoSincronizacion.CajaNoCoincide => "Caja no coincide",
        TipoConflictoSincronizacion.HashInvalido => "Contenido alterado",
        TipoConflictoSincronizacion.ContenidoDistinto => "Mismo mensaje con otro contenido",
        TipoConflictoSincronizacion.XmlAlterado => "XML del e-CF alterado",
        TipoConflictoSincronizacion.EncfDuplicado => "e-NCF duplicado",
        TipoConflictoSincronizacion.DocumentoInvalido => "Documento ilegible",
        _ => "Miembro de fidelidad duplicado",
    };

    public static string Hace(DateTimeOffset? momento, DateTimeOffset ahora)
    {
        if (momento is not { } valor)
            return "Nunca";

        var transcurrido = ahora - valor;
        return transcurrido.TotalMinutes < 1 ? "Hace instantes"
            : transcurrido.TotalHours < 1 ? $"Hace {(int)transcurrido.TotalMinutes} min"
            : transcurrido.TotalDays < 1 ? $"Hace {(int)transcurrido.TotalHours} h"
            : valor.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }
}
