namespace CgPos.Dominio.Fiscal;

/// <summary>
/// Cómo se calcula el ITBIS de una línea (RF-183). Los precios se manejan sin impuesto, como en Stellar: el ITBIS se
/// calcula sobre el importe de la línea, se redondea al centavo por línea y se suma aparte. Vive aquí, y no dentro de la
/// venta, porque la cotización del Central tiene que dar exactamente el mismo total que la factura que salga de ella: si
/// cada lado redondeara por su cuenta, el cliente vería un centavo de diferencia entre el presupuesto y lo que paga.
/// </summary>
public static class CalculoImpuestos
{
    /// <summary>ITBIS de un importe sin impuesto, redondeado al centavo.</summary>
    public static decimal ImpuestoSobre(decimal importeSinImpuesto, decimal porcentajeImpuesto) =>
        porcentajeImpuesto == 0m ? 0m : decimal.Round(importeSinImpuesto * porcentajeImpuesto / 100m, 2, MidpointRounding.AwayFromZero);
}
