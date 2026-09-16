using MudBlazor;

namespace CgPos.Central.Web.Maestros;

public enum TipoCampoCatalogo
{
    Texto,
    Decimal,
    Entero,
    Booleano,

    /// <summary>Lista fija de valores (un enum de la caja, guardado como texto).</summary>
    Opciones,

    /// <summary>Sí, No o nulo: el valor sugerido para el tipo.</summary>
    SiNoSegunTipo,

    /// <summary>Código de una moneda publicada.</summary>
    Moneda,

    /// <summary>Id de una sucursal.</summary>
    Sucursal,

    FechaHora,
}

public sealed record OpcionCatalogo(string Valor, string Etiqueta);

/// <param name="Nombre">Propiedad del registro de carga en JSON (camelCase).</param>
public sealed record CampoCatalogo(string Nombre, string Etiqueta, TipoCampoCatalogo Tipo = TipoCampoCatalogo.Texto)
{
    public bool Obligatorio { get; init; }
    public bool EnTabla { get; init; } = true;

    /// <summary>La caja no deja cambiarlo en un registro ya publicado.</summary>
    public bool FijoAlEditar { get; init; }

    public IReadOnlyList<OpcionCatalogo> Opciones { get; init; } = [];
    public string? Ayuda { get; init; }

    /// <summary>Valor de un registro nuevo o de una propiedad ausente.</summary>
    public object? Predeterminado { get; init; }

    /// <summary>El estado (activo o activa) se muestra como etiqueta.</summary>
    public bool EsEstado => Tipo == TipoCampoCatalogo.Booleano && Nombre is "activo" or "activa";
}

/// <param name="Ruta">Ruta del catálogo en la API y en el Manager.</param>
public sealed record DefinicionCatalogo(string Ruta, string Titulo, string TextoNuevo, string Descripcion, string Icono, IReadOnlyList<CampoCatalogo> Campos);

/// <summary>Catálogos de maestros que se administran con la página genérica. Artículos, clientes, precios y promociones tienen páginas propias.</summary>
public static class DefinicionesCatalogos
{
    private static CampoCatalogo Codigo(bool fijo = false, string? ayuda = null) =>
        new("codigo", "Código") { Obligatorio = true, FijoAlEditar = fijo, Ayuda = ayuda };

    private static readonly CampoCatalogo Nombre = new("nombre", "Nombre") { Obligatorio = true };

    private static CampoCatalogo Estado(string nombre = "activo") => new(nombre, "Activo", TipoCampoCatalogo.Booleano) { Predeterminado = true };

    public static IReadOnlyList<OpcionCatalogo> TiposFormaPago { get; } =
    [
        new("Efectivo", "Efectivo"),
        new("Tarjeta", "Tarjeta"),
        new("Transferencia", "Transferencia"),
        new("Cheque", "Cheque"),
        new("BonoRegalo", "Bono de regalo"),
        new("NotaCredito", "Nota de crédito"),
        new("PrestamoBancario", "Préstamo bancario"),
        new("TarjetaRegalo", "Tarjeta de regalo"),
        new("Puntos", "Puntos de fidelidad"),
        new("MonedaExtranjera", "Moneda extranjera"),
    ];

    public static IReadOnlyList<OpcionCatalogo> TiposReglaAcumulacion { get; } =
    [
        new("Monto", "Todo lo comprado"),
        new("Familia", "Una familia"),
        new("Articulo", "Un artículo"),
        new("DiaSemana", "Un día de la semana"),
        new("Promocion", "Lo vendido con una promoción"),
    ];

    public static IReadOnlyList<OpcionCatalogo> DiasSemana { get; } =
    [
        new("Sunday", "Domingo"),
        new("Monday", "Lunes"),
        new("Tuesday", "Martes"),
        new("Wednesday", "Miércoles"),
        new("Thursday", "Jueves"),
        new("Friday", "Viernes"),
        new("Saturday", "Sábado"),
    ];

    public static IReadOnlyList<OpcionCatalogo> TiposDescuentoTarjeta { get; } =
    [
        new("Porcentaje", "Porcentaje"),
        new("Monto", "Monto fijo"),
    ];

    public static IReadOnlyList<DefinicionCatalogo> Todos { get; } =
    [
        new("monedas", "Monedas", "Nueva moneda", "Monedas con las que se cobra y se cuadra. La moneda local se elige en el parámetro General.MonedaLocal.",
            Icons.Material.Filled.CurrencyExchange,
            [Codigo(fijo: true, ayuda: "Código ISO 4217, ej. DOP o USD."), Nombre, new("simbolo", "Símbolo") { Obligatorio = true }, Estado("activa")]),

        new("tasas-cambio", "Tasas de cambio", "Nueva tasa", "Pesos por unidad de moneda extranjera. La caja usa la tasa más reciente ya vigente.",
            Icons.Material.Filled.ShowChart,
            [
                new("moneda", "Moneda", TipoCampoCatalogo.Moneda) { Obligatorio = true },
                new("tasa", "Tasa", TipoCampoCatalogo.Decimal) { Obligatorio = true },
                new("vigenteDesde", "Vigente desde", TipoCampoCatalogo.FechaHora) { Obligatorio = true },
            ]),

        new("familias", "Familias", "Nueva familia", "Agrupan los artículos para el catálogo, las ofertas y los topes de descuento.",
            Icons.Material.Filled.Category,
            [
                Codigo(), Nombre,
                new("permiteDescuentoManual", "Permite descuento manual", TipoCampoCatalogo.Booleano) { Predeterminado = true },
                new("esNoCodificada", "No codificada", TipoCampoCatalogo.Booleano) { Predeterminado = false, Ayuda = "Artículos que se venden sin código, digitando el precio." },
                Estado("activa"),
            ]),

        new("unidades-medida", "Unidades de medida", "Nueva unidad", "Cómo se cuenta cada artículo y con cuántos decimales se vende.",
            Icons.Material.Filled.Straighten,
            [
                Codigo(), Nombre,
                new("permiteDecimales", "Permite decimales", TipoCampoCatalogo.Booleano) { Predeterminado = false },
                new("decimales", "Decimales", TipoCampoCatalogo.Entero) { Predeterminado = 0 },
            ]),

        new("impuestos", "Impuestos", "Nuevo impuesto", "Tasas de ITBIS con su indicador de facturación del e-CF.",
            Icons.Material.Filled.Percent,
            [
                Codigo(), Nombre,
                new("porcentaje", "Porcentaje", TipoCampoCatalogo.Decimal) { Obligatorio = true },
                new("indicadorFacturacion", "Indicador e-CF", TipoCampoCatalogo.Entero)
                    { Obligatorio = true, Ayuda = "1 = ITBIS 18%, 2 = ITBIS 16%, 3 = ITBIS 0%, 4 = exento." },
                Estado(),
            ]),

        new("formas-pago", "Formas de pago", "Nueva forma de pago", "Aparecen en la pantalla de cobro según su orden. Lo que se deja \"según el tipo\" toma el valor sugerido.",
            Icons.Material.Filled.Payments,
            [
                Codigo(), Nombre,
                new("tipo", "Tipo", TipoCampoCatalogo.Opciones) { Obligatorio = true, FijoAlEditar = true, Opciones = TiposFormaPago },
                new("orden", "Orden", TipoCampoCatalogo.Entero) { Obligatorio = true, Predeterminado = 1 },
                new("moneda", "Moneda", TipoCampoCatalogo.Moneda) { Obligatorio = true },
                new("abreGaveta", "Abre la gaveta", TipoCampoCatalogo.SiNoSegunTipo) { EnTabla = false },
                new("permiteDevuelta", "Permite devuelta", TipoCampoCatalogo.SiNoSegunTipo) { EnTabla = false },
                new("requiereReferencia", "Requiere referencia", TipoCampoCatalogo.SiNoSegunTipo) { EnTabla = false },
                new("requiereBanco", "Requiere banco", TipoCampoCatalogo.SiNoSegunTipo) { EnTabla = false },
                new("permiteComprobanteFiscal", "Permite crédito fiscal", TipoCampoCatalogo.SiNoSegunTipo) { EnTabla = false },
                Estado("activa"),
            ]),

        new("denominaciones", "Denominaciones", "Nueva denominación", "Billetes y monedas para los cuadres por denominación. Moneda, valor y tipo no cambian.",
            Icons.Material.Filled.LocalAtm,
            [
                new("moneda", "Moneda", TipoCampoCatalogo.Moneda) { Obligatorio = true, FijoAlEditar = true },
                new("valor", "Valor", TipoCampoCatalogo.Decimal) { Obligatorio = true, FijoAlEditar = true },
                new("tipo", "Tipo", TipoCampoCatalogo.Opciones)
                    { Obligatorio = true, FijoAlEditar = true, Opciones = [new("Billete", "Billete"), new("Moneda", "Moneda")], Predeterminado = "Billete" },
                Estado("activa"),
            ]),

        new("bancos", "Bancos", "Nuevo banco", "Bancos para transferencias, cheques y tarjetas.",
            Icons.Material.Filled.AccountBalance,
            [Codigo(), Nombre, new("rutaLogo", "Ruta del logo") { EnTabla = false }, Estado()]),

        new("tipos-tarjeta", "Tipos de tarjeta", "Nuevo tipo de tarjeta", "Marcas de tarjeta que registra el cobro.",
            Icons.Material.Filled.CreditCard,
            [Codigo(), Nombre, Estado()]),

        new("motivos-descuento", "Motivos de descuento", "Nuevo motivo", "Lista de motivos que el cajero elige al aplicar un descuento manual.",
            Icons.Material.Filled.Discount,
            [Codigo(), Nombre, Estado()]),

        new("motivos-devolucion", "Motivos de devolución", "Nuevo motivo", "Lista de motivos de una devolución.",
            Icons.Material.Filled.AssignmentReturn,
            [Codigo(), Nombre, Estado()]),

        new("almacenes", "Almacenes", "Nuevo almacén", "Dónde se retira la mercancía pendiente de entrega.",
            Icons.Material.Filled.Warehouse,
            [
                Codigo(fijo: true), Nombre,
                new("sucursalId", "Sucursal", TipoCampoCatalogo.Sucursal) { Obligatorio = true },
                new("direccion", "Dirección") { EnTabla = false },
                Estado(),
            ]),

        new("niveles-fidelidad", "Niveles de fidelidad", "Nuevo nivel", "Categorías del programa: el factor multiplica los puntos que acumula el cliente.",
            Icons.Material.Filled.MilitaryTech,
            [
                Codigo(fijo: true), Nombre,
                new("orden", "Orden", TipoCampoCatalogo.Entero) { Obligatorio = true, Predeterminado = 1 },
                new("factorAcumulacion", "Factor", TipoCampoCatalogo.Decimal)
                {
                    Obligatorio = true,
                    Predeterminado = 1m,
                    Ayuda = "Multiplica los puntos: 1 = normal, 1.5 = 50 % más.",
                },
                Estado(),
            ]),

        new("reglas-acumulacion", "Reglas de acumulación", "Nueva regla", "Cuántos puntos da cada compra. Si aplican varias a una línea, gana la más favorable.",
            Icons.Material.Filled.Rule,
            [
                Codigo(fijo: true), Nombre,
                new("tipo", "Aplica a", TipoCampoCatalogo.Opciones) { Obligatorio = true, Opciones = TiposReglaAcumulacion },
                new("montoBase", "Por cada", TipoCampoCatalogo.Decimal) { Obligatorio = true, Ayuda = "Monto comprado que otorga los puntos, ej. 100." },
                new("puntos", "Puntos", TipoCampoCatalogo.Decimal) { Obligatorio = true },
                new("referenciaId", "Familia, artículo o promoción", TipoCampoCatalogo.Texto)
                {
                    EnTabla = false,
                    Ayuda = "Id de lo que abarca la regla; se deja vacío si aplica a todo o a un día.",
                },
                new("diaSemana", "Día", TipoCampoCatalogo.Opciones) { EnTabla = false, Opciones = DiasSemana },
                new("vigenteDesde", "Vigente desde", TipoCampoCatalogo.FechaHora) { EnTabla = false },
                new("vigenteHasta", "Vigente hasta", TipoCampoCatalogo.FechaHora) { EnTabla = false },
                Estado("activa"),
            ]),

        new("descuentos-tarjeta", "Descuentos por tarjeta", "Nuevo descuento", "Descuento del banco por el BIN de la tarjeta; se aplica a la factura antes de emitir el e-CF.",
            Icons.Material.Filled.CreditScore,
            [
                Codigo(fijo: true), Nombre,
                new("bines", "BIN de las tarjetas") { Obligatorio = true, Ayuda = "Primeros 4 a 8 dígitos, separados por coma: 401234,455678." },
                new("tipo", "Tipo", TipoCampoCatalogo.Opciones) { Obligatorio = true, Opciones = TiposDescuentoTarjeta },
                new("valor", "Valor", TipoCampoCatalogo.Decimal) { Obligatorio = true },
                new("montoMinimo", "Compra mínima", TipoCampoCatalogo.Decimal) { EnTabla = false },
                new("montoMaximo", "Tope del descuento", TipoCampoCatalogo.Decimal) { EnTabla = false },
                new("vigenteDesde", "Vigente desde", TipoCampoCatalogo.FechaHora) { Obligatorio = true },
                new("vigenteHasta", "Vigente hasta", TipoCampoCatalogo.FechaHora) { Obligatorio = true },
                Estado(),
            ]),
    ];

    public static DefinicionCatalogo? Buscar(string? ruta) => Todos.FirstOrDefault(d => d.Ruta == ruta);
}
