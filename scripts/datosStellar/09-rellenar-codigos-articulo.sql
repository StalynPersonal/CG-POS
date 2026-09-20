/*
    CG-POS · Arreglo de los códigos de artículo migrados desde Stellar

    En Stellar el código del artículo es texto de seis dígitos ('000001'). La exportación a Excel lo convirtió en
    número y perdió los ceros de delante, así que los primeros scripts de migración cargaron '1' en vez de '000001'.

    Este script rellena con ceros hasta seis los códigos que son todo dígitos y miden menos de seis. Los de seis o
    más se dejan como están, y los alfanuméricos (C0010, AAFF001, ACTIVO-FIJO) no se tocan: ahí un cero delante no
    significa nada.

    Se ejecuta sobre la base CgPosCentral. Es idempotente: pasarlo dos veces no cambia nada la segunda vez, porque
    después de rellenar ya ninguno mide menos de seis.

    Solo hace falta en bases cargadas con los scripts viejos. Los scripts 06 y 07 ya traen los códigos rellenados.
*/

USE [CgPosCentral];
GO

SET NOCOUNT ON;
GO

/*
    El código del artículo. Los códigos de barra y de proveedor cuelgan del artículo por su Id, no por su código,
    así que no hay que tocarlos: siguen apuntando al mismo artículo.
*/
UPDATE [Articulos]
SET [Codigo] = RIGHT('000000' + [Codigo], 6)
WHERE LEN([Codigo]) < 6
  AND [Codigo] NOT LIKE '%[^0-9]%';

PRINT CONCAT('Artículos con el código rellenado: ', @@ROWCOUNT);
GO

/*
    Los documentos guardan el código del artículo copiado, no su Id: si no se rellenan aquí también, una cotización
    o una lista hecha antes dejaría de encontrar su artículo en el maestro.
*/
UPDATE [LineasCotizacion]
SET [ArticuloCodigo] = RIGHT('000000' + [ArticuloCodigo], 6)
WHERE LEN([ArticuloCodigo]) < 6
  AND [ArticuloCodigo] NOT LIKE '%[^0-9]%';

PRINT CONCAT('Líneas de cotización actualizadas: ', @@ROWCOUNT);
GO

UPDATE [ArticulosListaBoda]
SET [ArticuloCodigo] = RIGHT('000000' + [ArticuloCodigo], 6)
WHERE LEN([ArticuloCodigo]) < 6
  AND [ArticuloCodigo] NOT LIKE '%[^0-9]%';

PRINT CONCAT('Artículos de listas de boda actualizados: ', @@ROWCOUNT);
GO

/* Lo que quedó: no debería haber ningún código numérico de menos de seis dígitos. */
SELECT COUNT(*) AS [CodigosCortosQueQuedan]
FROM [Articulos]
WHERE LEN([Codigo]) < 6 AND [Codigo] NOT LIKE '%[^0-9]%';
GO
