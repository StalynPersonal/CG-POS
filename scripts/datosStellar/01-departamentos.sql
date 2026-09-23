/*
    CG-POS · Datos migrados desde Stellar (departamentos)
    Generado desde STELLAR.xlsx (Punta Cana) el 20/09/2026.

    Se ejecuta sobre la base CgPosCentral, ya creada con estructura_base_datos_central.sql.
    Es idempotente: lo que ya exista no se vuelve a insertar, así que se puede repetir sin miedo.
*/

USE [CgPosCentral];
GO

SET NOCOUNT ON;
GO
/*
    Departamentos de Stellar, con su código y su nombre tal como están en el maestro.
*/

CREATE TABLE #Departamentos (Codigo int NOT NULL PRIMARY KEY, Nombre nvarchar(100) NOT NULL);
GO
INSERT INTO #Departamentos (Codigo, Nombre) VALUES
    (100, N'Artículos'),
    (253, N'Cafeteria'),
    (254, N'Carnes'),
    (255, N'Congelados'),
    (256, N'Decoracion'),
    (257, N'Delicatessen'),
    (258, N'Deportes'),
    (259, N'Electricos'),
    (260, N'Electrodomesticos'),
    (261, N'Embutidos'),
    (263, N'Frutas y Vegetales'),
    (264, N'Granerias y Legumbre'),
    (265, N'Herrajes'),
    (266, N'Herramientas'),
    (267, N'Hig Personal y Salud'),
    (268, N'Hogar'),
    (269, N'Iluminacion'),
    (270, N'Jardineria'),
    (271, N'Lacteos'),
    (272, N'Licores'),
    (273, N'Maquinarias'),
    (274, N'Mascotas'),
    (275, N'Materia Prima'),
    (276, N'Material de Empaque'),
    (277, N'Mat de Construccion'),
    (278, N'Metales'),
    (279, N'Mob de Oficina'),
    (280, N'Muebles'),
    (281, N'Navidad'),
    (283, N'Panaderia'),
    (284, N'Pescados y Mariscos'),
    (285, N'Pintura y Selladores'),
    (286, N'Plomeria'),
    (287, N'Prov Comestibles'),
    (288, N'Prov No Comestibles'),
    (290, N'Reposteria'),
    (291, N'Servicio'),
    (292, N'Textiles'),
    (294, N'Activos Fijos');
GO

INSERT INTO [Departamentos] ([Id], [Codigo], [Nombre], [PermiteDescuentoManual], [EsNoCodificada], [Activa], [ModificadoEn], [ModificadoPor])
SELECT NEXT VALUE FOR [SecuenciaDepartamentos], o.Codigo, o.Nombre, 1, 0, 1, SYSDATETIMEOFFSET(), N'Migración'
FROM #Departamentos o
WHERE NOT EXISTS (SELECT 1 FROM [Departamentos] d WHERE d.Codigo = o.Codigo);
GO

DROP TABLE #Departamentos;
GO

DECLARE @total int = (SELECT COUNT(*) FROM [Departamentos]);
PRINT 'Departamentos: ' + CAST(@total AS varchar(10)) + ' en total.';
GO
