using CgPos.Central.Aplicacion.Organizacion;
using CgPos.Central.Infraestructura.Organizacion;
using System.Globalization;
using CgPos.Central.Aplicacion.Abstracciones;
using CgPos.Central.Aplicacion.Maestros;
using CgPos.Central.Aplicacion.Sincronizacion;
using CgPos.Central.Infraestructura.Persistencia;
using CgPos.Contratos.Catalogo;
using CgPos.Contratos.Central;
using CgPos.Contratos.Importacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Comun;
using CgPos.Dominio.Promociones;
using Microsoft.EntityFrameworkCore;

namespace CgPos.Central.Infraestructura.Maestros;

internal sealed class ServicioPromocionesCentral(ContextoDatosCentral contexto, IPublicadorMaestros publicador, INumeracionCentral numeracion)
    : IServicioPromocionesCentral
{
    private const int LargoMaximoArchivo = 5 * 1024 * 1024;
    private const int MaximoArticulosPorId = 500;
    // El código no es obligatorio: vacío crea una promoción nueva con el número de su secuencia; lleno actualiza esa promoción.
    private static readonly string[] ColumnasObligatorias = ["nombre", "tipo", "desde", "hasta"];

    /// <summary>Código provisional de una línea nueva mientras se valida; el de verdad se toma de la secuencia al publicar.</summary>
    private const string PrefijoProvisional = "#NUEVA";
    private static readonly string[] FormatosFecha = ["yyyy-MM-dd", "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm", "dd/MM/yyyy", "dd/MM/yyyy HH:mm", "dd/MM/yyyy H:mm"];

    public async Task<IReadOnlyList<DatosPromocionCentral>> ListarAsync(CancellationToken cancelacion = default)
    {
        var promociones = await TablasMaestros.Promociones.TodosAsync(contexto, new ResolutorCodigosCentral(contexto), cancelacion);
        var entidades = await contexto.Promociones.AsNoTracking()
            .Select(p => new { Promocion = p, Version = EF.Property<long>(p, ContextoDatosCentral.ColumnaVersion) })
            .ToDictionaryAsync(p => p.Promocion.Codigo, cancelacion);
        var cajas = await contexto.Cajas.AsNoTracking().Where(c => c.Habilitada).Select(c => new { c.Id, c.SucursalId }).ToListAsync(cancelacion);
        var confirmadas = await contexto.EstadosSincronizacionCaja.AsNoTracking().ToDictionaryAsync(e => e.CajaId, e => e.VersionMaestrosConfirmada, cancelacion);

        return promociones
            .Select(fila =>
            {
                var entidad = entidades[fila.Dato.Codigo];
                var destino = cajas.Where(c => entidad.Promocion.Sucursales.Count == 0 || entidad.Promocion.Sucursales.Contains(c.SucursalId)).ToList();
                var conPromocion = destino.Count(c => confirmadas.GetValueOrDefault(c.Id) >= entidad.Version);
                return new DatosPromocionCentral(fila.Dato, entidad.Promocion.DescripcionCorta, destino.Count, conPromocion, fila.ModificadoEn, fila.ModificadoPor);
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

        // Cada línea se construye con las referencias publicadas: un artículo, departamento o sucursal que no exista se informa en su línea.
        var resolutor = new ResolutorCodigosCentral(contexto);
        await resolutor.PrepararAsync(cancelacion);
        await resolutor.CargarArticulosAsync(filas.SelectMany(f => Lista(Valor(f.Campos, "articulos"))), null, cancelacion);
        var codigosArchivo = filas.Select(f => Valor(f.Campos, "codigo")?.ToUpperInvariant()).OfType<string>().ToList();
        var existentes = (await contexto.Promociones.AsNoTracking().Where(p => codigosArchivo.Contains(p.Codigo)).Select(p => p.Codigo).ToListAsync(cancelacion))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                // Sin código es nueva: toma el número de la secuencia al publicar. Con código actualiza esa promoción, que tiene
                // que existir: así ningún código queda fuera de la secuencia.
                var codigo = V("codigo")?.ToUpperInvariant() ?? $"{PrefijoProvisional}{numero}";
                if (!codigo.StartsWith(PrefijoProvisional, StringComparison.Ordinal))
                {
                    if (!codigosEnArchivo.Add(codigo))
                        throw new FormatException($"El código '{codigo}' está repetido en el archivo.");
                    if (!existentes.Contains(codigo))
                        throw new FormatException($"La promoción '{codigo}' no existe. Para crear una nueva, deje el código vacío: se le asigna el de la secuencia.");
                }

                var tipo = LeerTipo(V("tipo"));
                var valor = LeerDecimal(V("valor"), "valor") ?? (tipo == TipoPromocion.LlevaPaga ? 0m : throw new FormatException("Falta el valor."));
                var articulosLinea = Lista(V("articulos")).ToList();
                var departamentosLinea = Numeros(V("departamentos"), "departamentos");
                // Los códigos de sucursal viajan como texto de dos dígitos, igual que en el resto del sistema.
                var sucursalesLinea = Numeros(V("sucursales"), "sucursales")
                    .Select(s => s.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).ToList();
                var categoriasLinea = Numeros(V("categorias"), "categorias");
                var marcasLinea = Numeros(V("marcas"), "marcas");
                if (articulosLinea.Count == 0 && departamentosLinea.Count == 0 && categoriasLinea.Count == 0 && marcasLinea.Count == 0)
                    throw new FormatException("La promoción no aplica a ningún artículo, departamento, categoría ni marca.");

                var promocion = new PromocionCarga(
                    codigo,
                    V("nombre") ?? throw new FormatException("Falta el nombre."),
                    tipo,
                    valor,
                    LeerFecha(V("desde"), "desde", desplazamiento, finDelDia: false),
                    LeerFecha(V("hasta"), "hasta", desplazamiento, finDelDia: true),
                    articulosLinea,
                    departamentosLinea,
                    sucursalesLinea,
                    LeerEntero(V("lleva"), "lleva"),
                    LeerEntero(V("paga"), "paga"),
                    LeerDecimal(V("cantidad_minima"), "cantidad_minima"),
                    LeerDecimal(V("limite_cliente"), "limite_cliente"),
                    LeerDias(V("dias")),
                    LeerHora(V("hora_desde"), "hora_desde"),
                    LeerHora(V("hora_hasta"), "hora_hasta"),
                    LeerBooleano(V("solo_fidelidad"), "solo_fidelidad") ?? false,
                    LeerBooleano(V("activa"), "activa") ?? true,
                    categoriasLinea,
                    marcasLinea);

                MapeoMaestros.Crear(promocion, resolutor);
                promociones.Add(promocion);
                if (existentes.Contains(codigo)) actualizadas++; else nuevas++;
            }
            catch (Exception excepcion) when (excepcion is FormatException or ArgumentException or InvalidOperationException)
            {
                errores.Add(new ErrorImportacionCentral(numero, ValidacionMaestros.MensajeError(excepcion)));
            }
        }

        if (errores.Count > 0 || solicitud.SoloValidar)
            return new ResultadoImportacionPromociones(filas.Count, nuevas, actualizadas, false, errores);

        // Solo con el archivo ya válido se piden los números: una validación fallida no gasta la secuencia.
        try
        {
            for (var i = 0; i < promociones.Count; i++)
                if (promociones[i].Codigo.StartsWith(PrefijoProvisional, StringComparison.Ordinal))
                    promociones[i] = promociones[i] with { Codigo = await numeracion.SiguienteAsync(DocumentosNumerados.Promocion, cancelacion) };
        }
        catch (SecuenciaCentralNoConfiguradaExcepcion excepcion)
        {
            return Fallo(excepcion.Message);
        }

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
        var codigo = solicitud.ArticuloCodigo?.Trim() ?? string.Empty;
        var entidad = await contexto.Articulos.AsNoTracking().SingleOrDefaultAsync(a => a.Codigo == codigo, cancelacion);
        if (entidad is null || (await TablasMaestros.Articulos.PorIdAsync(contexto, new ResolutorCodigosCentral(contexto), entidad.Id, cancelacion))?.Dato is not { } articulo)
            return null;
        var cantidad = Math.Max(solicitud.Cantidad, 0m);
        var brutoDetalle = Redondear(cantidad * articulo.PrecioDetalle);

        // Como la caja: el mayor automático aplica desde su cantidad mínima y nunca a combos (RN-04).
        decimal? brutoMayor = articulo is { Tipo: not TipoArticulo.ComboKit, PrecioMayor: { } mayor, CantidadMinimaMayor: { } minima } && cantidad >= minima
            ? Redondear(cantidad * mayor)
            : null;
        var brutoSinOferta = brutoMayor is { } conMayor && conMayor < brutoDetalle ? conMayor : brutoDetalle;

        var promociones = (await contexto.Promociones.AsNoTracking().ToListAsync(cancelacion))
            .Where(p => p.AplicaA(entidad.Id, entidad.DepartamentoId, entidad.CategoriaId, entidad.MarcaId))
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
        int? ganadora = null;
        decimal total;
        string explicacion;
        if (mejor is null)
        {
            total = brutoSinOferta;
            explicacion = candidatas.Count == 0
                ? "Ninguna promoción incluye este artículo, su departamento, su categoría ni su marca."
                : "Ninguna promoción aplica en esas condiciones.";
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

    public async Task<IReadOnlyList<ArticuloCarga>> ArticulosPorCodigoAsync(IReadOnlyList<string> codigos, CancellationToken cancelacion = default)
    {
        var buscar = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().Take(MaximoArticulosPorId).ToList();
        return (await TablasMaestros.Articulos.ListarAsync(contexto, new ResolutorCodigosCentral(contexto), cancelacion, a => buscar.Contains(a.Codigo)))
            .Select(a => a.Dato)
            .ToList();
    }

    /// <summary>Códigos numéricos (departamentos, categorías, marcas, sucursales) separados por |.</summary>
    private static List<int> Numeros(string? texto, string columna) =>
        Lista(texto).Select(c => int.TryParse(c, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : throw new FormatException($"'{c}' no es un código numérico en {columna}.")).ToList();

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
            var clave = TextoBusqueda.Normalizar(dia) ?? string.Empty;
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
        TextoBusqueda.Normalizar(texto) switch
        {
            null => null,
            "si" or "s" or "true" or "1" or "x" => true,
            "no" or "n" or "false" or "0" => false,
            _ => throw new FormatException($"'{texto}' no es sí o no en {columna}."),
        };
}
