/*
    CG-POS · Agregar «ModificadoEn» y «ModificadoPor» en «CgPosCentral»

    Estas tablas se administran a mano y no guardaban quién las cambió por última vez ni cuándo. Este archivo
    agrega las dos columnas a una base que ya está en uso; las bases nuevas ya las traen.

        sqlcmd -S .\SQLEXPRESS -E -d CgPosCentral -i agregar-modificado-en-por.sql

    Tablas: Empresas, Sucursales, Cajas, Parametros, UsuariosCentral, RolesCentral, ListasBoda.

    IMPORTANTE:
      · Cambia la estructura: haga un respaldo antes y ejecútelo con el sistema detenido.
      · Las filas que ya existen quedan con la fecha del momento y «Migración» como responsable: ese dato no se
        puede reconstruir hacia atrás. Lo que sí está desde siempre es la tabla Auditoria, con el detalle real.
      · Se puede volver a ejecutar: si las columnas ya están, no hace nada.
*/

USE [CgPosCentral];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'Empresas', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [Empresas] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  Empresas: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [Empresas] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Empresas') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [Empresas] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [Empresas] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO


IF COL_LENGTH(N'Sucursales', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [Sucursales] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  Sucursales: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [Sucursales] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Sucursales') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [Sucursales] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [Sucursales] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO


IF COL_LENGTH(N'Cajas', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [Cajas] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  Cajas: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [Cajas] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Cajas') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [Cajas] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [Cajas] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO


IF COL_LENGTH(N'Parametros', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [Parametros] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  Parametros: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [Parametros] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Parametros') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [Parametros] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [Parametros] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO


IF COL_LENGTH(N'UsuariosCentral', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [UsuariosCentral] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  UsuariosCentral: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [UsuariosCentral] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'UsuariosCentral') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [UsuariosCentral] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [UsuariosCentral] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO


IF COL_LENGTH(N'RolesCentral', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [RolesCentral] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  RolesCentral: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [RolesCentral] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'RolesCentral') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [RolesCentral] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [RolesCentral] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO


IF COL_LENGTH(N'ListasBoda', N'ModificadoEn') IS NULL
BEGIN
    ALTER TABLE [ListasBoda] ADD [ModificadoEn] datetimeoffset(3) NULL, [ModificadoPor] nvarchar(150) NULL;
    PRINT '  ListasBoda: columnas agregadas.';
END
GO

/* A lo que ya existe no se le puede saber quién lo hizo: queda con la fecha de hoy y «Migración». */
UPDATE [ListasBoda] SET [ModificadoEn] = SYSDATETIMEOFFSET(), [ModificadoPor] = N'Migración' WHERE [ModificadoEn] IS NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'ListasBoda') AND name = N'ModificadoEn' AND is_nullable = 1)
BEGIN
    ALTER TABLE [ListasBoda] ALTER COLUMN [ModificadoEn] datetimeoffset(3) NOT NULL;
    ALTER TABLE [ListasBoda] ALTER COLUMN [ModificadoPor] nvarchar(150) NOT NULL;
END
GO

PRINT 'Listo. Las tablas indicadas ya guardan quién las cambió y cuándo.';
GO
