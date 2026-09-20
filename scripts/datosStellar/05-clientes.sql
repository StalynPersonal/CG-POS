/*
    CG-POS · Datos migrados desde Stellar (clientes)
    Generado desde STELLAR.xlsx (Punta Cana) el 20/09/2026.

    Se ejecuta sobre la base CgPosCentral, ya creada con estructura_base_datos_central.sql.
    Es idempotente: lo que ya exista no se vuelve a insertar, así que se puede repetir sin miedo.
*/

USE [CgPosCentral];
GO

SET NOCOUNT ON;
GO
/*
    En Stellar no hay clientes que migrar: su maestro solo tiene «CLIENTE CONTADO», sin documento, que
    es el mostrador. Por eso este archivo no carga nada.

    Los clientes salen del padrón de contribuyentes de la DGII, que es mejor fuente: trae el nombre
    oficial de cada RNC y cada cédula del país. Se carga con otro script que ya está en el proyecto:

        1. Deje DGII_RNC.TXT en una carpeta del SERVIDOR de base de datos (por omisión C:\CGPOS).
        2. sqlcmd -S . -E -d CgPosCentral -i scriptsase-datos\cargar-clientes-dgii.sql

    Carga los contribuyentes ACTIVOS (unos 397 mil del archivo de septiembre de 2026), les pone el tipo
    de comprobante según el documento —RNC de 9 dígitos con crédito fiscal E31, cédula de 11 con
    consumo E32— y usa el propio documento como código de cliente. Al repetirlo actualiza la razón
    social y el estado, y no toca el teléfono, el correo ni la lista de precios que usted haya puesto.
*/

USE [CgPosCentral];
GO

PRINT 'Los clientes no salen de Stellar: use scriptsase-datos\cargar-clientes-dgii.sql con DGII_RNC.TXT.';
GO
