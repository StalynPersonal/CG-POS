using System.Text.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Fiscal;
using CgPos.Dominio.Pagos;
using CgPos.Dominio.Promociones;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CgPos.Pos.Infraestructura.Catalogo;

internal sealed class ServicioCargaMaestros(
    ContextoDatosPos contexto,
    IAuditoria auditoria,
    TimeProvider reloj,
    ILogger<ServicioCargaMaestros> registro) : ICargaMaestros
{
    private int _creados;
    private int _actualizados;
    private int _precios;

    public async Task<ResultadoCargaMaestros> AplicarDesdeArchivoAsync(string ruta, CancellationToken cancelacion = default)
    {
        if (!File.Exists(ruta))
            throw new FileNotFoundException($"No se encontró el archivo de maestros: {ruta}", ruta);

        PaqueteMaestros? paquete;
        try
        {
            await using var archivo = File.OpenRead(ruta);
            paquete = await JsonSerializer.DeserializeAsync<PaqueteMaestros>(archivo, OpcionesJson.Predeterminadas, cancelacion);
        }
        catch (JsonException excepcion)
        {
            throw new CargaMaestrosInvalidaExcepcion([$"JSON inválido en {Path.GetFileName(ruta)}: {excepcion.Message}"]);
        }

        return await AplicarAsync(paquete ?? throw new CargaMaestrosInvalidaExcepcion(["El archivo está vacío."]), $"Archivo {Path.GetFileName(ruta)}", cancelacion);
    }

    public async Task<ResultadoCargaMaestros> AplicarAsync(PaqueteMaestros paquete, string origen, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(paquete);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);
        _creados = 0;
        _actualizados = 0;
        _precios = 0;

        var departamentos = paquete.Departamentos ?? [];
        var unidades = paquete.UnidadesMedida ?? [];
        var impuestos = paquete.Impuestos ?? [];
        var articulos = paquete.Articulos ?? [];
        var clientes = paquete.Clientes ?? [];
        var formasPago = paquete.FormasPago ?? [];
        var bancos = paquete.Bancos ?? [];
        var tiposTarjeta = paquete.TiposTarjeta ?? [];
        var denominaciones = paquete.Denominaciones ?? [];
        var promociones = paquete.Promociones ?? [];
        var motivosDescuento = paquete.MotivosDescuento ?? [];
        var topesDescuento = paquete.TopesDescuento ?? [];
        var tasasCambio = paquete.TasasCambio ?? [];
        var secuenciasEcf = paquete.SecuenciasEcf ?? [];
        var motivosDevolucion = paquete.MotivosDevolucion ?? [];
        var monedas = paquete.Monedas ?? [];
        var nivelesFidelidad = paquete.NivelesFidelidad ?? [];
        var reglasAcumulacion = paquete.ReglasAcumulacion ?? [];
        var miembrosFidelidad = paquete.MiembrosFidelidad ?? [];
        var almacenes = paquete.Almacenes ?? [];
        var descuentosTarjeta = paquete.DescuentosTarjeta ?? [];
        var categorias = paquete.Categorias ?? [];
        var marcas = paquete.Marcas ?? [];

        await ValidarAsync(departamentos, unidades, impuestos, articulos, categorias, marcas, cancelacion);
        if (promociones.GroupBy(p => p.Codigo.Trim().ToUpperInvariant()).FirstOrDefault(g => g.Count() > 1) is { } repetida)
            throw new CargaMaestrosInvalidaExcepcion([$"Código de promoción repetido en el paquete: {repetida.Key}."]);

        try
        {
            var ahora = reloj.GetUtcNow();

            // Las monedas primero: formas de pago, denominaciones y tasas las referencian.
            foreach (var dato in monedas)
                await AplicarMonedaAsync(dato, cancelacion);
            foreach (var dato in departamentos)
                await AplicarDepartamentoAsync(dato, cancelacion);
            foreach (var dato in categorias)
                await AplicarCategoriaAsync(dato, cancelacion);
            foreach (var dato in marcas)
                await AplicarMarcaAsync(dato, cancelacion);
            foreach (var dato in unidades)
                await AplicarUnidadAsync(dato, cancelacion);
            foreach (var dato in impuestos)
                await AplicarImpuestoAsync(dato, cancelacion);
            foreach (var dato in articulos)
                await AplicarArticuloAsync(dato, origen, ahora, cancelacion);
            foreach (var dato in clientes)
                await AplicarClienteAsync(dato, cancelacion);
            foreach (var dato in formasPago)
                await AplicarFormaPagoAsync(dato, cancelacion);
            foreach (var dato in bancos)
                await AplicarBancoAsync(dato, cancelacion);
            foreach (var dato in tiposTarjeta)
                await AplicarTipoTarjetaAsync(dato, cancelacion);
            foreach (var dato in denominaciones)
                await AplicarDenominacionAsync(dato, cancelacion);
            foreach (var dato in promociones)
                await AplicarPromocionAsync(dato, cancelacion);
            foreach (var dato in motivosDescuento)
                await AplicarMotivoDescuentoAsync(dato, cancelacion);
            foreach (var dato in topesDescuento)
                await AplicarTopeDescuentoAsync(dato, cancelacion);
            foreach (var dato in tasasCambio)
                await AplicarTasaCambioAsync(dato, cancelacion);
            foreach (var dato in secuenciasEcf)
                await AplicarSecuenciaEcfAsync(dato, cancelacion);
            foreach (var dato in motivosDevolucion)
                await AplicarMotivoDevolucionAsync(dato, cancelacion);
            foreach (var dato in nivelesFidelidad)
                await AplicarNivelFidelidadAsync(dato, cancelacion);
            foreach (var dato in reglasAcumulacion)
                await AplicarReglaAcumulacionAsync(dato, cancelacion);
            foreach (var dato in miembrosFidelidad)
                await AplicarMiembroFidelidadAsync(dato, cancelacion);
            foreach (var dato in almacenes)
                await AplicarAlmacenAsync(dato, cancelacion);
            foreach (var dato in descuentosTarjeta)
                await AplicarDescuentoTarjetaAsync(dato, cancelacion);

            var resultado = new ResultadoCargaMaestros(_creados, _actualizados, _precios);
            auditoria.Registrar(new EntradaAuditoria("Catalogo.CargaMaestros", "Maestros", Detalle: new { Origen = origen, resultado.Creados, resultado.Actualizados, resultado.PreciosRegistrados }));
            await contexto.SaveChangesAsync(cancelacion);

            registro.LogInformation("Maestros aplicados ({Origen}): {Creados} creados, {Actualizados} actualizados, {Precios} precios registrados",
                origen, resultado.Creados, resultado.Actualizados, resultado.PreciosRegistrados);
            return resultado;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            contexto.ChangeTracker.Clear();
            var detalle = excepcion is DbUpdateException { InnerException: { } interna } ? interna.Message : excepcion.Message;
            throw new CargaMaestrosInvalidaExcepcion([detalle]);
        }
    }

    private async Task ValidarAsync(
        IReadOnlyList<DepartamentoCarga> departamentos,
        IReadOnlyList<UnidadMedidaCarga> unidades,
        IReadOnlyList<ImpuestoCarga> impuestos,
        IReadOnlyList<ArticuloCarga> articulos,
        IReadOnlyList<CategoriaCarga> categorias,
        IReadOnlyList<MarcaCarga> marcas,
        CancellationToken cancelacion)
    {
        var errores = new List<string>();

        Repetidos(articulos.Select(a => a.Id), "Id de artículo", errores);
        Repetidos(articulos.Select(a => a.Codigo.Trim()), "Código de artículo", errores);

        var idsDepartamentos = departamentos.Select(f => f.Id).ToHashSet();
        idsDepartamentos.UnionWith(await contexto.Departamentos.Select(f => f.Id).ToListAsync(cancelacion));
        var idsUnidades = unidades.Select(u => u.Id).ToHashSet();
        idsUnidades.UnionWith(await contexto.UnidadesMedida.Select(u => u.Id).ToListAsync(cancelacion));
        var idsImpuestos = impuestos.Select(i => i.Id).ToHashSet();
        idsImpuestos.UnionWith(await contexto.Impuestos.Select(i => i.Id).ToListAsync(cancelacion));
        var idsMarcas = marcas.Select(m => m.Id).ToHashSet();
        idsMarcas.UnionWith(await contexto.Marcas.Select(m => m.Id).ToListAsync(cancelacion));

        // Departamento de cada categoría: la del paquete manda sobre la guardada.
        var departamentoDeCategoria = await contexto.Categorias.ToDictionaryAsync(c => c.Id, c => c.DepartamentoId, cancelacion);
        foreach (var categoria in categorias)
        {
            departamentoDeCategoria[categoria.Id] = categoria.DepartamentoId;
            if (!idsDepartamentos.Contains(categoria.DepartamentoId))
                errores.Add($"La categoría '{categoria.Codigo}' referencia un departamento inexistente ({categoria.DepartamentoId}).");
        }

        var codigosPorArticulo = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var articulo in articulos)
        {
            var etiqueta = $"El artículo '{articulo.Codigo}'";
            if (!idsDepartamentos.Contains(articulo.DepartamentoId))
                errores.Add($"{etiqueta} referencia un departamento inexistente ({articulo.DepartamentoId}).");
            if (!idsUnidades.Contains(articulo.UnidadMedidaId))
                errores.Add($"{etiqueta} referencia una unidad de medida inexistente ({articulo.UnidadMedidaId}).");
            if (!idsImpuestos.Contains(articulo.ImpuestoId))
                errores.Add($"{etiqueta} referencia un impuesto inexistente ({articulo.ImpuestoId}).");
            if (articulo.CategoriaId is { } categoriaId)
            {
                if (!departamentoDeCategoria.TryGetValue(categoriaId, out var departamentoCategoria))
                    errores.Add($"{etiqueta} referencia una categoría inexistente ({categoriaId}).");
                else if (departamentoCategoria != articulo.DepartamentoId)
                    errores.Add($"{etiqueta} tiene una categoría que no es de su departamento.");
            }
            if (articulo.MarcaId is { } marcaId && !idsMarcas.Contains(marcaId))
                errores.Add($"{etiqueta} referencia una marca inexistente ({marcaId}).");
            if (articulo.PrecioDetalle <= 0)
                errores.Add($"{etiqueta} debe tener precio detalle mayor que cero.");
            if (articulo.PrecioMayor <= 0)
                errores.Add($"{etiqueta} tiene un precio por mayor inválido.");

            foreach (var codigo in (articulo.CodigosBarras ?? []).Concat(articulo.CodigosProveedor ?? []).Select(c => c.Trim()))
            {
                if (codigosPorArticulo.TryGetValue(codigo, out var otro) && otro != articulo.Codigo)
                    errores.Add($"El código '{codigo}' está en los artículos '{otro}' y '{articulo.Codigo}'.");
                codigosPorArticulo[codigo] = articulo.Codigo;
            }
        }

        if (codigosPorArticulo.Count > 0)
        {
            var codigos = codigosPorArticulo.Keys.ToList();
            var idsPorCodigo = articulos.ToDictionary(a => a.Codigo.Trim(), a => a.Id);
            var enUso = await contexto.Set<CodigoArticulo>()
                .Where(c => codigos.Contains(c.Codigo))
                .Select(c => new { c.Codigo, c.ArticuloId })
                .ToListAsync(cancelacion);

            foreach (var usado in enUso.Where(u => idsPorCodigo[codigosPorArticulo[u.Codigo]] != u.ArticuloId))
                errores.Add($"El código '{usado.Codigo}' ya pertenece a otro artículo en la caja.");
        }

        if (errores.Count > 0)
            throw new CargaMaestrosInvalidaExcepcion(errores);
    }

    private static void Repetidos<T>(IEnumerable<T> valores, string campo, List<string> errores)
    {
        foreach (var repetido in valores.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key))
            errores.Add($"{campo} repetido en el paquete: {repetido}.");
    }

    private async Task AplicarDepartamentoAsync(DepartamentoCarga dato, CancellationToken cancelacion)
    {
        var departamento = await contexto.Departamentos.SingleOrDefaultAsync(f => f.Id == dato.Id, cancelacion);
        if (departamento is null)
        {
            departamento = Departamento.Crear(dato.Codigo, dato.Nombre, dato.PermiteDescuentoManual, dato.EsNoCodificada, dato.Id);
            contexto.Departamentos.Add(departamento);
            _creados++;
        }
        else
        {
            departamento.Actualizar(dato.Nombre, dato.PermiteDescuentoManual, dato.EsNoCodificada);
            _actualizados++;
        }

        if (dato.Activa) departamento.Activar(); else departamento.Desactivar();
    }

    private async Task AplicarCategoriaAsync(CategoriaCarga dato, CancellationToken cancelacion)
    {
        var categoria = await contexto.Categorias.SingleOrDefaultAsync(c => c.Id == dato.Id, cancelacion);
        if (categoria is null)
        {
            categoria = Categoria.Crear(dato.Codigo, dato.Nombre, dato.DepartamentoId, dato.Id);
            contexto.Categorias.Add(categoria);
            _creados++;
        }
        else
        {
            categoria.Actualizar(dato.Nombre, dato.DepartamentoId);
            _actualizados++;
        }

        if (dato.Activa) categoria.Activar(); else categoria.Desactivar();
    }

    private async Task AplicarMarcaAsync(MarcaCarga dato, CancellationToken cancelacion)
    {
        var marca = await contexto.Marcas.SingleOrDefaultAsync(m => m.Id == dato.Id, cancelacion);
        if (marca is null)
        {
            marca = Marca.Crear(dato.Codigo, dato.Nombre, dato.Id);
            contexto.Marcas.Add(marca);
            _creados++;
        }
        else
        {
            marca.CambiarNombre(dato.Nombre);
            _actualizados++;
        }

        if (dato.Activa) marca.Activar(); else marca.Desactivar();
    }

    private async Task AplicarUnidadAsync(UnidadMedidaCarga dato, CancellationToken cancelacion)
    {
        var unidad = await contexto.UnidadesMedida.SingleOrDefaultAsync(u => u.Id == dato.Id, cancelacion);
        if (unidad is null)
        {
            contexto.UnidadesMedida.Add(UnidadMedida.Crear(dato.Codigo, dato.Nombre, dato.PermiteDecimales, dato.Decimales, dato.Id));
            _creados++;
            return;
        }

        unidad.Actualizar(dato.Nombre, dato.PermiteDecimales, dato.Decimales);
        _actualizados++;
    }

    private async Task AplicarImpuestoAsync(ImpuestoCarga dato, CancellationToken cancelacion)
    {
        var impuesto = await contexto.Impuestos.SingleOrDefaultAsync(i => i.Id == dato.Id, cancelacion);
        if (impuesto is null)
        {
            impuesto = Impuesto.Crear(dato.Codigo, dato.Nombre, dato.Porcentaje, dato.IndicadorFacturacion, dato.Id);
            contexto.Impuestos.Add(impuesto);
            _creados++;
        }
        else
        {
            impuesto.Actualizar(dato.Nombre, dato.Porcentaje, dato.IndicadorFacturacion);
            _actualizados++;
        }

        if (dato.Activo) impuesto.Activar(); else impuesto.Desactivar();
    }

    private async Task AplicarArticuloAsync(ArticuloCarga dato, string origen, DateTimeOffset ahora, CancellationToken cancelacion)
    {
        var articulo = await contexto.Articulos.Include(a => a.Codigos).SingleOrDefaultAsync(a => a.Id == dato.Id, cancelacion);
        if (articulo is null)
        {
            articulo = Articulo.Crear(dato.Codigo, dato.Descripcion, dato.DepartamentoId, dato.UnidadMedidaId, dato.ImpuestoId, dato.Tipo, dato.Id);
            contexto.Articulos.Add(articulo);
            _creados++;
        }
        else
        {
            if (articulo.Codigo != dato.Codigo.Trim())
                throw new InvalidOperationException($"No se puede cambiar el código del artículo ({articulo.Codigo} → {dato.Codigo.Trim()}).");
            _actualizados++;
        }

        articulo.ActualizarDatos(dato.Descripcion, dato.Referencia, dato.DepartamentoId, dato.UnidadMedidaId, dato.ImpuestoId, dato.Tipo);
        articulo.ConfigurarPrecios(dato.Costo, dato.PrecioMinimo, dato.CantidadMinimaMayor);
        articulo.ConfigurarTara(dato.Tara);
        articulo.Clasificar(dato.CategoriaId, dato.MarcaId);
        articulo.ConfigurarNaturaleza(dato.EsServicio);
        articulo.ConfigurarPresentacion(dato.RutaImagen, dato.MostrarEnCatalogo, dato.VentaEnPos);
        articulo.ReemplazarCodigos(
            (dato.CodigosBarras ?? []).Select(c => (c, TipoCodigoArticulo.Barras))
                .Concat((dato.CodigosProveedor ?? []).Select(c => (c, TipoCodigoArticulo.Proveedor))));
        if (dato.Activo) articulo.Activar(); else articulo.Desactivar();

        var vigenteDesde = dato.PreciosVigentesDesde ?? ahora;
        if (await RegistroPrecios.RegistrarSiCambiaAsync(contexto, articulo.Id, ListaPrecio.Detalle, dato.PrecioDetalle, vigenteDesde, ahora, origen, null, null, cancelacion))
            _precios++;
        if (dato.PrecioMayor is { } mayor
            && await RegistroPrecios.RegistrarSiCambiaAsync(contexto, articulo.Id, ListaPrecio.Mayor, mayor, vigenteDesde, ahora, origen, null, null, cancelacion))
            _precios++;
    }

    private async Task AplicarClienteAsync(ClienteCarga dato, CancellationToken cancelacion)
    {
        var cliente = await contexto.Clientes.Include(c => c.Direcciones).SingleOrDefaultAsync(c => c.Id == dato.Id, cancelacion);
        if (cliente is null)
        {
            cliente = Cliente.Crear(dato.TipoDocumento, dato.Documento, dato.Nombre, dato.Id);
            contexto.Clientes.Add(cliente);
            _creados++;
        }
        else
        {
            _actualizados++;
        }

        cliente.ActualizarContacto(dato.Nombre, dato.Telefono, dato.Correo);
        cliente.ConfigurarFacturacion(dato.TipoComprobante, dato.ExoneradoItbis, dato.AplicaRetencion, dato.ListaPrecio);

        var direcciones = dato.Direcciones ?? [];
        var idsDeseados = direcciones.Select(d => d.Id).ToHashSet();
        foreach (var sobrante in cliente.Direcciones.Where(d => !idsDeseados.Contains(d.Id)).Select(d => d.Id).ToList())
            cliente.QuitarDireccion(sobrante);

        foreach (var direccion in direcciones)
        {
            if (cliente.Direcciones.Any(d => d.Id == direccion.Id))
                cliente.ActualizarDireccion(direccion.Id, direccion.Alias, direccion.Direccion, direccion.Sector, direccion.Ciudad, direccion.Referencia, direccion.Telefono);
            else
                cliente.AgregarDireccion(direccion.Alias, direccion.Direccion, direccion.Sector, direccion.Ciudad, direccion.Referencia, direccion.Telefono, id: direccion.Id);
        }

        var principal = direcciones.FirstOrDefault(d => d.EsPrincipal);
        if (principal is not null)
            cliente.MarcarPrincipal(principal.Id);

        if (dato.Activo) cliente.Activar(); else cliente.Desactivar();
    }

    private async Task AplicarMonedaAsync(MonedaCarga dato, CancellationToken cancelacion)
    {
        var moneda = await contexto.Monedas.SingleOrDefaultAsync(m => m.Id == dato.Id, cancelacion);
        if (moneda is null)
        {
            moneda = Moneda.Crear(dato.Codigo, dato.Nombre, dato.Simbolo, dato.Id);
            contexto.Monedas.Add(moneda);
            _creados++;
        }
        else
        {
            if (!string.Equals(moneda.Codigo, dato.Codigo?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"No se puede cambiar el código de la moneda '{moneda.Codigo}'; cree una nueva.");
            moneda.Actualizar(dato.Nombre, dato.Simbolo);
            _actualizados++;
        }

        if (dato.Activa) moneda.Activar(); else moneda.Desactivar();
    }

    /// <summary>Formas de pago, denominaciones y tasas solo pueden usar monedas del maestro (las del paquete o las ya cargadas).</summary>
    private async Task ValidarMonedaAsync(string? codigo, string referencia, CancellationToken cancelacion)
    {
        var normalizado = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!contexto.Monedas.Local.Any(m => m.Codigo == normalizado) && !await contexto.Monedas.AnyAsync(m => m.Codigo == normalizado, cancelacion))
            throw new InvalidOperationException($"{referencia} usa la moneda '{normalizado}', que no está en el maestro de monedas.");
    }

    private async Task AplicarFormaPagoAsync(FormaPagoCarga dato, CancellationToken cancelacion)
    {
        await ValidarMonedaAsync(dato.Moneda, $"La forma de pago '{dato.Codigo}'", cancelacion);
        var forma = await contexto.FormasPago.SingleOrDefaultAsync(f => f.Id == dato.Id, cancelacion);
        if (forma is null)
        {
            forma = FormaPago.Crear(dato.Codigo, dato.Nombre, dato.Tipo, dato.Orden, dato.Moneda, dato.Id);
            contexto.FormasPago.Add(forma);
            _creados++;
        }
        else
        {
            if (forma.Tipo != dato.Tipo)
                throw new InvalidOperationException($"No se puede cambiar el tipo de la forma de pago '{forma.Codigo}'.");
            _actualizados++;
        }

        var sugeridos = FormaPago.ValoresPorTipo(dato.Tipo);
        forma.Configurar(
            dato.Nombre,
            dato.Orden,
            dato.AbreGaveta ?? sugeridos.AbreGaveta,
            dato.PermiteDevuelta ?? sugeridos.PermiteDevuelta,
            dato.RequiereReferencia ?? sugeridos.RequiereReferencia,
            dato.RequiereBanco ?? sugeridos.RequiereBanco,
            dato.PermiteComprobanteFiscal ?? sugeridos.PermiteComprobanteFiscal);

        if (dato.Activa) forma.Activar(); else forma.Desactivar();
    }

    private async Task AplicarBancoAsync(BancoCarga dato, CancellationToken cancelacion)
    {
        var banco = await contexto.Bancos.SingleOrDefaultAsync(b => b.Id == dato.Id, cancelacion);
        if (banco is null)
        {
            banco = Banco.Crear(dato.Codigo, dato.Nombre, dato.RutaLogo, dato.Id);
            contexto.Bancos.Add(banco);
            _creados++;
        }
        else
        {
            banco.Actualizar(dato.Nombre, dato.RutaLogo);
            _actualizados++;
        }

        if (dato.Activo) banco.Activar(); else banco.Desactivar();
    }

    private async Task AplicarTipoTarjetaAsync(TipoTarjetaCarga dato, CancellationToken cancelacion)
    {
        var tipo = await contexto.TiposTarjeta.SingleOrDefaultAsync(t => t.Id == dato.Id, cancelacion);
        if (tipo is null)
        {
            tipo = TipoTarjeta.Crear(dato.Codigo, dato.Nombre, dato.Id);
            contexto.TiposTarjeta.Add(tipo);
            _creados++;
        }
        else
        {
            tipo.CambiarNombre(dato.Nombre);
            _actualizados++;
        }

        if (dato.Activo) tipo.Activar(); else tipo.Desactivar();
    }

    private async Task AplicarDenominacionAsync(DenominacionCarga dato, CancellationToken cancelacion)
    {
        await ValidarMonedaAsync(dato.Moneda, $"La denominación {dato.Valor}", cancelacion);
        var denominacion = await contexto.Denominaciones.SingleOrDefaultAsync(d => d.Id == dato.Id, cancelacion);
        if (denominacion is null)
        {
            denominacion = Denominacion.Crear(dato.Moneda, dato.Valor, dato.Tipo, dato.Id);
            contexto.Denominaciones.Add(denominacion);
            _creados++;
        }
        else
        {
            if (denominacion.Valor != dato.Valor || denominacion.Tipo != dato.Tipo || !string.Equals(denominacion.Moneda, dato.Moneda, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Una denominación no puede cambiar de moneda, valor ni tipo; cree una nueva.");
            _actualizados++;
        }

        if (dato.Activa) denominacion.Activar(); else denominacion.Desactivar();
    }

    private async Task AplicarPromocionAsync(PromocionCarga dato, CancellationToken cancelacion)
    {
        var promocion = await contexto.Promociones.SingleOrDefaultAsync(p => p.Id == dato.Id, cancelacion);
        if (promocion is null)
        {
            promocion = Promocion.Crear(dato.Codigo, dato.Nombre, dato.Tipo, dato.Valor, dato.VigenteDesde, dato.VigenteHasta, dato.Id);
            contexto.Promociones.Add(promocion);
            _creados++;
        }
        else
        {
            if (promocion.Codigo != dato.Codigo.Trim().ToUpperInvariant())
                throw new InvalidOperationException($"No se puede cambiar el código de la promoción ({promocion.Codigo} → {dato.Codigo.Trim()}).");
            promocion.Actualizar(dato.Nombre, dato.Tipo, dato.Valor, dato.VigenteDesde, dato.VigenteHasta);
            _actualizados++;
        }

        promocion.ConfigurarCantidades(dato.CantidadLleva, dato.CantidadPaga, dato.CantidadMinima, dato.LimitePorCliente);
        promocion.Programar(dato.Dias, dato.HoraDesde, dato.HoraHasta);
        promocion.AsignarAlcance(dato.Articulos, dato.Departamentos, dato.Sucursales, dato.Categorias, dato.Marcas);
        promocion.ConfigurarFidelidad(dato.SoloFidelidad);
        if (dato.Activa) promocion.Activar(); else promocion.Desactivar();
    }

    private async Task AplicarNivelFidelidadAsync(NivelFidelidadCarga dato, CancellationToken cancelacion)
    {
        var nivel = await contexto.NivelesFidelidad.SingleOrDefaultAsync(n => n.Id == dato.Id, cancelacion);
        if (nivel is null)
        {
            nivel = Dominio.Fidelidad.NivelFidelidad.Crear(dato.Codigo, dato.Nombre, dato.Orden, dato.FactorAcumulacion, dato.Id);
            contexto.NivelesFidelidad.Add(nivel);
            _creados++;
        }
        else
        {
            if (nivel.Codigo != dato.Codigo.Trim().ToUpperInvariant())
                throw new InvalidOperationException($"No se puede cambiar el código del nivel de fidelidad '{nivel.Codigo}'.");
            nivel.Actualizar(dato.Nombre, dato.Orden, dato.FactorAcumulacion);
            _actualizados++;
        }

        if (dato.Activo) nivel.Activar(); else nivel.Desactivar();
    }

    private async Task AplicarReglaAcumulacionAsync(ReglaAcumulacionCarga dato, CancellationToken cancelacion)
    {
        var regla = await contexto.ReglasAcumulacion.SingleOrDefaultAsync(r => r.Id == dato.Id, cancelacion);
        if (regla is null)
        {
            regla = Dominio.Fidelidad.ReglaAcumulacion.Crear(dato.Codigo, dato.Nombre, dato.Tipo, dato.MontoBase, dato.Puntos, dato.ReferenciaId, dato.DiaSemana,
                dato.VigenteDesde, dato.VigenteHasta, dato.Id);
            contexto.ReglasAcumulacion.Add(regla);
            _creados++;
        }
        else
        {
            if (regla.Codigo != dato.Codigo.Trim().ToUpperInvariant())
                throw new InvalidOperationException($"No se puede cambiar el código de la regla de acumulación '{regla.Codigo}'.");
            regla.Actualizar(dato.Nombre, dato.Tipo, dato.MontoBase, dato.Puntos, dato.ReferenciaId, dato.DiaSemana, dato.VigenteDesde, dato.VigenteHasta);
            _actualizados++;
        }

        if (dato.Activa) regla.Activar(); else regla.Desactivar();
    }

    /// <summary>Miembros y saldos que calcula el Central; una inscripción hecha en caja se confirma con el mismo Id.</summary>
    private async Task AplicarMiembroFidelidadAsync(MiembroFidelidadCarga dato, CancellationToken cancelacion)
    {
        var cedula = Dominio.Fidelidad.MiembroFidelidad.ValidarCedula(dato.Cedula);
        // La misma persona inscrita sin conexión en dos cajas llega del Central con el Id que conservó (RN-24): esta caja actualiza su registro por la
        // cédula (los movimientos de puntos viajan con la cédula) en lugar de detener la sincronización.
        var miembro = await contexto.MiembrosFidelidad.SingleOrDefaultAsync(m => m.Id == dato.Id, cancelacion)
            ?? await contexto.MiembrosFidelidad.SingleOrDefaultAsync(m => m.Cedula == cedula, cancelacion);
        if (miembro is null)
        {
            miembro = Dominio.Fidelidad.MiembroFidelidad.DesdeCentral(cedula, dato.Nombre, dato.InscritoEn ?? reloj.GetUtcNow(), dato.Id);
            contexto.MiembrosFidelidad.Add(miembro);
            _creados++;
        }
        else
        {
            if (miembro.Cedula != cedula)
                throw new InvalidOperationException($"No se puede cambiar la cédula del miembro {miembro.Cedula}.");
            _actualizados++;
        }

        miembro.ActualizarContacto(dato.Nombre, dato.Telefono, dato.Correo);
        miembro.AsignarNivel(dato.NivelId);
        if (dato.SaldoAl is { } saldoAl)
            miembro.SincronizarSaldo(dato.SaldoPuntos, saldoAl, dato.PuntosPorVencer, dato.ProximoVencimiento);
        if (dato.Activo) miembro.Activar(); else miembro.Desactivar();
    }

    /// <summary>Descuento del banco por BIN de tarjeta (RF-98): lo define el Central y la caja lo aplica al cobrar.</summary>
    private async Task AplicarDescuentoTarjetaAsync(DescuentoTarjetaCarga dato, CancellationToken cancelacion)
    {
        var descuento = await contexto.DescuentosTarjeta.SingleOrDefaultAsync(d => d.Id == dato.Id, cancelacion);
        if (descuento is null)
        {
            contexto.DescuentosTarjeta.Add(DescuentoTarjeta.Crear(dato.Id, dato.Codigo, dato.Nombre, dato.Bines, dato.Tipo, dato.Valor, dato.MontoMinimo,
                dato.MontoMaximo, dato.BancoId, dato.VigenteDesde, dato.VigenteHasta, dato.Dias, dato.Activo));
            _creados++;
            return;
        }

        if (descuento.Codigo != dato.Codigo.Trim().ToUpperInvariant())
            throw new InvalidOperationException($"No se puede cambiar el código del descuento de tarjeta '{descuento.Codigo}'.");

        descuento.Actualizar(dato.Nombre, dato.Bines, dato.Tipo, dato.Valor, dato.MontoMinimo, dato.MontoMaximo, dato.BancoId, dato.VigenteDesde,
            dato.VigenteHasta, dato.Dias, dato.Activo);
        _actualizados++;
    }

    private async Task AplicarAlmacenAsync(AlmacenCarga dato, CancellationToken cancelacion)
    {
        var almacen = await contexto.Almacenes.SingleOrDefaultAsync(a => a.Id == dato.Id, cancelacion);
        if (almacen is null)
        {
            almacen = Dominio.Entregas.Almacen.Crear(dato.Codigo, dato.Nombre, dato.SucursalId, dato.Direccion, dato.Id);
            contexto.Almacenes.Add(almacen);
            _creados++;
        }
        else
        {
            if (almacen.Codigo != dato.Codigo.Trim().ToUpperInvariant())
                throw new InvalidOperationException($"No se puede cambiar el código del almacén '{almacen.Codigo}'.");
            almacen.Actualizar(dato.Nombre, dato.SucursalId, dato.Direccion);
            _actualizados++;
        }

        if (dato.Activo) almacen.Activar(); else almacen.Desactivar();
    }

    private async Task AplicarMotivoDescuentoAsync(MotivoDescuentoCarga dato, CancellationToken cancelacion)
    {
        var motivo = await contexto.MotivosDescuento.SingleOrDefaultAsync(m => m.Id == dato.Id, cancelacion);
        if (motivo is null)
        {
            motivo = MotivoDescuento.Crear(dato.Codigo, dato.Nombre, dato.Id);
            contexto.MotivosDescuento.Add(motivo);
            _creados++;
        }
        else
        {
            motivo.CambiarNombre(dato.Nombre);
            _actualizados++;
        }

        if (dato.Activo) motivo.Activar(); else motivo.Desactivar();
    }

    private async Task AplicarMotivoDevolucionAsync(MotivoDevolucionCarga dato, CancellationToken cancelacion)
    {
        var motivo = await contexto.MotivosDevolucion.SingleOrDefaultAsync(m => m.Id == dato.Id, cancelacion);
        if (motivo is null)
        {
            motivo = Dominio.Devoluciones.MotivoDevolucion.Crear(dato.Codigo, dato.Nombre, dato.Id);
            contexto.MotivosDevolucion.Add(motivo);
            _creados++;
        }
        else
        {
            motivo.CambiarNombre(dato.Nombre);
            _actualizados++;
        }

        if (dato.Activo) motivo.Activar(); else motivo.Desactivar();
    }

    private async Task AplicarTopeDescuentoAsync(TopeDescuentoCarga dato, CancellationToken cancelacion)
    {
        var tope = await contexto.TopesDescuento.SingleOrDefaultAsync(t => t.Id == dato.Id, cancelacion);
        if (tope is null)
        {
            contexto.TopesDescuento.Add(TopeDescuento.Crear(dato.Nivel, dato.PorcentajeMaximo, dato.MontoMaximo, dato.DepartamentoId, dato.ArticuloId, dato.Id,
                dato.CategoriaId, dato.MarcaId));
            _creados++;
            return;
        }

        tope.Actualizar(dato.Nivel, dato.PorcentajeMaximo, dato.MontoMaximo, dato.DepartamentoId, dato.ArticuloId, dato.CategoriaId, dato.MarcaId);
        _actualizados++;
    }

    private async Task AplicarTasaCambioAsync(TasaCambioCarga dato, CancellationToken cancelacion)
    {
        await ValidarMonedaAsync(dato.Moneda, "La tasa de cambio", cancelacion);
        var tasa = await contexto.TasasCambio.SingleOrDefaultAsync(t => t.Id == dato.Id, cancelacion);
        if (tasa is null)
        {
            contexto.TasasCambio.Add(TasaCambio.Registrar(dato.Moneda, dato.Tasa, dato.VigenteDesde, dato.Id));
            _creados++;
            return;
        }

        tasa.Actualizar(dato.Tasa, dato.VigenteDesde);
        _actualizados++;
    }

    private async Task AplicarSecuenciaEcfAsync(SecuenciaEcfCarga dato, CancellationToken cancelacion)
    {
        var secuencia = await contexto.SecuenciasEcf.SingleOrDefaultAsync(s => s.Id == dato.Id, cancelacion);
        if (secuencia is null)
        {
            secuencia = SecuenciaEcf.Asignar(dato.CajaId, dato.TipoComprobante, dato.Desde, dato.Hasta, dato.VenceEn, dato.Id);
            contexto.SecuenciasEcf.Add(secuencia);
            _creados++;
        }
        else
        {
            if (secuencia.CajaId != dato.CajaId || secuencia.TipoComprobante != dato.TipoComprobante || secuencia.Desde != dato.Desde)
                throw new InvalidOperationException("Un rango de e-CF no cambia de caja, tipo ni inicio; asigne un rango nuevo.");
            _actualizados++;
        }

        secuencia.Actualizar(dato.Hasta, dato.VenceEn, dato.Activa);
    }
}
