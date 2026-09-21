using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Fiscal;
using Microsoft.EntityFrameworkCore;
using CgPos.Dominio.Comun;

namespace CgPos.Central.Infraestructura.Maestros;

internal sealed class ServicioMaestrosCentral(ContextoDatosCentral contexto, IPublicadorMaestros publicador, IAuditoriaCentral auditoria, TimeProvider reloj)
    : IServicioMaestrosCentral
{
    public async Task<IReadOnlyList<DatosMaestroCentral<T>>> ListarAsync<T>(CancellationToken cancelacion = default) where T : class =>
        await TablasMaestros.De<T>().TodosAsync(contexto, new ResolutorCodigosCentral(contexto), cancelacion);

    public Task<PaginaMaestros<T>> BuscarAsync<T>(string? texto, int pagina, int tamano, string? campo = null, CancellationToken cancelacion = default)
        where T : class =>
        TablasMaestros.De<T>().PaginaAsync(contexto, new ResolutorCodigosCentral(contexto), texto, campo,
            Math.Max(pagina, 0), Math.Clamp(tamano, 1, IServicioMaestrosCentral.TamanoMaximoPagina), cancelacion);

    public async Task<ResultadoAdministracion> GuardarAsync<T>(T dato, bool nuevo, UsuarioAuditoria actor, CancellationToken cancelacion = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(dato);
        var tabla = TablasMaestros.De<T>();
        var existente = await tabla.IdAsync(contexto, dato, cancelacion);
        if (nuevo && existente is not null)
            return ResultadoAdministracion.Error("Ya existe un registro con ese código; ábralo para cambiarlo.");
        if (!nuevo && existente is null)
            return ResultadoAdministracion.Inexistente("El registro no existe.");

        try
        {
            await publicador.PublicarAsync(Paquete(dato), actor.Nombre, cancelacion);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }

        return ResultadoAdministracion.Correcto(existente ?? await tabla.IdAsync(contexto, dato, cancelacion));
    }

    public async Task<ResultadoAdministracion> GuardarArticuloAsync(ArticuloCarga articulo, bool nuevo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(articulo);
        var anterior = nuevo ? null : await ArticuloAsync(articulo.Codigo, cancelacion);
        if (!nuevo && anterior is null)
            return ResultadoAdministracion.Inexistente("El artículo no existe.");

        var publicar = anterior is null
            ? articulo with { PreciosVigentesDesde = articulo.PreciosVigentesDesde ?? reloj.Ahora() }
            : articulo with
            {
                PrecioDetalle = anterior.PrecioDetalle,
                PrecioMayor = anterior.PrecioMayor,
                CantidadMinimaMayor = anterior.CantidadMinimaMayor,
                PrecioMinimo = anterior.PrecioMinimo,
                Costo = anterior.Costo,
                PreciosVigentesDesde = anterior.PreciosVigentesDesde,
            };

        return await GuardarAsync(publicar, nuevo, actor, cancelacion);
    }

    public async Task<ResultadoAdministracion> CorregirDocumentoClienteAsync(string codigoCliente, SolicitudCorreccionDocumentoCliente solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var codigo = codigoCliente?.Trim().ToUpperInvariant() ?? string.Empty;
        var id = await contexto.Clientes.AsNoTracking().Where(c => c.Codigo == codigo).Select(c => (int?)c.Id).SingleOrDefaultAsync(cancelacion);
        if (id is null || (await TablasMaestros.Clientes.PorIdAsync(contexto, new ResolutorCodigosCentral(contexto), id.Value, cancelacion))?.Dato is not { } actual)
            return ResultadoAdministracion.Inexistente("El cliente no existe.");
        if (string.IsNullOrWhiteSpace(solicitud.Motivo))
            return ResultadoAdministracion.Error("Indique el motivo de la corrección.");

        var documento = DocumentoIdentidad.Normalizar(solicitud.Documento);
        if (solicitud.TipoDocumento == actual.TipoDocumento && documento == DocumentoIdentidad.Normalizar(actual.Documento))
            return ResultadoAdministracion.Error("El documento es el mismo que ya tiene el cliente.");

        // RNC y cédula deben tener formato y dígito verificador válidos; el pasaporte solo se valida por formato (en el dominio).
        if (solicitud.TipoDocumento != TipoDocumentoIdentidad.Pasaporte)
        {
            var validacion = DocumentoIdentidad.Validar(documento);
            if (validacion.Tipo != solicitud.TipoDocumento || !validacion.EsValido)
                return ResultadoAdministracion.Error($"El documento '{solicitud.Documento}' no es {(solicitud.TipoDocumento == TipoDocumentoIdentidad.Rnc ? "un RNC válido" : "una cédula válida")}.");
        }

        if (await contexto.Clientes.AsNoTracking().AnyAsync(c => c.Id != id && c.TipoDocumento == solicitud.TipoDocumento && c.Documento == documento, cancelacion))
            return ResultadoAdministracion.Error("Otro cliente ya tiene ese documento.");

        try
        {
            await publicador.PublicarAsync(new PaqueteMaestros(Clientes: [actual with { TipoDocumento = solicitud.TipoDocumento, Documento = documento }]), actor.Nombre,
                cancelacion, corregirDocumentoCliente: true);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return ResultadoAdministracion.Error(string.Join(" ", excepcion.Errores));
        }

        auditoria.Registrar(new EntradaAuditoria("Maestros.ClienteDocumentoCorregido", "Cliente", actual.Codigo,
            new { Anterior = new { actual.TipoDocumento, actual.Documento }, Nuevo = new { solicitud.TipoDocumento, Documento = documento } }, solicitud.Motivo.Trim(), actor));
        await contexto.SaveChangesAsync(cancelacion);
        return ResultadoAdministracion.Correcto(id);
    }

    public async Task<ResultadoAdministracion> CambiarPreciosAsync(string codigoArticulo, SolicitudPreciosArticulo solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        if (await ArticuloAsync(codigoArticulo, cancelacion) is not { } anterior)
            return ResultadoAdministracion.Inexistente("El artículo no existe.");

        // Todas las cajas registran el cambio con la misma vigencia, aunque lo reciban en momentos distintos.
        var articulo = anterior with
        {
            PrecioDetalle = solicitud.PrecioDetalle,
            PrecioMayor = solicitud.PrecioMayor,
            CantidadMinimaMayor = solicitud.CantidadMinimaMayor,
            PrecioMinimo = solicitud.PrecioMinimo,
            Costo = solicitud.Costo,
            PreciosVigentesDesde = solicitud.VigenteDesde ?? reloj.Ahora(),
        };

        return await GuardarAsync(articulo, nuevo: false, actor, cancelacion);
    }

    public async Task<IReadOnlyList<DatosTopeDescuentoCentral>> ListarTopesAsync(CancellationToken cancelacion = default)
    {
        var resolutor = new ResolutorCodigosCentral(contexto);
        var topes = await TablasMaestros.TopesDescuento.TodosAsync(contexto, resolutor, cancelacion);
        var codigosArticulos = topes.Select(t => t.Dato.ArticuloCodigo).OfType<string>().Distinct().ToList();
        var articulos = await contexto.Articulos.AsNoTracking().Where(a => codigosArticulos.Contains(a.Codigo)).ToDictionaryAsync(a => a.Codigo, a => a.Descripcion, cancelacion);
        var departamentos = await contexto.Departamentos.AsNoTracking().ToDictionaryAsync(d => d.Codigo, d => d.Nombre, cancelacion);
        var categorias = await contexto.Categorias.AsNoTracking().ToDictionaryAsync(c => c.Codigo, c => c.Nombre, cancelacion);
        var marcas = await contexto.Marcas.AsNoTracking().ToDictionaryAsync(m => m.Codigo, m => m.Nombre, cancelacion);

        string Alcance(TopeDescuentoCarga tope) => tope switch
        {
            { ArticuloCodigo: { } articulo } => $"Artículo {articulo} · {articulos.GetValueOrDefault(articulo)}",
            { CategoriaCodigo: { } categoria } => $"Categoría {categoria} · {categorias.GetValueOrDefault(categoria)}",
            { MarcaCodigo: { } marca } => $"Marca {marca} · {marcas.GetValueOrDefault(marca)}",
            { DepartamentoCodigo: { } departamento } => $"Departamento {departamento} · {departamentos.GetValueOrDefault(departamento)}",
            _ => "General",
        };

        return topes
            .Select(t => new DatosTopeDescuentoCentral(t.Dato, Alcance(t.Dato), t.ModificadoEn, t.ModificadoPor))
            .OrderBy(t => t.Tope.ArticuloCodigo is not null ? 4 : t.Tope.CategoriaCodigo is not null ? 3 : t.Tope.MarcaCodigo is not null ? 2 : t.Tope.DepartamentoCodigo is not null ? 1 : 0)
            .ThenBy(t => t.Alcance, StringComparer.CurrentCulture)
            .ThenBy(t => t.Tope.Nivel)
            .ToList();
    }

    public async Task<int> SiguienteCodigoAsync<T>(CancellationToken cancelacion = default) where T : class
    {
        int? mayor = typeof(T).Name switch
        {
            nameof(DepartamentoCarga) => await contexto.Departamentos.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(CategoriaCarga) => await contexto.Categorias.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(MarcaCarga) => await contexto.Marcas.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(UnidadMedidaCarga) => await contexto.UnidadesMedida.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(TipoTarjetaCarga) => await contexto.TiposTarjeta.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(MotivoDescuentoCarga) => await contexto.MotivosDescuento.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(MotivoDevolucionCarga) => await contexto.MotivosDevolucion.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(NivelFidelidadCarga) => await contexto.NivelesFidelidad.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(ReglaAcumulacionCarga) => await contexto.ReglasAcumulacion.MaxAsync(e => (int?)e.Codigo, cancelacion),
            nameof(TopeDescuentoCarga) => await contexto.TopesDescuento.MaxAsync(e => (int?)e.Codigo, cancelacion),
            _ => throw new InvalidOperationException($"{typeof(T).Name} no tiene código numérico."),
        };

        return (mayor ?? 0) + 1;
    }

    private async Task<ArticuloCarga?> ArticuloAsync(string codigo, CancellationToken cancelacion)
    {
        var limpio = codigo?.Trim() ?? string.Empty;
        var id = await contexto.Articulos.AsNoTracking().Where(a => a.Codigo == limpio).Select(a => (int?)a.Id).SingleOrDefaultAsync(cancelacion);
        return id is null ? null : (await TablasMaestros.Articulos.PorIdAsync(contexto, new ResolutorCodigosCentral(contexto), id.Value, cancelacion))?.Dato;
    }

    /// <summary>Paquete con un solo registro, en la lista de su tipo.</summary>
    private static PaqueteMaestros Paquete<T>(T dato) where T : class => dato switch
    {
        MonedaCarga d => new PaqueteMaestros(Monedas: [d]),
        DepartamentoCarga d => new PaqueteMaestros(Departamentos: [d]),
        CategoriaCarga d => new PaqueteMaestros(Categorias: [d]),
        MarcaCarga d => new PaqueteMaestros(Marcas: [d]),
        UnidadMedidaCarga d => new PaqueteMaestros(UnidadesMedida: [d]),
        ImpuestoCarga d => new PaqueteMaestros(Impuestos: [d]),
        ArticuloCarga d => new PaqueteMaestros(Articulos: [d]),
        ClienteCarga d => new PaqueteMaestros(Clientes: [d]),
        FormaPagoCarga d => new PaqueteMaestros(FormasPago: [d]),
        BancoCarga d => new PaqueteMaestros(Bancos: [d]),
        TipoTarjetaCarga d => new PaqueteMaestros(TiposTarjeta: [d]),
        DenominacionCarga d => new PaqueteMaestros(Denominaciones: [d]),
        PromocionCarga d => new PaqueteMaestros(Promociones: [d]),
        MotivoDescuentoCarga d => new PaqueteMaestros(MotivosDescuento: [d]),
        TopeDescuentoCarga d => new PaqueteMaestros(TopesDescuento: [d]),
        TasaCambioCarga d => new PaqueteMaestros(TasasCambio: [d]),
        SecuenciaEcfCarga d => new PaqueteMaestros(SecuenciasEcf: [d]),
        MotivoDevolucionCarga d => new PaqueteMaestros(MotivosDevolucion: [d]),
        NivelFidelidadCarga d => new PaqueteMaestros(NivelesFidelidad: [d]),
        ReglaAcumulacionCarga d => new PaqueteMaestros(ReglasAcumulacion: [d]),
        MiembroFidelidadCarga d => new PaqueteMaestros(MiembrosFidelidad: [d]),
        DescuentoTarjetaCarga d => new PaqueteMaestros(DescuentosTarjeta: [d]),
        _ => throw new InvalidOperationException($"{typeof(T).Name} no se publica con el paquete de maestros."),
    };
}
