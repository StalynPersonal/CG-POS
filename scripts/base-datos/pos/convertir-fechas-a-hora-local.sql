/*
    CG-POS · Pasar a hora local las fechas guardadas en UTC de «CgPosCaja»

    Hasta este cambio las fechas se guardaban en UTC (11:41 de la mañana se veía como 15:41 +00:00). Ahora se
    guardan en la hora de aquí con su desfase (11:41 -04:00). Este archivo convierte lo que ya estaba guardado,
    para que todo se lea igual.

        sqlcmd -S .\SQLEXPRESS -E -d CgPosCaja -i convertir-fechas-a-hora-local.sql

    IMPORTANTE:
      · Modifica datos: haga un respaldo de la base antes de ejecutarlo.
      · Hágalo con el sistema detenido (Central y cajas), para que nadie escriba mientras convierte.
      · NO cambia el momento real de cada operación, solo el desfase con que se muestra: una venta de las
        11:41 de la mañana se seguía leyendo como las 11:41 de la mañana.
      · Solo convierte lo que aún esté en +00:00, así que volver a ejecutarlo no daña nada.
*/

USE [CgPosCaja];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;

DECLARE @tabla sysname, @columna sysname, @sql nvarchar(max), @filas int, @total int = 0;

DECLARE columnas CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name, c.name
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    JOIN sys.types y ON y.user_type_id = c.user_type_id
    WHERE y.name = 'datetimeoffset' AND t.is_ms_shipped = 0
    ORDER BY t.name, c.name;

OPEN columnas;
FETCH NEXT FROM columnas INTO @tabla, @columna;

WHILE @@FETCH_STATUS = 0
BEGIN
    /* SWITCHOFFSET conserva el instante: solo cambia el desfase con que queda expresado. */
    SET @sql = N'UPDATE [' + @tabla + N'] SET [' + @columna + N'] = SWITCHOFFSET([' + @columna + N'], ''-04:00'')'
             + N' WHERE [' + @columna + N'] IS NOT NULL AND DATEPART(TZoffset, [' + @columna + N']) <> -240;';
    EXEC sp_executesql @sql;
    SET @filas = @@ROWCOUNT;
    SET @total = @total + @filas;
    IF @filas > 0
        PRINT '  ' + @tabla + '.' + @columna + ': ' + CAST(@filas AS varchar(20)) + ' fila(s).';

    FETCH NEXT FROM columnas INTO @tabla, @columna;
END

CLOSE columnas;
DEALLOCATE columnas;

PRINT 'Listo. Fechas convertidas a hora local: ' + CAST(@total AS varchar(20)) + '.';
GO
