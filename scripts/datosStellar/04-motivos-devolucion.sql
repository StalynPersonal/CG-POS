/*
    CG-POS · Datos migrados desde Stellar (motivos de devolución)
    Generado desde STELLAR.xlsx (Punta Cana) el 20/09/2026.

    Se ejecuta sobre la base CgPosCentral, ya creada con estructura_base_datos_central.sql.
    Es idempotente: lo que ya exista no se vuelve a insertar, así que se puede repetir sin miedo.
*/

USE [CgPosCentral];
GO

SET NOCOUNT ON;
GO
/*
    Motivos de devolución de Stellar (MA_AUX_GRUPO, tipo MOTIVOS_DEV), con su mismo código.
*/

CREATE TABLE #Motivos (Codigo int NOT NULL PRIMARY KEY, Nombre nvarchar(100) NOT NULL);
GO
INSERT INTO #Motivos (Codigo, Nombre) VALUES
    (100, N'A.No Necestito El Producto'),
    (101, N'B.Diferencia.'),
    (102, N'C.Cambio NCF'),
    (103, N'D.Producto Averiado'),
    (104, N'E.Error en Código'),
    (105, N'F.Error en Precios'),
    (106, N'G.No retiro producto con pendiente'),
    (107, N'H.Re-Facturar con Pendiente Entrega/Envió'),
    (108, N'I.Anulación factura'),
    (109, N'J.Articulo Duplicado'),
    (110, N'K.Producto Agotado');
GO

INSERT INTO [MotivosDevolucion] ([Id], [Codigo], [Nombre], [Activo], [ModificadoEn], [ModificadoPor])
SELECT NEXT VALUE FOR [SecuenciaMotivosDevolucion], o.Codigo, o.Nombre, 1, SYSDATETIMEOFFSET(), N'Migración Stellar'
FROM #Motivos o
WHERE NOT EXISTS (SELECT 1 FROM [MotivosDevolucion] m WHERE m.Codigo = o.Codigo);
GO

DROP TABLE #Motivos;
GO

DECLARE @total int = (SELECT COUNT(*) FROM [MotivosDevolucion]);
PRINT 'Motivos de devolución: ' + CAST(@total AS varchar(10)) + ' en total.';
GO

/*
    Motivos que NO se migraron por ser relleno del maestro de Stellar, sin nombre real:

    111 - Z1
    112 - Z2
    113 - Z3
    114 - Z4
    115 - Z5
    116 - Z6
    117 - Z7
    118 - Z8
    119 - Z9
    120 - Z0
    121 - Z.
*/
