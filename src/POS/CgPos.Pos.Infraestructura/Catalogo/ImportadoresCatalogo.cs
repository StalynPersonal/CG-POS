using System.Data;
using System.Globalization;
using System.Text;
using CgPos.Contratos.Importacion;
using CgPos.Dominio.Catalogo;
using CgPos.Dominio.Fiscal;
using CgPos.Pos.Aplicacion.Abstracciones;
using CgPos.Pos.Aplicacion.Catalogo;
using CgPos.Pos.Infraestructura.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CgPos.Pos.Infraestructura.Catalogo;

internal sealed class ImportadorArticulosCsv(ContextoDatosPos contexto, IAuditoria auditoria, TimeProvider reloj) : IImportadorArticulos
{
    private static readonly string[] ColumnasObligatorias = ["codigo", "descripcion", "familia", "unidad", "impuesto", "precio_detalle"];

    public async Task<ResultadoImportacionArticulos> ImportarCsvAsync(Stream contenido, string origen, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        ArgumentException.ThrowIfNullOrWhiteSpace(origen);

        using var lector = new StreamReader(contenido, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var encabezado = await lector.ReadLineAsync(cancelacion)
            ?? throw new CargaMaestrosInvalidaExcepcion(["El archivo de artículos está vacío."]);

        var separador = LectorCsv.DetectarSeparador(encabezado);
        var columnas = LectorCsv.DividirLinea(encabezado, separador)
            .Select((nombre, indice) => (Nombre: nombre.Trim().ToLowerInvariant(), Indice: indice))
            .GroupBy(c => c.Nombre)
            .ToDictionary(g => g.Key, g => g.First().Indice);

        var faltantes = ColumnasObligatorias.Where(c => !columnas.ContainsKey(c)).ToList();
        if (faltantes.Count > 0)
            throw new CargaMaestrosInvalidaExcepcion([$"Faltan columnas obligatorias: {string.Join(", ", faltantes)}."]);

        var familias = await contexto.Familias.ToDictionaryAsync(f => f.Codigo, f => f.Id, StringComparer.OrdinalIgnoreCase, cancelacion);
        var unidades = await contexto.UnidadesMedida.ToDictionaryAsync(u => u.Codigo, u => u.Id, StringComparer.OrdinalIgnoreCase, cancelacion);
        var impuestos = await contexto.Impuestos.ToDictionaryAsync(i => i.Codigo, i => i.Id, StringComparer.OrdinalIgnoreCase, cancelacion);

        var ahora = reloj.GetUtcNow();
        var errores = new List<ErrorImportacion>();
        var codigosEnArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int creados = 0, actualizados = 0, precios = 0, numeroLinea = 1;

        while (await lector.ReadLineAsync(cancelacion) is { } linea)
        {
            numeroLinea++;
            if (string.IsNullOrWhiteSpace(linea))
                continue;

            var campos = LectorCsv.DividirLinea(linea, separador);
            string? Valor(string columna) =>
                columnas.TryGetValue(columna, out var indice) && indice < campos.Length && !string.IsNullOrWhiteSpace(campos[indice])
                    ? campos[indice].Trim()
                    : null;

            try
            {
                var codigo = Valor("codigo") ?? throw new FormatException("Falta el código.");
                if (!codigosEnArchivo.Add(codigo))
                    throw new FormatException($"El código '{codigo}' está repetido en el archivo.");

                var descripcion = Valor("descripcion") ?? throw new FormatException("Falta la descripción.");
                var familiaId = BuscarId(familias, Valor("familia"), "familia");
                var unidadId = BuscarId(unidades, Valor("unidad"), "unidad de medida");
                var impuestoId = BuscarId(impuestos, Valor("impuesto"), "impuesto");
                var precioDetalle = LeerDecimal(Valor("precio_detalle"), "precio_detalle") ?? throw new FormatException("Falta el precio detalle.");
                var precioMayor = LeerDecimal(Valor("precio_mayor"), "precio_mayor");
                var tipo = Valor("tipo") is { } textoTipo
                    ? (Enum.TryParse<TipoArticulo>(textoTipo, ignoreCase: true, out var tipoLeido) && Enum.IsDefined(tipoLeido)
                        ? tipoLeido
                        : throw new FormatException($"Tipo de artículo desconocido: '{textoTipo}'."))
                    : TipoArticulo.Normal;
                var costo = LeerDecimal(Valor("costo"), "costo");
                var precioMinimo = LeerDecimal(Valor("precio_minimo"), "precio_minimo");
                var cantidadMinimaMayor = LeerDecimal(Valor("cantidad_minima_mayor"), "cantidad_minima_mayor");
                var codigos = Lista(Valor("codigos_barras")).Select(c => (c, TipoCodigoArticulo.Barras))
                    .Concat(Lista(Valor("codigos_proveedor")).Select(c => (c, TipoCodigoArticulo.Proveedor)))
                    .ToList();
                var activo = LeerBooleano(Valor("activo"), "activo") ?? true;
                var mostrarEnCatalogo = LeerBooleano(Valor("mostrar_en_catalogo"), "mostrar_en_catalogo") ?? false;

                if (precioDetalle <= 0 || precioMayor <= 0)
                    throw new FormatException("Los precios deben ser mayores que cero.");

                // Se valida todo contra un artículo de prueba antes de tocar el real: una línea con error no deja cambios a medias.
                var prueba = Articulo.Crear(codigo, descripcion, familiaId, unidadId, impuestoId, tipo);
                prueba.ActualizarDatos(descripcion, Valor("referencia"), familiaId, unidadId, impuestoId, tipo);
                prueba.ConfigurarPrecios(costo, precioMinimo, cantidadMinimaMayor);
                prueba.ConfigurarPresentacion(Valor("ruta_imagen"), mostrarEnCatalogo, ventaEnPos: true);
                prueba.ReemplazarCodigos(codigos);

                var articulo = await contexto.Articulos.Include(a => a.Codigos).SingleOrDefaultAsync(a => a.Codigo == codigo, cancelacion);
                var idArticulo = articulo?.Id ?? prueba.Id;

                var codigosLinea = prueba.Codigos.Select(c => c.Codigo).ToList();
                var enOtroArticulo = await contexto.Set<CodigoArticulo>()
                    .Where(c => codigosLinea.Contains(c.Codigo) && c.ArticuloId != idArticulo)
                    .Select(c => c.Codigo)
                    .FirstOrDefaultAsync(cancelacion);
                if (enOtroArticulo is not null)
                    throw new FormatException($"El código '{enOtroArticulo}' ya pertenece a otro artículo.");

                if (articulo is null)
                {
                    articulo = prueba;
                    contexto.Articulos.Add(articulo);
                    creados++;
                }
                else
                {
                    articulo.ActualizarDatos(descripcion, Valor("referencia"), familiaId, unidadId, impuestoId, tipo);
                    articulo.ConfigurarPrecios(costo, precioMinimo, cantidadMinimaMayor);
                    articulo.ConfigurarPresentacion(Valor("ruta_imagen"), mostrarEnCatalogo, articulo.VentaEnPos);
                    articulo.ReemplazarCodigos(codigos);
                    actualizados++;
                }

                if (activo) articulo.Activar(); else articulo.Desactivar();

                if (await RegistroPrecios.RegistrarSiCambiaAsync(contexto, articulo.Id, ListaPrecio.Detalle, precioDetalle, ahora, ahora, origen, null, null, cancelacion))
                    precios++;
                if (precioMayor is { } mayor
                    && await RegistroPrecios.RegistrarSiCambiaAsync(contexto, articulo.Id, ListaPrecio.Mayor, mayor, ahora, ahora, origen, null, null, cancelacion))
                    precios++;
            }
            catch (Exception excepcion) when (excepcion is FormatException or ArgumentException)
            {
                errores.Add(new ErrorImportacion(numeroLinea, excepcion.Message));
            }
        }

        auditoria.Registrar(new EntradaAuditoria("Catalogo.ImportacionArticulos", "Maestros",
            Detalle: new { Origen = origen, Creados = creados, Actualizados = actualizados, Precios = precios, Errores = errores.Count }));
        await contexto.SaveChangesAsync(cancelacion);

        return new ResultadoImportacionArticulos(creados, actualizados, precios, errores);
    }

    private static Guid BuscarId(Dictionary<string, Guid> ids, string? codigo, string nombre) =>
        codigo is not null && ids.TryGetValue(codigo, out var id)
            ? id
            : throw new FormatException(codigo is null ? $"Falta la {nombre}." : $"No existe la {nombre} '{codigo}'.");

    private static decimal? LeerDecimal(string? valor, string columna) =>
        valor is null
            ? null
            // Sin separador de miles: "10,50" debe rechazarse, no leerse como 1050.
            : decimal.TryParse(valor, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var numero)
                ? numero
                : throw new FormatException($"Valor numérico inválido en '{columna}': '{valor}' (use punto decimal).");

    private static bool? LeerBooleano(string? valor, string columna) =>
        valor?.ToLowerInvariant() switch
        {
            null => null,
            "1" or "si" or "sí" or "true" or "verdadero" => true,
            "0" or "no" or "false" or "falso" => false,
            _ => throw new FormatException($"Valor lógico inválido en '{columna}': '{valor}'."),
        };

    private static IEnumerable<string> Lista(string? valor) =>
        (valor ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

internal sealed class ImportadorPadronDgii(ContextoDatosPos contexto, TimeProvider reloj) : IImportadorPadronDgii
{
    private const int TamanoLote = 50_000;

    public async Task<ResultadoImportacionPadron> ImportarAsync(Stream contenido, CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        // El archivo de la DGII suele venir en Latin-1; si trae BOM se respeta su codificación.
        using var lector = new StreamReader(contenido, Encoding.Latin1, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        await contexto.Database.OpenConnectionAsync(cancelacion);
        try
        {
            var conexion = (SqlConnection)contexto.Database.GetDbConnection();
            await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
            var transaccionSql = (SqlTransaction)transaccion.GetDbTransaction();

            await EjecutarAsync(conexion, transaccionSql, """
                CREATE TABLE #PadronDgii (
                    Fila int NOT NULL,
                    Documento varchar(11) NOT NULL,
                    RazonSocial nvarchar(250) NOT NULL,
                    NombreComercial nvarchar(250) NULL,
                    Estado nvarchar(50) NULL,
                    RegimenPago nvarchar(50) NULL)
                """, cancelacion);

            var tabla = CrearTablaLote();
            int leidas = 0, validas = 0;

            while (await lector.ReadLineAsync(cancelacion) is { } linea)
            {
                leidas++;
                if (!TryLeerRegistro(linea, out var registro))
                    continue;

                validas++;
                tabla.Rows.Add(validas, registro.Documento, registro.RazonSocial, registro.NombreComercial, registro.Estado, registro.RegimenPago);

                if (tabla.Rows.Count >= TamanoLote)
                {
                    await CopiarLoteAsync(conexion, transaccionSql, tabla, cancelacion);
                    tabla.Clear();
                }
            }

            if (tabla.Rows.Count > 0)
                await CopiarLoteAsync(conexion, transaccionSql, tabla, cancelacion);

            await using (var fusion = new SqlCommand("""
                MERGE ContribuyentesDgii WITH (HOLDLOCK) AS destino
                USING (
                    SELECT Documento, RazonSocial, NombreComercial, Estado, RegimenPago
                    FROM (SELECT *, ROW_NUMBER() OVER (PARTITION BY Documento ORDER BY Fila DESC) AS Orden FROM #PadronDgii) AS lecturas
                    WHERE Orden = 1
                ) AS origen
                ON destino.Documento = origen.Documento
                WHEN MATCHED AND (
                        destino.RazonSocial <> origen.RazonSocial
                        OR ISNULL(destino.NombreComercial, N'') <> ISNULL(origen.NombreComercial, N'')
                        OR ISNULL(destino.Estado, N'') <> ISNULL(origen.Estado, N'')
                        OR ISNULL(destino.RegimenPago, N'') <> ISNULL(origen.RegimenPago, N''))
                    THEN UPDATE SET RazonSocial = origen.RazonSocial, NombreComercial = origen.NombreComercial,
                                    Estado = origen.Estado, RegimenPago = origen.RegimenPago, ActualizadoEn = @ahora
                WHEN NOT MATCHED THEN
                    INSERT (Documento, RazonSocial, NombreComercial, Estado, RegimenPago, ActualizadoEn)
                    VALUES (origen.Documento, origen.RazonSocial, origen.NombreComercial, origen.Estado, origen.RegimenPago, @ahora);
                """, conexion, transaccionSql) { CommandTimeout = 600 })
            {
                fusion.Parameters.Add(new SqlParameter("@ahora", SqlDbType.DateTimeOffset) { Value = reloj.GetUtcNow() });
                await fusion.ExecuteNonQueryAsync(cancelacion);
            }

            await EjecutarAsync(conexion, transaccionSql, "DROP TABLE #PadronDgii", cancelacion);
            await transaccion.CommitAsync(cancelacion);

            return new ResultadoImportacionPadron(leidas, validas, leidas - validas);
        }
        finally
        {
            await contexto.Database.CloseConnectionAsync();
        }
    }

    private sealed record RegistroPadron(string Documento, string RazonSocial, string? NombreComercial, string? Estado, string? RegimenPago);

    private static bool TryLeerRegistro(string linea, out RegistroPadron registro)
    {
        registro = null!;
        var partes = linea.Split('|');
        if (partes.Length < 2)
            return false;

        var documento = DocumentoIdentidad.Normalizar(partes[0]);
        var razonSocial = Recortar(partes[1], ContribuyenteDgii.LargoMaximoNombre);
        if (documento.Length is not (DocumentoIdentidad.LargoRnc or DocumentoIdentidad.LargoCedula) || !documento.All(char.IsAsciiDigit) || razonSocial is null)
            return false;

        // Formato DGII: …|ESTADO|RÉGIMEN DE PAGO en las dos últimas columnas.
        var tieneEstado = partes.Length >= 5;
        registro = new RegistroPadron(
            documento,
            razonSocial,
            partes.Length > 2 ? Recortar(partes[2], ContribuyenteDgii.LargoMaximoNombre) : null,
            tieneEstado ? Recortar(partes[^2], ContribuyenteDgii.LargoMaximoEstado) : null,
            tieneEstado ? Recortar(partes[^1], ContribuyenteDgii.LargoMaximoEstado) : null);
        return true;
    }

    private static string? Recortar(string valor, int largoMaximo)
    {
        var limpio = valor.Trim();
        return limpio.Length == 0 ? null : limpio.Length <= largoMaximo ? limpio : limpio[..largoMaximo];
    }

    private static DataTable CrearTablaLote()
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Fila", typeof(int));
        tabla.Columns.Add("Documento", typeof(string));
        tabla.Columns.Add("RazonSocial", typeof(string));
        tabla.Columns.Add("NombreComercial", typeof(string));
        tabla.Columns.Add("Estado", typeof(string));
        tabla.Columns.Add("RegimenPago", typeof(string));
        return tabla;
    }

    private static async Task CopiarLoteAsync(SqlConnection conexion, SqlTransaction transaccion, DataTable tabla, CancellationToken cancelacion)
    {
        using var copia = new SqlBulkCopy(conexion, SqlBulkCopyOptions.Default, transaccion)
        {
            DestinationTableName = "#PadronDgii",
            BatchSize = TamanoLote,
            BulkCopyTimeout = 600,
        };
        foreach (DataColumn columna in tabla.Columns)
            copia.ColumnMappings.Add(columna.ColumnName, columna.ColumnName);

        await copia.WriteToServerAsync(tabla, cancelacion);
    }

    private static async Task EjecutarAsync(SqlConnection conexion, SqlTransaction transaccion, string sql, CancellationToken cancelacion)
    {
        await using var comando = new SqlCommand(sql, conexion, transaccion);
        await comando.ExecuteNonQueryAsync(cancelacion);
    }
}
