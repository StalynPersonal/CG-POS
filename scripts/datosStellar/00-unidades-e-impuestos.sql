/*
    CG-POS · Datos migrados desde Stellar (unidades de medida)
    Generado desde STELLAR.xlsx (Punta Cana) el 20/09/2026.

    Se ejecuta sobre la base CgPosCentral, ya creada con estructura_base_datos_central.sql.
    Es idempotente: lo que ya exista no se vuelve a insertar, así que se puede repetir sin miedo.
*/

USE [CgPosCentral];
GO

SET NOCOUNT ON;
GO
/*
    Las unidades de medida que usa Stellar en la presentación del producto y que el Central no trae de
    fábrica. LIBRA, YARDA y GALON no están aquí porque ya existen como LB, YD y GAL, y los artículos se
    cuelgan de esas. Solo se crea lo que falte.

    Los decimales son una propuesta: revíselos en Maestros -> Catálogos, porque de ahí depende si la
    caja deja vender fracciones de ese artículo.
*/

CREATE TABLE #Unidades (Abreviatura nvarchar(10) NOT NULL PRIMARY KEY, Nombre nvarchar(50) NOT NULL, Decimales int NOT NULL);
GO

INSERT INTO #Unidades (Abreviatura, Nombre, Decimales) VALUES
    (N'CAJ500/1', N'Caj500/1', 0),
    (N'CAJA', N'Caja', 0),
    (N'GL', N'Gl', 0),
    (N'GRAMO', N'Gramo', 0),
    (N'GRUESA', N'Gruesa', 0),
    (N'JUEGO', N'Juego', 0),
    (N'KG', N'Kg', 3),
    (N'M', N'M', 2),
    (N'M2', N'M2', 2),
    (N'PAQ.', N'Paq.', 0),
    (N'PAQUETE', N'Paquete', 0),
    (N'PAR', N'Par', 0),
    (N'PLANCHA', N'Plancha', 0),
    (N'PULG', N'Pulg', 2),
    (N'ROLLO', N'Rollo', 0);
GO

/* El código se numera a partir del mayor que ya exista, para no chocar con las unidades de la instalación. */
DECLARE @codigo int = (SELECT ISNULL(MAX([Codigo]), 0) FROM [UnidadesMedida]);

INSERT INTO [UnidadesMedida] ([Id], [Codigo], [Abreviatura], [Nombre], [PermiteDecimales], [Decimales], [ModificadoEn], [ModificadoPor])
SELECT NEXT VALUE FOR [SecuenciaUnidadesMedida],
    @codigo + ROW_NUMBER() OVER (ORDER BY o.Abreviatura), o.Abreviatura, o.Nombre,
    CASE WHEN o.Decimales > 0 THEN 1 ELSE 0 END, o.Decimales, SYSDATETIMEOFFSET(), N'Migración Stellar'
FROM #Unidades o
WHERE NOT EXISTS (SELECT 1 FROM [UnidadesMedida] u WHERE u.Abreviatura = o.Abreviatura);
GO

DROP TABLE #Unidades;
GO

/*
    Los impuestos no se crean aquí: la estructura del Central ya trae ITBIS18, ITBIS16, ITBIS0 y
    EXENTO. Solo se comprueba que estén, porque los artículos se cuelgan de ellos por su código.
*/
IF NOT EXISTS (SELECT 1 FROM [Impuestos] WHERE [Codigo] IN (N'ITBIS18', N'ITBIS16', N'EXENTO'))
    THROW 50001, 'Faltan impuestos: la base no se creó con estructura_base_datos_central.sql.', 1;
GO

DECLARE @unidades int = (SELECT COUNT(*) FROM [UnidadesMedida]);
PRINT 'Unidades de medida: ' + CAST(@unidades AS varchar(10)) + '.';
GO
