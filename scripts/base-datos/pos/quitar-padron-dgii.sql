/*
    CG-POS · Quitar el padrón de la DGII de «CgPosCaja»

    La caja ya no maneja el padrón de contribuyentes: los clientes se administran en el Central y bajan como cualquier
    otro maestro. Este archivo elimina la tabla que lo guardaba, que ya no usa ninguna parte del sistema.

        sqlcmd -S .\SQLEXPRESS -E -d CgPosCaja -i quitar-padron-dgii.sql

    QUÉ HACE Y QUÉ NO:
      · Elimina la tabla ContribuyentesDgii y su secuencia de Id, si existen.
      · NO toca ninguna otra tabla, ni los clientes, ni los artículos, ni los documentos.
      · Se puede volver a ejecutar: si ya no están, no hace nada.

    OJO: esto sí borra una tabla y su contenido. Haga un respaldo antes y ejecútelo con la caja detenida. Lo que había
    ahí era una copia del archivo público de la DGII, así que no se pierde información propia del negocio.
*/

USE [CgPosCaja];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'dbo.ContribuyentesDgii', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ContribuyentesDgii];
    PRINT '  Tabla ContribuyentesDgii eliminada.';
END
ELSE
    PRINT '  La tabla ContribuyentesDgii ya no existe.';
GO

IF EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'SecuenciaContribuyentesDgii')
BEGIN
    DROP SEQUENCE [SecuenciaContribuyentesDgii];
    PRINT '  Secuencia SecuenciaContribuyentesDgii eliminada.';
END
GO

/* La marca de la última importación tampoco tiene sentido ya. */
DELETE FROM [MarcasSincronizacion] WHERE [Clave] = N'Padron.Dgii';
GO

PRINT 'Listo. La caja ya no guarda el padrón: los clientes bajan del Central.';
GO
