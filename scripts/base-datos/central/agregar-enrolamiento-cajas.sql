/*
    CG-POS · Enrolamiento de cajas en «CgPosCentral»

    Desde ahora una caja no se configura copiándole un secreto: pide entrar al Central y alguien la acepta. Al aceptarla,
    su credencial queda atada al equipo que la pidió, así que la misma credencial no sirve en otra máquina.

        sqlcmd -S .\SQLEXPRESS -E -d CgPosCentral -i agregar-enrolamiento-cajas.sql

    QUÉ HACE Y QUÉ NO:
      · Agrega tres columnas a [CredencialesDispositivo], que quedan vacías: las credenciales que ya existen siguen
        funcionando y se atan al equipo la primera vez que la caja se conecte.
      · Crea la tabla [SolicitudesEnrolamiento] y su secuencia de Id.
      · NO borra ni modifica ninguna fila. No hay DELETE, DROP ni TRUNCATE.
      · Se puede volver a ejecutar: lo que ya esté hecho no se repite.
*/

USE [CgPosCentral];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'CredencialesDispositivo', N'HuellaEquipo') IS NULL
BEGIN
    ALTER TABLE [CredencialesDispositivo] ADD [HuellaEquipo] char(64) NULL, [NombreEquipo] nvarchar(100) NULL, [EquipoFijadoEn] datetimeoffset(3) NULL;
    PRINT '  CredencialesDispositivo: columnas del equipo agregadas.';
END
ELSE
    PRINT '  CredencialesDispositivo ya tenía las columnas del equipo.';
GO

IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'SecuenciaSolicitudesEnrolamiento')
    CREATE SEQUENCE [SecuenciaSolicitudesEnrolamiento] START WITH 1 INCREMENT BY 1 NO CYCLE;
GO

IF OBJECT_ID(N'dbo.SolicitudesEnrolamiento', N'U') IS NULL
BEGIN
    CREATE TABLE [SolicitudesEnrolamiento] (
        [Id] int NOT NULL DEFAULT (NEXT VALUE FOR [SecuenciaSolicitudesEnrolamiento]),
        [SucursalCodigo] char(2) NOT NULL,
        [CajaCodigo] char(2) NOT NULL,
        [CajaId] int NULL,
        [HuellaEquipo] char(64) NOT NULL,
        [NombreEquipo] nvarchar(100) NOT NULL,
        [TokenHash] char(64) NOT NULL,
        [DireccionIp] varchar(45) NULL,
        [SolicitadaEn] datetimeoffset(3) NOT NULL,
        [Estado] varchar(20) NOT NULL,
        [ResueltaEn] datetimeoffset(3) NULL,
        [ResueltaPor] nvarchar(150) NULL,
        [Motivo] nvarchar(250) NULL,
        CONSTRAINT [PK_SolicitudesEnrolamiento] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SolicitudesEnrolamiento_Cajas_CajaId] FOREIGN KEY ([CajaId]) REFERENCES [Cajas] ([Id])
    );

    CREATE UNIQUE INDEX [IX_SolicitudesEnrolamiento_Equipo] ON [SolicitudesEnrolamiento] ([SucursalCodigo], [CajaCodigo], [HuellaEquipo]);
    CREATE INDEX [IX_SolicitudesEnrolamiento_CajaId] ON [SolicitudesEnrolamiento] ([CajaId]);
    PRINT '  Tabla SolicitudesEnrolamiento creada.';
END
ELSE
    PRINT '  La tabla SolicitudesEnrolamiento ya existía.';
GO

PRINT 'Listo. Las cajas nuevas piden entrar y se aceptan en Organización → Solicitudes de cajas.';
GO
