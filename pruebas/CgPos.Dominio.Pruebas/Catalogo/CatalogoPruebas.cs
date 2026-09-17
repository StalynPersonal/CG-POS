using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;

namespace CgPos.Dominio.Pruebas.Catalogo;

public class CatalogoPruebas
{
    private static readonly FormatoCodigoBalanza FormatoPredeterminado = new("21", "22", 5, 5, 3, 2);

    [Fact]
    public void Etiqueta_de_balanza_con_peso_embebido()
    {
        // 21 + artículo 12345 + 01250 (1.250 kg) + dígito de control 6
        Assert.True(InterpreteCodigoBalanza.TryInterpretar("2112345012506", FormatoPredeterminado, out var lectura));

        Assert.Equal("12345", lectura!.CodigoArticulo);
        Assert.Equal(TipoValorBalanza.Peso, lectura.Tipo);
        Assert.Equal(1.250m, lectura.Valor);
    }

    [Fact]
    public void Etiqueta_de_balanza_con_precio_embebido()
    {
        // 22 + artículo 54321 + 15075 (RD$150.75) + dígito de control 5
        Assert.True(InterpreteCodigoBalanza.TryInterpretar("2254321150755", FormatoPredeterminado, out var lectura));

        Assert.Equal("54321", lectura!.CodigoArticulo);
        Assert.Equal(TipoValorBalanza.Precio, lectura.Tipo);
        Assert.Equal(150.75m, lectura.Valor);
    }

    [Theory]
    [InlineData("2112345012507")]   // dígito de control incorrecto
    [InlineData("7891114119695")]   // código de barras normal
    [InlineData("211234501250")]    // largo incorrecto
    [InlineData("21123450125A6")]
    public void Codigos_que_no_son_etiqueta_de_balanza(string codigo)
    {
        Assert.False(InterpreteCodigoBalanza.TryInterpretar(codigo, FormatoPredeterminado, out _));
    }

    [Fact]
    public void Digito_de_control_gs1_de_un_ean13_real()
    {
        Assert.True(InterpreteCodigoBalanza.DigitoControlGs1Valido("7891114119695"));
        Assert.Equal(5, InterpreteCodigoBalanza.CalcularDigitoControlGs1("789111411969"));
    }

    [Fact]
    public void Articulo_admite_varios_codigos_sin_duplicarlos()
    {
        var articulo = Articulo.Crear("7891114119695", "Cincel", Ids.Siguiente(), Ids.Siguiente(), Ids.Siguiente());

        articulo.AgregarCodigo("7891114119695", TipoCodigoArticulo.Barras);
        articulo.AgregarCodigo(" 7891114119695 ", TipoCodigoArticulo.Barras);
        articulo.AgregarCodigo("TRA-43138", TipoCodigoArticulo.Proveedor);

        Assert.Equal(2, articulo.Codigos.Count);

        articulo.ReemplazarCodigos([("TRA-43138", TipoCodigoArticulo.Proveedor), ("7891114119701", TipoCodigoArticulo.Barras)]);

        Assert.Equal(new[] { "7891114119701", "TRA-43138" }, articulo.Codigos.Select(c => c.Codigo).Order());
    }

    [Fact]
    public void Articulo_valida_configuracion_de_precios()
    {
        var articulo = Articulo.Crear("A1", "Artículo", Ids.Siguiente(), Ids.Siguiente(), Ids.Siguiente());

        Assert.Throws<ArgumentOutOfRangeException>(() => articulo.ConfigurarPrecios(-1m, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => articulo.ConfigurarPrecios(null, null, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrecioArticulo.Registrar(articulo.Id, ListaPrecio.Detalle, 0m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Prueba"));
    }

    [Theory]
    [InlineData(1.5, false, false)]
    [InlineData(2, false, true)]
    [InlineData(1.256, true, true)]
    [InlineData(0, true, false)]
    public void Unidad_de_medida_valida_decimales(decimal cantidad, bool permiteDecimales, bool valida)
    {
        var unidad = UnidadMedida.Crear(1, "LB", "Libra", permiteDecimales, permiteDecimales ? 3 : 0);

        Assert.Equal(valida, unidad.EsCantidadValida(cantidad));
    }

    [Fact]
    public void Cliente_normaliza_documento_y_mantiene_una_sola_direccion_principal()
    {
        var cliente = Cliente.Crear($"CL{Codigos.Siguiente()}", TipoDocumentoIdentidad.Rnc, "131-24679-6", "Constructora Ejemplo SRL");

        var casa = cliente.AgregarDireccion("Oficina", "Av. Principal 1");
        var almacen = cliente.AgregarDireccion("Almacén", "Calle 2", esPrincipal: true);

        Assert.Equal("131246796", cliente.Documento);
        Assert.False(casa.EsPrincipal);
        Assert.True(almacen.EsPrincipal);

        cliente.QuitarDireccion(almacen.Alias);
        Assert.True(Assert.Single(cliente.Direcciones).EsPrincipal);

        Assert.Throws<ArgumentException>(() => Cliente.Crear($"CL{Codigos.Siguiente()}", TipoDocumentoIdentidad.Cedula, "12345", "Formato inválido"));
    }

    [Fact]
    public void Forma_de_pago_toma_valores_sugeridos_por_tipo()
    {
        var efectivo = FormaPago.Crear("EFE", "Efectivo", TipoFormaPago.Efectivo, 1, "DOP");
        var tarjeta = FormaPago.Crear("TAR", "Tarjeta", TipoFormaPago.Tarjeta, 2, "DOP");
        var bono = FormaPago.Crear("BONO", "Bono de regalo", TipoFormaPago.BonoRegalo, 5, "DOP");

        Assert.True(efectivo.AbreGaveta);
        Assert.True(efectivo.PermiteDevuelta);
        Assert.False(tarjeta.AbreGaveta);
        Assert.True(tarjeta.RequiereReferencia);
        Assert.False(bono.PermiteComprobanteFiscal);
        Assert.Throws<ArgumentException>(() => FormaPago.Crear("USD", "Dólares", TipoFormaPago.MonedaExtranjera, 3, "US$"));
    }

    [Fact]
    public void Moneda_valida_codigo_iso_y_simbolo()
    {
        var euro = Moneda.Crear("eur", "Euro", "€");

        Assert.Equal("EUR", euro.Codigo);
        Assert.Equal("€", euro.Simbolo);
        Assert.Throws<ArgumentException>(() => Moneda.Crear("EURO", "Euro", "€"));
        Assert.Throws<ArgumentException>(() => Moneda.Crear("EUR", "Euro", " "));
    }
}
