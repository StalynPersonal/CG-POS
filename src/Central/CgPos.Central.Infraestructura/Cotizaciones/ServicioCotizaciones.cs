using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Cotizaciones;
using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Aplicacion.Reportes;
using CgPos.Central.Infraestructura.Reportes;
using CgPos.Central.Aplicacion.Seguridad;
using CgPos.Central.Infraestructura.Organizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Persistencia.Configuraciones;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Cotizaciones;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Cotizaciones;

internal sealed class ServicioCotizaciones(
    ContextoDatosCentral contexto,
    IParametrosCentral parametros,
    INumeracionCentral numeracion,
    IAuditoriaCentral auditoria,
    TimeProvider reloj) : IServicioCotizaciones
{
    private const string TipoEntidad = "Cotizacion";
    internal const string CodigoDocumento = DocumentosNumerados.Cotizacion;

    public async Task<DatosCotizacionParaCaja?> BuscarParaCajaAsync(string numero, CancellationToken cancelacion = default)
    {
        var buscado = (numero ?? string.Empty).Trim().ToUpperInvariant();
        if (buscado.Length == 0)
            return null;

        var cotizacion = await contexto.Cotizaciones.AsNoTracking().Include(c => c.Lineas).FirstOrDefaultAsync(c => c.Numero == buscado, cancelacion);
        if (cotizacion is null)
            return null;

        // Se devuelve aunque esté vencida, facturada o anulada: la caja decide qué hacer y se lo explica al cajero.
        return new DatosCotizacionParaCaja(
            cotizacion.Numero,
            cotizacion.ClienteNombre,
            cotizacion.ClienteDocumento,
            cotizacion.Estado,
            cotizacion.VenceEn,
            cotizacion.EstaVencida(Hoy()),
            cotizacion.Totales().Total,
            cotizacion.Lineas.OrderBy(l => l.NumeroLinea)
                .Select(l => new DatosLineaCotizacionParaCaja(l.ArticuloCodigo, l.Descripcion, l.Cantidad, l.PrecioUnitario, l.Descuento))
                .ToList());
    }

    public async Task<IReadOnlyList<DatosCotizacion>> ListarAsync(string? buscar, EstadoCotizacion? estado, CancellationToken cancelacion = default)
    {
        var consulta = contexto.Cotizaciones.AsNoTracking().Include(c => c.Lineas).AsQueryable();
        if (estado is { } filtro)
            consulta = consulta.Where(c => c.Estado == filtro);
        if (buscar is { Length: > 0 })
        {
            var texto = buscar.Trim();
            consulta = consulta.Where(c => c.Numero.Contains(texto) || c.ClienteNombre.Contains(texto)
                || (c.ClienteDocumento != null && c.ClienteDocumento.Contains(texto))
                || (c.VentaNumero != null && c.VentaNumero.Contains(texto)));
        }

        var cotizaciones = await consulta.OrderByDescending(c => c.CreadaEn).Take(200).ToListAsync(cancelacion);
        return await DatosAsync(cotizaciones, cancelacion);
    }

    public async Task<DateOnly> VencimientoPredeterminadoAsync(CancellationToken cancelacion = default) =>
        Hoy().AddDays(await parametros.ObtenerEnteroPositivoAsync(ClavesParametrosCentral.CotizacionesDiasVigencia, cancelacion));

    public async Task<DatosCotizacion?> ObtenerAsync(int cotizacionId, CancellationToken cancelacion = default)
    {
        var cotizacion = await contexto.Cotizaciones.AsNoTracking().Include(c => c.Lineas).FirstOrDefaultAsync(c => c.Id == cotizacionId, cancelacion);
        return cotizacion is null ? null : (await DatosAsync([cotizacion], cancelacion))[0];
    }

    public async Task<ResultadoAdministracion> CrearAsync(SolicitudCotizacion solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (Invalida(solicitud) is { } problema)
            return ResultadoAdministracion.Error(problema);
        if (solicitud.SucursalId is { } sucursalId && !await contexto.Sucursales.AnyAsync(s => s.Id == sucursalId, cancelacion))
            return ResultadoAdministracion.Error("La sucursal indicada no existe.");

        var lineas = await ResolverLineasAsync(solicitud, cancelacion);
        if (lineas.Problema is { } faltan)
            return ResultadoAdministracion.Error(faltan);

        var ahora = reloj.Ahora();
        var vence = solicitud.VenceEn ?? await VencimientoPredeterminadoAsync(cancelacion);

        try
        {
            string numero;
            try
            {
                numero = await numeracion.SiguienteAsync(CodigoDocumento, cancelacion);
            }
            catch (SecuenciaCentralNoConfiguradaExcepcion excepcion)
            {
                return ResultadoAdministracion.Error(excepcion.Message);
            }

            var cotizacion = Cotizacion.Crear(numero, solicitud.ClienteNombre, solicitud.ClienteDocumento, solicitud.ClienteTelefono, solicitud.ClienteCorreo,
                solicitud.SucursalId, vence, solicitud.Observacion, actor.Nombre, ahora);
            contexto.Cotizaciones.Add(cotizacion);

            // Las líneas necesitan el Id de la cotización, que lo pone la base.
            await contexto.SaveChangesAsync(cancelacion);
            cotizacion.ReemplazarLineas(lineas.Datos!, ahora);

            auditoria.Registrar(new EntradaAuditoria("Cotizaciones.Creada", TipoEntidad, cotizacion.Numero,
                Detalle: new { cotizacion.ClienteNombre, cotizacion.VenceEn, Lineas = lineas.Datos!.Count, Total = cotizacion.Totales().Total },
                Usuario: actor));
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoAdministracion.Correcto(cotizacion.Id);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }
    }

    public async Task<ResultadoAdministracion> ActualizarAsync(int cotizacionId, SolicitudCotizacion solicitud, UsuarioAuditoria actor,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (Invalida(solicitud) is { } problema)
            return ResultadoAdministracion.Error(problema);

        var cotizacion = await contexto.Cotizaciones.Include(c => c.Lineas).FirstOrDefaultAsync(c => c.Id == cotizacionId, cancelacion);
        if (cotizacion is null)
            return ResultadoAdministracion.Inexistente("La cotización no existe.");
        if (solicitud.SucursalId is { } sucursalId && !await contexto.Sucursales.AnyAsync(s => s.Id == sucursalId, cancelacion))
            return ResultadoAdministracion.Error("La sucursal indicada no existe.");

        var lineas = await ResolverLineasAsync(solicitud, cancelacion);
        if (lineas.Problema is { } faltan)
            return ResultadoAdministracion.Error(faltan);

        var ahora = reloj.Ahora();
        try
        {
            cotizacion.Actualizar(solicitud.ClienteNombre, solicitud.ClienteDocumento, solicitud.ClienteTelefono, solicitud.ClienteCorreo,
                solicitud.SucursalId, solicitud.VenceEn ?? cotizacion.VenceEn, solicitud.Observacion, ahora);
            cotizacion.ReemplazarLineas(lineas.Datos!, ahora);

            auditoria.Registrar(new EntradaAuditoria("Cotizaciones.Actualizada", TipoEntidad, cotizacion.Numero,
                Detalle: new { cotizacion.ClienteNombre, cotizacion.VenceEn, Lineas = lineas.Datos!.Count, Total = cotizacion.Totales().Total },
                Usuario: actor));
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoAdministracion.Correcto(cotizacion.Id);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }
    }

    public async Task<ResultadoAdministracion> AnularAsync(int cotizacionId, string motivo, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return ResultadoAdministracion.Error("Indique el motivo de la anulación.");

        var cotizacion = await contexto.Cotizaciones.FirstOrDefaultAsync(c => c.Id == cotizacionId, cancelacion);
        if (cotizacion is null)
            return ResultadoAdministracion.Inexistente("La cotización no existe.");

        try
        {
            cotizacion.Anular(motivo, reloj.Ahora());
            auditoria.Registrar(new EntradaAuditoria("Cotizaciones.Anulada", TipoEntidad, cotizacion.Numero,
                Motivo: cotizacion.MotivoAnulacion, Usuario: actor));
            await contexto.SaveChangesAsync(cancelacion);
            return ResultadoAdministracion.Correcto(cotizacion.Id);
        }
        catch (ArgumentException excepcion)
        {
            contexto.ChangeTracker.Clear();
            return ResultadoAdministracion.Error(ValidacionMaestros.MensajeError(excepcion));
        }
    }

    public async Task<ArchivoReporte?> DocumentoAsync(int cotizacionId, CancellationToken cancelacion = default)
    {
        if (await ObtenerAsync(cotizacionId, cancelacion) is not { } cotizacion)
            return null;

        var empresa = await contexto.Empresas.AsNoTracking().SingleAsync(cancelacion);
        var condiciones = await parametros.ObtenerAsync(ClavesParametrosCentral.CotizacionesCondiciones, cancelacion);
        var pdf = GeneradorPdfCotizacion.Crear(cotizacion,
            new EmpresaEnDocumento(empresa.NombreComercial ?? empresa.RazonSocial, empresa.Rnc, empresa.Direccion, empresa.Telefono), condiciones);

        return new ArchivoReporte($"{cotizacion.Numero}.pdf", "application/pdf", pdf);
    }

    /// <summary>
    /// Completa cada línea con lo que hay en el maestro: descripción, unidad e impuesto, y el precio del día si no se indicó
    /// otro. Ese precio queda congelado en la cotización, que es lo que se le promete al cliente.
    /// </summary>
    private async Task<(IReadOnlyList<DatosLineaCotizacion>? Datos, string? Problema)> ResolverLineasAsync(SolicitudCotizacion solicitud,
        CancellationToken cancelacion)
    {
        var codigos = solicitud.Lineas.Select(l => (l.ArticuloCodigo ?? string.Empty).Trim())
            .Where(codigo => codigo.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (codigos.Count == 0)
            return (null, "Agregue al menos un artículo a la cotización.");

        var maestro = await contexto.Articulos.AsNoTracking()
            .Where(a => codigos.Contains(a.Codigo))
            .Select(a => new
            {
                a.Codigo,
                a.Descripcion,
                a.ImpuestoId,
                a.UnidadMedidaId,
                PrecioDetalle = EF.Property<decimal>(a, ArticuloConfiguracion.PrecioDetalle),
            })
            .ToListAsync(cancelacion);

        var faltan = codigos.Except(maestro.Select(a => a.Codigo), StringComparer.Ordinal).ToList();
        if (faltan.Count > 0)
            return (null, $"{(faltan.Count == 1 ? "No existe el artículo" : "No existen los artículos")} {string.Join(", ", faltan)} en el maestro.");

        var impuestos = await contexto.Impuestos.AsNoTracking().ToDictionaryAsync(i => i.Id, i => (i.Porcentaje, i.IndicadorFacturacion), cancelacion);
        // La abreviatura es la que se imprime (UND, LB), no el código numérico de la unidad.
        var unidades = await contexto.UnidadesMedida.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Abreviatura, cancelacion);

        var lineas = new List<DatosLineaCotizacion>(solicitud.Lineas.Count);
        foreach (var pedida in solicitud.Lineas)
        {
            var codigo = (pedida.ArticuloCodigo ?? string.Empty).Trim();
            var articulo = maestro.First(a => string.Equals(a.Codigo, codigo, StringComparison.Ordinal));
            var impuesto = impuestos.GetValueOrDefault(articulo.ImpuestoId, (0m, 4));

            lineas.Add(new DatosLineaCotizacion(
                articulo.Codigo,
                articulo.Descripcion,
                unidades.GetValueOrDefault(articulo.UnidadMedidaId),
                pedida.Cantidad,
                pedida.PrecioUnitario ?? articulo.PrecioDetalle,
                pedida.Descuento,
                impuesto.Item1,
                impuesto.Item2));
        }

        return (lineas, null);
    }

    private static string? Invalida(SolicitudCotizacion solicitud) =>
        string.IsNullOrWhiteSpace(solicitud.ClienteNombre) ? "Indique el nombre del cliente."
        : solicitud.Lineas is not { Count: > 0 } ? "Agregue al menos un artículo a la cotización."
        : solicitud.Lineas.Any(l => l.Cantidad <= 0) ? "Cada línea necesita una cantidad mayor que cero."
        : solicitud.VenceEn is { } vence && vence < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-4).Date)
            ? "La fecha de vigencia no puede ser anterior a hoy."
            : null;

    /// <summary>El día de aquí, no el del reloj del servidor.</summary>
    private DateOnly Hoy() => DateOnly.FromDateTime(reloj.GetUtcNow().ToOffset(RelojNegocio.Desfase).Date);

    private async Task<IReadOnlyList<DatosCotizacion>> DatosAsync(IReadOnlyList<Cotizacion> cotizaciones, CancellationToken cancelacion)
    {
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nombre, cancelacion);
        var hoy = Hoy();

        return cotizaciones.Select(c =>
        {
            var totales = c.Totales();
            return new DatosCotizacion(
                c.Id,
                c.Numero,
                c.ClienteNombre,
                c.ClienteDocumento,
                c.ClienteTelefono,
                c.ClienteCorreo,
                c.SucursalId,
                c.SucursalId is { } id ? sucursales.GetValueOrDefault(id) : null,
                c.VenceEn,
                c.EstaVencida(hoy),
                c.Estado,
                c.VentaNumero,
                c.Observacion,
                c.MotivoAnulacion,
                c.CreadaPor,
                c.CreadaEn,
                totales.Subtotal,
                totales.Impuesto,
                totales.Descuento,
                totales.Total,
                c.Lineas.OrderBy(l => l.NumeroLinea)
                    .Select(l => new DatosLineaCotizacionCentral(l.NumeroLinea, l.ArticuloCodigo, l.Descripcion, l.UnidadMedida, l.Cantidad,
                        l.PrecioUnitario, l.Descuento, l.PorcentajeImpuesto, l.IndicadorFacturacion, l.Importe))
                    .ToList());
        }).ToList();
    }
}
