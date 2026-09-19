/*
    CG-POS · Quitar las vistas de hora local de «CgPosCentral»

    Las fechas ahora se guardan en la hora del negocio (UTC-4) con su desfase, así que las vistas del esquema
    «local» y la función dbo.HoraRd ya no hacen falta. Este archivo las elimina si existen.

        sqlcmd -S .\SQLEXPRESS -E -d CgPosCentral -i quitar-vistas-hora-local.sql

    No borra ni modifica ninguna tabla ni ningún dato: solo esas vistas y esa función.
    Si nunca se crearon, el archivo no hace nada y no da error.
*/

USE [CgPosCentral];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @sql nvarchar(max) = N'';

SELECT @sql = @sql + N'DROP VIEW [local].[' + v.name + N'];' + CHAR(10)
FROM sys.views v
JOIN sys.schemas s ON s.schema_id = v.schema_id
WHERE s.name = N'local';

IF LEN(@sql) > 0
    EXEC sp_executesql @sql;
GO

IF OBJECT_ID(N'dbo.HoraRd', N'FN') IS NOT NULL
    DROP FUNCTION dbo.HoraRd;
GO

IF SCHEMA_ID(N'local') IS NOT NULL
    EXEC (N'DROP SCHEMA [local]');
GO

PRINT 'Vistas de hora local eliminadas. Las tablas y sus datos no se tocaron.';
GO
