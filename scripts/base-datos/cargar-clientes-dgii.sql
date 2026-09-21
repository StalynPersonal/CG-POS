/*
    CG-POS · Cargar el archivo de la DGII (DGII_RNC.TXT) en los clientes de «CgPosCentral»

    Toma el archivo que publica la DGII y lo lleva a la tabla Clientes: si el RNC o la cédula ya existe, le actualiza
    la razón social y el estado; si no existe, lo crea. Los clientes bajan solos a las cajas en la siguiente
    sincronización, como cualquier otro maestro. ACTIVO en la DGII es cliente activo; SUSPENDIDO es cliente inactivo.

        1. Descargue el archivo de la DGII y déjelo en una carpeta del SERVIDOR de base de datos.
        2. Cambie la ruta de @archivo, aquí abajo.
        3. sqlcmd -S .\SQLEXPRESS -E -d CgPosCentral -i cargar-clientes-dgii.sql

    IMPORTANTE:
      · La ruta la lee SQL Server, no su equipo: el archivo debe estar en el servidor o en una carpeta compartida
        a la que tenga acceso la cuenta del servicio de SQL Server.
      · Se cargan los contribuyentes en estado ACTIVO y SUSPENDIDO. El ACTIVO queda activo; el SUSPENDIDO queda
        inactivo: si ya existía se desactiva, y si no existía se crea inactivo. Los demás estados se ignoran.
      · De un cliente que ya existe se actualizan únicamente la razón social y el estado. El teléfono, el correo,
        el contacto, el tipo de comprobante, la lista de precios y las direcciones NO se tocan: eso lo llenó usted.
      · Un cliente que usted desactivó vuelve a quedar activo si la DGII lo reporta activo.
      · Los clientes nuevos toman su tipo del documento: RNC de 9 dígitos factura con crédito fiscal (E31) y
        cédula de 11 con consumo (E32). El código del cliente es su propio documento.
      · No borra clientes: lo que ya no venga en el archivo se queda como está.
      · Haga un respaldo antes. Es una carga masiva: ejecútela fuera del horario de venta.
*/

USE [CgPosCentral];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;

/* ------------------------------------------------------------------------
   Ruta del archivo de la DGII, vista desde el servidor de base de datos.
   ------------------------------------------------------------------------ */
DECLARE @archivo nvarchar(4000) = N'C:\CGPOS\DGII_RNC.TXT';

IF OBJECT_ID(N'tempdb..#Padron') IS NOT NULL DROP TABLE #Padron;
CREATE TABLE #Padron
(
    Documento     nvarchar(50)  NULL,
    RazonSocial   nvarchar(300) NULL,
    NombreComercial nvarchar(300) NULL,
    Actividad     nvarchar(300) NULL,
    Campo5        nvarchar(100) NULL,
    Campo6        nvarchar(100) NULL,
    Campo7        nvarchar(100) NULL,
    Campo8        nvarchar(100) NULL,
    Fecha         nvarchar(50)  NULL,
    Estado        nvarchar(50)  NULL,
    Regimen       nvarchar(50)  NULL
);

/* El archivo de la DGII viene separado por «|», una línea por contribuyente y en codificación del sistema. */
DECLARE @carga nvarchar(max) = N'
    BULK INSERT #Padron
    FROM ''' + REPLACE(@archivo, '''', '''''') + N'''
    WITH (FIELDTERMINATOR = ''|'', ROWTERMINATOR = ''0x0a'', CODEPAGE = ''ACP'', TABLOCK, MAXERRORS = 1000);';

BEGIN TRY
    EXEC sp_executesql @carga;
END TRY
BEGIN CATCH
    PRINT 'No se pudo leer el archivo: ' + ERROR_MESSAGE();
    PRINT 'Recuerde que la ruta la abre SQL Server, no su equipo.';
    RETURN;
END CATCH

/* Activos y suspendidos (estos quedan inactivos), con documento de 9 dígitos (RNC) u 11 (cédula) y con razón social. */
IF OBJECT_ID(N'tempdb..#Limpio') IS NOT NULL DROP TABLE #Limpio;
SELECT
    Documento   = LTRIM(RTRIM(REPLACE(REPLACE(Documento, '-', ''), CHAR(13), ''))),
    RazonSocial = LTRIM(RTRIM(RazonSocial)),
    Activo      = CASE WHEN LTRIM(RTRIM(UPPER(REPLACE(Estado, CHAR(13), '')))) = N'ACTIVO' THEN 1 ELSE 0 END
INTO #Limpio
FROM #Padron
WHERE LTRIM(RTRIM(UPPER(REPLACE(Estado, CHAR(13), '')))) IN (N'ACTIVO', N'SUSPENDIDO')
  AND LTRIM(RTRIM(RazonSocial)) <> N'';

DELETE FROM #Limpio
WHERE LEN(Documento) NOT IN (9, 11)
   OR Documento LIKE '%[^0-9]%';

/* Si el archivo trae el mismo documento dos veces, se queda con uno; si alguna de las dos está activa, queda activo. */
IF OBJECT_ID(N'tempdb..#Unicos') IS NOT NULL DROP TABLE #Unicos;
SELECT Documento, RazonSocial = MIN(RazonSocial), Activo = CAST(MAX(Activo) AS bit)
INTO #Unicos
FROM #Limpio
GROUP BY Documento;

CREATE UNIQUE CLUSTERED INDEX IX_Unicos ON #Unicos (Documento);

DECLARE @activos int = (SELECT COUNT(*) FROM #Unicos WHERE Activo = 1);
DECLARE @suspendidos int = (SELECT COUNT(*) FROM #Unicos WHERE Activo = 0);
PRINT 'Contribuyentes activos en el archivo:     ' + CAST(@activos AS varchar(20));
PRINT 'Contribuyentes suspendidos en el archivo: ' + CAST(@suspendidos AS varchar(20));

/* ------------------------------------------------------------------------
   Existe: se actualiza la razón social y el estado (activo o, si está
   suspendido en la DGII, inactivo).
   ------------------------------------------------------------------------ */
UPDATE c
SET c.Nombre        = u.RazonSocial,
    c.Activo        = u.Activo,
    c.ModificadoEn  = SYSDATETIMEOFFSET(),
    c.ModificadoPor = N'Padrón DGII'
FROM [Clientes] c
JOIN #Unicos u ON u.Documento = c.Documento
WHERE c.Nombre <> u.RazonSocial OR c.Activo <> u.Activo;

DECLARE @actualizados int = @@ROWCOUNT;

/* ------------------------------------------------------------------------
   No existe: se crea. El tipo sale del documento (9 dígitos RNC, 11 cédula).
   El suspendido se crea inactivo.
   ------------------------------------------------------------------------ */
INSERT INTO [Clientes]
    ([Id], [Codigo], [TipoDocumento], [Documento], [Nombre], [TipoComprobantePredeterminado],
     [ExoneradoItbis], [AplicaRetencion], [ListaPrecioPredeterminada], [Activo], [ModificadoEn], [ModificadoPor])
SELECT
    NEXT VALUE FOR [SecuenciaClientes],
    u.Documento,
    CASE WHEN LEN(u.Documento) = 9 THEN 1 ELSE 0 END,   /* 1 = RNC, 0 = cédula */
    u.Documento,
    u.RazonSocial,
    CASE WHEN LEN(u.Documento) = 9 THEN 31 ELSE 32 END, /* E31 crédito fiscal, E32 consumo */
    0, 0, 0, u.Activo,
    SYSDATETIMEOFFSET(),
    N'Padrón DGII'
FROM #Unicos u
WHERE NOT EXISTS (SELECT 1 FROM [Clientes] c WHERE c.Documento = u.Documento)
  AND NOT EXISTS (SELECT 1 FROM [Clientes] c WHERE c.Codigo = u.Documento);

DECLARE @creados int = @@ROWCOUNT;

DROP TABLE #Padron;
DROP TABLE #Limpio;
DROP TABLE #Unicos;

PRINT 'Clientes creados:      ' + CAST(@creados AS varchar(20));
PRINT 'Clientes actualizados: ' + CAST(@actualizados AS varchar(20));
PRINT 'Listo. Las cajas los reciben en su próxima sincronización.';
GO
