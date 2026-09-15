namespace CgPos.ECF.Tests.Firma;

/// <summary>
/// e-CF tipo 31 de ejemplo con estructura ilustrativa (no validado contra el XSD oficial; eso llega en la Fase C7).
/// </summary>
internal static class XmlEjemploEcf
{
    public const string E31 = """
        <?xml version="1.0" encoding="utf-8"?>
        <ECF>
          <Encabezado>
            <Version>1.0</Version>
            <IdDoc>
              <TipoeCF>31</TipoeCF>
              <eNCF>E310000000001</eNCF>
              <FechaVencimientoSecuencia>31-12-2027</FechaVencimientoSecuencia>
              <IndicadorMontoGravado>1</IndicadorMontoGravado>
              <TipoIngresos>01</TipoIngresos>
              <TipoPago>1</TipoPago>
            </IdDoc>
            <Emisor>
              <RNCEmisor>101000000</RNCEmisor>
              <RazonSocialEmisor>CONTRERAS GROUP SRL</RazonSocialEmisor>
              <DireccionEmisor>SANTO DOMINGO</DireccionEmisor>
              <FechaEmision>14-09-2026</FechaEmision>
            </Emisor>
            <Comprador>
              <RNCComprador>131000000</RNCComprador>
              <RazonSocialComprador>CLIENTE DE PRUEBA SRL</RazonSocialComprador>
            </Comprador>
            <Totales>
              <MontoGravadoTotal>720.34</MontoGravadoTotal>
              <MontoGravadoI1>720.34</MontoGravadoI1>
              <ITBIS1>18</ITBIS1>
              <TotalITBIS>129.66</TotalITBIS>
              <TotalITBIS1>129.66</TotalITBIS1>
              <MontoTotal>850.00</MontoTotal>
            </Totales>
          </Encabezado>
          <DetallesItems>
            <Item>
              <NumeroLinea>1</NumeroLinea>
              <IndicadorFacturacion>1</IndicadorFacturacion>
              <NombreItem>Cincel de Punta SDS MAX 5/8"x11" Tramon</NombreItem>
              <IndicadorBienoServicio>1</IndicadorBienoServicio>
              <CantidadItem>1.00</CantidadItem>
              <PrecioUnitarioItem>720.34</PrecioUnitarioItem>
              <MontoItem>720.34</MontoItem>
            </Item>
          </DetallesItems>
          <FechaHoraFirma>14-09-2026 10:00:00</FechaHoraFirma>
        </ECF>
        """;
}
