using CgPos.Contratos.Catalogo;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Aplicacion.Organizacion;
using CgPos.Pos.Aplicacion.Seguridad;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Pos.Infraestructura.Catalogo;

internal sealed class ConsultaArticulos(
    ContextoDatosPos contexto,
    IParametros parametros,
    IContextoCaja contextoCaja,
    TimeProvider reloj) : IConsultaArticulos
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

        var porInterno = await contexto.Articulos.Where(a => a.Codigo == leido).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(cancelacion);
        if (porInterno is { } idInterno)
            return await ArmarDatosVentaAsync(idInterno, leido, OrigenCodigoLeido.CodigoInterno, null, cancelacion);

        var formato = await ObtenerFormatoBalanzaAsync(cancelacion);
        if (formato is null || !InterpreteCodigoBalanza.TryInterpretar(leido, formato, out var lectura))
            return null;

        var idBalanza = await contexto.Articulos
            .Where(a => a.Codigo == lectura!.CodigoArticulo || a.Codigos.Any(c => c.Codigo == lectura.CodigoArticulo))
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancelacion);

        return idBalanza is { } id
            ? await ArmarDatosVentaAsync(id, leido, OrigenCodigoLeido.EtiquetaBalanza, lectura, cancelacion)
            : null;
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> BuscarAsync(string? texto, Guid? familiaId = null, int maximo = 50, CancellationToken cancelacion = default)
    {
        maximo = Math.Clamp(maximo, 1, 200);
        var consulta = Vendibles();

        if (familiaId is { } familia)
            consulta = consulta.Where(a => a.FamiliaId == familia);

        foreach (var palabra in (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
        {
            var termino = palabra;
            consulta = consulta.Where(a =>
                a.Descripcion.Contains(termino)
                || a.Codigo.StartsWith(termino)
                || (a.Referencia != null && a.Referencia.Contains(termino))
                || a.Codigos.Any(c => c.Codigo.StartsWith(termino)));
        }

        var articulos = await consulta.OrderBy(a => a.Descripcion).Take(maximo).ToListAsync(cancelacion);
        return await ArmarResumenesAsync(articulos, cancelacion);
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> ListarCatalogoAsync(Guid? familiaId = null, CancellationToken cancelacion = default)
    {
        var consulta = Vendibles().Where(a => a.MostrarEnCatalogo);
        if (familiaId is { } familia)
            consulta = consulta.Where(a => a.FamiliaId == familia);

        var articulos = await consulta.OrderBy(a => a.Descripcion).Take(500).ToListAsync(cancelacion);
        return await ArmarResumenesAsync(articulos, cancelacion);
    }

    public async Task<IReadOnlyList<DatosArticuloResumen>> ListarNoCodificadosAsync(Guid? familiaId = null, CancellationToken cancelacion = default)
    {
        var familiasNoCodificadas = contexto.Familias.Where(f => f.Activa && f.EsNoCodificada).Select(f => f.Id);
        var consulta = Vendibles().Where(a => familiasNoCodificadas.Contains(a.FamiliaId));
        if (familiaId is { } familia)
            consulta = consulta.Where(a => a.FamiliaId == familia);

        var articulos = await consulta.OrderBy(a => a.Descripcion).ToListAsync(cancelacion);
        return await ArmarResumenesAsync(articulos, cancelacion);
    }

    public async Task<IReadOnlyList<DatosPrecioHistorico>> ObtenerHistorialPreciosAsync(Guid articuloId, CancellationToken cancelacion = default) =>
        await contexto.PreciosArticulo
            .AsNoTracking()
            .Where(p => p.ArticuloId == articuloId)
            .OrderByDescending(p => p.VigenteDesde)
            .ThenByDescending(p => p.RegistradoEn)
            .Select(p => new DatosPrecioHistorico(p.Lista, p.Precio, p.VigenteDesde, p.RegistradoEn, p.Origen, p.UsuarioNombre))
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<DatosFamilia>> ListarFamiliasAsync(CancellationToken cancelacion = default) =>
        await contexto.Familias
            .AsNoTracking()
            .Where(f => f.Activa)
            .OrderBy(f => f.Nombre)
            .Select(f => new DatosFamilia(f.Id, f.Codigo, f.Nombre, f.EsNoCodificada))
            .ToListAsync(cancelacion);

    private IQueryable<Articulo> Vendibles() => contexto.Articulos.AsNoTracking().Where(a => a.Activo && a.VentaEnPos);

    private async Task<DatosArticuloVenta?> ArmarDatosVentaAsync(Guid articuloId, string codigoLeido, OrigenCodigoLeido origen, LecturaBalanza? lectura, CancellationToken cancelacion)
    {
        var datos = await (
                from articulo in Vendibles()
                join familia in contexto.Familias on articulo.FamiliaId equals familia.Id
                join unidad in contexto.UnidadesMedida on articulo.UnidadMedidaId equals unidad.Id
                join impuesto in contexto.Impuestos on articulo.ImpuestoId equals impuesto.Id
                where articulo.Id == articuloId
                select new { articulo, familia, unidad, impuesto })
            .SingleOrDefaultAsync(cancelacion);

        if (datos is null)
            return null;

        var precios = await PreciosVigentesAsync([articuloId], cancelacion);
        var vigentes = precios.GetValueOrDefault(articuloId) ?? new PreciosVigentes(null, null);

        return new DatosArticuloVenta(
            datos.articulo.Id,
            datos.articulo.Codigo,
            datos.articulo.Descripcion,
            datos.articulo.Referencia,
            codigoLeido,
            origen,
            datos.articulo.Tipo,
            datos.familia.Id,
            datos.familia.Nombre,
            datos.familia.PermiteDescuentoManual,
            datos.unidad.Codigo,
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
            datos.articulo.Tara,
            datos.articulo.EsServicio);
    }

    private async Task<IReadOnlyList<DatosArticuloResumen>> ArmarResumenesAsync(List<Articulo> articulos, CancellationToken cancelacion)
    {
        if (articulos.Count == 0)
            return [];

        var ids = articulos.Select(a => a.Id).ToList();
        var familiasIds = articulos.Select(a => a.FamiliaId).Distinct().ToList();
        var unidadesIds = articulos.Select(a => a.UnidadMedidaId).Distinct().ToList();

        var familias = await contexto.Familias.Where(f => familiasIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nombre, cancelacion);
        var unidades = await contexto.UnidadesMedida.Where(u => unidadesIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Codigo, cancelacion);
        var precios = await PreciosVigentesAsync(ids, cancelacion);

        return articulos
            .Select(a =>
            {
                var vigentes = precios.GetValueOrDefault(a.Id);
                return new DatosArticuloResumen(a.Id, a.Codigo, a.Descripcion, a.Referencia, familias[a.FamiliaId], unidades[a.UnidadMedidaId], vigentes?.Detalle, vigentes?.Mayor, a.RutaImagen);
            })
            .ToList();
    }

    private async Task<Dictionary<Guid, PreciosVigentes>> PreciosVigentesAsync(List<Guid> articulosIds, CancellationToken cancelacion)
    {
        var ahora = reloj.GetUtcNow();
        var historial = await contexto.PreciosArticulo
            .AsNoTracking()
            .Where(p => articulosIds.Contains(p.ArticuloId) && p.VigenteDesde <= ahora)
            .ToListAsync(cancelacion);

        return historial
            .GroupBy(p => p.ArticuloId)
            .ToDictionary(g => g.Key, g => PreciosVigentes.Resolver(g, ahora));
    }

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
    public async Task<DatosConsultaDocumento> ConsultarAsync(string documento, CancellationToken cancelacion = default)
    {
        var validacion = DocumentoIdentidad.Validar(documento);
        var normalizado = validacion.Documento;

        var contribuyente = validacion.FormatoValido
            ? await contexto.ContribuyentesDgii.AsNoTracking().SingleOrDefaultAsync(c => c.Documento == normalizado, cancelacion)
            : null;

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
            contribuyente is not null,
            contribuyente?.RazonSocial,
            contribuyente?.NombreComercial,
            contribuyente?.Estado,
            contribuyente?.EstaActivo,
            datosCliente);
    }
}

internal sealed class ConsultaCatalogoCobro(ContextoDatosPos contexto, TimeProvider reloj) : IConsultaCatalogoCobro
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
        var ahora = reloj.GetUtcNow();
        var tasas = (await contexto.TasasCambio.AsNoTracking().Where(t => t.VigenteDesde <= ahora).ToListAsync(cancelacion))
            .GroupBy(t => t.Moneda)
            .Select(grupo => grupo.OrderByDescending(t => t.VigenteDesde).First())
            .OrderBy(t => t.Moneda)
            .Select(t => new DatosTasaCambio(t.Moneda, t.Tasa, t.VigenteDesde))
            .ToList();

        return new DatosCatalogoCobro(formas, bancos, tipos, denominaciones, tasas);
    }
}

internal sealed class ServicioPrecios(ContextoDatosPos contexto, IAuditoria auditoria, TimeProvider reloj) : IServicioPrecios
{
    public async Task RegistrarCambioAsync(Guid articuloId, ListaPrecio lista, decimal precio, DateTimeOffset vigenteDesde, string origen,
        SesionUsuario? usuario = null, CancellationToken cancelacion = default)
    {
        if (!await contexto.Articulos.AnyAsync(a => a.Id == articuloId, cancelacion))
            throw new ArgumentException("El artículo no existe.", nameof(articuloId));

        var ahora = reloj.GetUtcNow();
        var registrado = await RegistroPrecios.RegistrarSiCambiaAsync(contexto, articuloId, lista, precio, vigenteDesde, ahora, origen, usuario?.UsuarioId, usuario?.Nombre, cancelacion);
        if (!registrado)
            return;

        auditoria.Registrar(new EntradaAuditoria("Catalogo.CambioPrecio", "Articulo", articuloId.ToString(),
            Detalle: new { Lista = lista.ToString(), Precio = precio, VigenteDesde = vigenteDesde, Origen = origen },
            Usuario: usuario is null ? null : new UsuarioAuditoria(usuario.UsuarioId, usuario.Nombre)));
        await contexto.SaveChangesAsync(cancelacion);
    }
}
