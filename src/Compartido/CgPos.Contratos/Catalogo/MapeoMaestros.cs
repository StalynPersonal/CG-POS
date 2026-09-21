using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Devoluciones;
using CgPos.Dominio.Entregas;
using CgPos.Dominio.Fidelidad;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;

namespace CgPos.Contratos.Catalogo;

/// <summary>
/// Traduce los códigos de un paquete a los Id de la base donde se aplica (la caja o el Central): cada base tiene sus propios Id.
/// </summary>
/// <remarks>Lanza <see cref="InvalidOperationException"/> si el código no existe.</remarks>
public interface IResolutorCodigos
{
    int Departamento(int codigo);
    int Categoria(int codigo);
    int Marca(int codigo);
    int UnidadMedida(int codigo);
    int Impuesto(string codigo);
    int Articulo(string codigo);
    int Sucursal(string codigo);
    int Caja(string sucursalCodigo, string cajaCodigo);
    int Banco(string codigo);
    int NivelFidelidad(int codigo);
    int Promocion(string codigo);
}

/// <summary>
/// Creación y actualización de las entidades del dominio a partir de las cargas, con las mismas reglas en la caja y en el Central.
/// Lo que no puede cambiar (códigos, llaves naturales) se rechaza con <see cref="InvalidOperationException"/>.
/// </summary>
public static class MapeoMaestros
{
    public static Moneda Crear(MonedaCarga d) => Activar(Moneda.Crear(d.Codigo, d.Nombre, d.Simbolo), d.Activa, m => m.Activar(), m => m.Desactivar());

    public static void Actualizar(Moneda e, MonedaCarga d)
    {
        e.Actualizar(d.Nombre, d.Simbolo);
        Activar(e, d.Activa, m => m.Activar(), m => m.Desactivar());
    }

    public static Departamento Crear(DepartamentoCarga d) =>
        Activar(Departamento.Crear(d.Codigo, d.Nombre, d.PermiteDescuentoManual, d.EsNoCodificada), d.Activa, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(Departamento e, DepartamentoCarga d)
    {
        e.Actualizar(d.Nombre, d.PermiteDescuentoManual, d.EsNoCodificada);
        Activar(e, d.Activa, x => x.Activar(), x => x.Desactivar());
    }

    public static Categoria Crear(CategoriaCarga d, IResolutorCodigos r) =>
        Activar(Categoria.Crear(d.Codigo, d.Nombre, r.Departamento(d.DepartamentoCodigo)), d.Activa, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(Categoria e, CategoriaCarga d, IResolutorCodigos r)
    {
        e.Actualizar(d.Nombre, r.Departamento(d.DepartamentoCodigo));
        Activar(e, d.Activa, x => x.Activar(), x => x.Desactivar());
    }

    public static Marca Crear(MarcaCarga d) => Activar(Marca.Crear(d.Codigo, d.Nombre), d.Activa, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(Marca e, MarcaCarga d)
    {
        e.CambiarNombre(d.Nombre);
        Activar(e, d.Activa, x => x.Activar(), x => x.Desactivar());
    }

    public static UnidadMedida Crear(UnidadMedidaCarga d) => UnidadMedida.Crear(d.Codigo, d.Abreviatura, d.Nombre, d.PermiteDecimales, d.Decimales);

    public static void Actualizar(UnidadMedida e, UnidadMedidaCarga d) => e.Actualizar(d.Abreviatura, d.Nombre, d.PermiteDecimales, d.Decimales);

    public static Impuesto Crear(ImpuestoCarga d) =>
        Activar(Impuesto.Crear(d.Codigo, d.Nombre, d.Porcentaje, d.IndicadorFacturacion), d.Activo, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(Impuesto e, ImpuestoCarga d)
    {
        e.Actualizar(d.Nombre, d.Porcentaje, d.IndicadorFacturacion);
        Activar(e, d.Activo, x => x.Activar(), x => x.Desactivar());
    }

    /// <summary>Los precios no son parte del artículo: los registra quien aplica la carga, con su historial.</summary>
    public static Articulo Crear(ArticuloCarga d, IResolutorCodigos r)
    {
        var articulo = Articulo.Crear(d.Codigo, d.Descripcion, r.Departamento(d.DepartamentoCodigo), r.UnidadMedida(d.UnidadMedidaCodigo), r.Impuesto(d.ImpuestoCodigo), d.Tipo);
        Actualizar(articulo, d, r);
        return articulo;
    }

    public static void Actualizar(Articulo e, ArticuloCarga d, IResolutorCodigos r)
    {
        if (e.Codigo != d.Codigo?.Trim())
            throw new InvalidOperationException($"No se puede cambiar el código del artículo ({e.Codigo} → {d.Codigo?.Trim()}).");

        if (d.CategoriaCodigo is null)
            throw new ArgumentException("Indique la categoría del artículo.");
        // Un artículo inactivo no se vende, así que puede venir sin precio: es lo que trae cualquier migración de un
        // sistema viejo, donde lo descontinuado queda con precio cero. Exigírselo dejaría a la caja sin bajar nada.
        if (d.PrecioDetalle <= 0 && d.Activo)
            throw new ArgumentException("El precio detalle debe ser mayor que cero.");
        if (d.PrecioMayor <= 0)
            throw new ArgumentException("El precio por mayor debe ser mayor que cero.");

        var departamentoId = r.Departamento(d.DepartamentoCodigo);
        e.ActualizarDatos(d.Descripcion, d.Referencia, departamentoId, r.UnidadMedida(d.UnidadMedidaCodigo), r.Impuesto(d.ImpuestoCodigo), d.Tipo);
        e.ConfigurarPrecios(d.Costo, d.PrecioMinimo, d.CantidadMinimaMayor);
        e.ConfigurarPesoEmpaque(d.PesoEmpaque);
        e.Clasificar(r.Categoria(d.CategoriaCodigo.Value), d.MarcaCodigo is { } marca ? r.Marca(marca) : null);
        e.ConfigurarNaturaleza(d.EsServicio);
        e.ConfigurarPresentacion(d.RutaImagen, d.MostrarEnCatalogo, d.VentaEnPos);
        e.ReemplazarCodigos(
            (d.CodigosBarras ?? []).Select(c => (c, TipoCodigoArticulo.Barras))
                .Concat((d.CodigosProveedor ?? []).Select(c => (c, TipoCodigoArticulo.Proveedor))));
        if (d.Activo) e.Activar(); else e.Desactivar();
    }

    public static Cliente Crear(ClienteCarga d)
    {
        var cliente = Cliente.Crear(d.Codigo, d.TipoDocumento, d.Documento, d.Nombre);
        Actualizar(cliente, d, corregirDocumento: false);
        return cliente;
    }

    /// <param name="corregirDocumento">
    /// Falso: el documento debe ser el mismo. En la caja siempre es verdadero, porque la corrección ya se auditó en el Central.
    /// </param>
    public static void Actualizar(Cliente e, ClienteCarga d, bool corregirDocumento)
    {
        if (!string.Equals(e.Codigo, d.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"No se puede cambiar el código del cliente '{e.Codigo}'.");

        if (e.TipoDocumento != d.TipoDocumento || e.Documento != DocumentoIdentidad.Normalizar(d.Documento))
        {
            if (!corregirDocumento)
                throw new InvalidOperationException($"El documento del cliente '{e.Documento}' se cambia con «Corregir documento».");
            e.CorregirDocumento(d.TipoDocumento, d.Documento);
        }

        e.ActualizarContacto(d.Nombre, d.Telefono, d.Correo, d.Contacto, d.TelefonoAlterno);
        e.ConfigurarFacturacion(d.TipoComprobante, d.ExoneradoItbis, d.AplicaRetencion, d.ListaPrecio);

        // Las direcciones se identifican por su alias dentro del cliente.
        var direcciones = d.Direcciones ?? [];
        var alias = direcciones.Select(x => x.Alias?.Trim() ?? string.Empty).ToList();
        if (alias.GroupBy(a => a, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            throw new ArgumentException($"La dirección «{repetido.Key}» está repetida en el cliente.");

        foreach (var sobrante in e.Direcciones.Where(x => !alias.Contains(x.Alias, StringComparer.OrdinalIgnoreCase)).Select(x => x.Alias).ToList())
            e.QuitarDireccion(sobrante);

        foreach (var direccion in direcciones)
        {
            var existente = e.Direcciones.FirstOrDefault(x => string.Equals(x.Alias, direccion.Alias?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existente is null)
                e.AgregarDireccion(direccion.Alias!, direccion.Direccion, direccion.Sector, direccion.Ciudad, direccion.Referencia, direccion.Telefono);
            else
                e.ActualizarDireccion(direccion.Alias!, direccion.Direccion, direccion.Sector, direccion.Ciudad, direccion.Referencia, direccion.Telefono);
        }

        if (direcciones.FirstOrDefault(x => x.EsPrincipal) is { } principal)
            e.MarcarPrincipal(principal.Alias!);

        if (d.Activo) e.Activar(); else e.Desactivar();
    }

    public static FormaPago Crear(FormaPagoCarga d)
    {
        var forma = FormaPago.Crear(d.Codigo, d.Nombre, d.Tipo, d.Orden, d.Moneda);
        Actualizar(forma, d);
        return forma;
    }

    public static void Actualizar(FormaPago e, FormaPagoCarga d)
    {
        if (e.Tipo != d.Tipo)
            throw new InvalidOperationException($"No se puede cambiar el tipo de la forma de pago '{e.Codigo}'; cree una nueva.");

        var sugeridos = FormaPago.ValoresPorTipo(d.Tipo);
        e.Configurar(d.Nombre, d.Orden, d.AbreGaveta ?? sugeridos.AbreGaveta, d.PermiteDevuelta ?? sugeridos.PermiteDevuelta,
            d.RequiereReferencia ?? sugeridos.RequiereReferencia, d.RequiereBanco ?? sugeridos.RequiereBanco, d.PermiteComprobanteFiscal ?? sugeridos.PermiteComprobanteFiscal);
        if (d.Activa) e.Activar(); else e.Desactivar();
    }

    public static Banco Crear(BancoCarga d) => Activar(Dominio.Pagos.Banco.Crear(d.Codigo, d.Nombre, d.RutaLogo), d.Activo, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(Banco e, BancoCarga d)
    {
        e.Actualizar(d.Nombre, d.RutaLogo);
        Activar(e, d.Activo, x => x.Activar(), x => x.Desactivar());
    }

    public static TipoTarjeta Crear(TipoTarjetaCarga d) => Activar(TipoTarjeta.Crear(d.Codigo, d.Nombre), d.Activo, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(TipoTarjeta e, TipoTarjetaCarga d)
    {
        e.CambiarNombre(d.Nombre);
        Activar(e, d.Activo, x => x.Activar(), x => x.Desactivar());
    }

    public static Denominacion Crear(DenominacionCarga d) =>
        Activar(Denominacion.Crear(d.Moneda, d.Valor, d.Tipo), d.Activa, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(Denominacion e, DenominacionCarga d) => Activar(e, d.Activa, x => x.Activar(), x => x.Desactivar());

    public static Promocion Crear(PromocionCarga d, IResolutorCodigos r)
    {
        var promocion = Promocion.Crear(d.Codigo, d.Nombre, d.Tipo, d.Valor, d.VigenteDesde, d.VigenteHasta);
        Actualizar(promocion, d, r);
        return promocion;
    }

    public static void Actualizar(Promocion e, PromocionCarga d, IResolutorCodigos r)
    {
        if (e.Codigo != d.Codigo?.Trim().ToUpperInvariant())
            throw new InvalidOperationException($"No se puede cambiar el código de la promoción ({e.Codigo} → {d.Codigo?.Trim()}).");

        e.Actualizar(d.Nombre, d.Tipo, d.Valor, d.VigenteDesde, d.VigenteHasta);
        e.ConfigurarCantidades(d.CantidadLleva, d.CantidadPaga, d.CantidadMinima, d.LimitePorCliente);
        e.Programar(d.Dias, d.HoraDesde, d.HoraHasta);
        e.AsignarAlcance(
            (d.Articulos ?? []).Select(r.Articulo).ToList(),
            (d.Departamentos ?? []).Select(r.Departamento).ToList(),
            (d.Sucursales ?? []).Select(r.Sucursal).ToList(),
            (d.Categorias ?? []).Select(r.Categoria).ToList(),
            (d.Marcas ?? []).Select(r.Marca).ToList());
        e.ConfigurarFidelidad(d.SoloFidelidad);
        if (d.Activa) e.Activar(); else e.Desactivar();
    }

    public static MotivoDescuento Crear(MotivoDescuentoCarga d) =>
        Activar(MotivoDescuento.Crear(d.Codigo, d.Nombre), d.Activo, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(MotivoDescuento e, MotivoDescuentoCarga d)
    {
        e.CambiarNombre(d.Nombre);
        Activar(e, d.Activo, x => x.Activar(), x => x.Desactivar());
    }

    public static TopeDescuento Crear(TopeDescuentoCarga d, IResolutorCodigos r)
    {
        var (departamento, articulo, categoria, marca) = AlcanceTope(d, r);
        return TopeDescuento.Crear(d.Codigo, d.Nivel, d.PorcentajeMaximo, d.MontoMaximo, departamento, articulo, categoriaId: categoria, marcaId: marca);
    }

    public static void Actualizar(TopeDescuento e, TopeDescuentoCarga d, IResolutorCodigos r)
    {
        var (departamento, articulo, categoria, marca) = AlcanceTope(d, r);
        e.Actualizar(d.Nivel, d.PorcentajeMaximo, d.MontoMaximo, departamento, articulo, categoria, marca);
    }

    public static TasaCambio Crear(TasaCambioCarga d) => TasaCambio.Registrar(d.Moneda, d.Tasa, d.VigenteDesde);

    public static void Actualizar(TasaCambio e, TasaCambioCarga d) => e.Actualizar(d.Tasa, e.VigenteDesde);

    public static SecuenciaEcf Crear(SecuenciaEcfCarga d, IResolutorCodigos r)
    {
        var secuencia = SecuenciaEcf.Asignar(r.Caja(d.SucursalCodigo, d.CajaCodigo), d.TipoComprobante, d.Desde, d.Hasta, d.VenceEn, d.Proximo, d.Serie);
        secuencia.Actualizar(d.Hasta, d.VenceEn, d.Activa);
        return secuencia;
    }

    public static void Actualizar(SecuenciaEcf e, SecuenciaEcfCarga d, IResolutorCodigos r)
    {
        if (e.CajaId != r.Caja(d.SucursalCodigo, d.CajaCodigo) || e.TipoComprobante != d.TipoComprobante || e.Desde != d.Desde)
            throw new InvalidOperationException("Un rango de e-CF no cambia de caja, tipo ni inicio; asigne un rango nuevo.");

        e.Actualizar(d.Hasta, d.VenceEn, d.Activa);

        // El Central puede corregir por dónde va la caja; sin indicarlo, cada caja sigue con lo suyo.
        if (d.Proximo is { } proximo)
            e.CambiarProximo(proximo);
    }

    public static MotivoDevolucion Crear(MotivoDevolucionCarga d) =>
        Activar(MotivoDevolucion.Crear(d.Codigo, d.Nombre), d.Activo, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(MotivoDevolucion e, MotivoDevolucionCarga d)
    {
        e.CambiarNombre(d.Nombre);
        Activar(e, d.Activo, x => x.Activar(), x => x.Desactivar());
    }

    public static NivelFidelidad Crear(NivelFidelidadCarga d) =>
        Activar(NivelFidelidad.Crear(d.Codigo, d.Nombre, d.Orden, d.FactorAcumulacion), d.Activo, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(NivelFidelidad e, NivelFidelidadCarga d)
    {
        e.Actualizar(d.Nombre, d.Orden, d.FactorAcumulacion);
        Activar(e, d.Activo, x => x.Activar(), x => x.Desactivar());
    }

    public static ReglaAcumulacion Crear(ReglaAcumulacionCarga d, IResolutorCodigos r) =>
        Activar(ReglaAcumulacion.Crear(d.Codigo, d.Nombre, d.Tipo, d.MontoBase, d.Puntos, ReferenciaRegla(d, r), d.DiaSemana, d.VigenteDesde, d.VigenteHasta),
            d.Activa, x => x.Activar(), x => x.Desactivar());

    public static void Actualizar(ReglaAcumulacion e, ReglaAcumulacionCarga d, IResolutorCodigos r)
    {
        e.Actualizar(d.Nombre, d.Tipo, d.MontoBase, d.Puntos, ReferenciaRegla(d, r), d.DiaSemana, d.VigenteDesde, d.VigenteHasta);
        Activar(e, d.Activa, x => x.Activar(), x => x.Desactivar());
    }

    public static MiembroFidelidad Crear(MiembroFidelidadCarga d, IResolutorCodigos r, DateTimeOffset ahora)
    {
        var miembro = MiembroFidelidad.DesdeCentral(MiembroFidelidad.ValidarCedula(d.Cedula), d.Nombre, d.InscritoEn ?? ahora);
        Actualizar(miembro, d, r);
        return miembro;
    }

    public static void Actualizar(MiembroFidelidad e, MiembroFidelidadCarga d, IResolutorCodigos r)
    {
        if (e.Cedula != MiembroFidelidad.ValidarCedula(d.Cedula))
            throw new InvalidOperationException($"No se puede cambiar la cédula del miembro {e.Cedula}.");

        e.ActualizarContacto(d.Nombre, d.Telefono, d.Correo);
        e.AsignarNivel(d.NivelCodigo is { } nivel ? r.NivelFidelidad(nivel) : null);
        if (d.SaldoAl is { } saldoAl)
            e.SincronizarSaldo(d.SaldoPuntos, saldoAl, d.PuntosPorVencer, d.ProximoVencimiento);
        if (d.Activo) e.Activar(); else e.Desactivar();
    }

    public static DescuentoTarjeta Crear(DescuentoTarjetaCarga d, IResolutorCodigos r) =>
        DescuentoTarjeta.Crear(d.Codigo, d.Nombre, d.Bines, d.Tipo, d.Valor, d.MontoMinimo, d.MontoMaximo, BancoDe(d, r),
            d.VigenteDesde, d.VigenteHasta, d.Dias, d.Activo);

    public static void Actualizar(DescuentoTarjeta e, DescuentoTarjetaCarga d, IResolutorCodigos r)
    {
        if (e.Codigo != d.Codigo?.Trim().ToUpperInvariant())
            throw new InvalidOperationException($"No se puede cambiar el código del descuento de tarjeta '{e.Codigo}'.");

        e.Actualizar(d.Nombre, d.Bines, d.Tipo, d.Valor, d.MontoMinimo, d.MontoMaximo, BancoDe(d, r), d.VigenteDesde, d.VigenteHasta, d.Dias, d.Activo);
    }

    /// <summary>Código de la referencia de una regla de acumulación según su tipo.</summary>
    public static int? ReferenciaRegla(ReglaAcumulacionCarga d, IResolutorCodigos r)
    {
        var referencia = d.Referencia?.Trim();
        if (string.IsNullOrEmpty(referencia))
            return null;

        int Numero() => int.TryParse(referencia, out var numero)
            ? numero
            : throw new ArgumentException($"La regla '{d.Codigo}' debe referenciar el código numérico del {d.Tipo.ToString().ToLowerInvariant()}.");

        return d.Tipo switch
        {
            TipoReglaAcumulacion.Departamento => r.Departamento(Numero()),
            TipoReglaAcumulacion.Categoria => r.Categoria(Numero()),
            TipoReglaAcumulacion.Marca => r.Marca(Numero()),
            TipoReglaAcumulacion.Articulo => r.Articulo(referencia),
            TipoReglaAcumulacion.Promocion => r.Promocion(referencia),
            _ => null,
        };
    }

    private static (int? Departamento, int? Articulo, int? Categoria, int? Marca) AlcanceTope(TopeDescuentoCarga d, IResolutorCodigos r) =>
        (d.DepartamentoCodigo is { } departamento ? r.Departamento(departamento) : null,
         string.IsNullOrWhiteSpace(d.ArticuloCodigo) ? null : r.Articulo(d.ArticuloCodigo),
         d.CategoriaCodigo is { } categoria ? r.Categoria(categoria) : null,
         d.MarcaCodigo is { } marca ? r.Marca(marca) : null);

    private static int? BancoDe(DescuentoTarjetaCarga d, IResolutorCodigos r) => string.IsNullOrWhiteSpace(d.BancoCodigo) ? null : r.Banco(d.BancoCodigo);

    private static T Activar<T>(T entidad, bool activa, Action<T> activar, Action<T> desactivar)
    {
        if (activa) activar(entidad); else desactivar(entidad);
        return entidad;
    }
}
