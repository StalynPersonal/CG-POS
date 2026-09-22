namespace CgPos.Dominio.Comun;

/// <summary>
/// Dónde se emitió un documento de la caja (factura, nota de crédito): sucursal, caja y turno tal como estaban en ese
/// momento. El documento lo guarda como foto, además de los Id, para que el histórico no cambie si después se renombra o
/// recodifica algo.
/// </summary>
public sealed record OrigenDocumento
{
    public const int LargoCodigo = 2;
    public const int LargoMaximoNombreSucursal = CgPos.Dominio.Organizacion.Empresa.LargoMaximoNombre;

    /// <param name="turnoNumero">Número del turno en su caja; nulo si el documento se emitió sin turno abierto.</param>
    public OrigenDocumento(string sucursalCodigo, string sucursalNombre, string cajaCodigo, long? turnoNumero)
    {
        if (turnoNumero is < 1)
            throw new ArgumentOutOfRangeException(nameof(turnoNumero), turnoNumero, "El número de turno empieza en 1.");

        SucursalCodigo = Validar.CodigoDosDigitos(sucursalCodigo, "Código de sucursal");
        SucursalNombre = Validar.Texto(sucursalNombre, "Nombre de sucursal", LargoMaximoNombreSucursal);
        CajaCodigo = Validar.CodigoDosDigitos(cajaCodigo, "Código de caja");
        TurnoNumero = turnoNumero;
    }

    public string SucursalCodigo { get; }
    public string SucursalNombre { get; }
    public string CajaCodigo { get; }
    public long? TurnoNumero { get; }
}
