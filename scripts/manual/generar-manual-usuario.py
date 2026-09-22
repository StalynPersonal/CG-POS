# -*- coding: utf-8 -*-
"""
Genera el manual de usuario de CG-POS en Word.

    python scripts/manual/generar-manual-usuario.py

Deja el documento en «scripts/manual/Manual de usuario CG-POS.docx», junto a este script.
El manual NO se edita a mano: se edita este script y se vuelve a generar, para que no se pierda el cambio.
Cada vez que cambie una pantalla, una ruta, una tecla, un permiso o un paso de un flujo, hay que actualizarlo aquí.
"""
import os
from docx import Document
from docx.shared import Pt, RGBColor, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.section import WD_SECTION

VERDE = RGBColor(0x1B, 0x4D, 0x3E)
VERDE_CLARO = RGBColor(0x2E, 0x7D, 0x32)

doc = Document()

# Márgenes y fuente base
for seccion in doc.sections:
    seccion.top_margin = Cm(2.2)
    seccion.bottom_margin = Cm(2.2)
    seccion.left_margin = Cm(2.4)
    seccion.right_margin = Cm(2.4)

normal = doc.styles['Normal']
normal.font.name = 'Segoe UI'
normal.font.size = Pt(10.5)
normal.paragraph_format.space_after = Pt(6)

for nivel, tamano in ((1, 18), (2, 14), (3, 12)):
    estilo = doc.styles[f'Heading {nivel}']
    estilo.font.name = 'Segoe UI'
    estilo.font.size = Pt(tamano)
    estilo.font.color.rgb = VERDE if nivel < 3 else VERDE_CLARO
    estilo.font.bold = True


def titulo(texto, nivel=1):
    doc.add_heading(texto, level=nivel)


def p(texto=''):
    return doc.add_paragraph(texto)


def viñeta(texto):
    return doc.add_paragraph(texto, style='List Bullet')


def paso(texto):
    return doc.add_paragraph(texto, style='List Number')


def nota(texto):
    parrafo = doc.add_paragraph()
    corrida = parrafo.add_run(texto)
    corrida.italic = True
    corrida.font.color.rgb = VERDE_CLARO
    return parrafo


def tabla(encabezados, filas, anchos=None):
    t = doc.add_table(rows=1, cols=len(encabezados))
    t.style = 'Table Grid'
    for i, texto in enumerate(encabezados):
        celda = t.rows[0].cells[i]
        celda.text = ''
        corrida = celda.paragraphs[0].add_run(texto)
        corrida.bold = True
        corrida.font.color.rgb = VERDE
    for fila in filas:
        celdas = t.add_row().cells
        for i, texto in enumerate(fila):
            celdas[i].text = str(texto)
    if anchos:
        for fila in t.rows:
            for i, ancho in enumerate(anchos):
                fila.cells[i].width = Cm(ancho)
    doc.add_paragraph()
    return t


# ---------------------------------------------------------------- Portada
portada = doc.add_paragraph()
portada.alignment = WD_ALIGN_PARAGRAPH.CENTER
marca = portada.add_run('CG-POS\n')
marca.font.size = Pt(44)
marca.bold = True
marca.font.color.rgb = VERDE
sub = portada.add_run('Manual de usuario')
sub.font.size = Pt(22)
sub.font.color.rgb = VERDE_CLARO

datos = doc.add_paragraph()
datos.alignment = WD_ALIGN_PARAGRAPH.CENTER
datos.add_run('Sistema de punto de venta y administración central\nContreras Group\n\n'
              'Incluye el recorrido completo del sistema: qué hace cada parte, dónde se entra y en qué orden se usa.')

doc.add_page_break()

# ---------------------------------------------------------------- Introducción
titulo('1. Qué es CG-POS y cómo está organizado')
p('CG-POS tiene dos programas que trabajan juntos:')
viñeta('La CAJA, que se instala en cada punto de venta. Vende, cobra, emite la factura electrónica (e-CF), '
       'hace devoluciones y cierra el turno. Funciona aunque se caiga el internet: todo queda guardado en la propia caja '
       'y se envía al Central cuando vuelve la comunicación.')
viñeta('El CENTRAL, que está en el servidor de la empresa. Ahí se configuran los artículos, los precios, las promociones, '
       'los usuarios y todas las reglas del negocio; se reciben las facturas de todas las cajas; se envían los e-CF a la DGII '
       'y se sacan los reportes.')
p('Este manual empieza por el CENTRAL y después explica la CAJA, porque ese es el orden real: una caja no puede vender si en '
  'el Central no están creados antes la sucursal, la caja, los artículos con sus precios, los rangos de comprobantes fiscales '
  'y los usuarios.')
p('Todo lo que la caja necesita (artículos, precios, ofertas, usuarios, parámetros) baja del Central automáticamente. '
  'Todo lo que la caja hace (facturas, notas de crédito, cierres, puntos) sube al Central automáticamente.')

titulo('1.1. Dónde se entra', 2)
p('La caja se abre en el navegador del mismo equipo de la caja; el Central, desde cualquier computadora de la empresa.')
tabla(['Pantalla', 'Dónde se entra', 'Quién la usa'],
      [['Central – administración', 'La dirección del servidor (en pruebas, http://localhost:5280)', 'Administración, contabilidad, gerencia'],
       ['Chequeador de precios', 'La dirección del servidor + /chequeador/01, con el código de la sucursal al final', 'El cliente, en el pasillo de la tienda'],
       ['Caja – pantalla de ventas principal', 'http://localhost:5180/ingreso?pantalla=principal', 'Cajero, con lector y teclado'],
       ['Caja – pantalla de ventas secundaria', 'http://localhost:5180/ingreso?pantalla=secundaria', 'Cajero, tocando el catálogo'],
       ['Caja – pantalla de clientes', 'http://localhost:5180/cliente', 'El cliente la ve (no pide usuario)'],
       ['Caja – devoluciones', 'http://localhost:5180/ingreso?pantalla=devoluciones', 'Cajero o encargado de devoluciones']],
      anchos=[6.0, 7.0, 4.0])
nota('Las pantallas de la caja se abren solas al encender el equipo, cada una en su monitor, con el acceso directo que deja '
     'instalado el técnico. Ninguna lleva a otra: cada monitor muestra siempre la suya. Cada dirección pide el usuario y, al '
     'entrar, abre su pantalla; la del cliente no pide usuario.')

titulo('1.2. Quién hace qué', 2)
tabla(['Rol', 'Qué hace', 'Dónde'],
      [['Cajero', 'Abre turno, vende, cobra, imprime, cierra su turno', 'Caja'],
       ['Supervisor', 'Autoriza lo que el cajero no puede hacer solo (eliminar líneas, descuentos, devoluciones, retiros, notas internas)', 'Caja (con su usuario y clave)'],
       ['Encargado de sucursal', 'Revisa cierres de caja y hace el cierre consolidado del día con sus depósitos', 'Central'],
       ['Administración / maestros', 'Artículos, precios, promociones, clientes, parámetros, usuarios', 'Central'],
       ['Contabilidad', 'Facturas recibidas, reportes, 607, e-CF enviados a la DGII', 'Central']],
      anchos=[4.0, 9.0, 4.0])

doc.add_page_break()

# ---------------------------------------------------------------- PARTE 1: EL CENTRAL
titulo('2. El Central, módulo por módulo')
p('Se entra con usuario y contraseña. El menú de la izquierda muestra solo lo que el rol de cada persona tiene permitido.')
p('El menú viene agrupado: cada grupo (Organización, Maestros, Precios, Promociones, Ventas, Reportes…) arranca cerrado y se '
  'abre al tocar su título. El grupo de la pantalla en la que usted está se abre solo, para que siempre vea dónde está parado.')

titulo('2.1. Crear la base de datos del Central y entrar por primera vez', 2)
p('El sistema no crea ni cambia bases de datos por su cuenta: la base se crea con el script que viene con el sistema. '
  'Esto lo hace una sola vez el personal de tecnología.')
paso('En el servidor, abra SQL Server Management Studio (o use sqlcmd) y ejecute el archivo '
     'scripts/base-datos/estructura_base_datos_central.sql. Crea la base CgPosCentral con todas sus tablas, llaves e índices.')
paso('Ese mismo script deja lo mínimo para arrancar: la empresa, el usuario administrador, los parámetros del negocio con '
     'valores razonables y los catálogos que son iguales en cualquier negocio dominicano.')
nota('El RNC que trae el script es un marcador: lo primero que debe hacer al entrar al Central es corregirlo en '
     'Organización → Empresa, junto con la razón social, el nombre comercial, la dirección y el teléfono. Es el dato '
     'que sale en las facturas y ante la DGII. Se admite un RNC de 9 dígitos o una cédula de 11, si quien factura es '
     'una persona física; el sistema le valida el dígito verificador.')
tabla(['La base recién creada trae', 'No trae (lo crea usted)'],
      [['Usuario administrador con todos los permisos', 'Sucursales y cajas'],
       ['La empresa, con un RNC de marcador que usted corrige al entrar', 'Usuarios y roles de caja'],
       ['Parámetros con valores de arranque (plazos, topes, fidelidad, cuadre…)', ''],
       ['Monedas: peso dominicano y dólar', 'Departamentos, categorías y marcas'],
       ['Impuestos: ITBIS 18 %, 16 %, 0 % y exento', 'Artículos y sus precios'],
       ['Unidades de medida: unidad, libra, pie, yarda, galón', 'Clientes'],
       ['Denominaciones de billetes y monedas, para el cuadre', 'Bancos'],
       ['Formas de pago: efectivo, tarjeta, transferencia, cheque, dólares, nota de crédito, bonos, puntos…', 'Promociones'],
       ['Tipos de tarjeta y motivos de descuento y de devolución', 'Rangos de comprobantes fiscales']],
      anchos=[8.5, 8.5])
paso('Configure la conexión del Central a esa base (lo hace tecnología en el archivo de configuración del servidor) y levante el Central.')
paso('Abra el Central en el navegador y entre con el administrador:')
tabla(['Usuario', 'Contraseña', 'Qué pasa al entrar'],
      [['ADMIN', 'Admin.CGPOS#2026', 'El sistema exige cambiarla de inmediato; esa nueva contraseña es la suya.']],
      anchos=[3.5, 5.0, 8.5])
nota('Si el Central avisa que la base no existe o le faltan tablas, es que no se ejecutó el script o se apuntó a otra base.')
p('Con ese usuario ya puede crear todo lo demás, en el orden de la sección siguiente. Lo primero que conviene hacer es crear '
  'su propio usuario y el de las demás personas (Seguridad → Usuarios), para que cada quien entre con el suyo.')
nota('El sistema no carga datos de archivos: todo lo que ve en el Central se creó desde el Central y se guarda en su base de datos.')

titulo('2.2. Lo mínimo que debe existir antes de abrir una caja', 2)
p('La caja no inventa nada: todo lo que usa baja del Central. Si el Central está vacío, la caja no puede ni abrir turno. '
  'Antes de poner a vender una caja, en el Central debe estar creado y configurado esto:')
tabla(['Qué', 'Dónde se hace', 'Por qué hace falta'],
      [['Datos de la empresa', 'Organización → Empresa', 'El RNC (o la cédula) y la razón social con los que se factura. El script los siembra como marcador: corríjalos antes de emitir el primer comprobante.'],
       ['Sucursal y caja', 'Organización', 'La caja se identifica por el código de su sucursal y el suyo, ambos de dos dígitos (01, 02…); sin eso no se conecta.'],
       ['Credencial de la caja', 'Organización → Cajas', 'Se emite desde el Central y se escribe una vez en la pantalla de la caja, junto con su IP.'],
       ['Parámetros del negocio', 'Organización → Parámetros', 'Fondo de caja, redondeo, vigencia de notas de crédito, retención, plazos. Si falta uno obligatorio, la operación se rechaza.'],
       ['Catálogos', 'Maestros → Catálogos', 'Moneda, impuestos, departamentos, unidades, formas de pago, denominaciones, bancos, motivos de descuento y de devolución.'],
       ['Artículos y precios', 'Maestros → Artículos y Precios', 'Sin artículos con precio no hay nada que vender. El sistema no trae productos: vea los sugeridos más adelante.'],
       ['Rangos de e-CF', 'Fiscal → Rangos de e-CF', 'Sin comprobantes fiscales disponibles la caja no puede facturar.'],
       ['Usuarios y roles de caja', 'Usuarios de caja', 'El cajero, el supervisor y el gerente, con sus niveles y permisos.']],
      anchos=[4.2, 4.8, 8.0])
nota('Todo esto baja solo a las cajas en la siguiente sincronización: no hay que copiar nada a mano.')

titulo('2.3. Organización', 2)
tabla(['Opción', 'Ruta', 'Para qué sirve'],
      [['Empresa', '/organizacion/empresa', 'Datos fiscales de la empresa, el RNC incluido: RNC de 9 dígitos o cédula de 11. Los comprobantes ya emitidos conservan el que llevaban y el cambio queda en la auditoría.'],
       ['Sucursales', '/organizacion/sucursales', 'Alta y datos de cada sucursal. El código es de dos dígitos (01 a 99), no cambia después de crearla y es el que sale en los números de documento.'],
       ['Cajas', '/organizacion/cajas', 'Alta de cajas con su código de dos dígitos (01 a 99) y su IP fija, habilitarlas, y emitir o revocar su credencial. El código es único en la sucursal y la IP es única en toda la empresa.'],
       ['Parámetros', '/organizacion/parametros', 'Todas las reglas del negocio: vigencia de notas de crédito, retención de la Ley 32-23, redondeo, fondo de caja, chequeador, listas de boda, fidelidad, y también cada cuánto la caja sincroniza, se mantiene y respalda. Se pueden fijar en general, por sucursal o por caja.'],
       ['Actualizaciones', '/organizacion/actualizaciones', 'Versión publicada del programa de las cajas y en qué versión está cada una.']],
      anchos=[3.8, 5.0, 8.2])

titulo('2.4. Seguridad', 2)
viñeta('Usuarios y roles del Central (/seguridad/usuarios y /seguridad/roles): quién entra al Central y qué puede ver o hacer.')
viñeta('Usuarios y roles de caja (/cajas/usuarios y /cajas/roles): los cajeros y supervisores, con su nivel (1 a 9), sus '
       'permisos y en qué cajas trabajan. Bajan solos a las cajas.')
viñeta('Auditoría (/seguridad/auditoria): todo lo que se ha hecho en el Central, con el valor que tenía antes y el que '
       'quedó después de cada campo que cambió.')
nota('El nivel se usa para las autorizaciones: un descuento que pasa el tope de un supervisor pide la clave de alguien de nivel superior.')

titulo('2.4.1. Auditoría: el antes y el después', 3)
p('Cada vez que alguien guarda algo en el Central, el sistema anota qué se hizo, quién lo hizo, cuándo, con qué motivo y, '
  'campo por campo, qué valor había antes y cuál quedó después. No hay que activar nada: se anota solo. Los movimientos no '
  'se editan ni se borran desde la aplicación.')
tabla(['Columna', 'Qué muestra'],
      [['Cuándo', 'Fecha y hora del movimiento, en la hora del servidor donde se consulta.'],
       ['Acción', 'Qué se hizo (por ejemplo, actualizar la empresa), sobre qué entidad y con qué motivo, si lo lleva.'],
       ['Usuario', 'Quién lo hizo y, en las operaciones que la piden, quién la autorizó.'],
       ['Qué cambió', 'Un resumen de lo que se tocó, por ejemplo «Modificado: Sucursal #12 · 3 campos». Se hace clic ahí y se abre el detalle.']],
      anchos=[3.5, 10.5])
p('Los filtros de arriba acotan la búsqueda: rango de fechas, entidad, usuario, un texto libre (busca también dentro '
  'de los valores que cambiaron) y el interruptor «Solo cambios de datos», que deja fuera los ingresos y las consultas. Abajo '
  'se elige el tamaño de página (10, 25, 50, 100 o 200) y se pasa de una página a otra; el Central envía solo la página que '
  'se está viendo, así que la consulta es rápida aunque haya años de movimientos.')
p('Al hacer clic en el resumen se abre una ventana con el detalle: cada campo con su valor anterior (tachado, en rojo) y el '
  'que quedó (en verde). Una creación no tiene valor anterior y una eliminación no tiene valor nuevo. El detalle se pide al '
  'abrirlo, así el listado se mantiene liviano aunque un movimiento haya tocado cientos de filas.')
nota('Las contraseñas, los certificados y las firmas se anotan como cambiados, pero su contenido nunca se muestra: en su lugar '
     'aparece «(oculto)».')
nota('Entrar al sistema no cuenta como modificar al usuario, así que la auditoría no se llena con la fecha del último ingreso '
     'de cada quien.')
nota('Para entrar a esta pantalla hace falta el permiso «Consultar la auditoría del Central y de las cajas». Se puede dar a '
     'contabilidad o a auditoría sin darles permiso para administrar nada.')

titulo('2.5. Maestros, artículos y precios', 2)
tabla(['Opción', 'Ruta', 'Para qué sirve'],
      [['Catálogos', '/maestros', 'Monedas, tasas de cambio, departamentos, categorías, marcas, unidades, impuestos, formas de pago, denominaciones, bancos, tipos de tarjeta, motivos de descuento y devolución, almacenes, niveles y reglas de fidelidad y descuentos por tarjeta (BIN).'],
       ['Artículos', '/articulos', 'Alta y edición de artículos, con sus códigos de barras y de proveedor.'],
       ['Precios', '/precios/articulos', 'Precio de detalle, precio por mayor con su cantidad mínima, precio mínimo y costo; de inmediato o a partir de una fecha.'],
       ['Topes de descuento', '/precios/topes', 'Hasta cuánto puede descontar cada nivel, en general o por departamento o artículo.'],
       ['Clientes', '/clientes', 'Clientes con su contacto, sus dos teléfonos, su comprobante habitual, exoneraciones y direcciones de envío.']],
      anchos=[3.8, 4.4, 8.8])
nota('En Artículos y en Precios, al lado del buscador hay un «Buscar por» donde se elige Descripción o Código. La '
     'descripción se busca por parecido; el código (interno, de barras o de proveedor) hay que escribirlo completo, '
     'porque un código es el artículo o no lo es: buscar «040100» por pedazos devolvía cientos de artículos que '
     'solo empiezan igual.')

titulo('2.5.1. Cargar los clientes desde el archivo de la DGII', 3)
p('La DGII publica un archivo con todos los contribuyentes registrados del país (DGII_RNC.TXT). Ese archivo se puede cargar '
  'de una vez en los clientes del Central, y de ahí bajan solos a las cajas. Sirve para que, cuando un cliente dicte su RNC '
  'en la caja, su razón social aparezca escrita exactamente como la tiene la DGII.')
paso('Descargue el archivo del portal de la DGII y déjelo en una carpeta del servidor de base de datos.')
paso('Abra scripts/base-datos/cargar-clientes-dgii.sql, cambie la ruta del archivo arriba y ejecútelo.')
paso('Al terminar informa cuántos clientes creó y cuántos actualizó. Las cajas los reciben en su próxima sincronización.')
tabla(['Qué hace', 'Detalle'],
      [['Si el cliente ya existe', 'Le actualiza la razón social y el estado: activo o, si la DGII lo tiene suspendido, inactivo. El teléfono, el correo, el contacto, el tipo de comprobante, la lista de precios y las direcciones no se tocan: eso lo llenó usted.'],
       ['Si no existe', 'Lo crea (inactivo si está suspendido). Un RNC de 9 dígitos nace con crédito fiscal (E31) y una cédula de 11 con consumo (E32). El código del cliente es su propio documento.'],
       ['Estado', 'Se cargan los contribuyentes ACTIVO, que quedan activos, y los SUSPENDIDO, que quedan inactivos. Los demás estados se ignoran. Un cliente que usted había desactivado vuelve a quedar activo si la DGII lo reporta activo.'],
       ['Lo que no hace', 'No borra clientes: lo que ya no venga en el archivo se queda como está.']],
      anchos=[4.0, 12.0])
nota('La ruta la abre SQL Server, no su equipo: el archivo debe estar en el servidor o en una carpeta compartida a la que '
     'tenga acceso la cuenta del servicio de SQL Server. Haga un respaldo antes y ejecútelo fuera del horario de venta.')

titulo('2.6. Artículos para empezar (sugerencia)', 2)
p('El sistema se instala sin productos: usted crea los suyos. Para arrancar y para probar todo el sistema, conviene cargar '
  'primero unos pocos artículos que cubran cada caso del negocio de bebidas, y después ya cargar el catálogo completo.')
p('Antes de los artículos cree lo que ellos necesitan, en este orden: impuestos (ITBIS 18 % y exento), unidades de medida '
  '(unidad, caja, libra), departamentos, categorías y marcas. Los departamentos, las categorías y las marcas sugeridas '
  'están en la sección siguiente; los artículos de aquí abajo ya usan esos mismos nombres.')

p('Ficha del artículo: esto es lo que se llena en Maestros → Artículos.')
tabla(['Código', 'Descripción', 'Departamento', 'Categoría', 'Marca', 'Unidad', 'Tipo'],
      [['CER-PRE-650', 'Cerveza Presidente 650 ml', 'Cervezas', 'Nacionales', 'Presidente', 'Unidad', 'Normal'],
       ['CER-PRE-CJ', 'Cerveza Presidente 650 ml, caja de 12', 'Cervezas', 'Nacionales', 'Presidente', 'Caja', 'Normal'],
       ['CER-COR-355', 'Cerveza Corona 355 ml', 'Cervezas', 'Importadas', 'Corona', 'Unidad', 'Normal'],
       ['RON-BRU-AN', 'Ron Brugal Añejo 750 ml', 'Licores', 'Ron', 'Brugal', 'Unidad', 'Normal'],
       ['WHI-JWB-750', 'Whisky Johnnie Walker Black 750 ml', 'Licores', 'Whisky', 'Johnnie Walker', 'Unidad', 'Normal'],
       ['VIN-TIN-750', 'Vino tinto reserva 750 ml', 'Vinos', 'Tintos', 'Marqués de Riscal', 'Unidad', 'Normal'],
       ['REF-COLA-2L', 'Refresco de cola 2 litros', 'Refrescos y aguas', 'Gaseosas', 'Coca-Cola', 'Unidad', 'Normal'],
       ['HIE-LB', 'Hielo a granel', 'Hielo y desechables', 'Hielo', '(sin marca)', 'Libra', 'Pesado'],
       ['BAR-PRE-50', 'Barril de cerveza 50 litros', 'Cervezas', 'Nacionales', 'Presidente', 'Unidad', 'Serializado'],
       ['COM-FIESTA', 'Combo fiesta (ron, refrescos y hielo)', 'Licores', 'Ron', '(sin marca)', 'Unidad', 'Combo'],
       ['CAS-CER-CJ', 'Casco retornable de caja de cerveza', 'Cervezas', 'Nacionales', 'Presidente', 'Unidad', 'Normal'],
       ['SRV-ENTREGA', 'Servicio de entrega a domicilio', 'Servicios', 'Entregas', '(sin marca)', 'Unidad', 'Normal']],
      anchos=[2.4, 4.4, 2.6, 2.2, 2.4, 1.5, 1.9])
nota('La marca es opcional: un artículo genérico como el hielo o un servicio puede quedarse sin ella. La categoría sí la '
     'exige el Central al publicar, y siempre pertenece a un departamento.')

p('Precios, impuesto y código de barras del mismo artículo. El precio de detalle y el de mayor se cargan SIN ITBIS, como '
  'en Stellar: al vender, el ITBIS se calcula sobre cada línea y se suma aparte. El precio por mayor se aplica solo, desde '
  'la cantidad indicada.')
tabla(['Código', 'ITBIS', 'Precio detalle', 'Precio mayor', 'Desde', 'Código de barras (ejemplo)', 'Sirve para probar'],
      [['CER-PRE-650', '18 %', '230.00', '205.00', '12', '7401000000011', 'Venta normal y precio por mayor automático'],
       ['CER-PRE-CJ', '18 %', '2,460.00', '2,400.00', '5', '7401000000028', 'Venta por caja y segundo código del mismo empaque'],
       ['CER-COR-355', '18 %', '180.00', '165.00', '12', '7501000000035', 'Oferta lleva 3 paga 2'],
       ['RON-BRU-AN', '18 %', '490.00', '470.00', '6', '7401000000042', 'Descuento por línea con motivo'],
       ['WHI-JWB-750', '18 %', '3,200.00', '—', '—', '5000267000059', 'Descuento que pasa el tope y pide autorización'],
       ['VIN-TIN-750', '18 %', '850.00', '800.00', '6', '8410000000066', 'Oferta por categoría (todos los tintos)'],
       ['REF-COLA-2L', '18 %', '130.00', '120.00', '6', '7401000000073', 'Cantidades grandes con el lector (6*REF-COLA-2L)'],
       ['HIE-LB', '18 %', '18.00', '—', '—', '(sin código)', 'Artículo pesado: peso de la balanza o digitado'],
       ['BAR-PRE-50', '18 %', '9,500.00', '—', '—', '7401000000097', 'Serializado: pide el número del barril al venderlo'],
       ['COM-FIESTA', '18 %', '1,190.00', '—', '—', '7401000000103', 'Combo: se vende como uno solo y nunca toma precio por mayor'],
       ['CAS-CER-CJ', '18 %', '350.00', '—', '—', '7401000000110', 'Depósito de envase: se cobra y se devuelve al retornarlo'],
       ['SRV-ENTREGA', 'Exento', '250.00', '—', '—', '(sin código)', 'Servicio y comprobante con línea exenta']],
      anchos=[2.4, 1.3, 2.2, 2.0, 1.2, 3.4, 5.0])
nota('Los códigos de barras de la tabla son de ejemplo: use el real del empaque. En bebidas conviene registrarle a la unidad '
     'su código y a la caja el suyo, porque el empaque trae los dos y el cajero escanea cualquiera de ellos. Un artículo '
     'admite varios códigos de barras y también el del proveedor.')
p('Con esos doce artículos ya puede probar venta por unidad y por caja, precio por mayor, pesados, serializados, combos, '
  'cascos retornables, exentos, ofertas y descuentos con autorización. El catálogo completo se carga después desde '
  'Artículos, uno por uno o importando el archivo.')
nota('El casco retornable se maneja como un artículo más: se le cobra al cliente que se lleva la caja y se le devuelve con '
     'una devolución cuando trae los envases. Así queda en la factura y en el cuadre del turno.')

titulo('2.7. Los demás datos del negocio (sugerencia)', 2)
p('Estos no vienen en el sistema porque son decisiones suyas. Esta es una sugerencia para un negocio de venta de bebidas; '
  'ajústela a como trabaja. El orden importa: cada cosa necesita la anterior.')

p('1. Departamentos (Maestros → Catálogos). El departamento manda en los reportes y decide si admite descuento manual.')
tabla(['Código', 'Departamento', 'Admite descuento manual'],
      [['1', 'Licores', 'Sí'], ['2', 'Vinos', 'Sí'], ['3', 'Cervezas', 'Sí'],
       ['4', 'Refrescos y aguas', 'Sí'], ['5', 'Snacks y picaderas', 'Sí'],
       ['6', 'Hielo y desechables', 'Sí'], ['7', 'Servicios', 'No']],
      anchos=[2.2, 6.0, 5.0])

p('2. Categorías. Cada una vive dentro de un departamento y es lo que después permite dirigir una oferta.')
tabla(['Código', 'Categoría', 'Departamento'],
      [['1', 'Whisky', 'Licores'], ['2', 'Ron', 'Licores'], ['3', 'Vodka', 'Licores'],
       ['4', 'Tequila', 'Licores'], ['5', 'Ginebra', 'Licores'],
       ['6', 'Tintos', 'Vinos'], ['7', 'Blancos', 'Vinos'], ['8', 'Rosados', 'Vinos'], ['9', 'Espumosos', 'Vinos'],
       ['10', 'Nacionales', 'Cervezas'], ['11', 'Importadas', 'Cervezas'], ['12', 'Artesanales', 'Cervezas'],
       ['13', 'Gaseosas', 'Refrescos y aguas'], ['14', 'Jugos', 'Refrescos y aguas'],
       ['15', 'Agua', 'Refrescos y aguas'], ['16', 'Energizantes', 'Refrescos y aguas'],
       ['17', 'Hielo', 'Hielo y desechables'], ['18', 'Desechables', 'Hielo y desechables'],
       ['19', 'Picaderas', 'Snacks y picaderas'], ['20', 'Entregas', 'Servicios']],
      anchos=[2.0, 5.0, 6.0])

p('3. Marcas, para agrupar y para dirigir ofertas por marca.')
tabla(['Código', 'Marca', 'Código', 'Marca'],
      [['1', 'Presidente', '7', 'Absolut'],
       ['2', 'Corona', '8', 'Don Julio'],
       ['3', 'Heineken', '9', 'Marqués de Riscal'],
       ['4', 'Brugal', '10', 'Coca-Cola'],
       ['5', 'Barceló', '11', 'Pepsi'],
       ['6', 'Johnnie Walker', '12', 'Red Bull']],
      anchos=[2.0, 5.5, 2.0, 5.5])

p('4. Bancos, para transferencias, cheques y los depósitos del cierre de sucursal.')
tabla(['Código', 'Banco'],
      [['BPD', 'Banco Popular Dominicano'], ['BRD', 'Banreservas'], ['BHD', 'Banco BHD'],
       ['SCO', 'Scotiabank'], ['APA', 'Asociación Popular de Ahorros y Préstamos']],
      anchos=[2.5, 10.0])

nota('Las entregas no necesitan nada más: lo que el cliente deja para retirar se retira en una sucursal, la suya o la '
     'que él diga, y las sucursales ya están creadas.')

p('5. Programa de fidelidad (si lo van a usar): niveles y cómo se acumulan los puntos.')
tabla(['Nivel', 'Factor', 'Regla de acumulación sugerida'],
      [['Clásico', '1.0', '1 punto por cada RD$100 de compra'],
       ['Oro', '1.5', 'El mismo acumulado, multiplicado por el factor del nivel']],
      anchos=[3.0, 2.5, 9.5])

p('6. Topes de descuento por nivel de quien autoriza, para que nadie descuente de más.')
tabla(['Nivel', 'Tope sugerido'],
      [['Supervisor (nivel 5)', 'Hasta 10 % o RD$2,000 por factura'],
       ['Gerente (nivel 8)', 'Hasta 30 % o RD$20,000 por factura']],
      anchos=[5.0, 9.0])

p('7. Usuarios y roles de caja: al menos un cajero (solo vender y cobrar), un supervisor (autoriza descuentos, '
  'devoluciones, retiros y notas internas) y un gerente (además autoriza lo de mayor monto).')

p('8. Rangos de comprobantes fiscales por caja (E31, E32, E34, E44 y E45), con los números que le asignó la DGII.')

p('9. Clientes: los colmados, bares y restaurantes a los que les factura con crédito fiscal conviene registrarlos, con su '
  'contacto, sus teléfonos y su dirección de entrega. Los demás se buscan en la caja por cédula o RNC, y el listado de la '
  'DGII se puede cargar completo como se explica en 2.5.1.')

titulo('2.7.1. Usuarios y roles de caja (sugerencia)', 3)
p('Los roles dicen qué puede hacer cada quien y el nivel decide quién autoriza a quién: para autorizar hay que tener el '
  'permiso y un nivel igual o mayor al que lo pide.')
tabla(['Código', 'Rol', 'Nivel', 'Qué puede hacer'],
      [['CAJERO', 'Cajero', '1', 'Abrir turno, vender, cobrar, imprimir y cerrar su turno. No descuenta ni anula.'],
       ['SUPERVISOR', 'Supervisor', '5', 'Todo lo del cajero y además autoriza descuentos hasta su tope, anulaciones, devoluciones, retiros de efectivo, notas de crédito internas y apertura de gaveta.'],
       ['GERENTE', 'Gerente', '8', 'Todo lo anterior, más autorizar lo que pasa el tope del supervisor y cambiar el comprobante de una factura.']],
      anchos=[2.4, 2.6, 1.4, 10.0])
tabla(['Usuario', 'Nombre', 'Rol', 'Cajas asignadas'],
      [['C001', 'Cajero de la caja 01', 'CAJERO', 'Caja 01'],
       ['C002', 'Cajero de la caja 02', 'CAJERO', 'Caja 02'],
       ['S001', 'Supervisor de turno', 'SUPERVISOR', 'Caja 01 y Caja 02'],
       ['G001', 'Gerente de la sucursal', 'GERENTE', 'Todas las de su sucursal']],
      anchos=[2.2, 5.0, 3.0, 6.0])
nota('El nivel va del 1 al 9 y solo se usa para las autorizaciones: quien autoriza necesita el permiso y un nivel igual o '
     'mayor al de quien lo pide. Se sugieren 1, 5 y 8, y no 1, 2 y 3, para dejar huecos e intercalar después un rol '
     'intermedio (por ejemplo un encargado en el 6) sin tener que renumerar los que ya existen.')
nota('Cada persona con su propio usuario: el ticket, el cuadre y la auditoría dicen quién vendió y quién autorizó. Un usuario '
     'compartido hace imposible saberlo.')

titulo('2.7.2. Parámetros recomendados para el negocio (sugerencia)', 3)
p('Los parámetros vienen con un valor de arranque razonable; estos son los que conviene revisar según cómo trabaje el '
  'negocio de bebidas. Se cambian en Organización → Parámetros y pueden fijarse en general, por sucursal o por caja.')
tabla(['Parámetro', 'Valor sugerido', 'Por qué'],
      [['Fondo de caja', '3,000.00', 'Efectivo con el que abre el turno, para dar devuelta desde el primer cliente.'],
       ['Redondeo del efectivo', '0', 'Sin redondeo. Póngalo en 1 si no quiere entregar monedas de menos de un peso.'],
       ['Cierre ciego', 'Sí', 'El cajero declara lo que contó sin ver lo esperado: es lo que hace útil el cuadre.'],
       ['Vigencia de la nota de crédito', '180 días', 'Medio año para que el cliente use su saldo a favor.'],
       ['Días de retención del ITBIS en devoluciones', '30 días', 'Pasado ese plazo la devolución retiene el ITBIS, como manda la norma.'],
       ['Largo de la contraseña del Central (mínimo y máximo)', '10 · sin tope', 'Para los usuarios del Central Manager. El máximo es opcional: vacío, no hay tope.'],
       ['Largo de la clave de caja (mínimo y máximo)', '6 · sin tope', 'Para los usuarios de caja, aparte de los del Central: la escriben en el teclado de la caja. Si mínimo y máximo son iguales, la clave debe tener ese largo exacto.'],
       ['Monto que exige identificación', '250,000.00', 'Desde ese total la factura de consumo exige cédula o RNC.'],
       ['Retención de la Ley 32-23', '0 %', 'Solo para facturas gubernamentales (E45), y quien factura en e-CF está exento.'],
       ['Dígitos de la secuencia de documentos', '7', 'Diez millones de documentos por caja y tipo antes de crecer el número.'],
       ['Tipo de ingresos del e-CF', '01', 'Ingresos por operaciones: es lo que corresponde a la venta de mercancía.'],
       ['Valor del punto de fidelidad', '1.00', 'Cada punto vale un peso al canjearlo.'],
       ['Puntos mínimos para canjear', '50', 'Evita canjes de montos ínfimos.'],
       ['Meses de vigencia de los puntos', '12', 'Los puntos vencen al año de ganados.']],
      anchos=[5.0, 3.0, 9.0])
nota('Los parámetros obligatorios que no tengan valor se avisan arriba en la pantalla de Parámetros, y la operación que los '
     'necesita se rechaza hasta configurarlos.')
nota('En el módulo «Sincronización y mantenimiento» también se configura el ritmo de la caja: cada cuántos segundos sincroniza, '
     'cada cuánto baja los maestros, desde qué hora respalda y contra qué servidor verifica su reloj. Se leen en cada ciclo, así '
     'que cambiarlos aquí se nota en las cajas sin reinstalar ni reiniciar nada. Si no los configura, cada caja usa los valores '
     'con que fue instalada.')

titulo('2.8. Promociones', 2)
viñeta('Crear (/promociones): porcentaje, monto por unidad, precio especial, lleva X paga Y y precio desde cierta cantidad; '
       'por artículos, departamentos, categorías o marcas, en las sucursales que se elijan, con fechas, días y horario.')
viñeta('El código de la promoción no se escribe: lo da la secuencia «Promoción» (PRO000001) al guardarla, y no cambia. Su prefijo y '
       'sus dígitos se configuran en Organización → Secuencias de documentos.')
viñeta('Importar (/promociones/importar): carga masiva desde un archivo CSV; se valida todo el archivo y solo se publica si no hay errores. '
       'Una línea sin código crea una promoción nueva con el número de la secuencia; con código, actualiza esa promoción, que tiene que existir.')
viñeta('Simular (/promociones/simular): antes de publicar, muestra qué oferta tomaría la caja para un artículo, cantidad, sucursal y fecha.')
viñeta('Los artículos se agregan digitando el código (o el de barras) y Enter: entra de una vez a la lista, con el precio de antes y el que '
       'quedaría con la oferta, y el foco vuelve al código para seguir escaneando. El último agregado se ve arriba.')
viñeta('La lista muestra el estado de cada promoción y cuántas cajas ya la recibieron; se filtra por texto, sucursal y rango de fechas.')

titulo('2.8.1. Promociones para empezar (sugerencia)', 3)
p('Estas son promociones típicas del rubro, con los artículos y las categorías sugeridas más arriba. Antes de publicarlas '
  'conviene simularlas en Promociones → Simular.')
tabla(['Ejemplo', 'Promoción', 'Tipo', 'Alcance', 'Detalle'],
      [['PROM-3X2CER', 'Lleva 3 paga 2 en Corona', 'Lleva X paga Y', 'Artículo CER-COR-355', 'Lleva 3, paga 2. Fin de semana, viernes a domingo.'],
       ['PROM-VINO10', '10 % en vinos tintos', 'Porcentaje', 'Categoría Tintos', '10 % de descuento, todo el mes.'],
       ['PROM-CJPRE', 'Caja de Presidente a precio especial', 'Precio especial', 'Artículo CER-PRE-CJ', 'Precio fijo de 2,350.00 mientras dure la promoción.'],
       ['PROM-RON6', 'Ron por cantidad', 'Precio por cantidad', 'Artículo RON-BRU-AN', 'Desde 6 unidades, a 460.00 cada una.'],
       ['PROM-HAPPY', 'Happy hour de cervezas', 'Monto por unidad', 'Departamento Cervezas', 'RD$20 menos por unidad, de lunes a viernes de 5 a 8 de la tarde.']],
      anchos=[2.6, 4.4, 2.6, 3.4, 6.0])
nota('Cuando dos promociones alcanzan al mismo artículo, la caja aplica la más favorable para el cliente, y solo reemplaza al '
     'precio por mayor si mejora el precio. Los combos nunca toman precio por mayor.')

titulo('2.9. Facturación electrónica y DGII', 2)
viñeta('Rangos de e-CF (/fiscal/secuencias): se asignan a cada caja por tipo de comprobante, con inicio, fin y vencimiento. '
       'La lista muestra el último usado y cuánto queda.')
viñeta('Comprobantes enviados a la DGII (/monitor/comprobantes): estado de cada e-CF (aceptado, rechazado, en cola), su '
       'trackId, el mensaje de la DGII, la descarga del XML y el reenvío dirigido.')

titulo('2.9.1. Rangos de comprobantes (ejemplo)', 3)
p('Los números se los asigna la DGII a la empresa; aquí se reparten por caja y por tipo, para que dos cajas no usen el mismo. '
  'Este es el formato con el que se cargan en Fiscal → Rangos de e-CF.')
tabla(['Caja', 'Tipo', 'Desde', 'Hasta', 'Vence'],
      [['01', 'E32 · Consumo', '1', '10000', '31/12/2027'],
       ['01', 'E31 · Crédito fiscal', '1', '2000', '31/12/2027'],
       ['01', 'E34 · Nota de crédito', '1', '1000', '31/12/2027'],
       ['02', 'E32 · Consumo', '10001', '20000', '31/12/2027'],
       ['02', 'E31 · Crédito fiscal', '2001', '4000', '31/12/2027'],
       ['02', 'E34 · Nota de crédito', '1001', '2000', '31/12/2027']],
      anchos=[1.6, 4.4, 2.4, 2.4, 3.0])
nota('Los rangos de una caja no se solapan con los de otra. El sistema avisa en la barra de estado cuando queda poco del rango '
     'o está por vencer, y no deja facturar si se agota: por eso conviene pedir el próximo con tiempo.')
p('Al asignar el rango se elige primero la sucursal y después su caja. El campo «Próximo» se deja vacío casi siempre: el '
  'consumo arranca en el primer número del rango. Solo se llena cuando parte de esa numeración ya se usó en otro sistema y '
  'hay que continuar desde cierto número.')
nota('En el listado, «Próximo» es el número que la caja emitirá en su siguiente comprobante, y «Usado» es hasta dónde llegó '
     'según lo que ya subió al Central. La columna «Asignado» dice cuándo se cargó el rango y quién lo hizo.')

titulo('2.10. Facturas de las cajas', 2)
p('Ruta: /facturas. Es la vista de todo lo que las cajas subieron al Central.')
viñeta('Filtros por rango de días, sucursal, tipo (factura o nota de crédito) y búsqueda por número, e-NCF, documento o nombre del cliente.')
viñeta('Cada fila muestra el documento, la sucursal, la caja, el turno, el cajero, el cliente, el total y el estado del e-CF.')
viñeta('Al abrir una se ven sus líneas tal como se cobraron (código, descripción, cantidad, precio, descuento, ITBIS, importe, '
       'serial y oferta), los totales, el ITBIS por tasa y las formas de pago.')

titulo('2.11. Notas de crédito entre sucursales', 2)
p('Ruta: /notas-credito. Todas las notas emitidas por cualquier caja, con su saldo, lo consumido y lo retenido mientras una '
  'caja está cobrando. Sirve para responderle al cliente que quiere usar en una sucursal una nota emitida en otra. '
  'Las notas vencidas se habilitan subiendo los días de vigencia en los parámetros.')

titulo('2.12. Listas de boda y de regalos', 2)
p('Ruta: /listas-boda.')
paso('Para hacer una nueva, use «Nueva lista» del menú o el botón del listado: se abre una pantalla completa, no una '
     'ventanita.')
paso('Escriba los datos de los festejados (cédula o RNC, teléfono, correo) y los del evento (nombre, fecha, lugar).')
paso('Agregue los artículos sin soltar el teclado: digite el código y presione Enter (trae la descripción y el precio del '
     'día y salta a la cantidad), escriba la cantidad y otro Enter lo pasa a la lista y vuelve al código, vacío y listo '
     'para el siguiente. Si no escribe cantidad, vale una unidad. Lo último agregado queda arriba.')
paso('Si no se sabe el código, presione la lupa del campo y busque el artículo por código o por descripción. Los artículos '
     'siempre se eligen del maestro, nunca se escriben a mano, para que lo pedido sea exactamente lo que la caja cobra.')
nota('El precio que se ve al agregar es el del día, solo para orientar a los festejados: la lista guarda lo pedido, no '
     'precios, y la caja cobra el precio que esté vigente el día de cada compra. Por eso los artículos de una lista ya '
     'guardada muestran una raya en vez de precio.')
paso('El Central le asigna un número (por ejemplo LB000001): ese es el número que el cliente da en la caja.')
paso('A medida que la gente compra, la lista muestra lo comprado, lo que falta y las facturas registradas.')
paso('Cuando pasa el evento, la lista se cierra (y se puede reabrir si hace falta).')
p('El listado queda solo para buscar: escriba el número, el evento, el cliente o su documento, o filtre por estado. Al tocar '
  'una fila se abre esa lista en su pantalla, con lo comprado y las facturas registradas.')

titulo('2.13. Cotizaciones', 2)
p('Ruta: /cotizaciones. Es el presupuesto que se le arma a un cliente desde cualquier computadora con navegador: no hace '
  'falta que sea una caja. Los precios quedan congelados mientras la cotización esté vigente, y después cualquier caja de '
  'cualquier sucursal la convierte en factura buscándola por su número (F9).')
paso('Para hacer una nueva, use «Nueva cotización» del menú o el botón del listado: se abre una pantalla completa, no una '
     'ventanita.')
paso('Escriba el nombre del cliente y, si lo tiene, su cédula o RNC, teléfono y correo.')
paso('Agregue los artículos sin soltar el teclado: digite el código y presione Enter (trae la descripción y el precio del '
     'día y salta a la cantidad), escriba la cantidad y Enter (salta al descuento), y otro Enter lo pasa a la lista y '
     'vuelve al código, vacío y listo para el siguiente. La cantidad y el descuento salen vacíos: si no escribe nada, '
     'vale una unidad y sin descuento.')
paso('Si no se sabe el código, presione la lupa del campo: se abre una ventana para buscar el artículo por código o por '
     'descripción, y el que se elija cae en el campo del código. Si lo digitado no existe, esa ventana se abre sola con lo '
     'que se escribió.')
p('Lo último agregado queda arriba en la lista. El precio es el del maestro y no se digita: lo que se cotiza es el precio '
  'del día. La cantidad y el descuento sí se pueden corregir en la línea, y el total se ve arriba, al lado de la '
  'observación.')
paso('Guarde: el Central le asigna su número (por ejemplo COT000001) y su fecha de vencimiento, según los días '
     'configurados en Parámetros.')
paso('Con «Imprimir» sale el PDF en tamaño carta para entregárselo o enviárselo al cliente.')
p('El listado queda solo para buscar: escriba el número, el cliente, su documento o el número de la factura, o filtre por '
  'estado. Al tocar una fila se abre esa cotización en su pantalla.')
nota('Una cotización ya facturada o anulada se abre igual, pero solo para consultarla: no se modifica.')

titulo('2.14. Fidelidad', 2)
p('Ruta: /fidelidad/miembros. Miembros con su nivel, saldo de puntos, movimientos y ajustes. Los niveles y las reglas de '
  'acumulación se configuran en los catálogos.')

titulo('2.15. Despacho de pendientes y envíos', 2)
p('Ruta: /despacho/pendientes. Aquí se despacha TODO lo que quedó pendiente de entregar en cualquier sucursal. El despacho se '
  'hace en el Central y no en la caja: no emite comprobante fiscal ni toca la gaveta, y quien atiende a un cliente que llama o '
  'que llega a otra tienda necesita verlos todos. La caja solo crea el pendiente al cobrar.')
p('En la lista se ven el estado, la fecha comprometida y los atrasos; se filtra por sucursal, método, estado, o solo los '
  'abiertos o los atrasados, y se busca por número, factura, cliente, teléfono o destino.')
p('Al abrir un pendiente:')
paso('Preparación: se marca En preparación, Preparado y, si es un envío, Despachado cuando sale con el transportista. No se '
     'pueden saltar pasos.')
paso('Entrega: se escribe cuánto se entrega de cada artículo (puede ser todo o una parte), el serial de los serializados, y el '
     'nombre y la cédula de quien recibe. Con una entrega parcial el pendiente queda en Parcial y guarda lo que falta.')
paso('Constancia: de cada entrega se imprime un PDF en tamaño carta para que lo firme quien recibe. Se imprime en una '
     'impresora normal, no en la de tickets.')
paso('Anular: solo si todavía no se ha entregado nada, con motivo. Libera la mercancía para poder devolverla.')
nota('Anular un pendiente lleva su propio permiso (Central.Despacho.Anular), aparte del de operar el despacho: libera '
     'mercancía que ya se facturó.')
nota('Mientras algo siga pendiente de entregar, no se puede devolver: el cliente todavía no lo tiene. Para devolverlo hay que '
     'anular primero el pendiente. Si el negocio lo activa, el Central le avisa por correo al cliente cuando su pedido queda '
     'preparado.')

titulo('2.16. Secuencias de documentos', 2)
p('Ruta: /organizacion/secuencias. Aquí se dice cómo se numera cada documento que emite el Central: el prefijo que lleva '
  'delante, cuántos dígitos tiene el correlativo y por cuál va.')
nota('Sin su secuencia, el documento NO se puede crear: el sistema lo rechaza diciendo cuál falta. Es a propósito, para que '
     'la numeración la decida el negocio y no se invente sola la primera vez que alguien hace el documento.')
p('El Central le pone su propio número a los documentos que le suben las cajas. Una factura llega con el número de la caja '
  '(010110000001) y el Central le agrega el suyo (FAC000001): los dos quedan guardados y se ven juntos en el listado de '
  'facturas. Lo mismo con las notas de crédito, los despachos y el cierre de sucursal. Las cotizaciones, las listas de boda y '
  'las promociones toman de aquí su número al crearse (COT000001, LB000001, PRO000001).')
nota('Si al llegar un documento de la caja falta su secuencia, el documento se guarda igual pero sin número del Central: la '
     'caja ya lo emitió y perderlo sería peor. Configure la secuencia y los siguientes ya lo traerán.')
p('En el sistema hay tres numeraciones distintas y conviene no confundirlas:')
tabla(['Numeración', 'Quién la lleva', 'Cómo se ve'],
      [['Documentos de la caja', 'Cada caja, en su propia base', 'Sucursal + caja + tipo + secuencia (010110000001)'],
       ['Documentos del Central', 'El Central, en esta pantalla', 'Prefijo + correlativo (COT000001)'],
       ['Comprobantes fiscales', 'La caja, con el rango que da la DGII', 'e-NCF (E320000000001)']],
      anchos=[5.0, 6.0, 6.0])
p('La de la caja funciona sin internet: la caja factura aunque el Central esté apagado. La de esta pantalla numera lo que '
  'nace en el Central (cotizaciones, listas de boda). Y el e-NCF se administra en Fiscal, que es otra cosa.')
viñeta('El CÓDIGO es con lo que el sistema encuentra la secuencia y no se cambia. El PREFIJO es solo lo que se ve delante '
       'del número, y ese sí se puede cambiar cuando quiera: lo que ya se emitió conserva el prefijo con el que salió.')
paso('Para agregar la numeración de un documento nuevo, presione «Nueva secuencia» y escriba su código, su prefijo y cómo '
     'se llama.')
paso('Para continuar una numeración que venía de otro sistema, cambie «Último número entregado». Solo se puede subir: '
     'bajarlo repetiría números ya usados.')
paso('Desactivar una secuencia impide crear ese documento y conserva el contador.')

titulo('2.17. Cierre consolidado de sucursal', 2)
p('Ruta: /cierres-sucursal. Es el cierre del día de toda la sucursal.')
paso('Elija la sucursal y el día y presione Preparar: se ven todos los cierres de caja, las formas de pago sumadas y lo que falta (si algún turno no ha cerrado, lo dice).')
paso('El sistema calcula el efectivo a depositar por moneda (las tarjetas y transferencias no se depositan).')
paso('Registre los depósitos: banco, número de boleta, monto y fecha. Puede ser más de uno.')
paso('Cierre la sucursal: queda la diferencia entre lo depositado y lo que había que depositar, y ya no se modifica.')
nota('Si una caja informa un cierre de ese día después de consolidar, el consolidado no cambia, pero la lista lo avisa.')

titulo('2.18. Reportes', 2)
p('Ruta: /reportes. Todos por rango de días y, si se quiere, por sucursal o caja. Cada uno se descarga en Excel y en PDF.')
tabla(['Reporte', 'Qué muestra'],
      [['Ventas', 'Por día, sucursal y caja: facturas, notas de crédito, subtotal, descuento, ITBIS y total.'],
       ['ITBIS por tasa', 'Base e impuesto de cada tasa del período.'],
       ['Formato 607', 'Un registro por comprobante para la DGII, con la descarga del archivo de envío.'],
       ['Cuadres de caja', 'Turno, cajero, esperado, declarado y diferencia.'],
       ['e-CF y DGII', 'Estado de cada comprobante enviado.'],
       ['Sincronización', 'Última comunicación de cada caja, mensajes, rechazos y alertas.']],
      anchos=[4.5, 12.5])

titulo('2.19. Monitor de sincronización', 2)
viñeta('/monitor: estado de cada caja, cuánto hace que no se comunica y cuántos documentos trae pendientes.')
viñeta('/monitor/conflictos: documentos que el Central no pudo aceptar (por ejemplo un número repetido), para resolverlos.')

titulo('2.20. Chequeador de precios', 2)
p('Ruta: /chequeador/01, donde 01 es el código de la sucursal, en la pantalla que se pone en el pasillo de la tienda. Tecnología '
  'deja cada pantalla con la dirección de su sucursal, así el cliente nunca elige sucursal y siempre ve el precio y las ofertas '
  'de la tienda donde está parado. El cliente pasa el producto por el lector y ve la descripción, el precio grande, el precio '
  'por cantidad y las ofertas vigentes; la consulta se borra sola a los pocos segundos para el siguiente cliente. Los precios '
  'se muestran con el ITBIS ya sumado, que es lo que el cliente paga.')
nota('El chequeador viene apagado: se enciende en Parámetros, con Central.Chequeador.Habilitado.')
nota('Si la dirección trae un código de sucursal que no existe o está inactiva, la pantalla dice «Página no encontrada» y no '
     'consulta nada: mostrar el precio de otra sucursal sería engañar al cliente.')

doc.add_page_break()

# ---------------------------------------------------------------- PARTE 2: LA CAJA
titulo('3. La caja, paso a paso')

titulo('3.1. Instalar una caja desde cero', 2)
p('Cada caja tiene su propia base de datos en su propio equipo: por eso sigue vendiendo aunque se caiga la red. '
  'La instalación la hace tecnología, una sola vez por caja.')
paso('En el Central, cree la sucursal y la caja (Organización → Cajas), con la IP fija del equipo donde va a correr. Después, '
     'en el menú de esa caja, «Emitir credencial»: el secreto se muestra una sola vez, cópielo.')
paso('En el equipo de la caja, instale SQL Server Express y ejecute scripts/base-datos/estructura_base_datos_pos.sql. '
     'Crea la base CgPosCaja vacía: no lleva datos, todo baja del Central.')
paso('Instale el programa de la caja con scripts/caja/instalar-caja.ps1, indicando el número de sucursal, el de caja y '
     'la dirección del Central. No se le pide ninguna clave.')
paso('Arranque la caja y abra cualquiera de sus pantallas. Como todavía no sabe cuál es, le pedirá cinco datos: el código de '
     'su sucursal, el de la caja, la IP de ese equipo, la dirección del Central y la credencial que copió. Además le pedirá su '
     'usuario y contraseña del Central, para saber quién está montando esa caja.')
paso('Copie el certificado digital de la empresa en el equipo. El PIN no se guarda: lo digita un supervisor en la caja.')
paso('Ya con su credencial, la caja baja artículos, precios, usuarios, parámetros y sus rangos de comprobantes.')
nota('Si la caja avisa que la base no existe o le faltan tablas, es que no se ejecutó el script en ese equipo.')
nota('Quien configura una caja tiene que ser un usuario del Central con el permiso «Autorizar la configuración de una caja '
     'desde el propio equipo». El Central lo comprueba en el momento, junto con la credencial y la dirección del equipo, y deja '
     'anotado quién configuró esa caja. La contraseña no se guarda en la caja: solo el nombre del usuario.')
nota('La credencial queda cifrada en la base de esa caja: no se vuelve a ver y copiarla a otra máquina no sirve. En cada '
     'comunicación el Central comprueba los cuatro datos juntos —sucursal, caja, IP y credencial—, así que dos cajas no pueden '
     'usar lo mismo.')

titulo('3.1.1. La impresora de tickets', 3)
p('La caja imprime en la impresora térmica del mostrador. Se indica una sola vez, al instalarla, y hay tres formas según '
  'cómo esté conectada:')
tabla(['Tipo', 'Cuándo se usa', 'Qué se indica'],
      [['Windows', 'La impresora está instalada en el equipo (USB o serie). Es lo más común.', 'El nombre exacto con el que aparece en Dispositivos e impresoras de Windows.'],
       ['Red', 'La impresora tiene su propio puerto de red y su dirección.', 'Su dirección y su puerto (normalmente 9100).'],
       ['Archivo', 'Todavía no hay impresora: sirve para probar la caja.', 'La carpeta donde se van guardando los tickets.']],
      anchos=[2.2, 7.0, 7.6])
nota('La gaveta del dinero se conecta a la impresora, no al equipo: se abre con un pulso que viaja por el mismo cable. Por eso '
     'no se configura aparte.')
nota('Si cambia la impresora de una caja, hay que reiniciar el servicio de la caja para que lo tome.')

titulo('3.1.2. La publicidad de la pantalla del cliente', 3)
p('El segundo monitor muestra la venta y, al lado, la publicidad del negocio. Los archivos se copian en la carpeta '
  r'C:\CGPOS\Publicidad' ' del equipo de esa caja y se muestran en orden alfabético, así que conviene nombrarlos '
  '01-..., 02-... y así.')
tabla(['Qué se puede poner', 'Formatos', 'Cuánto dura en pantalla'],
      [['Imágenes', '.png, .jpg, .jpeg, .webp, .svg', 'Los segundos del parámetro Pantallas.SegundosPorImagen'],
       ['Videos', '.mp4, .webm', 'Lo que dure el video: se reproduce completo y sin sonido']],
      anchos=[3.2, 5.5, 8.1])
nota('Los archivos se pueden cambiar con la caja encendida: entran solos cuando el carrusel termina la vuelta, sin '
     'reiniciar nada. Y si cambia los segundos por imagen en el Central, la pantalla lo toma en cuanto la caja '
     'sincroniza.')
nota('Un video .avi o .mov no se ve: conviértalo a .mp4 antes de copiarlo. Sin bocinas no se pierde nada, porque el '
     'video va siempre sin sonido.')

titulo('3.1.3. Cambiar una caja de equipo', 3)
p('El equipo de la caja se dañó, se reinstaló Windows o se reemplaza por otro. La caja es la misma; lo que cambia es la máquina.')
paso('En el Central, Organización → Cajas, edite la caja y ponga la IP del equipo nuevo; después emítale una credencial nueva.')
paso('Instale la caja en el equipo nuevo, con la misma sucursal y el mismo número.')
paso('Arránquela y llene su pantalla de configuración con los mismos códigos, la IP nueva y la credencial nueva.')
nota('Si el equipo se perdió o se lo robaron, revoque la credencial de inmediato: la anterior deja de servir aunque enciendan '
     'el equipo.')
nota('Mientras los datos no cuadren, la caja sigue vendiendo con lo que tiene en su base; lo que no hace es sincronizar. En su '
     'barra de estado dirá «Sin conexión (credenciales)», para distinguirlo de quedarse sin red.')

titulo('3.2. Entrar a la caja', 2)
paso('En la pantalla de ingreso digite su usuario y su clave (no hay PIN ni carné: siempre usuario y clave).')
nota('Una caja recién configurada tarda en tener sus datos: mientras el Central se los manda, la pantalla dice '
     '«Actualizando los datos de la caja…», en qué anda y cuántos artículos lleva de cuántos. No hay nada que hacer, solo esperar: la pantalla se habilita '
     'sola al terminar. Si en vez de eso sale un aviso rojo, ahí sí hay algo que revisar, y debajo dice qué fue lo que '
     'no se pudo aplicar.')
paso('Si se equivoca varias veces seguidas, el usuario se bloquea por unos minutos; un supervisor lo desbloquea desde el Central.')
paso('Si la caja no tiene turno abierto, el sistema le pide abrirlo.')

titulo('3.3. Abrir el turno', 2)
p('El turno es el período de trabajo de un cajero en esa caja. Solo puede haber un turno abierto por caja.')
viñeta('Al abrir se digita el fondo de caja (el sistema sugiere el monto configurado).')
viñeta('Si la caja tiene abierto el turno de otro cajero, aparece Relevar turno: con la autorización de un supervisor, '
       'usted toma el turno sin cerrarlo ni cuadrar.')
viñeta('Cerrar el turno es definitivo: la caja no puede volver atrás. Cuente con calma antes de cerrar; si de todos modos '
       'quedó mal, la corrección se hace en el Central.')

titulo('3.4. La pantalla de ventas principal', 2)
p('La pantalla está dividida en cinco zonas:')
tabla(['Zona', 'Para qué sirve'],
      [['Encabezado izquierdo', 'Tipo de comprobante que se va a emitir (E31, E32, E44 o E45). Al tocarlo se abre el cliente.'],
       ['Encabezado central', 'Cliente de la factura, número de factura que le tocará (se toma de verdad al cobrar: si otra venta '
        'se cobra antes, esta pasa al siguiente), cantidad de artículos, límite de compra, programa de fidelidad y lista de boda.'],
       ['Encabezado derecho', 'Subtotal (la suma de las líneas, sin ITBIS), ITBIS (o el aviso de exenta en régimen especial), descuentos, TOTAL (subtotal más ITBIS) y, en facturas gubernamentales con retención, el total a pagar.'],
       ['Campo de escaneo', 'Donde el lector escribe el código. También se puede digitar. A su derecha: catálogo, teclado en '
        'pantalla, Buscar (F2), Totalizar (F8) y el botón ☰ que abre el panel de funciones.'],
       ['Grilla de líneas', 'Los artículos de la venta: línea, código, descripción, cantidad, precio y subtotal sin ITBIS, y la oferta aplicada.'],
       ['Panel de funciones (☰)', 'Se despliega desde la derecha con todas las funciones. Se cierra solo al elegir una, al tocar '
        'fuera o al escanear: lo que se lee va a la venta.'],
       ['Barra de estado (abajo)', 'Usuario, caja, turno, versión, estado del certificado e-CF y estado de la sincronización con el Central.']],
      anchos=[5.0, 12.0])

titulo('3.5. La pantalla de ventas secundaria', 2)
p('La caja tiene tres pantallas, cada una en su monitor: la de ventas principal (con lector y teclado), la de ventas '
  'secundaria (táctil, con el catálogo en mosaicos, como el mostrador de una cafetería) y la del cliente. Las abre el '
  'equipo al encenderse; no hay que navegar de una a otra.')
p('La pantalla de ventas secundaria trabaja la misma venta que la principal: lo que se agrega en una aparece en la otra al '
  'instante, y se cobra desde cualquiera de las dos.')
tabla(['Parte de la pantalla', 'Qué hace'],
      [['Columna izquierda', 'La venta: sus líneas, los totales y el botón Cobrar. Al tocar el encabezado se abre cliente y comprobante.'],
       ['Tocar una línea', 'La selecciona; con los botones de abajo se cambia la cantidad o se quita (la de quitar pide autorización).'],
       ['Buscador (arriba a la derecha)', 'Busca por descripción o por código lo que no está en los mosaicos.'],
       ['Pestañas de departamento y categoría', 'Filtran los mosaicos, como las secciones de un menú.'],
       ['Mosaicos', 'Un toque agrega el artículo con su imagen y su precio. Aparecen los artículos marcados para el catálogo.'],
       ['Barra de abajo', 'Cliente, consulta de precio, facturas en espera y devoluciones.']],
      anchos=[5.5, 11.5])
nota('Los artículos que se pesan o que piden serial se atienden mejor en la pantalla principal; desde la secundaria se agrega '
     'una unidad y luego se ajusta la cantidad.')

titulo('3.6. Las teclas de función', 2)
p('Buscar (F2) y Totalizar (F8) están siempre a la vista, junto al campo de escaneo. Todas las demás están en el panel de '
  'funciones, que se abre con el botón ☰. Las teclas F del teclado físico funcionan siempre, con el panel abierto o cerrado.')
p('Teclas de función:')
tabla(['Tecla', 'Qué hace'],
      [['F2', 'Buscar un artículo por descripción'],
       ['F3', 'Límite de compra que pidió el cliente: avisa al pasarse y, al totalizar, pide confirmar «¿Cobrar de todas formas?» (queda en la auditoría)'],
       ['F4', 'Cambiar la cantidad de la línea seleccionada'],
       ['F5', 'Tomar el peso de la balanza'],
       ['F6', 'Lista de boda: asociar la venta a una lista de regalos'],
       ['F7', 'Facturas en espera (guardar la actual y retomar otra)'],
       ['F8', 'Totalizar: abre el cobro'],
       ['F9', 'Cotización: traer un presupuesto hecho en el Central'],
       ['F10', 'Ir a devoluciones'],
       ['F11', 'Consultar el precio de un artículo sin venderlo'],
       ['F12', 'Cliente, comprobante y programa de fidelidad']],
      anchos=[3.0, 14.0])
p('Más operaciones (en el mismo panel, debajo): catálogo en mosaicos, eliminar línea, eliminar por escaneo, '
  'limpiar pantalla, descuento a la línea, descuento a la factura, entrega o envío, anular, suspender, gaveta, '
  'reimprimir, retiro de efectivo, cierre de turno y salir.')
nota('El teclado en pantalla de códigos, documentos y textos cambia entre números y letras con la tecla ABC / 123. El de '
     'cantidades y montos es solo numérico.')

titulo('3.7. Hacer una venta', 2)
paso('Pase el código del artículo por el lector (o dígitelo y presione Enter). Para varias unidades: 12*CEM-425.')
paso('Para cambiar una cantidad, toque la cantidad en la línea o use F4. Para ver el otro código del artículo, toque el código.')
paso('Si el artículo se vende por peso, se toma el peso de la balanza (F5) o se digita.')
paso('Si el artículo lleva serial, el sistema lo pide al escanearlo.')
paso('Con F12 asigne el cliente si lleva comprobante fiscal, y su cédula del programa de fidelidad.')
paso('Revise el total con el cliente y presione F8 para cobrar.')
nota('Cada operación se guarda al instante: si la caja se apaga, al volver a entrar la venta aparece tal como estaba.')

titulo('3.8. Cliente y tipo de comprobante (F12)', 2)
p('Se digita la cédula o el RNC; la caja lo busca en los clientes, que bajan del Central. Si no aparece, se digita el nombre. '
  'El tipo de comprobante sale del cliente y cambiarlo a mano requiere permiso.')
tabla(['Comprobante', 'Cuándo se usa'],
      [['E32 – Consumo', 'Cliente común. Desde el monto configurado (RD$250,000 por defecto) exige cédula o RNC.'],
       ['E31 – Crédito fiscal', 'Empresa que necesita el ITBIS. Exige RNC o cédula.'],
       ['E44 – Régimen especial', 'Zonas francas, diplomáticos y demás acogidos a un régimen especial, que deben presentar su carné o certificación de exención de la DGII. La factura va SIN ITBIS.'],
       ['E45 – Gubernamental', 'Instituciones del Estado. Exige RNC. Lleva ITBIS, salvo que la entidad presente su certificación de exención.']],
      anchos=[5.0, 12.0])
p('Factura exenta (E44): al elegir este comprobante, toda la factura pasa a ser exenta de ITBIS, tengan o no impuesto los '
  'artículos. Como los precios ya van sin ITBIS, simplemente no se le suma: un artículo de RD$100 se cobra a RD$100 y no a '
  'RD$118. La '
  'pantalla, la pantalla del cliente y el ticket lo indican con “EXENTA DE ITBIS – RÉGIMEN ESPECIAL”. Si el bien que se '
  'vende no está exento para ese cliente, no se usa el E44: se le factura con E31.')
p('Entidad del Estado con exención (E45): si la institución presenta su certificación de exención de ITBIS, digite el número '
  'en el diálogo del cliente y toque “Aplicar exención”. La factura pasa a ser exenta y el ticket lo indica con el número de '
  'la certificación. Si no la presenta, la factura lleva su ITBIS normal.')
p('Retención de la Ley 32-23: en las facturas E45, si el negocio configuró el porcentaje, el sistema lo calcula sobre el '
  'subtotal ya con descuentos y se lo descuenta a lo que el cliente paga en caja. La factura mantiene su total; el ticket '
  'muestra “RETENCIÓN LEY 32-23” y “TOTAL A PAGAR”. Normalmente va en cero: quien factura electrónicamente está exento de '
  'esa retención.')

titulo('3.9. Lista de boda (F6)', 2)
paso('Pida al cliente el número de la lista (la crea la administración en el Central).')
paso('Presione F6, digite el número y acepte: la lista queda en el encabezado de la venta y sale en el ticket.')
paso('Al cobrar, la compra queda registrada en la lista y, si el negocio lo tiene configurado, baja las cantidades pedidas.')
nota('La lista se consulta en el Central: si no hay comunicación, no se puede asociar. Una lista cerrada no se acepta.')

titulo('3.10. Descuentos y ofertas', 2)
viñeta('Ofertas: se aplican solas según lo configurado en el Central. La columna Promo muestra cuál se aplicó; al tocarla '
       'se ve el detalle y se puede desactivar (con permiso).')
viñeta('Descuento a la línea: toque el precio de la línea. Descuento a la factura: panel de funciones (☰). Ambos piden '
       'motivo y la autorización de quien tenga tope suficiente; si el descuento pasa su tope, se pide una clave de nivel superior.')
viñeta('No se aplican descuentos manuales a artículos en oferta ni a departamentos que el negocio excluyó.')
viñeta('Descuento del banco por tarjeta: se aplica solo, al pasar la tarjeta en el cobro (ver 3.11).')

titulo('3.11. Cobrar (F8)', 2)
p('En la pantalla de cobro elija la forma de pago, digite el monto y agréguelo. Se puede combinar cuantas formas haga falta; '
  'arriba siempre se ve lo pagado, lo que falta o la devuelta.')
tabla(['Forma de pago', 'Qué pide y qué hay que saber'],
      [['Efectivo', 'Es el único que da devuelta. Hay botones de billetes rápidos y “Exacto”.'],
       ['Tarjeta', 'Se presiona Pasar tarjeta: el terminal pide la tarjeta al cliente. Si el terminal lee el BIN, el descuento del banco se aplica solo antes de cobrar y se le cobra menos. Si el terminal no responde, se puede registrar la aprobación manual con autorización de supervisor.'],
       ['Dólares u otra moneda', 'Se convierte con la tasa del día; la devuelta se da en pesos.'],
       ['Transferencia o cheque', 'Piden banco y número.'],
       ['Bono o tarjeta de regalo', 'Pide el serial. No se acepta con crédito fiscal.'],
       ['Nota de crédito', 'Se escanea el código de la nota. El sistema valida que exista, esté vigente y tenga saldo; si es de otra sucursal, lo consulta al Central.'],
       ['Puntos de fidelidad', 'Requiere cliente con cédula inscrita y autorización para canjear.']],
      anchos=[4.0, 13.0])
p('Al terminar: se emite y firma la factura electrónica, se imprime el ticket, se abre la gaveta si hubo efectivo y empieza '
  'la siguiente venta. La pantalla del cliente muestra el total y la devuelta.')
nota('Si no hay e-NCF disponible o el certificado no está cargado, la venta NO se cobra: primero hay que resolverlo con '
     'administración. El sistema no permite facturar sin comprobante fiscal.')

titulo('3.12. Facturar una cotización (F9)', 2)
p('Una cotización es el presupuesto que administración le hizo al cliente desde el Central, con los precios del momento. El '
  'cliente llega con el papel y se le factura sin volver a digitar nada.')
paso('Presione F9 y escanee o digite el número de la cotización (empieza con COT).')
paso('La caja se la pide al Central y carga los artículos con los precios que se le prometieron al cliente: no se les aplican '
     'ofertas ni se recalculan.')
paso('Cobre normalmente. El ticket sale con el número de la cotización y en el Central queda marcada como facturada.')
nota('Si la cotización venció, la caja pide la autorización de un supervisor y el motivo queda registrado. Una cotización ya '
     'facturada o anulada no se puede volver a usar.')
nota('La cotización vive en el Central: si la caja está sin comunicación, no se puede traer. La pantalla lo dice con esas '
     'palabras, y siempre se puede facturar a mano.')
nota('La venta tiene que estar vacía: si ya tiene artículos, termínela o límpiela antes de traer la cotización.')

titulo('3.13. Facturas en espera, anular y suspender', 2)
viñeta('F7 – En espera: guarda la venta actual para atender a otro cliente y retomarla después. Se le pone una referencia '
       'corta, el nombre del cliente o unos dígitos («Sra. María», «102»), que es con lo que se encuentra en la lista al volver. '
       'Dos facturas en espera del mismo turno no pueden llamarse igual.')
viñeta('Para retomar una factura con otra venta en pantalla, escriba también la referencia de la que está en pantalla: las dos '
       'se intercambian. Si la de pantalla no tiene artículos, simplemente se descarta.')
viñeta('Mientras la venta se arma, la pantalla muestra el número de factura que le tocaría, pero el número se le da de verdad '
       'al cobrar, en el orden en que se cobra: una factura en espera no se lleva el número de otro cliente, y una venta que no '
       'se cobró no deja un hueco en la numeración. En la auditoría, la venta sin cobrar aparece como B-000015.')
viñeta('Una venta con tarjeta ya aprobada no se pone en espera: cóbrela o anule la tarjeta primero.')
viñeta('Anular (panel de funciones ☰): cancela la transacción en curso con motivo y autorización. Como todavía no era una factura, '
       'desaparece de la caja; lo que tenía, quién la anuló, quién lo autorizó y el motivo quedan en la auditoría.')
viñeta('Suspender: bloquea la pantalla; se reanuda con la clave del cajero.')
viñeta('Eliminar línea, eliminar por escaneo y limpiar pantalla piden autorización de supervisor; la línea eliminada queda '
       'tachada en su lugar, con su mismo número, y sale del total. La numeración de las líneas sigue continua; quién la '
       'eliminó y quién lo autorizó queda en la auditoría.')

titulo('3.14. Entregas y envíos', 2)
p('Cuando el cliente se lleva parte de la mercancía después:')
paso('En el panel de funciones (☰), elija Entrega / envío.')
paso('Marque qué líneas y qué cantidad quedan pendientes, y si el cliente lo retira en una sucursal (la suya u otra) o se le envía a una dirección, con la fecha comprometida.')
paso('Al cobrar se imprime un comprobante de pendiente por cada destino, con código de barras: una copia para el cliente y otra '
     'para quien despacha.')
paso('De ahí en adelante el pendiente se despacha desde el Central (Despacho → Pendientes): la caja ya no tiene nada que ver '
     'con él. Vea «2.14. Despacho de pendientes y envíos».')

titulo('3.15. Devoluciones y notas de crédito', 2)
p('Se entra con F10 o directamente a /devoluciones.')
paso('Escanee el código de barras del ticket o digite el e-NCF de la factura.')
paso('Indique qué se devuelve de cada línea (el sistema muestra lo vendido, lo ya devuelto y lo disponible).')
paso('Elija el motivo y, si la factura no tiene cliente, digite la cédula o el RNC.')
paso('Indique cómo se le devuelve: saldo en la nota (lo normal), efectivo, a la tarjeta o cheque, según lo que el negocio permita.')
paso('Pida la autorización del encargado: se imprime la nota de crédito para el cliente y la copia de contabilidad.')
p('Tipos de nota de crédito:')
tabla(['Tipo', 'Para qué', 'Importante'],
      [['Nota de crédito E34 (normal)', 'Devolución de mercancía', 'Es un comprobante fiscal: va a la DGII y al 607. Deja saldo que el cliente usa como pago hasta su vencimiento.'],
       ['Nota de crédito interna', 'Corregir un problema de la factura sin devolver dinero', 'No lleva comprobante fiscal, no va a la DGII, no deja saldo y no sirve como forma de pago. Requiere autorización de un supervisor. Sale en los reportes de ventas.']],
      anchos=[4.5, 5.5, 7.0])
nota('La vigencia de las notas de crédito se cuenta desde su emisión con los días configurados HOY en el Central: si una nota '
     'se venció y el negocio decide aceptarla, se suben los días en el Central y vuelve a poder usarse.')

p('De dónde sale la factura:')
viñeta('La factura SIEMPRE se busca en el Central, aunque la haya vendido esta misma caja. El Central es el único que sabe '
       'cuánto se devolvió ya de cada línea en toda la empresa, y una nota de crédito nunca se hace contra una factura que '
       'el Central no tenga registrada.')
viñeta('Se puede devolver una factura de cualquier sucursal y de cualquier caja. Si no es de esta caja, la pantalla avisa de '
       'qué sucursal y caja viene.')
viñeta('La nota de crédito la emite SIEMPRE la caja donde está el cliente, con su propio certificado y su propio rango de '
       'e-NCF. La factura solo se consulta.')
viñeta('Mientras se emite, el Central le retiene esas líneas a esta caja, para que otra no devuelva al mismo tiempo la misma '
       'mercancía. La retención se suelta sola a los minutos configurados en el Central si la nota no se llega a emitir.')
viñeta('La copia de la factura que trajo el Central se borra en cuanto se emite la nota: la caja no guarda facturas.')
nota('Las notas de crédito necesitan conexión con el Central. Si la caja está sin red, o si la factura es tan reciente que '
     'todavía no ha terminado de subir, la pantalla lo dice con esas palabras en vez de dejar al cajero esperando.')

titulo('3.16. Retiros, pre-cierre y cierre de turno', 2)
viñeta('Retiro de efectivo: monto y motivo, autorización de supervisor, comprobante impreso con firmas. No se puede retirar '
       'más del efectivo que hay en la gaveta.')
viñeta('Pre-cierre: imprime lo esperado, con clave de supervisor (útil antes de cuadrar).')
viñeta('Cierre de turno: se declara lo que hay por forma de pago y se cuenta el efectivo por denominaciones. Normalmente el '
       'cajero no ve lo esperado (cierre ciego).')
viñeta('No se puede cerrar con facturas en espera, transacciones con artículos sin cobrar o ventas sin factura electrónica '
       'firmada: la pantalla dice exactamente qué falta.')
viñeta('Cerrar lote: cierra el lote del terminal de tarjetas y compara lo aprobado en la caja con lo que reporta el terminal.')
viñeta('Al cerrar se imprime el reporte del turno: esperado, declarado y diferencia por forma de pago, denominaciones, '
       'retiros, reembolsos y relevos.')

titulo('3.17. La barra de estado', 2)
p('Abajo de la pantalla, siempre a la vista:')
viñeta('Sincronización: si la caja está comunicada con el Central y cuántos documentos están pendientes de enviar. '
       'Mientras baja datos dice «Actualizando» y en qué anda, y al pasar el mouse por encima se lee el detalle del '
       'último error, si lo hubo.')
viñeta('e-CF: si el certificado está cargado, cuántos comprobantes quedan en el rango y si algo está por vencer. '
       'Al tocarlo se digita el PIN del certificado cuando hace falta.')
viñeta('Avisos de la base de datos, la hora del equipo y el respaldo.')

doc.add_page_break()

# ---------------------------------------------------------------- Flujo completo
titulo('4. Recorrido completo del sistema (para probarlo todo)')
p('Este es el orden recomendado para recorrer el sistema de punta a punta y comprobar que todo funciona.')

titulo('Primero, en el Central', 2)
paso('Cree la base del Central con scripts/base-datos/estructura_base_datos_central.sql y entre con el usuario ADMIN (cambie su contraseña).')
paso('Revise Organización: cree la empresa, la sucursal y la caja. La credencial la pedirá la caja al instalarse.')
paso('En Parámetros, configure lo que el negocio necesita: fondo de caja, redondeo, días de vigencia de notas de crédito, '
     'retención de la Ley 32-23 (si aplica), chequeador y listas de boda.')
paso('Revise los catálogos que ya trae la base (monedas, impuestos, unidades, formas de pago, denominaciones, motivos) y '
     'cree los suyos: departamentos, categorías, marcas y bancos.')
paso('Cree los artículos sugeridos en el manual (o los suyos) con sus códigos de barras y precios, y al menos una promoción.')
paso('Asigne los rangos de e-CF a la caja (E31, E32, E34, E44, E45).')
paso('Cree los usuarios de caja: un cajero, un supervisor y un gerente, con sus niveles.')
paso('Cree una lista de boda de prueba y anote su número.')

titulo('Después, en la caja', 2)
paso('Cree la base de la caja con scripts/base-datos/estructura_base_datos_pos.sql e instale la caja con su sucursal, su número y la dirección del Central.')
paso('Llene la pantalla de configuración de la caja con su sucursal, su código, su IP, la dirección del Central y la credencial.')
paso('Espere el primer ciclo de sincronización: la caja baja artículos, precios, usuarios y parámetros del Central.')
paso('Entre con el usuario del cajero y abra el turno con su fondo.')
paso('Escanee artículos, cambie una cantidad y elimine una línea (le pedirá la clave del supervisor).')
paso('Con F12 asigne un cliente con RNC y verifique que el comprobante cambia a crédito fiscal.')
paso('Pruebe un descuento a la línea y otro a la factura, con su motivo y autorización.')
paso('Con F6 asocie la lista de boda de prueba.')
paso('Cobre con F8: primero parte en efectivo y el resto con tarjeta, y verifique el ticket y la devuelta.')
paso('Repita una venta y cóbrela completa en efectivo, para tener dos facturas.')
paso('Vaya a devoluciones (F10), devuelva un artículo de la primera factura y emita la nota de crédito.')
paso('Haga una segunda devolución marcando Nota de crédito interna y compruebe que no se puede usar como pago.')
paso('Cobre una tercera venta usando la nota de crédito normal como forma de pago.')
paso('Haga un retiro de efectivo con motivo y autorización.')
paso('Cierre el turno: declare por forma de pago, cuente las denominaciones y revise el reporte impreso.')

titulo('Por último, de vuelta en el Central', 2)
paso('En Facturas, busque las facturas y notas de crédito que acaba de hacer y abra su detalle.')
paso('En Listas de boda, compruebe que la compra quedó registrada y que bajó lo pedido.')
paso('En Notas de crédito, revise el saldo de la nota emitida y su consumo.')
paso('En Cierre de sucursal, prepare el día, registre el depósito y ciérrelo.')
paso('En Reportes, saque Ventas, ITBIS, Cuadres y el Formato 607, y descárguelos en Excel y PDF.')
paso('En Monitor, verifique que la caja está comunicada y sin documentos pendientes ni conflictos.')
paso('Abra el chequeador de precios y consulte uno de los artículos creados.')

doc.add_page_break()

# ---------------------------------------------------------------- Qué hacer si
titulo('5. Qué hacer si…')
tabla(['Situación', 'Qué pasa y qué hacer'],
      [['La caja o el Central avisan que falta la base de datos', 'No se ejecutó el script de creación en ese equipo, o el sistema está apuntando a otra base. Ejecute scripts/base-datos/estructura_base_datos_central.sql en el servidor o scripts/base-datos/estructura_base_datos_pos.sql en la caja.'],
       ['No hay internet', 'La caja sigue vendiendo, cobrando y facturando normal: todo se guarda y se envía cuando vuelva la comunicación. Solo quedan sin servicio las listas de boda, las notas de crédito de otra sucursal y el chequeador.'],
       ['El terminal de tarjeta no responde', 'La caja lo avisa. Se puede registrar la aprobación manual del banco con autorización de supervisor, y queda marcada para conciliar.'],
       ['La caja no baja los artículos nuevos', 'Pase el mouse por el indicador de sincronización de la barra de estado: si la última actualización falló, ahí dice por qué y de qué artículo se trata. Casi siempre es un dato del Central que la caja no acepta (un artículo activo sin precio, una categoría que no es de su departamento). Se corrige en el Central y la caja lo aplica sola en el siguiente ciclo, sin perder nada.'],
       ['Se acabaron los e-NCF o venció el rango', 'No se puede facturar. Administración debe asignar un rango nuevo en el Central; la caja lo recibe en su próxima sincronización. La barra de estado avisa antes de que se acabe.'],
       ['El certificado pide PIN', 'Toque el indicador e-CF de la barra de estado y digite el PIN. Queda solo en memoria: si se reinicia el equipo, se vuelve a pedir.'],
       ['La nota de crédito está vencida', 'Si el negocio decide aceptarla, se suben los días de vigencia en los parámetros del Central y la nota vuelve a poder usarse.'],
       ['El cliente quiere su dinero de vuelta', 'En la devolución se elige efectivo, tarjeta o cheque, según lo que el negocio tenga habilitado; la nota de crédito se emite igual pero sin saldo.'],
       ['Se cerró el turno por error', 'La caja no lo puede deshacer: el cierre es definitivo. En el Central, en Cierres de caja, se corrige lo declarado en la forma de pago que falló, con el motivo; queda guardado lo que la caja había informado.'],
       ['Un usuario quedó bloqueado', 'Se desbloquea desde el Central, en Usuarios de caja.'],
       ['Falta un parámetro', 'La operación se rechaza con el mensaje “Falta configurar el parámetro…”. Se configura en el Central, en Parámetros.'],
       ['Al revisar la base de datos, las horas se ven adelantadas', 'No están mal: las fechas se guardan en UTC, que va cuatro horas adelante de la hora dominicana. Para verlas en hora de aquí consulte las vistas del esquema «local» (por ejemplo SELECT * FROM local.Auditoria) o use dbo.HoraRd(fecha).']],
      anchos=[4.5, 12.5])

titulo('5.1. Quién cambió cada cosa', 2)
p('Las tablas que se administran a mano guardan en la propia fila quién las dejó así y cuándo: la empresa, las sucursales, '
  'las cajas, los parámetros, los usuarios y roles del Central, las listas de boda y todos los maestros. Es lo primero que '
  'se mira en un soporte, sin tener que buscar en otro lado.')
p('Los documentos (ventas, notas de crédito, devoluciones, cierres) no llevan esas columnas porque ya tienen las suyas: la '
  'fecha del documento y el cajero que lo hizo son datos del negocio, no del sistema. Y el detalle completo de cada cambio, '
  'con el antes y el después, está siempre en Seguridad → Auditoría.')
nota('En una base que venía de antes, las filas anteriores al cambio aparecen como «Migración»: ese dato no se podía '
     'reconstruir hacia atrás.')

titulo('5.2. La hora de los datos', 2)
p('El sistema guarda las fechas con la hora de aquí y su desfase del meridiano (por ejemplo 2026-09-18 11:41:21 -04:00). '
  'Quien consulte la base directamente ve la hora real del negocio, sin tener que convertir nada.')
p('El «-04:00» que acompaña a cada fecha es el desfase de República Dominicana, y va guardado junto al dato. Eso permite '
  'que el sistema ordene y compare operaciones de cualquier caja sin ambigüedad, aunque el reloj de un equipo esté mal '
  'puesto. No hay que quitarlo ni cambiarlo.')
nota('El país no tiene horario de verano desde el año 2000, por eso el desfase es siempre -04:00.')

titulo('6. Reglas que conviene recordar')
viñeta('Sin factura electrónica no se cobra: el sistema no permite facturar sin e-NCF disponible.')
viñeta('Todo lo que el cajero no puede hacer solo se autoriza con usuario y clave de un supervisor, queda auditado con el '
       'motivo y la autorización sirve una sola vez.')
viñeta('Las reglas del negocio (plazos, montos, topes, porcentajes) no están fijas en el programa: se cambian en el Central, '
       'en Parámetros, y llegan solas a las cajas.')
viñeta('Cada documento tiene un número único en toda la empresa que dice de qué sucursal, de qué caja y de qué tipo es.')
viñeta('La caja es la que manda sobre sus ventas: el Central refleja lo que las cajas informan y nunca las modifica.')

nota('Documento generado para CG-POS. Las direcciones de ejemplo (localhost) corresponden a la instalación de pruebas; '
     'en producción se usa la dirección del servidor del Central que indique el equipo de tecnología.')

destino = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Manual de usuario CG-POS.docx')
try:
    doc.save(destino)
    print('generado:', destino)
except PermissionError:
    # El manual está abierto en Word: se deja al lado para no perder el trabajo.
    alterno = destino.replace('.docx', ' (nuevo).docx')
    doc.save(alterno)
    print('El manual está abierto en Word. Se generó:', alterno)
    print('Ciérrelo y reemplace el archivo, o vuelva a ejecutar el script con el Word cerrado.')
