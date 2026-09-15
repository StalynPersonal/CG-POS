using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;

namespace CgPos.Contratos.Catalogo;

/// <summary>
/// Maestros de catálogo para la caja (M03, M04). Hoy se cargan desde archivo; luego los enviará el Central.
/// Todo es idempotente por Id.
/// </summary>
public sealed record PaqueteMaestros(
    IReadOnlyList<FamiliaCarga>? Familias = null,
    IReadOnlyList<UnidadMedidaCarga>? UnidadesMedida = null,
    IReadOnlyList<ImpuestoCarga>? Impuestos = null,
    IReadOnlyList<ArticuloCarga>? Articulos = null,
    IReadOnlyList<ClienteCarga>? Clientes = null,
    IReadOnlyList<FormaPagoCarga>? FormasPago = null,
    IReadOnlyList<BancoCarga>? Bancos = null,
    IReadOnlyList<TipoTarjetaCarga>? TiposTarjeta = null,
    IReadOnlyList<DenominacionCarga>? Denominaciones = null,
    IReadOnlyList<PromocionCarga>? Promociones = null,
    IReadOnlyList<MotivoDescuentoCarga>? MotivosDescuento = null,
    IReadOnlyList<TopeDescuentoCarga>? TopesDescuento = null);

/// <summary>Oferta del Central (RF-59). Vacíos en sucursales = todas; los días y horas son locales.</summary>
public sealed record PromocionCarga(
    Guid Id,
    string Codigo,
    string Nombre,
    TipoPromocion Tipo,
    decimal Valor,
    DateTimeOffset VigenteDesde,
    DateTimeOffset VigenteHasta,
    IReadOnlyList<Guid>? Articulos = null,
    IReadOnlyList<Guid>? Familias = null,
    IReadOnlyList<Guid>? Sucursales = null,
    int? CantidadLleva = null,
    int? CantidadPaga = null,
    decimal? CantidadMinima = null,
    decimal? LimitePorCliente = null,
    DiasSemana Dias = DiasSemana.Todos,
    TimeOnly? HoraDesde = null,
    TimeOnly? HoraHasta = null,
    bool SoloFidelidad = false,
    bool Activa = true);

public sealed record MotivoDescuentoCarga(Guid Id, string Codigo, string Nombre, bool Activo = true);

/// <summary>Tope de descuento por nivel: general, o para una familia o un artículo (RF-202).</summary>
public sealed record TopeDescuentoCarga(Guid Id, int Nivel, decimal? PorcentajeMaximo, decimal? MontoMaximo, Guid? FamiliaId = null, Guid? ArticuloId = null);

public sealed record DatosMotivoDescuento(string Codigo, string Nombre);

/// <summary>Oferta vigente de un artículo, para la consulta de precio (RF-24) y la columna Promo (RF-141).</summary>
public sealed record DatosPromocionVigente(Guid Id, string Codigo, string Nombre, string Descripcion, TipoPromocion Tipo, DateTimeOffset VigenteHasta);

public sealed record FamiliaCarga(Guid Id, string Codigo, string Nombre, bool PermiteDescuentoManual = true, bool EsNoCodificada = false, bool Activa = true);

public sealed record UnidadMedidaCarga(Guid Id, string Codigo, string Nombre, bool PermiteDecimales = false, int Decimales = 0);

public sealed record ImpuestoCarga(Guid Id, string Codigo, string Nombre, decimal Porcentaje, int IndicadorFacturacion, bool Activo = true);

/// <param name="PrecioDetalle">Precio con impuesto incluido.</param>
/// <param name="PrecioMayor">Precio por mayor con impuesto incluido; nulo si no aplica.</param>
/// <param name="PreciosVigentesDesde">Vigencia de los precios del paquete; nulo = desde el momento de la carga.</param>
public sealed record ArticuloCarga(
    Guid Id,
    string Codigo,
    string Descripcion,
    Guid FamiliaId,
    Guid UnidadMedidaId,
    Guid ImpuestoId,
    decimal PrecioDetalle,
    decimal? PrecioMayor = null,
    TipoArticulo Tipo = TipoArticulo.Normal,
    string? Referencia = null,
    decimal? Costo = null,
    decimal? PrecioMinimo = null,
    decimal? CantidadMinimaMayor = null,
    IReadOnlyList<string>? CodigosBarras = null,
    IReadOnlyList<string>? CodigosProveedor = null,
    string? RutaImagen = null,
    bool MostrarEnCatalogo = false,
    bool VentaEnPos = true,
    bool Activo = true,
    DateTimeOffset? PreciosVigentesDesde = null,
    decimal? Tara = null);

public sealed record ClienteCarga(
    Guid Id,
    TipoDocumentoIdentidad TipoDocumento,
    string Documento,
    string Nombre,
    TipoComprobante TipoComprobante = TipoComprobante.FacturaConsumo,
    bool ExoneradoItbis = false,
    bool AplicaRetencion = false,
    ListaPrecio ListaPrecio = ListaPrecio.Detalle,
    string? Telefono = null,
    string? Correo = null,
    IReadOnlyList<DireccionClienteCarga>? Direcciones = null,
    bool Activo = true);

public sealed record DireccionClienteCarga(
    Guid Id,
    string Alias,
    string Direccion,
    string? Sector = null,
    string? Ciudad = null,
    string? Referencia = null,
    string? Telefono = null,
    bool EsPrincipal = false);

/// <remarks>Los indicadores nulos toman el valor sugerido para el tipo de forma de pago.</remarks>
public sealed record FormaPagoCarga(
    Guid Id,
    string Codigo,
    string Nombre,
    TipoFormaPago Tipo,
    int Orden,
    string Moneda = "DOP",
    bool? AbreGaveta = null,
    bool? PermiteDevuelta = null,
    bool? RequiereReferencia = null,
    bool? RequiereBanco = null,
    bool? PermiteComprobanteFiscal = null,
    bool Activa = true);

public sealed record BancoCarga(Guid Id, string Codigo, string Nombre, string? RutaLogo = null, bool Activo = true);

public sealed record TipoTarjetaCarga(Guid Id, string Codigo, string Nombre, bool Activo = true);

public sealed record DenominacionCarga(Guid Id, string Moneda, decimal Valor, TipoDenominacion Tipo, bool Activa = true);

// ---------- Consultas ----------

public enum OrigenCodigoLeido
{
    CodigoInterno,
    CodigoBarras,
    CodigoProveedor,
    EtiquetaBalanza,
}

/// <summary>Artículo listo para agregar a una venta, con sus precios vigentes y datos fiscales.</summary>
public sealed record DatosArticuloVenta(
    Guid ArticuloId,
    string Codigo,
    string Descripcion,
    string? Referencia,
    string CodigoLeido,
    OrigenCodigoLeido OrigenCodigo,
    TipoArticulo Tipo,
    Guid FamiliaId,
    string FamiliaNombre,
    bool PermiteDescuentoManual,
    string UnidadMedidaCodigo,
    bool PermiteDecimales,
    int DecimalesCantidad,
    Guid ImpuestoId,
    string ImpuestoCodigo,
    decimal PorcentajeImpuesto,
    int IndicadorFacturacion,
    decimal? PrecioDetalle,
    decimal? PrecioMayor,
    decimal? CantidadMinimaMayor,
    decimal? PrecioMinimo,
    decimal? PesoLeido,
    decimal? PrecioLeido,
    string? RutaImagen,
    decimal? Tara = null);

public sealed record DatosArticuloResumen(
    Guid ArticuloId,
    string Codigo,
    string Descripcion,
    string? Referencia,
    string FamiliaNombre,
    string UnidadMedidaCodigo,
    decimal? PrecioDetalle,
    decimal? PrecioMayor,
    string? RutaImagen);

public sealed record DatosPrecioHistorico(
    ListaPrecio Lista,
    decimal Precio,
    DateTimeOffset VigenteDesde,
    DateTimeOffset RegistradoEn,
    string Origen,
    string? UsuarioNombre);

public sealed record DatosDireccionCliente(
    Guid Id,
    string Alias,
    string Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    bool EsPrincipal);

public sealed record DatosClienteResumen(
    Guid ClienteId,
    TipoDocumentoIdentidad TipoDocumento,
    string Documento,
    string Nombre,
    TipoComprobante TipoComprobante,
    bool ExoneradoItbis,
    bool AplicaRetencion,
    ListaPrecio ListaPrecio,
    string? Telefono,
    string? Correo,
    IReadOnlyList<DatosDireccionCliente> Direcciones);

/// <summary>Resultado de digitar un RNC/cédula: validez, padrón DGII local y cliente registrado (RF-181, RF-182).</summary>
public sealed record DatosConsultaDocumento(
    string Documento,
    TipoDocumentoIdentidad? Tipo,
    bool FormatoValido,
    bool DigitoVerificadorValido,
    bool EnPadron,
    string? RazonSocial,
    string? NombreComercial,
    string? EstadoDgii,
    bool? ContribuyenteActivo,
    DatosClienteResumen? Cliente);

public sealed record DatosFormaPago(
    Guid Id,
    string Codigo,
    string Nombre,
    TipoFormaPago Tipo,
    string Moneda,
    int Orden,
    bool AbreGaveta,
    bool PermiteDevuelta,
    bool RequiereReferencia,
    bool RequiereBanco,
    bool PermiteComprobanteFiscal);

public sealed record DatosBanco(Guid Id, string Codigo, string Nombre, string? RutaLogo);

public sealed record DatosTipoTarjeta(Guid Id, string Codigo, string Nombre);

public sealed record DatosDenominacion(Guid Id, string Moneda, decimal Valor, TipoDenominacion Tipo);

/// <summary>Maestros que necesita la pantalla de cobro (RF-150, RF-184).</summary>
public sealed record DatosCatalogoCobro(
    IReadOnlyList<DatosFormaPago> FormasPago,
    IReadOnlyList<DatosBanco> Bancos,
    IReadOnlyList<DatosTipoTarjeta> TiposTarjeta,
    IReadOnlyList<DatosDenominacion> Denominaciones);

public sealed record DatosFamilia(Guid Id, string Codigo, string Nombre, bool EsNoCodificada);
