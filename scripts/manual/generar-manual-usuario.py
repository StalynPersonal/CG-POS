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
       ['Chequeador de precios', 'La dirección del servidor + /chequeador', 'El cliente, en el pasillo de la tienda'],
       ['Caja – pantalla de ventas principal', 'http://localhost:5180/ingreso?pantalla=principal', 'Cajero, con lector y teclado'],
       ['Caja – pantalla de ventas secundaria', 'http://localhost:5180/ingreso?pantalla=secundaria', 'Cajero, tocando el catálogo'],
       ['Caja – pantalla de clientes', 'http://localhost:5180/cliente', 'El cliente la ve (no pide usuario)'],
       ['Caja – devoluciones', 'http://localhost:5180/ingreso?pantalla=devoluciones', 'Cajero o encargado de devoluciones'],
       ['Caja – despacho de pendientes', 'http://localhost:5180/ingreso?pantalla=despacho', 'Personal de entrega']],
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
     'scripts/base-datos/central/structura_base_datos.sql. Crea la base CgPosCentral con todas sus tablas, llaves e índices.')
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
       ['Denominaciones de billetes y monedas, para el cuadre', 'Bancos y almacenes'],
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
       ['Credencial de la caja', 'Organización → Cajas', 'Es la clave con la que la caja se comunica con el Central.'],
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
       ['Cajas', '/organizacion/cajas', 'Alta de cajas, habilitarlas y emitir la credencial con la que la caja se conecta al Central. El código también es de dos dígitos (01 a 99) y único dentro de su sucursal.'],
       ['Parámetros', '/organizacion/parametros', 'Todas las reglas del negocio: vigencia de notas de crédito, retención de la Ley 32-23, redondeo, fondo de caja, chequeador, listas de boda, fidelidad… Se pueden fijar en general, por sucursal o por caja.'],
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
       ['Qué cambió', 'Cada campo con su valor anterior (tachado) y el que quedó. Una creación no tiene valor anterior y una eliminación no tiene valor nuevo.']],
      anchos=[3.5, 10.5])
p('Los filtros de arriba acotan la búsqueda: rango de fechas, acción, entidad, usuario, un texto libre (busca también dentro '
  'de los valores que cambiaron) y el interruptor «Solo cambios de datos», que deja fuera los ingresos y las consultas. Abajo '
  'se elige el tamaño de página (10, 25, 50, 100 o 200) y se pasa de una página a otra; el Central envía solo la página que '
  'se está viendo, así que la consulta es rápida aunque haya años de movimientos.')
nota('Las contraseñas, los certificados y las firmas se anotan como cambiados, pero su contenido nunca se muestra: en su lugar '
     'aparece «(oculto)».')
nota('Para entrar a esta pantalla hace falta el permiso «Consultar la auditoría del Central y de las cajas». Se puede dar a '
     'contabilidad o a auditoría sin darles permiso para administrar nada.')

titulo('2.5. Maestros, artículos y precios', 2)
tabla(['Opción', 'Ruta', 'Para qué sirve'],
      [['Catálogos', '/maestros', 'Monedas, tasas de cambio, departamentos, categorías, marcas, unidades, impuestos, formas de pago, denominaciones, bancos, tipos de tarjeta, motivos de descuento y devolución, almacenes, niveles y reglas de fidelidad y descuentos por tarjeta (BIN).'],
       ['Artículos', '/articulos', 'Alta y edición de artículos, con sus códigos de barras y de proveedor.'],
       ['Precios', '/precios/articulos', 'Precio de detalle, precio por mayor con su cantidad mínima, precio mínimo y costo; de inmediato o a partir de una fecha.'],
       ['Topes de descuento', '/precios/topes', 'Hasta cuánto puede descontar cada nivel, en general o por departamento o artículo.'],
       ['Clientes', '/clientes', 'Clientes con su comprobante habitual, exoneraciones y direcciones de envío.']],
      anchos=[3.8, 4.4, 8.8])

titulo('2.6. Artículos para empezar (sugerencia)', 2)
p('El sistema se instala sin productos: usted crea los suyos. Para arrancar y para probar todo el sistema, conviene cargar '
  'primero unos pocos artículos que cubran cada caso, y después ya cargar el inventario completo.')
p('Antes de los artículos cree lo que ellos necesitan: el impuesto (ITBIS 18 % y el exento), las unidades de medida '
  '(unidad, libra, saco, galón) y los departamentos (por ejemplo Construcción, Ferretería, Pinturas, Eléctrico).')
tabla(['Código', 'Descripción', 'Departamento', 'Unidad', 'ITBIS', 'Precio', 'Sirve para probar'],
      [['CEM-425', 'Cemento gris 42.5 kg', 'Construcción', 'Saco', '18 %', '520.00', 'Venta normal y precio por mayor (desde 10 sacos a 495.00)'],
       ['VAR-38', 'Varilla 3/8 x 30 pies', 'Construcción', 'Unidad', '18 %', '445.00', 'Venta por cantidad con el lector'],
       ['BLK-6', 'Block de 6 pulgadas', 'Construcción', 'Unidad', '18 %', '38.00', 'Cantidades grandes (12*BLK-6)'],
       ['PIN-BLA-GL', 'Pintura acrílica blanca, galón', 'Pinturas', 'Galón', '18 %', '1,250.00', 'Ofertas y descuentos'],
       ['CLA-2', 'Clavos de 2 pulgadas', 'Ferretería', 'Libra', '18 %', '65.00', 'Artículo pesado: peso de la balanza o digitado'],
       ['TAL-500', 'Taladro percutor 1/2', 'Ferretería', 'Unidad', '18 %', '8,900.00', 'Artículo serializado: pide el serial al vender'],
       ['FOC-LED-9', 'Bombillo LED 9 W', 'Eléctrico', 'Unidad', '18 %', '185.00', 'Oferta lleva 3 paga 2'],
       ['COM-BANO', 'Combo baño completo', 'Ferretería', 'Unidad', '18 %', '15,900.00', 'Combo: se vende como un artículo normal, sin precio por mayor'],
       ['SRV-CORTE', 'Servicio de corte de madera', 'Ferretería', 'Unidad', 'Exento', '150.00', 'Artículo de servicio y comprobante con exento']],
      anchos=[2.6, 4.4, 2.6, 1.8, 1.5, 1.8, 5.0])
nota('Póngale a cada uno su código de barras real (el del empaque): en la caja se busca igual por el código interno, el de '
     'barras, el del proveedor o la referencia.')
p('Con esos nueve artículos ya puede probar venta normal, cantidades, precio por mayor, pesados, serializados, combos, '
  'exentos, ofertas y descuentos. El inventario completo se carga después desde Artículos.')

titulo('2.7. Los demás datos del negocio (sugerencia)', 2)
p('Estos no vienen en el sistema porque son decisiones suyas. Esta es una sugerencia para empezar; ajústela a como trabaja '
  'el negocio. El orden importa: cada cosa necesita la anterior.')

p('1. Departamentos (Maestros → Catálogos). El departamento manda en los reportes y decide si admite descuento manual.')
tabla(['Código', 'Departamento', 'Admite descuento manual'],
      [['1', 'Construcción', 'Sí'], ['2', 'Ferretería', 'Sí'], ['3', 'Pinturas', 'Sí'],
       ['4', 'Eléctrico', 'Sí'], ['5', 'Plomería', 'Sí'], ['6', 'Hogar', 'Sí'], ['7', 'Servicios', 'No']],
      anchos=[2.2, 6.0, 5.0])

p('2. Categorías (cada una dentro de un departamento) y marcas, para agrupar y para dirigir las ofertas.')
tabla(['Categorías sugeridas', 'Marcas sugeridas'],
      [['Cemento y agregados, Aceros, Blocks (Construcción)', 'Las marcas con las que trabaja: Cemex, Domicem, Truper, Stanley, Popular…'],
       ['Herramientas manuales, Herramientas eléctricas (Ferretería)', ''],
       ['Pintura de interiores, Pintura de exteriores (Pinturas)', ''],
       ['Iluminación, Cables y accesorios (Eléctrico)', '']],
      anchos=[8.5, 8.5])

p('3. Bancos, para transferencias, cheques y los depósitos del cierre de sucursal.')
tabla(['Código', 'Banco'],
      [['BPD', 'Banco Popular Dominicano'], ['BRD', 'Banreservas'], ['BHD', 'Banco BHD'],
       ['SCO', 'Scotiabank'], ['APA', 'Asociación Popular de Ahorros y Préstamos']],
      anchos=[2.5, 10.0])

p('4. Almacenes, para las entregas y los envíos: uno por sucursal y, si aplica, el depósito central.')
tabla(['Código', 'Almacén', 'Para qué'],
      [['ALM-01', 'Almacén de la sucursal', 'Retiro del cliente en la tienda'],
       ['DEP-CEN', 'Depósito central', 'Mercancía que se despacha desde el depósito']],
      anchos=[2.8, 5.0, 7.0])

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
  'devoluciones, retiros y notas internas) y un gerente (además reabre cierres y autoriza lo de mayor monto).')

p('8. Rangos de comprobantes fiscales por caja (E31, E32, E34, E44 y E45), con los números que le asignó la DGII.')

p('9. Clientes: no hace falta crearlos por adelantado. En la caja se buscan por cédula o RNC contra el padrón de la DGII; '
  'se registran aquí los que tienen condiciones especiales (comprobante fijo, exoneración o direcciones de envío).')

titulo('2.8. Promociones', 2)
viñeta('Crear (/promociones): porcentaje, monto por unidad, precio especial, lleva X paga Y y precio desde cierta cantidad; '
       'por artículos, departamentos, categorías o marcas, en las sucursales que se elijan, con fechas, días y horario.')
viñeta('Importar (/promociones/importar): carga masiva desde un archivo CSV; se valida todo el archivo y solo se publica si no hay errores.')
viñeta('Simular (/promociones/simular): antes de publicar, muestra qué oferta tomaría la caja para un artículo, cantidad, sucursal y fecha.')
viñeta('La lista muestra el estado de cada promoción y cuántas cajas ya la recibieron.')

titulo('2.9. Facturación electrónica y DGII', 2)
viñeta('Rangos de e-CF (/fiscal/secuencias): se asignan a cada caja por tipo de comprobante, con inicio, fin y vencimiento. '
       'La lista muestra el último usado y cuánto queda.')
viñeta('Comprobantes enviados a la DGII (/monitor/comprobantes): estado de cada e-CF (aceptado, rechazado, en cola), su '
       'trackId, el mensaje de la DGII, la descarga del XML y el reenvío dirigido.')

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
paso('Nueva lista: datos de los festejados (cédula o RNC, teléfono, correo), del evento (nombre, fecha, lugar) y los artículos pedidos con su cantidad.')
paso('El Central le asigna un número (por ejemplo LB000001): ese es el número que el cliente da en la caja.')
paso('A medida que la gente compra, la lista muestra lo comprado, lo que falta y las facturas registradas.')
paso('Cuando pasa el evento, la lista se cierra (y se puede reabrir si hace falta).')

titulo('2.13. Fidelidad', 2)
p('Ruta: /fidelidad/miembros. Miembros con su nivel, saldo de puntos, movimientos y ajustes. Los niveles y las reglas de '
  'acumulación se configuran en los catálogos.')

titulo('2.14. Despacho', 2)
p('Ruta: /despacho/pendientes. Todos los pendientes de entrega y envíos de todas las sucursales, con su estado y sus atrasos; '
  'si el negocio lo activa, el Central le avisa por correo al cliente cuando su pedido queda preparado.')

titulo('2.15. Cierre consolidado de sucursal', 2)
p('Ruta: /cierres-sucursal. Es el cierre del día de toda la sucursal.')
paso('Elija la sucursal y el día y presione Preparar: se ven todos los cierres de caja, las formas de pago sumadas y lo que falta (si algún turno no ha cerrado, lo dice).')
paso('El sistema calcula el efectivo a depositar por moneda (las tarjetas y transferencias no se depositan).')
paso('Registre los depósitos: banco, número de boleta, monto y fecha. Puede ser más de uno.')
paso('Cierre la sucursal: queda la diferencia entre lo depositado y lo que había que depositar, y ya no se modifica.')
nota('Si una caja informa un cierre de ese día después de consolidar, el consolidado no cambia, pero la lista lo avisa.')

titulo('2.16. Reportes', 2)
p('Ruta: /reportes. Todos por rango de días y, si se quiere, por sucursal o caja. Cada uno se descarga en Excel y en PDF.')
tabla(['Reporte', 'Qué muestra'],
      [['Ventas', 'Por día, sucursal y caja: facturas, notas de crédito, subtotal, descuento, ITBIS y total.'],
       ['ITBIS por tasa', 'Base e impuesto de cada tasa del período.'],
       ['Formato 607', 'Un registro por comprobante para la DGII, con la descarga del archivo de envío.'],
       ['Cuadres de caja', 'Turno, cajero, esperado, declarado y diferencia.'],
       ['e-CF y DGII', 'Estado de cada comprobante enviado.'],
       ['Sincronización', 'Última comunicación de cada caja, mensajes, rechazos y alertas.']],
      anchos=[4.5, 12.5])

titulo('2.17. Monitor de sincronización', 2)
viñeta('/monitor: estado de cada caja, cuánto hace que no se comunica y cuántos documentos trae pendientes.')
viñeta('/monitor/conflictos: documentos que el Central no pudo aceptar (por ejemplo un número repetido), para resolverlos.')

titulo('2.18. Chequeador de precios', 2)
p('Ruta: /chequeador, en la pantalla que se pone en el pasillo de la tienda. El cliente pasa el producto por el lector y ve '
  'la descripción, el precio grande, el precio por cantidad y las ofertas vigentes; la consulta se borra sola a los pocos '
  'segundos para el siguiente cliente.')
nota('El chequeador viene apagado: se enciende en Parámetros, con Central.Chequeador.Habilitado.')

doc.add_page_break()

# ---------------------------------------------------------------- PARTE 2: LA CAJA
titulo('3. La caja, paso a paso')

titulo('3.1. Instalar una caja desde cero', 2)
p('Cada caja tiene su propia base de datos en su propio equipo: por eso sigue vendiendo aunque se caiga la red. '
  'La instalación la hace tecnología, una sola vez por caja.')
paso('En el Central, cree la sucursal y la caja (Organización) y emita la credencial de esa caja. El secreto se muestra una sola vez: cópielo.')
paso('En el equipo de la caja, instale SQL Server Express y ejecute scripts/base-datos/pos/structura_base_datos.sql. '
     'Crea la base CgPosCaja vacía: no lleva datos, todo baja del Central.')
paso('Instale el programa de la caja con scripts/caja/instalar-caja.ps1, indicando el número de sucursal, el de caja, '
     'el secreto que copió y la dirección del Central.')
paso('Copie el certificado digital de la empresa en el equipo. El PIN no se guarda: lo digita un supervisor en la caja.')
paso('Arranque la caja: en el primer ciclo se conecta al Central y baja artículos, precios, usuarios, parámetros y sus rangos de comprobantes.')
nota('Si la caja avisa que la base no existe o le faltan tablas, es que no se ejecutó el script en ese equipo.')

titulo('3.2. Entrar a la caja', 2)
paso('En la pantalla de ingreso digite su usuario y su clave (no hay PIN ni carné: siempre usuario y clave).')
paso('Si se equivoca varias veces seguidas, el usuario se bloquea por unos minutos; un supervisor lo desbloquea desde el Central.')
paso('Si la caja no tiene turno abierto, el sistema le pide abrirlo.')

titulo('3.3. Abrir el turno', 2)
p('El turno es el período de trabajo de un cajero en esa caja. Solo puede haber un turno abierto por caja.')
viñeta('Al abrir se digita el fondo de caja (el sistema sugiere el monto configurado).')
viñeta('Si la caja tiene abierto el turno de otro cajero, aparece Relevar turno: con la autorización de un supervisor, '
       'usted toma el turno sin cerrarlo ni cuadrar.')
viñeta('Si el último cierre se hizo por error, aparece Reabrir el último cierre: pide motivo y la autorización de alguien '
       'de nivel superior, y queda registrado.')

titulo('3.4. La pantalla de ventas principal', 2)
p('La pantalla está dividida en cinco zonas:')
tabla(['Zona', 'Para qué sirve'],
      [['Encabezado izquierdo', 'Tipo de comprobante que se va a emitir (E31, E32, E44 o E45). Al tocarlo se abre el cliente.'],
       ['Encabezado central', 'Cliente de la factura, número de transacción, cantidad de artículos, límite de compra, programa de fidelidad y lista de boda.'],
       ['Encabezado derecho', 'Subtotal, ITBIS, descuentos, TOTAL y, en facturas de régimen especial, la retención y el total a pagar.'],
       ['Campo de escaneo', 'Donde el lector escribe el código. También se puede digitar.'],
       ['Grilla de líneas', 'Los artículos de la venta: línea, código, descripción, cantidad, precio, importe y la oferta aplicada.'],
       ['Barra de teclas F', 'Las funciones, en dos páginas.'],
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
p('Primera página:')
tabla(['Tecla', 'Qué hace'],
      [['F2', 'Buscar un artículo por descripción'],
       ['F3', 'Límite de compra que pidió el cliente (avisa al pasarse)'],
       ['F4', 'Cambiar la cantidad de la línea seleccionada'],
       ['F5', 'Tomar el peso de la balanza'],
       ['F6', 'Lista de boda: asociar la venta a una lista de regalos'],
       ['F7', 'Facturas en espera (guardar la actual y retomar otra)'],
       ['F8', 'Totalizar: abre el cobro'],
       ['F10', 'Ir a devoluciones'],
       ['F11', 'Consultar el precio de un artículo sin venderlo'],
       ['F12', 'Cliente, comprobante y programa de fidelidad']],
      anchos=[3.0, 14.0])
p('Segunda página (se cambia con el botón de la misma barra): catálogo en mosaicos, eliminar línea, eliminar por escaneo, '
  'limpiar pantalla, descuento a la línea, descuento a la factura, entrega o envío, despacho, anular, suspender, gaveta, '
  'reimprimir, retiro de efectivo, cierre de turno y salir.')

titulo('3.7. Hacer una venta', 2)
paso('Pase el código del artículo por el lector (o dígitelo y presione Enter). Para varias unidades: 12*CEM-425.')
paso('Para cambiar una cantidad, toque la cantidad en la línea o use F4. Para ver el otro código del artículo, toque el código.')
paso('Si el artículo se vende por peso, se toma el peso de la balanza (F5) o se digita.')
paso('Si el artículo lleva serial, el sistema lo pide al escanearlo.')
paso('Con F12 asigne el cliente si lleva comprobante fiscal, y su cédula del programa de fidelidad.')
paso('Revise el total con el cliente y presione F8 para cobrar.')
nota('Cada operación se guarda al instante: si la caja se apaga, al volver a entrar la venta aparece tal como estaba.')

titulo('3.8. Cliente y tipo de comprobante (F12)', 2)
p('Se digita la cédula o el RNC; el sistema lo busca en el padrón de la DGII y en los clientes registrados. Si no aparece, '
  'se digita el nombre. El tipo de comprobante sale del cliente y cambiarlo a mano requiere permiso.')
tabla(['Comprobante', 'Cuándo se usa'],
      [['E32 – Consumo', 'Cliente común. Desde el monto configurado (RD$250,000 por defecto) exige cédula o RNC.'],
       ['E31 – Crédito fiscal', 'Empresa que necesita el ITBIS. Exige RNC o cédula.'],
       ['E44 – Régimen especial', 'Clientes de régimen especial. Si el negocio lo tiene configurado, se le aplica la retención de la Ley 32-23.'],
       ['E45 – Gubernamental', 'Instituciones del Estado. Exige RNC.']],
      anchos=[5.0, 12.0])
p('Retención de la Ley 32-23: en las facturas E44, el sistema calcula el porcentaje configurado sobre el subtotal ya con '
  'descuentos y se lo descuenta a lo que el cliente paga en caja. La factura mantiene su total; el ticket muestra '
  '“RETENCIÓN LEY 32-23” y “TOTAL A PAGAR”.')

titulo('3.9. Lista de boda (F6)', 2)
paso('Pida al cliente el número de la lista (la crea la administración en el Central).')
paso('Presione F6, digite el número y acepte: la lista queda en el encabezado de la venta y sale en el ticket.')
paso('Al cobrar, la compra queda registrada en la lista y, si el negocio lo tiene configurado, baja las cantidades pedidas.')
nota('La lista se consulta en el Central: si no hay comunicación, no se puede asociar. Una lista cerrada no se acepta.')

titulo('3.10. Descuentos y ofertas', 2)
viñeta('Ofertas: se aplican solas según lo configurado en el Central. La columna Promo muestra cuál se aplicó; al tocarla '
       'se ve el detalle y se puede desactivar (con permiso).')
viñeta('Descuento a la línea: toque el precio de la línea. Descuento a la factura: segunda página de teclas. Ambos piden '
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

titulo('3.12. Facturas en espera, anular y suspender', 2)
viñeta('F7 – En espera: guarda la venta actual para atender a otro cliente y retomarla después.')
viñeta('Anular (segunda página): cancela la transacción en curso con motivo y autorización.')
viñeta('Suspender: bloquea la pantalla; se reanuda con la clave del cajero.')
viñeta('Eliminar línea, eliminar por escaneo y limpiar pantalla piden autorización de supervisor; la línea eliminada queda '
       'tachada y con su reverso en rojo, para que todo quede a la vista.')

titulo('3.13. Entregas y envíos', 2)
p('Cuando el cliente se lleva parte de la mercancía después:')
paso('En la segunda página de teclas, elija Entrega / envío.')
paso('Marque qué líneas y qué cantidad quedan pendientes, y si es retiro en un almacén o envío a una dirección, con la fecha comprometida.')
paso('Al cobrar se imprime un comprobante de pendiente por cada destino, con código de barras.')
paso('En la pantalla /despacho se escanea ese comprobante para preparar, entregar (total o parcial, con quien recibe) o anular.')

titulo('3.14. Devoluciones y notas de crédito', 2)
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

titulo('3.15. Retiros, pre-cierre y cierre de turno', 2)
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

titulo('3.16. La barra de estado', 2)
p('Abajo de la pantalla, siempre a la vista:')
viñeta('Sincronización: si la caja está comunicada con el Central y cuántos documentos están pendientes de enviar.')
viñeta('e-CF: si el certificado está cargado, cuántos comprobantes quedan en el rango y si algo está por vencer. '
       'Al tocarlo se digita el PIN del certificado cuando hace falta.')
viñeta('Avisos de la base de datos, la hora del equipo y el respaldo.')

doc.add_page_break()

# ---------------------------------------------------------------- Flujo completo
titulo('4. Recorrido completo del sistema (para probarlo todo)')
p('Este es el orden recomendado para recorrer el sistema de punta a punta y comprobar que todo funciona.')

titulo('Primero, en el Central', 2)
paso('Cree la base del Central con scripts/base-datos/central/structura_base_datos.sql y entre con el usuario ADMIN (cambie su contraseña).')
paso('Revise Organización: cree la empresa, la sucursal y la caja, y emita la credencial de la caja.')
paso('En Parámetros, configure lo que el negocio necesita: fondo de caja, redondeo, días de vigencia de notas de crédito, '
     'retención de la Ley 32-23 (si aplica), chequeador y listas de boda.')
paso('Revise los catálogos que ya trae la base (monedas, impuestos, unidades, formas de pago, denominaciones, motivos) y '
     'cree los suyos: departamentos, categorías, marcas, bancos y almacenes.')
paso('Cree los artículos sugeridos en el manual (o los suyos) con sus códigos de barras y precios, y al menos una promoción.')
paso('Asigne los rangos de e-CF a la caja (E31, E32, E34, E44, E45).')
paso('Cree los usuarios de caja: un cajero, un supervisor y un gerente, con sus niveles.')
paso('Cree una lista de boda de prueba y anote su número.')

titulo('Después, en la caja', 2)
paso('Cree la base de la caja con scripts/base-datos/pos/structura_base_datos.sql e instale la caja con su sucursal, su número y el secreto.')
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
      [['La caja o el Central avisan que falta la base de datos', 'No se ejecutó el script de creación en ese equipo, o el sistema está apuntando a otra base. Ejecute scripts/base-datos/central/structura_base_datos.sql en el servidor o scripts/base-datos/pos/structura_base_datos.sql en la caja.'],
       ['No hay internet', 'La caja sigue vendiendo, cobrando y facturando normal: todo se guarda y se envía cuando vuelva la comunicación. Solo quedan sin servicio las listas de boda, las notas de crédito de otra sucursal y el chequeador.'],
       ['El terminal de tarjeta no responde', 'La caja lo avisa. Se puede registrar la aprobación manual del banco con autorización de supervisor, y queda marcada para conciliar.'],
       ['Se acabaron los e-NCF o venció el rango', 'No se puede facturar. Administración debe asignar un rango nuevo en el Central; la caja lo recibe en su próxima sincronización. La barra de estado avisa antes de que se acabe.'],
       ['El certificado pide PIN', 'Toque el indicador e-CF de la barra de estado y digite el PIN. Queda solo en memoria: si se reinicia el equipo, se vuelve a pedir.'],
       ['La nota de crédito está vencida', 'Si el negocio decide aceptarla, se suben los días de vigencia en los parámetros del Central y la nota vuelve a poder usarse.'],
       ['El cliente quiere su dinero de vuelta', 'En la devolución se elige efectivo, tarjeta o cheque, según lo que el negocio tenga habilitado; la nota de crédito se emite igual pero sin saldo.'],
       ['Se cerró el turno por error', 'Desde la apertura, Reabrir el último cierre con motivo y autorización de nivel superior.'],
       ['Un usuario quedó bloqueado', 'Se desbloquea desde el Central, en Usuarios de caja.'],
       ['Falta un parámetro', 'La operación se rechaza con el mensaje “Falta configurar el parámetro…”. Se configura en el Central, en Parámetros.']],
      anchos=[4.5, 12.5])

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
