using System.Globalization;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Central.Infraestructura.Sincronizacion;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Importacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Promociones;
using CgPos.Dominio.Sincronizacion;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Maestros;

internal sealed class ServicioPromocionesCentral(ContextoDatosCentral contexto, IPublicadorMaestros publicador) : IServicioPromocionesCentral
{
    private const int LargoMaximoArchivo = 5 * 1024 * 1024;
    private const int MaximoArticulosPorId = 500;
    private const int TamanoBloqueConsulta = 1000;
    private static readonly string[] ColumnasObligatorias = ["codigo", "nombre", "tipo", "desde", "hasta"];
    private static readonly string[] FormatosFecha = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm"];

    public async Task<IReadOnlyList<DatosPromocionCentral>> ListarAsync(CancellationToken cancelacion = default)
    {
        var filas = await contexto.MaestrosCentral.AsNoTracking()
            .Where(m => m.Tipo == TipoMaestro.Promocion)
            .Select(m => new { Maestro = m, Version = EF.Property<long>(m, ContextoDatosCentral.ColumnaVersion) })
            .ToListAsync(cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().Where(c => c.Habilitada).Select(c => new { c.Id, c.SucursalId }).ToListAsync(cancelacion);
        var confirmadas = await contexto.EstadosSincronizacionCaja.AsNoTracking().ToDictionaryAsync(e => e.CajaId, e => e.VersionMaestrosConfirmada, cancelacion);

        return filas
            .Select(fila =>
            {
                var dato = FormatoMaestros.Leer<PromocionCarga>(fila.Maestro);
                var destino = cajas.Where(c => dato.Sucursales is not { Count: > 0 } sucursales || sucursales.Contains(c.SucursalId)).ToList();
                var conPromocion = destino.Count(c => confirmadas.GetValueOrDefault(c.Id) >= fila.Version);
                return new DatosPromocionCentral(dato, Oferta(dato), destino.Count, conPromocion, fila.Maestro.ModificadoEn, fila.Maestro.ModificadoPor);
            })
            .OrderByDescending(p => p.Promocion.VigenteDesde)
            .ThenBy(p => p.Promocion.Codigo, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ResultadoImportacionPromociones> ImportarAsync(SolicitudImportacionPromociones solicitud, UsuarioAuditoria actor, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        static ResultadoImportacionPromociones Fallo(string mensaje) => new(0, 0, 0, false, [new ErrorImportacionCentral(0, mensaje)]);

        if (string.IsNullOrWhiteSpace(solicitud.Contenido))
            return Fallo("El archivo está vacío.");
        if (solicitud.Contenido.Length > LargoMaximoArchivo)
            return Fallo("El archivo supera los 5 MB.");
        if (solicitud.DesplazamientoMinutos is < -14 * 60 or > 14 * 60)
            return Fallo("La zona horaria indicada no es válida.");

        var lineas = solicitud.Contenido.TrimStart('﻿').Replace("\r\n", "\n").Split('\n');
        var separador = LectorCsv.DetectarSeparador(lineas[0]);
        var columnas = LectorCsv.DividirLinea(lineas[0], separador)
            .Select((nombre, indice) => (Nombre: nombre.Trim().ToLowerInvariant(), Indice: indice))
            .GroupBy(c => c.Nombre)
            .ToDictionary(g => g.Key, g => g.First().Indice);

        var faltantes = ColumnasObligatorias.Where(c => !columnas.ContainsKey(c)).ToList();
        if (faltantes.Count > 0)
            return Fallo($"Faltan columnas obligatorias: {string.Join(", ", faltantes)}.");

        var filas = lineas
            .Select((texto, indice) => (Numero: indice + 1, Texto: texto))
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
            .Select(l => (l.Numero, Campos: LectorCsv.DividirLinea(l.Texto, separador)))
            .ToList();
        if (filas.Count == 0)
            return Fallo("El archivo no tiene promociones.");

        string? Valor(string[] campos, string columna) =>
            columnas.TryGetValue(columna, out var indice) && indice < campos.Length && !string.IsNullOrWhiteSpace(campos[indice]) ? campos[indice].Trim() : null;

        var articulos = await IdsPorCodigoAsync(TipoMaestro.Articulo, filas.SelectMany(f => Lista(Valor(f.Campos, "articulos"))), cancelacion);
        var familias = await IdsPorCodigoAsync(TipoMaestro.Familia, filas.SelectMany(f => Lista(Valor(f.Campos, "familias"))), cancelacion);
        var existentes = await IdsPorCodigoAsync(TipoMaestro.Promocion, filas.Select(f => Valor(f.Campos, "codigo")).OfType<string>(), cancelacion);
        var sucursales = await contexto.Sucursales.AsNoTracking().ToDictionaryAsync(s => s.Codigo, s => s.Id, StringComparer.OrdinalIgnoreCase, cancelacion);

        var desplazamiento = TimeSpan.FromMinutes(solicitud.DesplazamientoMinutos);
        var errores = new List<ErrorImportacionCentral>();
        var promociones = new List<PromocionCarga>();
        var codigosEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int nuevas = 0, actualizadas = 0;

        foreach (var (numero, campos) in filas)
        {
            string? V(string columna) => Valor(campos, columna);
            try
            {
                var codigo = V("codigo") ?? throw new FormatException("Falta el código.");
                if (!codigosEnArchivo.Add(codigo))
                    throw new FormatException($"El código '{codigo}' está repetido en el archivo.");

                var tipo = LeerTipo(V("tipo"));
                var valor = LeerDecimal(V("valor"), "valor") ?? (tipo == TipoPromocion.LlevaPaga ? 0m : throw new FormatException("Falta el valor."));
                var articulosLinea = Lista(V("articulos")).Select(c => articulos.TryGetValue(c, out var id) ? id : throw new FormatException($"No existe el artículo '{c}'.")).ToList();
                var familiasLinea = Lista(V("familias")).Select(c => familias.TryGetValue(c, out var id) ? id : throw new FormatException($"No existe la familia '{c}'.")).ToList();
                var sucursalesLinea = Lista(V("sucursales")).Select(c => sucursales.TryGetValue(c, out var id) ? id : throw new FormatException($"No existe la sucursal '{c}'.")).ToList();
                if (articulosLinea.Count == 0 && familiasLinea.Count == 0)
                    throw new FormatException("La promoción no aplica a ningún artículo ni familia.");

                var promocion = new PromocionCarga(
                    existentes.TryGetValue(codigo, out var idExistente) ? idExistente : Guid.CreateVersion7(),
                    codigo,
                    V("nombre") ?? throw new FormatException("Falta el nombre."),
                    tipo,
                    valor,
                    LeerFecha(V("desde"), "desde", desplazamiento, finDelDia: false),
                    LeerFecha(V("hasta"), "hasta", desplazamiento, finDelDia: true),
                    articulosLinea,
                    familiasLinea,
                    sucursalesLinea,
                    LeerEntero(V("lleva"), "lleva"),
                    LeerEntero(V("paga"), "paga"),
                    LeerDecimal(V("cantidad_minima"), "cantidad_minima"),
                    LeerDecimal(V("limite_cliente"), "limite_cliente"),
                    LeerDias(V("dias")),
                    LeerHora(V("hora_desde"), "hora_desde"),
                    LeerHora(V("hora_hasta"), "hora_hasta"),
                    LeerBooleano(V("solo_fidelidad"), "solo_fidelidad") ?? false,
                    LeerBooleano(V("activa"), "activa") ?? true);

                ConversionMaestros.ConstruirPromocion(promocion);
                promociones.Add(promocion);
                if (existentes.ContainsKey(codigo)) actualizadas++; else nuevas++;
            }
            catch (Exception excepcion) when (excepcion is FormatException or ArgumentException or InvalidOperationException)
            {
                errores.Add(new ErrorImportacionCentral(numero, ValidacionMaestros.MensajeError(excepcion)));
            }
        }

        if (errores.Count > 0 || solicitud.SoloValidar)
            return new ResultadoImportacionPromociones(filas.Count, nuevas, actualizadas, false, errores);

        try
        {
            await publicador.PublicarAsync(new PaqueteMaestros(Promociones: promociones), actor.Nombre, cancelacion);
        }
        catch (PublicacionInvalidaExcepcion excepcion)
        {
            return new ResultadoImportacionPromociones(filas.Count, nuevas, actualizadas, false, excepcion.Errores.Select(e => new ErrorImportacionCentral(0, e)).ToList());
        }

        return new ResultadoImportacionPromociones(filas.Count, nuevas, actualizadas, true, []);
    }

    public async Task<ResultadoSimulacionPromociones?> SimularAsync(SolicitudSimulacionPromociones solicitud, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (await contexto.MaestrosCentral.AsNoTracking().SingleOrDefaultAsync(m => m.Tipo == TipoMaestro.Articulo && m.Id == solicitud.ArticuloId, cancelacion) is not { } fila)
            return null;

        var articulo = FormatoMaestros.Leer<ArticuloCarga>(fila);
        var cantidad = Math.Max(solicitud.Cantidad, 0m);
        var brutoDetalle = Redondear(cantidad * articulo.PrecioDetalle);

        // Como la caja: el mayor automático aplica desde su cantidad mínima y nunca a combos (RN-04).
        decimal? brutoMayor = articulo is { Tipo: not TipoArticulo.ComboKit, PrecioMayor: { } mayor, CantidadMinimaMayor: { } minima } && cantidad >= minima
            ? Redondear(cantidad * mayor)
            : null;
        var brutoSinOferta = brutoMayor is { } conMayor && conMayor < brutoDetalle ? conMayor : brutoDetalle;

        var promociones = (await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.Promocion).ToListAsync(cancelacion))
            .Select(FormatoMaestros.Leer<PromocionCarga>)
            .Select(ConstruirSiEsValida)
            .OfType<Promocion>()
            .Where(p => p.AplicaA(articulo.Id, articulo.FamiliaId))
            .ToList();

        var candidatas = promociones
            .Select(p =>
            {
                var motivo = !p.Activa ? "La promoción está inactiva."
                    : p.SoloFidelidad && !solicitud.ConFidelidad ? "Solo aplica a miembros del programa de fidelidad."
                    : !p.EstaVigente(solicitud.SucursalId, solicitud.Momento) ? "No está vigente en esa fecha, día, hora o sucursal."
                    : null;
                var descuento = motivo is null && cantidad > 0 ? MotorPromociones.CalcularDescuento(p, cantidad, articulo.PrecioDetalle, brutoDetalle) : 0m;
                if (motivo is null && descuento <= 0)
                    motivo = "No da descuento con esa cantidad.";
                return new DatosCandidataPromocion(p.Id, p.Codigo, p.Nombre, p.DescripcionCorta, motivo is null, motivo, descuento);
            })
            .OrderByDescending(c => c.Descuento)
            .ThenBy(c => c.Codigo, StringComparer.Ordinal)
            .ToList();

        var mejor = candidatas.FirstOrDefault(c => c.Aplica);
        Guid? ganadora = null;
        decimal total;
        string explicacion;
        if (mejor is null)
        {
            total = brutoSinOferta;
            explicacion = candidatas.Count == 0 ? "Ninguna promoción incluye este artículo o su familia." : "Ninguna promoción aplica en esas condiciones.";
        }
        else if (brutoDetalle - mejor.Descuento >= brutoSinOferta)
        {
            total = brutoSinOferta;
            explicacion = $"La oferta {mejor.Codigo} no mejora el precio por mayor: se cobra por mayor.";
        }
        else
        {
            ganadora = mejor.Id;
            total = brutoDetalle - mejor.Descuento;
            explicacion = $"Aplica {mejor.Codigo}: es la oferta más favorable.";
        }

        return new ResultadoSimulacionPromociones($"{articulo.Codigo} · {articulo.Descripcion}", cantidad, articulo.PrecioDetalle, brutoDetalle, brutoMayor,
            candidatas, ganadora, total, explicacion);
    }

    public async Task<IReadOnlyList<ArticuloCarga>> ArticulosPorIdAsync(IReadOnlyList<Guid> ids, CancellationToken cancelacion = default)
    {
        var buscar = ids.Distinct().Take(MaximoArticulosPorId).ToList();
        return (await contexto.MaestrosCentral.AsNoTracking().Where(m => m.Tipo == TipoMaestro.Articulo && buscar.Contains(m.Id)).ToListAsync(cancelacion))
            .Select(FormatoMaestros.Leer<ArticuloCarga>)
            .ToList();
    }

    private async Task<Dictionary<string, Guid>> IdsPorCodigoAsync(TipoMaestro tipo, IEnumerable<string> codigos, CancellationToken cancelacion)
    {
        var resultado = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var bloque in codigos.Select(c => c.Trim().ToUpperInvariant()).Distinct().Chunk(TamanoBloqueConsulta))
        {
            var lista = bloque.ToList();
            foreach (var fila in await contexto.MaestrosCentral.AsNoTracking()
                         .Where(m => m.Tipo == tipo && m.Codigo != null && lista.Contains(m.Codigo))
                         .Select(m => new { m.Codigo, m.Id })
                         .ToListAsync(cancelacion))
                resultado[fila.Codigo!] = fila.Id;
        }

        return resultado;
    }

    private static Promocion? ConstruirSiEsValida(PromocionCarga dato)
    {
        try
        {
            return ConversionMaestros.ConstruirPromocion(dato);
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Oferta(PromocionCarga dato) => ConstruirSiEsValida(dato)?.DescripcionCorta ?? dato.Nombre;

    private static decimal Redondear(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static IEnumerable<string> Lista(string? texto) =>
        (texto ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static TipoPromocion LeerTipo(string? texto)
    {
        var limpio = (texto ?? throw new FormatException("Falta el tipo.")).Replace("_", string.Empty).Replace(" ", string.Empty);
        return Enum.TryParse<TipoPromocion>(limpio, ignoreCase: true, out var tipo) && Enum.IsDefined(tipo) && !limpio.All(char.IsAsciiDigit)
            ? tipo
            : throw new FormatException($"Tipo de promoción desconocido: '{texto}'. Use porcentaje, monto_por_unidad, precio_especial, lleva_paga o precio_por_cantidad.");
    }

    private static decimal? LeerDecimal(string? texto, string columna)
    {
        if (texto is null)
            return null;

        var normalizado = texto.Contains('.') ? texto.Replace(",", string.Empty) : texto.Replace(',', '.');
        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw new FormatException($"'{texto}' no es un número válido en {columna}.");
    }

    private static int? LeerEntero(string? texto, string columna) =>
        texto is null ? null
        : int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor
        : throw new FormatException($"'{texto}' no es un número entero en {columna}.");

    /// <param name="finDelDia">Una fecha sin hora al final de la vigencia incluye todo ese día.</param>
    private static DateTimeOffset LeerFecha(string? texto, string columna, TimeSpan desplazamiento, bool finDelDia)
    {
        if (texto is null)
            throw new FormatException($"Falta la fecha {columna}.");
        if (!DateTime.TryParseExact(texto, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            throw new FormatException($"'{texto}' no es una fecha válida en {columna} (use aaaa-mm-dd o dd/mm/aaaa, con hora opcional).");

        if (finDelDia && !texto.Contains(':'))
            fecha = fecha.Date.AddDays(1).AddSeconds(-1);

        return new DateTimeOffset(DateTime.SpecifyKind(fecha, DateTimeKind.Unspecified), desplazamiento);
    }

    private static TimeOnly? LeerHora(string? texto, string columna) =>
        texto is null ? null
        : TimeOnly.TryParseExact(texto, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora) ? hora
        : throw new FormatException($"'{texto}' no es una hora válida en {columna} (use HH:mm).");

    private static DiasSemana LeerDias(string? texto)
    {
        if (texto is null || texto.Equals("todos", StringComparison.OrdinalIgnoreCase))
            return DiasSemana.Todos;

        var dias = DiasSemana.Ninguno;
        foreach (var dia in Lista(texto))
        {
            var clave = MaestroCentral.NormalizarBusqueda(dia) ?? string.Empty;
            dias |= (clave.Length >= 3 ? clave[..3] : clave) switch
            {
                "lun" => DiasSemana.Lunes,
                "mar" => DiasSemana.Martes,
                "mie" => DiasSemana.Miercoles,
                "jue" => DiasSemana.Jueves,
                "vie" => DiasSemana.Viernes,
                "sab" => DiasSemana.Sabado,
                "dom" => DiasSemana.Domingo,
                _ => throw new FormatException($"Día desconocido: '{dia}'. Use todos, o lun|mar|mie|jue|vie|sab|dom."),
            };
        }

        return dias;
    }

    private static bool? LeerBooleano(string? texto, string columna) =>
        MaestroCentral.NormalizarBusqueda(texto) switch
        {
            null => null,
            "si" or "s" or "true" or "1" or "x" => true,
            "no" or "n" or "false" or "0" => false,
            _ => throw new FormatException($"'{texto}' no es sí o no en {columna}."),
        };
}
