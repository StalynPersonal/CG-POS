using System.Text.Json;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Serializacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Clientes;
using CgPos.Dominio.Pagos;
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

        var familias = paquete.Familias ?? [];
        var unidades = paquete.UnidadesMedida ?? [];
        var impuestos = paquete.Impuestos ?? [];
        var articulos = paquete.Articulos ?? [];
        var clientes = paquete.Clientes ?? [];
        var formasPago = paquete.FormasPago ?? [];
        var bancos = paquete.Bancos ?? [];
        var tiposTarjeta = paquete.TiposTarjeta ?? [];
        var denominaciones = paquete.Denominaciones ?? [];

        await ValidarAsync(familias, unidades, impuestos, articulos, cancelacion);

        try
        {
            var ahora = reloj.GetUtcNow();

            foreach (var dato in familias)
                await AplicarFamiliaAsync(dato, cancelacion);
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
        IReadOnlyList<FamiliaCarga> familias,
        IReadOnlyList<UnidadMedidaCarga> unidades,
        IReadOnlyList<ImpuestoCarga> impuestos,
        IReadOnlyList<ArticuloCarga> articulos,
        CancellationToken cancelacion)
    {
        var errores = new List<string>();

        Repetidos(articulos.Select(a => a.Id), "Id de artículo", errores);
        Repetidos(articulos.Select(a => a.Codigo.Trim()), "Código de artículo", errores);

        var idsFamilias = familias.Select(f => f.Id).ToHashSet();
        idsFamilias.UnionWith(await contexto.Familias.Select(f => f.Id).ToListAsync(cancelacion));
        var idsUnidades = unidades.Select(u => u.Id).ToHashSet();
        idsUnidades.UnionWith(await contexto.UnidadesMedida.Select(u => u.Id).ToListAsync(cancelacion));
        var idsImpuestos = impuestos.Select(i => i.Id).ToHashSet();
        idsImpuestos.UnionWith(await contexto.Impuestos.Select(i => i.Id).ToListAsync(cancelacion));

        var codigosPorArticulo = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var articulo in articulos)
        {
            var etiqueta = $"El artículo '{articulo.Codigo}'";
            if (!idsFamilias.Contains(articulo.FamiliaId))
                errores.Add($"{etiqueta} referencia una familia inexistente ({articulo.FamiliaId}).");
            if (!idsUnidades.Contains(articulo.UnidadMedidaId))
                errores.Add($"{etiqueta} referencia una unidad de medida inexistente ({articulo.UnidadMedidaId}).");
            if (!idsImpuestos.Contains(articulo.ImpuestoId))
                errores.Add($"{etiqueta} referencia un impuesto inexistente ({articulo.ImpuestoId}).");
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

    private async Task AplicarFamiliaAsync(FamiliaCarga dato, CancellationToken cancelacion)
    {
        var familia = await contexto.Familias.SingleOrDefaultAsync(f => f.Id == dato.Id, cancelacion);
        if (familia is null)
        {
            familia = Familia.Crear(dato.Codigo, dato.Nombre, dato.PermiteDescuentoManual, dato.EsNoCodificada, dato.Id);
            contexto.Familias.Add(familia);
            _creados++;
        }
        else
        {
            familia.Actualizar(dato.Nombre, dato.PermiteDescuentoManual, dato.EsNoCodificada);
            _actualizados++;
        }

        if (dato.Activa) familia.Activar(); else familia.Desactivar();
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
            articulo = Articulo.Crear(dato.Codigo, dato.Descripcion, dato.FamiliaId, dato.UnidadMedidaId, dato.ImpuestoId, dato.Tipo, dato.Id);
            contexto.Articulos.Add(articulo);
            _creados++;
        }
        else
        {
            if (articulo.Codigo != dato.Codigo.Trim())
                throw new InvalidOperationException($"No se puede cambiar el código del artículo ({articulo.Codigo} → {dato.Codigo.Trim()}).");
            _actualizados++;
        }

        articulo.ActualizarDatos(dato.Descripcion, dato.Referencia, dato.FamiliaId, dato.UnidadMedidaId, dato.ImpuestoId, dato.Tipo);
        articulo.ConfigurarPrecios(dato.Costo, dato.PrecioMinimo, dato.CantidadMinimaMayor);
        articulo.ConfigurarTara(dato.Tara);
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

    private async Task AplicarFormaPagoAsync(FormaPagoCarga dato, CancellationToken cancelacion)
    {
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
}
