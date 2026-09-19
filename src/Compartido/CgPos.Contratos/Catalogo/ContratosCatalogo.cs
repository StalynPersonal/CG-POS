using CgPos.Dominio.Clientes;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;

namespace CgPos.Contratos.Catalogo;

/// <summary>
/// Maestros de catálogo que el Central publica para las cajas (M03, M04). No llevan Id: cada base tiene los suyos. Los maestros se identifican
/// por su código (texto), los catálogos por su código numérico y los demás por su llave natural; las referencias entre ellos también van por código.
/// Aplicar el mismo paquete dos veces no cambia nada.
/// </summary>
public sealed record PaqueteMaestros(
    IReadOnlyList<DepartamentoCarga>? Departamentos = null,
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
    IReadOnlyList<TopeDescuentoCarga>? TopesDescuento = null,
    IReadOnlyList<TasaCambioCarga>? TasasCambio = null,
    IReadOnlyList<SecuenciaEcfCarga>? SecuenciasEcf = null,
    IReadOnlyList<MotivoDevolucionCarga>? MotivosDevolucion = null,
    IReadOnlyList<MonedaCarga>? Monedas = null,
    IReadOnlyList<NivelFidelidadCarga>? NivelesFidelidad = null,
    IReadOnlyList<ReglaAcumulacionCarga>? ReglasAcumulacion = null,
    IReadOnlyList<MiembroFidelidadCarga>? MiembrosFidelidad = null,
    IReadOnlyList<AlmacenCarga>? Almacenes = null,
    IReadOnlyList<DescuentoTarjetaCarga>? DescuentosTarjeta = null,
    IReadOnlyList<CategoriaCarga>? Categorias = null,
    IReadOnlyList<MarcaCarga>? Marcas = null);

/// <summary>Descuento del banco al pagar con ciertas tarjetas, identificadas por su BIN (RF-98).</summary>
public sealed record DescuentoTarjetaCarga(
    string Codigo,
    string Nombre,
    string Bines,
    TipoDescuentoTarjeta Tipo,
    decimal Valor,
    DateTimeOffset VigenteDesde,
    DateTimeOffset VigenteHasta,
    decimal? MontoMinimo = null,
    decimal? MontoMaximo = null,
    string? BancoCodigo = null,
    DiasSemana Dias = DiasSemana.Todos,
    bool Activo = true);

/// <summary>Almacén o sucursal donde se retira mercancía pendiente (RF-140).</summary>
public sealed record AlmacenCarga(string Codigo, string Nombre, string SucursalCodigo, string? Direccion = null, bool Activo = true);

/// <summary>Nivel del programa de fidelidad (RF-241): el factor multiplica los puntos que acumula.</summary>
public sealed record NivelFidelidadCarga(int Codigo, string Nombre, int Orden, decimal FactorAcumulacion, bool Activo = true);

/// <summary>Regla de acumulación (RF-238): <paramref name="Puntos"/> por cada <paramref name="MontoBase"/> comprado de lo que abarca.</summary>
/// <param name="Referencia">Código de lo que abarca según el tipo: departamento, categoría o marca (número), artículo o promoción (texto).</param>
public sealed record ReglaAcumulacionCarga(
    int Codigo,
    string Nombre,
    CgPos.Dominio.Fidelidad.TipoReglaAcumulacion Tipo,
    decimal MontoBase,
    decimal Puntos,
    string? Referencia = null,
    DayOfWeek? DiaSemana = null,
    DateTimeOffset? VigenteDesde = null,
    DateTimeOffset? VigenteHasta = null,
    bool Activa = true);

/// <summary>Miembro del programa con el saldo que calculó el Central (RF-240, RF-242). Se identifica por la cédula.</summary>
public sealed record MiembroFidelidadCarga(
    string Cedula,
    string Nombre,
    string? Telefono = null,
    string? Correo = null,
    int? NivelCodigo = null,
    int SaldoPuntos = 0,
    DateTimeOffset? SaldoAl = null,
    int PuntosPorVencer = 0,
    DateOnly? ProximoVencimiento = null,
    DateTimeOffset? InscritoEn = null,
    bool Activo = true);

/// <summary>Moneda del maestro del Central (ISO 4217) con su símbolo para pantallas y tickets.</summary>
public sealed record MonedaCarga(string Codigo, string Nombre, string Simbolo, bool Activa = true);

/// <summary>Moneda local de la caja, según el parámetro General.MonedaLocal y el maestro de monedas.</summary>
public sealed record DatosMoneda(string Codigo, string Nombre, string Simbolo);

/// <summary>Motivo seleccionable de devolución (RF-232).</summary>
public sealed record MotivoDevolucionCarga(int Codigo, string Nombre, bool Activo = true);

/// <summary>
/// Rango de e-CF que el Central asigna a una caja (RF-28). Se identifica por su tipo y su inicio: un e-NCF es único en la empresa y los rangos no se
/// solapan. Ampliar el mismo rango extiende su final.
/// </summary>
public sealed record SecuenciaEcfCarga(string SucursalCodigo, string CajaCodigo, TipoComprobante TipoComprobante, long Desde, long Hasta, DateOnly VenceEn, bool Activa = true);

/// <summary>Tasa del día de SAP B1: pesos por unidad de la moneda (RF-212). Se identifica por la moneda y desde cuándo rige.</summary>
public sealed record TasaCambioCarga(string Moneda, decimal Tasa, DateTimeOffset VigenteDesde);

public sealed record DatosTasaCambio(string Moneda, decimal Tasa, DateTimeOffset VigenteDesde);

/// <summary>Oferta del Central (RF-59). Vacíos en sucursales = todas; los días y horas son locales. El alcance va por códigos.</summary>
public sealed record PromocionCarga(
    string Codigo,
    string Nombre,
    TipoPromocion Tipo,
    decimal Valor,
    DateTimeOffset VigenteDesde,
    DateTimeOffset VigenteHasta,
    IReadOnlyList<string>? Articulos = null,
    IReadOnlyList<int>? Departamentos = null,
    IReadOnlyList<string>? Sucursales = null,
    int? CantidadLleva = null,
    int? CantidadPaga = null,
    decimal? CantidadMinima = null,
    decimal? LimitePorCliente = null,
    DiasSemana Dias = DiasSemana.Todos,
    TimeOnly? HoraDesde = null,
    TimeOnly? HoraHasta = null,
    bool SoloFidelidad = false,
    bool Activa = true,
    IReadOnlyList<int>? Categorias = null,
    IReadOnlyList<int>? Marcas = null);

public sealed record MotivoDescuentoCarga(int Codigo, string Nombre, bool Activo = true);

/// <summary>Tope de descuento por nivel: general, o para un departamento, una categoría, una marca o un artículo (RF-202).</summary>
public sealed record TopeDescuentoCarga(int Codigo, int Nivel, decimal? PorcentajeMaximo, decimal? MontoMaximo, int? DepartamentoCodigo = null,
    string? ArticuloCodigo = null, int? CategoriaCodigo = null, int? MarcaCodigo = null);

public sealed record DatosMotivoDescuento(int Codigo, string Nombre);

/// <summary>Oferta vigente de un artículo, para la consulta de precio (RF-24) y la columna Promo (RF-141).</summary>
public sealed record DatosPromocionVigente(int Id, string Codigo, string Nombre, string Descripcion, TipoPromocion Tipo, DateTimeOffset VigenteHasta);

public sealed record DepartamentoCarga(int Codigo, string Nombre, bool PermiteDescuentoManual = true, bool EsNoCodificada = false, bool Activa = true);

/// <summary>Categoría dentro de un departamento.</summary>
public sealed record CategoriaCarga(int Codigo, string Nombre, int DepartamentoCodigo, bool Activa = true);

public sealed record MarcaCarga(int Codigo, string Nombre, bool Activa = true);

/// <param name="Abreviatura">Lo que se imprime en el ticket y va en el e-CF (ej. UND).</param>
public sealed record UnidadMedidaCarga(int Codigo, string Abreviatura, string Nombre, bool PermiteDecimales = false, int Decimales = 0);

public sealed record ImpuestoCarga(string Codigo, string Nombre, decimal Porcentaje, int IndicadorFacturacion, bool Activo = true);

/// <param name="PrecioDetalle">Precio con impuesto incluido.</param>
/// <param name="PrecioMayor">Precio por mayor con impuesto incluido; nulo si no aplica.</param>
/// <param name="PreciosVigentesDesde">Vigencia de los precios del paquete; nulo = desde el momento de la carga.</param>
public sealed record ArticuloCarga(
    string Codigo,
    string Descripcion,
    int DepartamentoCodigo,
    int UnidadMedidaCodigo,
    string ImpuestoCodigo,
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
    decimal? Tara = null,
    bool EsServicio = false,
    int? CategoriaCodigo = null,
    int? MarcaCodigo = null);

/// <param name="Codigo">Lo asigna el Central y no cambia; el documento se puede corregir.</param>
public sealed record ClienteCarga(
    string Codigo,
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
    bool Activo = true,
    string? Contacto = null,
    string? TelefonoAlterno = null);

/// <param name="Alias">Identifica la dirección dentro del cliente (ej. "Casa", "Oficina").</param>
public sealed record DireccionClienteCarga(
    string Alias,
    string Direccion,
    string? Sector = null,
    string? Ciudad = null,
    string? Referencia = null,
    string? Telefono = null,
    bool EsPrincipal = false);

/// <remarks>Los indicadores nulos toman el valor sugerido para el tipo de forma de pago.</remarks>
public sealed record FormaPagoCarga(
    string Codigo,
    string Nombre,
    TipoFormaPago Tipo,
    int Orden,
    string Moneda,
    bool? AbreGaveta = null,
    bool? PermiteDevuelta = null,
    bool? RequiereReferencia = null,
    bool? RequiereBanco = null,
    bool? PermiteComprobanteFiscal = null,
    bool Activa = true);

public sealed record BancoCarga(string Codigo, string Nombre, string? RutaLogo = null, bool Activo = true);

public sealed record TipoTarjetaCarga(int Codigo, string Nombre, bool Activo = true);

/// <summary>Billete o moneda; se identifica por la moneda, el valor y el tipo.</summary>
public sealed record DenominacionCarga(string Moneda, decimal Valor, TipoDenominacion Tipo, bool Activa = true);

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
    int ArticuloId,
    string Codigo,
    string Descripcion,
    string? Referencia,
    string CodigoLeido,
    OrigenCodigoLeido OrigenCodigo,
    TipoArticulo Tipo,
    int DepartamentoId,
    string DepartamentoNombre,
    bool PermiteDescuentoManual,
    string UnidadMedidaCodigo,
    bool PermiteDecimales,
    int DecimalesCantidad,
    int ImpuestoId,
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
    decimal? Tara = null,
    bool EsServicio = false,
    int? CategoriaId = null,
    string? CategoriaNombre = null,
    int? MarcaId = null,
    string? MarcaNombre = null);

public sealed record DatosArticuloResumen(
    int ArticuloId,
    string Codigo,
    string Descripcion,
    string? Referencia,
    string DepartamentoNombre,
    string UnidadMedidaCodigo,
    decimal? PrecioDetalle,
    decimal? PrecioMayor,
    string? RutaImagen,
    string? CategoriaNombre = null);

public sealed record DatosPrecioHistorico(
    ListaPrecio Lista,
    decimal Precio,
    DateTimeOffset VigenteDesde,
    DateTimeOffset RegistradoEn,
    string Origen,
    string? UsuarioNombre);

public sealed record DatosDireccionCliente(
    int Id,
    string Alias,
    string Direccion,
    string? Sector,
    string? Ciudad,
    string? Referencia,
    string? Telefono,
    bool EsPrincipal);

public sealed record DatosClienteResumen(
    int ClienteId,
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
    int Id,
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

public sealed record DatosBanco(int Id, string Codigo, string Nombre, string? RutaLogo);

public sealed record DatosTipoTarjeta(int Id, int Codigo, string Nombre);

public sealed record DatosDenominacion(int Id, string Moneda, decimal Valor, TipoDenominacion Tipo);

/// <summary>Maestros que necesita la pantalla de cobro (RF-150, RF-184).</summary>
public sealed record DatosCatalogoCobro(
    IReadOnlyList<DatosFormaPago> FormasPago,
    IReadOnlyList<DatosBanco> Bancos,
    IReadOnlyList<DatosTipoTarjeta> TiposTarjeta,
    IReadOnlyList<DatosDenominacion> Denominaciones,
    IReadOnlyList<DatosTasaCambio>? Tasas = null);

public sealed record DatosDepartamento(int Id, int Codigo, string Nombre, bool EsNoCodificada);
