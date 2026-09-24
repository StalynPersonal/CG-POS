using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using CgPos.Pos.Infraestructura.Persistencia.Configuraciones;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Pos.Infraestructura.Catalogo;

internal sealed class ConsultaArticulos(
    ContextoDatosPos contexto,
    IParametros parametros,
    IContextoCaja contextoCaja) : IConsultaArticulos
{
    public async Task<DatosArticuloVenta?> BuscarPorCodigoAsync(string codigo, CancellationToken cancelacion = default)
    {
        var leido = codigo?.Trim();
        if (string.IsNullOrEmpty(leido))
            return null;

        var porCodigo = await contexto.Set<CodigoArticulo>()
            .Where(c => c.Codigo == leido)
            .Select(c => new { c.ArticuloId, c.Tipo })
            .FirstOrDefaultAsync(cancelacion);
        if (porCodigo is not null)
        {
            var origen = porCodigo.Tipo == TipoCodigoArticulo.Barras ? OrigenCodigoLeido.CodigoBarras : OrigenCodigoLeido.CodigoProveedor;
            return await ArmarDatosVentaAsync(porCodigo.ArticuloId, leido, origen, null, cancelacion);
        }

        var porInterno = await contexto.Articulos.Where(a => a.Codigo == leido).Select(a => (int?)a.Id).FirstOrDefaultAsync(cancelacion);
        if (porInterno is { } idInterno)
            return await ArmarDatosVentaAsync(idInterno, leido, OrigenCodigoLeido.CodigoInterno, null, cancelacion);

        var formato = await ObtenerFormatoBalanzaAsync(cancelacion);
        if (formato is null || !InterpreteCodigoBalanza.TryInterpretar(leido, formato, out var lectura))
            return null;

        var idBalanza = await contexto.Articulos
            .Where(a => a.Codigo == lectura!.CodigoArticulo || a.Codigos.Any(c => c.Codigo == lectura.CodigoArticulo))
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync(cancelacion);

        return idBalanza is { } id
            ? await ArmarDatosVentaAsync(id, leido, OrigenCodigoLeido.EtiquetaBalanza, lectura, cancelacion)
            : null;
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> BuscarAsync(string? texto, int? departamentoId = null, int maximo = 50, CancellationToken cancelacion = default)
    {
        // Hasta mil: la búsqueda de artículos trae todo lo que coincide, pero una o dos letras entre miles de artículos no
        // deben trabar la caja.
        maximo = Math.Clamp(maximo, 1, 1000);
        var consulta = Vendibles();

        if (departamentoId is { } departamento)
            consulta = consulta.Where(a => a.DepartamentoId == departamento);

        foreach (var palabra in (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
        {
            var termino = palabra;
            consulta = consulta.Where(a =>
                a.Descripcion.Contains(termino)
                || a.Codigo.StartsWith(termino)
                || (a.Referencia != null && a.Referencia.Contains(termino))
                || a.Codigos.Any(c => c.Codigo.StartsWith(termino)));
        }

        return await ArmarResumenesAsync(consulta.OrderBy(a => a.Descripcion).Take(maximo), cancelacion);
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> ListarCatalogoAsync(int? departamentoId = null, CancellationToken cancelacion = default)
    {
        var consulta = Vendibles().Where(a => a.MostrarEnCatalogo);
        if (departamentoId is { } departamento)
            consulta = consulta.Where(a => a.DepartamentoId == departamento);

        return await ArmarResumenesAsync(consulta.OrderBy(a => a.Descripcion).Take(500), cancelacion);
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> ListarNoCodificadosAsync(int? departamentoId = null, CancellationToken cancelacion = default)
    {
        var departamentosNoCodificadas = contexto.Departamentos.Where(f => f.Activa && f.EsNoCodificada).Select(f => f.Id);
        var consulta = Vendibles().Where(a => departamentosNoCodificadas.Contains(a.DepartamentoId));
        if (departamentoId is { } departamento)
            consulta = consulta.Where(a => a.DepartamentoId == departamento);

        return await ArmarResumenesAsync(consulta.OrderBy(a => a.Descripcion), cancelacion);
    }

    public async Task<IReadOnlyList<DatosDepartamento>> ListarDepartamentosAsync(CancellationToken cancelacion = default) =>
        await contexto.Departamentos
            .AsNoTracking()
            .Where(f => f.Activa)
            .OrderBy(f => f.Nombre)
            .Select(f => new DatosDepartamento(f.Id, f.Codigo, f.Nombre, f.EsNoCodificada))
            .ToListAsync(cancelacion);

    private IQueryable<Articulo> Vendibles() => contexto.Articulos.AsNoTracking().Where(a => a.Activo && a.VentaEnPos);

    private async Task<DatosArticuloVenta?> ArmarDatosVentaAsync(int articuloId, string codigoLeido, OrigenCodigoLeido origen, LecturaBalanza? lectura, CancellationToken cancelacion)
    {
        var datos = await (
                from articulo in Vendibles()
                join departamento in contexto.Departamentos on articulo.DepartamentoId equals departamento.Id
                join unidad in contexto.UnidadesMedida on articulo.UnidadMedidaId equals unidad.Id
                join impuesto in contexto.Impuestos on articulo.ImpuestoId equals impuesto.Id
                where articulo.Id == articuloId
                select new
                {
                    articulo,
                    departamento,
                    unidad,
                    impuesto,
                    PrecioDetalle = EF.Property<decimal>(articulo, ArticuloConfiguracion.PrecioDetalle),
                    PrecioMayor = EF.Property<decimal?>(articulo, ArticuloConfiguracion.PrecioMayor),
                })
            .SingleOrDefaultAsync(cancelacion);

        if (datos is null)
            return null;

        var vigentes = Precios(datos.PrecioDetalle, datos.PrecioMayor);

        return new DatosArticuloVenta(
            datos.articulo.Id,
            datos.articulo.Codigo,
            datos.articulo.Descripcion,
            datos.articulo.Referencia,
            codigoLeido,
            origen,
            datos.articulo.Tipo,
            datos.departamento.Id,
            datos.departamento.Nombre,
            datos.departamento.PermiteDescuentoManual,
            datos.unidad.Abreviatura,
            datos.unidad.PermiteDecimales,
            datos.unidad.Decimales,
            datos.impuesto.Id,
            datos.impuesto.Codigo,
            datos.impuesto.Porcentaje,
            datos.impuesto.IndicadorFacturacion,
            vigentes.Detalle,
            vigentes.Mayor,
            datos.articulo.CantidadMinimaMayor,
            datos.articulo.PrecioMinimo,
            lectura is { Tipo: TipoValorBalanza.Peso } ? lectura.Valor : null,
            lectura is { Tipo: TipoValorBalanza.Precio } ? lectura.Valor : null,
            datos.articulo.RutaImagen,
            datos.articulo.PesoEmpaque,
            datos.articulo.EsServicio,
            datos.articulo.CategoriaId,
            datos.articulo.CategoriaId is { } categoriaId
                ? await contexto.Categorias.Where(c => c.Id == categoriaId).Select(c => c.Nombre).FirstOrDefaultAsync(cancelacion)
                : null,
            datos.articulo.MarcaId,
            datos.articulo.MarcaId is { } marcaId
                ? await contexto.Marcas.Where(m => m.Id == marcaId).Select(m => m.Nombre).FirstOrDefaultAsync(cancelacion)
                : null);
    }

    /// <summary>
    /// El precio está en el propio artículo, como en el Central, y sale en la misma consulta: el listado no necesita una
    /// segunda vuelta a la base para saber a cuánto está cada uno.
    /// </summary>
    private async Task<IReadOnlyList<DatosArticuloResumen>> ArmarResumenesAsync(IQueryable<Articulo> consulta, CancellationToken cancelacion)
    {
        // Solo las columnas que se muestran: el listado no necesita el artículo entero y así la base mueve menos.
        var articulos = await consulta
            .Select(a => new
            {
                a.Id,
                a.Codigo,
                a.Descripcion,
                a.Referencia,
                a.DepartamentoId,
                a.UnidadMedidaId,
                a.CategoriaId,
                a.RutaImagen,
                PrecioDetalle = EF.Property<decimal>(a, ArticuloConfiguracion.PrecioDetalle),
                PrecioMayor = EF.Property<decimal?>(a, ArticuloConfiguracion.PrecioMayor),
            })
            .ToListAsync(cancelacion);
        if (articulos.Count == 0)
            return [];

        var departamentosIds = articulos.Select(a => a.DepartamentoId).Distinct().ToList();
        var unidadesIds = articulos.Select(a => a.UnidadMedidaId).Distinct().ToList();
        var categoriasIds = articulos.Select(a => a.CategoriaId).OfType<int>().Distinct().ToList();

        var departamentos = await contexto.Departamentos.Where(f => departamentosIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nombre, cancelacion);
        var unidades = await contexto.UnidadesMedida.Where(u => unidadesIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Abreviatura, cancelacion);
        var categorias = await contexto.Categorias.Where(c => categoriasIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre, cancelacion);

        return articulos
            .Select(a =>
            {
                var vigentes = Precios(a.PrecioDetalle, a.PrecioMayor);
                return new DatosArticuloResumen(a.Id, a.Codigo, a.Descripcion, a.Referencia, departamentos[a.DepartamentoId],
                    unidades[a.UnidadMedidaId], vigentes.Detalle, vigentes.Mayor, a.RutaImagen,
                    a.CategoriaId is { } categoria ? categorias.GetValueOrDefault(categoria) : null);
            })
            .ToList();
    }

    /// <summary>El artículo sin precio (el descontinuado que llega en cero) no tiene precio, no un precio de cero.</summary>
    private static PreciosVigentes Precios(decimal detalle, decimal? mayor) =>
        new(detalle > 0 ? detalle : null, mayor > 0 ? mayor : null);

    /// <summary>Formato de etiquetas de balanza configurado; nulo si la caja no tiene etiquetas de balanza configuradas.</summary>
    private async Task<FormatoCodigoBalanza?> ObtenerFormatoBalanzaAsync(CancellationToken cancelacion)
    {
        var cajaId = contextoCaja.CajaId;
        var prefijoPeso = await parametros.ObtenerAsync(ClavesParametros.BalanzaPrefijoPeso, cajaId, cancelacion);
        var prefijoPrecio = await parametros.ObtenerAsync(ClavesParametros.BalanzaPrefijoPrecio, cajaId, cancelacion);
        if (string.IsNullOrEmpty(prefijoPeso) && string.IsNullOrEmpty(prefijoPrecio))
            return null;

        return new FormatoCodigoBalanza(
            prefijoPeso,
            prefijoPrecio,
            await parametros.ObtenerEnteroAsync(ClavesParametros.BalanzaDigitosCodigoArticulo, cajaId, cancelacion),
            await parametros.ObtenerEnteroAsync(ClavesParametros.BalanzaDigitosValor, cajaId, cancelacion),
            await parametros.ObtenerEnteroAsync(ClavesParametros.BalanzaDecimalesPeso, cajaId, cancelacion),
            await parametros.ObtenerEnteroAsync(ClavesParametros.BalanzaDecimalesPrecio, cajaId, cancelacion));
    }
}

internal sealed class ConsultaDocumentos(ContextoDatosPos contexto) : IConsultaDocumentos
{
    /// <summary>
    /// Busca por documento o por nombre. Por nombre se intenta primero «empieza por», que usa el índice y responde al
    /// instante; solo si eso no da nada se recorre buscando el texto en medio, que en el padrón entero cuesta medio
    /// segundo. Así lo normal es inmediato y lo raro igual aparece.
    /// </summary>
    public async Task<IReadOnlyList<DatosClienteEncontrado>> BuscarAsync(string? texto, int maximo = 50, CancellationToken cancelacion = default)
    {
        var buscado = (texto ?? string.Empty).Trim();
        if (buscado.Length < 3)
            return [];

        maximo = Math.Clamp(maximo, 1, 200);
        var activos = contexto.Clientes.AsNoTracking().Where(c => c.Activo);

        if (buscado.All(char.IsAsciiDigit))
            return await ResumenAsync(activos.Where(c => c.Documento.StartsWith(buscado)).OrderBy(c => c.Nombre), maximo, cancelacion);

        var empiezan = await ResumenAsync(activos.Where(c => c.Nombre.StartsWith(buscado)).OrderBy(c => c.Nombre), maximo, cancelacion);
        return empiezan.Count > 0
            ? empiezan
            : await ResumenAsync(activos.Where(c => c.Nombre.Contains(buscado)).OrderBy(c => c.Nombre), maximo, cancelacion);
    }

    private static async Task<IReadOnlyList<DatosClienteEncontrado>> ResumenAsync(IQueryable<CgPos.Dominio.Clientes.Cliente> consulta, int maximo,
        CancellationToken cancelacion) =>
        await consulta.Take(maximo)
            .Select(c => new DatosClienteEncontrado(c.TipoDocumento, c.Documento, c.Nombre, c.TipoComprobantePredeterminado, c.ListaPrecioPredeterminada, c.Telefono))
            .ToListAsync(cancelacion);

    public async Task<DatosConsultaDocumento> ConsultarAsync(string documento, CancellationToken cancelacion = default)
    {
        var validacion = DocumentoIdentidad.Validar(documento);
        var normalizado = validacion.Documento;

        var cliente = normalizado.Length == 0
            ? null
            : await contexto.Clientes.AsNoTracking().Include(c => c.Direcciones)
                .Where(c => c.Documento == normalizado && c.Activo)
                .FirstOrDefaultAsync(cancelacion);

        var datosCliente = cliente is null
            ? null
            : new DatosClienteResumen(
                cliente.Id,
                cliente.TipoDocumento,
                cliente.Documento,
                cliente.Nombre,
                cliente.TipoComprobantePredeterminado,
                cliente.ExoneradoItbis,
                cliente.AplicaRetencion,
                cliente.ListaPrecioPredeterminada,
                cliente.Telefono,
                cliente.Correo,
                cliente.Direcciones
                    .OrderByDescending(d => d.EsPrincipal)
                    .ThenBy(d => d.Alias)
                    .Select(d => new DatosDireccionCliente(d.Id, d.Alias, d.Direccion, d.Sector, d.Ciudad, d.Referencia, d.Telefono, d.EsPrincipal))
                    .ToList());

        return new DatosConsultaDocumento(
            normalizado,
            validacion.Tipo ?? cliente?.TipoDocumento,
            validacion.FormatoValido || cliente is not null,
            validacion.DigitoVerificadorValido,
            datosCliente);
    }
}

internal sealed class ConsultaCatalogoCobro(ContextoDatosPos contexto, TimeProvider reloj, CgPos.Pos.Aplicacion.Perifericos.ITerminalPago terminal) : IConsultaCatalogoCobro
{
    public async Task<DatosCatalogoCobro> ObtenerAsync(CancellationToken cancelacion = default)
    {
        var formas = await contexto.FormasPago.AsNoTracking().Where(f => f.Activa).OrderBy(f => f.Orden).ThenBy(f => f.Nombre)
            .Select(f => new DatosFormaPago(f.Id, f.Codigo, f.Nombre, f.Tipo, f.Moneda, f.Orden, f.AbreGaveta, f.PermiteDevuelta, f.RequiereReferencia, f.RequiereBanco, f.PermiteComprobanteFiscal))
            .ToListAsync(cancelacion);
        var bancos = await contexto.Bancos.AsNoTracking().Where(b => b.Activo).OrderBy(b => b.Nombre)
            .Select(b => new DatosBanco(b.Id, b.Codigo, b.Nombre, b.RutaLogo))
            .ToListAsync(cancelacion);
        var tipos = await contexto.TiposTarjeta.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.Nombre)
            .Select(t => new DatosTipoTarjeta(t.Id, t.Codigo, t.Nombre))
            .ToListAsync(cancelacion);
        var denominaciones = await contexto.Denominaciones.AsNoTracking().Where(d => d.Activa).OrderBy(d => d.Moneda).ThenByDescending(d => d.Valor)
            .Select(d => new DatosDenominacion(d.Id, d.Moneda, d.Valor, d.Tipo))
            .ToListAsync(cancelacion);

        // La tasa vigente más reciente de cada moneda.
        var ahora = reloj.Ahora();
        var tasas = (await contexto.TasasCambio.AsNoTracking().Where(t => t.VigenteDesde <= ahora).ToListAsync(cancelacion))
            .GroupBy(t => t.Moneda)
            .Select(grupo => grupo.OrderByDescending(t => t.VigenteDesde).First())
            .OrderBy(t => t.Moneda)
            .Select(t => new DatosTasaCambio(t.Moneda, t.Tasa, t.VigenteDesde))
            .ToList();

        return new DatosCatalogoCobro(formas, bancos, tipos, denominaciones, tasas, terminal.Integrado);
    }
}
