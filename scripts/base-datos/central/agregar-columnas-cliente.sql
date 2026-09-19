/*
    CG-POS · Agregar «Contacto» y «TelefonoAlterno» a los clientes de «CgPosCentral»

        sqlcmd -S .\SQLEXPRESS -E -d CgPosCentral -i agregar-columnas-cliente.sql

    QUÉ HACE Y QUÉ NO:
      · Solo ejecuta ALTER TABLE [Clientes] ADD con las dos columnas nuevas, que quedan vacías.
      · NO borra ni modifica ninguna fila ni ninguna otra columna. No hay DELETE, DROP ni TRUNCATE.
      · Se puede volver a ejecutar: si las columnas ya están, no hace nada.
*/

USE [CgPosCentral];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'Clientes', N'Contacto') IS NULL
BEGIN
    ALTER TABLE [Clientes] ADD [Contacto] nvarchar(150) NULL;
    PRINT '  Clientes.Contacto agregada.';
END
GO

IF COL_LENGTH(N'Clientes', N'TelefonoAlterno') IS NULL
BEGIN
    ALTER TABLE [Clientes] ADD [TelefonoAlterno] nvarchar(20) NULL;
    PRINT '  Clientes.TelefonoAlterno agregada.';
END
GO

PRINT 'Listo. El cliente ya guarda su contacto y un segundo teléfono.';
GO
