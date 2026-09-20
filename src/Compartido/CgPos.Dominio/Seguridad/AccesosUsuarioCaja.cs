using CgPos.Dominio.Comun;

namespace CgPos.Dominio.Seguridad;

/// <summary>
/// Cuándo entró por última vez un usuario de caja y en cuál. Solo vive en el Central, que ve todas las terminales, y en su
/// propia tabla a propósito: si se guardara en la fila del usuario, cada ingreso cambiaría su versión y repartiría el
/// maestro de usuarios a todas las cajas.
/// </summary>
public sealed class AccesoUsuarioCaja : Entidad
{
    private AccesoUsuarioCaja()
    {
    }

    public int UsuarioId { get; private set; }
    public int CajaId { get; private set; }
    public DateTimeOffset IngresoEn { get; private set; }

    public static AccesoUsuarioCaja Registrar(int usuarioId, int cajaId, DateTimeOffset ingresoEn) =>
        new()
        {
            UsuarioId = Validar.Id(usuarioId, "Usuario"),
            CajaId = Validar.Id(cajaId, "Caja"),
            IngresoEn = ingresoEn,
        };

    /// <summary>Un aviso que llega tarde (la caja estuvo sin red) no retrocede la fecha del último acceso.</summary>
    public void Actualizar(int cajaId, DateTimeOffset ingresoEn)
    {
        if (IngresoEn >= ingresoEn)
            return;

        CajaId = Validar.Id(cajaId, "Caja");
        IngresoEn = ingresoEn;
    }
}
