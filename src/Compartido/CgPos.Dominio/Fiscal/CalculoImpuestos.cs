namespace CgPos.Dominio.Fiscal;

/// <summary>
/// Cómo se separa el ITBIS de un precio que ya lo lleva incluido (RF-183). Vive aquí, y no dentro de la venta, porque la
/// cotización del Central tiene que dar exactamente el mismo total que la factura que salga de ella: si cada lado
/// redondeara por su cuenta, el cliente vería un centavo de diferencia entre el presupuesto y lo que paga.
/// </summary>
public static class CalculoImpuestos
{
    /// <summary>Base imponible contenida en un importe con impuesto incluido, redondeada al centavo.</summary>
    public static decimal BaseDe(decimal importeConImpuesto, decimal porcentajeImpuesto) =>
        porcentajeImpuesto == 0m
            ? importeConImpuesto
            : decimal.Round(importeConImpuesto / (1 + porcentajeImpuesto / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>El impuesto contenido en ese importe: lo que queda al quitarle su base.</summary>
    public static decimal ImpuestoDe(decimal importeConImpuesto, decimal porcentajeImpuesto) =>
        importeConImpuesto - BaseDe(importeConImpuesto, porcentajeImpuesto);
}
