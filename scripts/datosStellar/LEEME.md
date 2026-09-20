# Datos migrados desde Stellar

Scripts de carga generados desde `STELLAR.xlsx` (exportación de Stellar de Punta Cana: VAD10 y VAD20)
al formato de CG-POS. Se ejecutan sobre `CgPosCentral`, ya creada con
`scripts/base-datos/estructura_base_datos_central.sql`.

## Orden

Se ejecutan en orden, porque cada uno se cuelga de los anteriores:

```powershell
cd C:\Users\scontreras\Desktop\mio\cg-pos\scripts\datosStellar
foreach ($f in Get-ChildItem *.sql | Sort-Object Name) { sqlcmd -S . -E -b -f 65001 -i $f.Name }
```

| Archivo | Qué carga | Filas |
| --- | --- | --- |
| `00-unidades-e-impuestos.sql` | Unidades de medida que el Central no trae de fábrica | 15 |
| `01-departamentos.sql` | Departamentos, con su nombre | 39 |
| `02-categorias.sql` | Categorías (el «grupo» de Stellar), con su nombre | 669 |
| `03-marcas.sql` | Marcas | 6.060 |
| `04-motivos-devolucion.sql` | Motivos de devolución | 11 |
| `05-clientes.sql` | Nada: explica de dónde salen los clientes | — |
| `06-articulos.sql` | Artículos | 157.892 |
| `07-codigos-barra.sql` | Códigos de barra | 315.011 |
| `08-secuencias-ecf.sql` | Rangos de e-NCF por caja | 196 · **lea el aviso** |
| `09-rellenar-codigos-articulo.sql` | Arregla los códigos cargados con los scripts viejos | solo si ya cargó |

El `-f 65001` es necesario: los archivos están en UTF-8 y sin él los acentos se cargan mal.

Todos son **idempotentes**: lo que ya exista no se vuelve a insertar, así que se pueden repetir sin
duplicar nada. Cargar todo tarda alrededor de un minuto y medio.

## Los clientes salen del padrón de la DGII

En Stellar no hay clientes que migrar: su maestro solo tiene «CLIENTE CONTADO», que es el mostrador.
Los clientes se cargan del padrón de contribuyentes de la DGII (`DGII_RNC.TXT`, que está en esta misma
carpeta), con un script que ya estaba en el proyecto:

```powershell
copy DGII_RNC.TXT C:\CGPOS\
sqlcmd -S . -E -d CgPosCentral -i ..\base-datos\cargar-clientes-dgii.sql
```

Carga los contribuyentes **activos** —397.261 en el archivo de septiembre de 2026— con el nombre
oficial de cada RNC y cada cédula. Tarda unos 11 segundos. Al repetirlo actualiza la razón social y el
estado, y no toca el teléfono, el correo ni la lista de precios que usted haya puesto a mano.

## El archivo 08 no se ejecuta a la ligera

`08-secuencias-ecf.sql` trae los rangos de comprobantes fiscales que **Stellar está usando ahora
mismo**, con el correlativo por donde va cada caja. Cargarlos significa que CG-POS seguirá numerando
donde Stellar se quedó.

Si los dos sistemas emiten a la vez con el mismo rango se repiten e-NCF, y eso es un problema fiscal
con la DGII. Ejecútelo solo cuando la caja de Stellar ya no vaya a facturar, o pida rangos nuevos para
CG-POS y no use este archivo.

Además necesita que las cajas de CG-POS ya estén creadas: el archivo trae una tabla de equivalencias
(caja 1 de Stellar → caja 01 de la sucursal 01) que hay que ajustar antes de ejecutarlo. Lo que no
cuadre no se carga y sale listado al terminar.

## Lo que hay que revisar después de cargar

**El código del artículo va a seis dígitos.** En Stellar es texto (`000001`) y la exportación a Excel lo
convirtió en número, perdiendo los ceros de delante. Los scripts ya lo rellenan: los códigos que son todo
dígitos y miden menos de seis se completan con ceros (`1` → `000001`), los de seis o más se dejan igual y
los alfanuméricos (`C0010`, `ACTIVO-FIJO`) no se tocan. Si cargó la base con los scripts anteriores,
ejecute `09-rellenar-codigos-articulo.sql`: hace lo mismo sobre lo ya cargado y se puede repetir sin
riesgo.

**El código de la categoría es compuesto.** Un mismo grupo de Stellar aparece en varios departamentos y
aquí el código de categoría es único en todo el sistema, así que se compone como
`departamento * 1000 + grupo`. La categoría 286273 es el grupo 273 del departamento 286.

**El 0 % se cargó como exento.** Los artículos que en Stellar no llevan ITBIS quedan con el impuesto
`EXENTO`. Si en su caso son de tasa cero (exportación) y no exentos, cambie `EXENTO` por `ITBIS0` en
`06-articulos.sql` antes de ejecutarlo: para la DGII no son lo mismo.

**Los artículos entran fuera del catálogo visual.** Con venta en POS activada, pero sin marcar para los
mosaicos: eso se elige después, artículo por artículo o por familia.

**Los decimales de las unidades son una propuesta.** Revíselos en Maestros → Catálogos, porque de ahí
depende si la caja deja vender fracciones de ese artículo. Hay unidades raras que vienen así de Stellar
(`CAJ500/1`, `PAQ.`) y conviene limpiarlas.

**Se descartaron 11 motivos de devolución de relleno** (`Z.`, `Z0`…`Z9`), que en Stellar no tienen
nombre real. Están listados al final de `04-motivos-devolucion.sql` por si los quiere.

**14.173 códigos de barra apuntaban a productos que no existen** en el maestro y se descartaron.

## Cómo se tradujo cada cosa

| CG-POS | Stellar | Nota |
| --- | --- | --- |
| Departamento | `MA_DEPARTAMENTOS` | código y nombre del maestro |
| Categoría | `MA_GRUPOS(CATEGORIAS)` | código compuesto con el departamento |
| — | `MA_SUBGRUPOS` | **no se migra**: Stellar tiene tres niveles y CG-POS dos |
| Marca | `MA_PRODUCTOS.c_Marca` | es texto libre en el producto, no un maestro; se numeran alfabéticamente |
| Artículo · Código | `MA_PRODUCTOS.c_Codigo` | |
| Artículo · Descripción | `c_Descri` | recortada a 200 caracteres |
| Artículo · Referencia | `c_CodFabricante` | |
| Artículo · Unidad | `c_Presenta` | LIBRA, YARDA y GALON son LB, YD y GAL, que el Central ya trae |
| Artículo · Impuesto | `n_Impuesto1` | 18 % → ITBIS18, 16 % → ITBIS16, 0 % → EXENTO |
| Artículo · Tipo | `c_Seriales`, `n_TipoPeso` | seriales = 2 → serializado; peso ≠ 0 → pesado |
| Artículo · Precio detalle | `n_Precio1` | |
| Artículo · Precio mayor | `n_Precio2` | vacío si no es mayor que cero o si es igual al de detalle |
| Artículo · Costo | `n_CostoAct` | |
| Código de barras | `MA_CODIGOS` | uno no puede repetirse entre artículos: se queda el primero |
| Motivo de devolución | `MA_AUX_GRUPO`, tipo `MOTIVOS_DEV` | con su mismo código |
| Rango de e-NCF | `MA_GESTION_NCF` | ver el aviso de arriba |
| Cliente | — | del padrón de la DGII, no de Stellar |

## Cómo se regeneran

Los scripts se generan leyendo `STELLAR.xlsx` directamente con Python (openpyxl en modo *read only*,
que va en streaming: el Excel tiene 57 MB comprimidos y más de 400 MB de XML dentro). La hoja
`MA_PRODUCTOS` tarda varios minutos en leerse.
