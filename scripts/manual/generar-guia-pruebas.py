# -*- coding: utf-8 -*-
"""
Genera la guía de pruebas de CG-POS en Word.

    python scripts/manual/generar-guia-pruebas.py

Deja el documento en «scripts/manual/Guia de pruebas CG-POS.docx», junto a este script.
La guía NO se edita a mano: se edita este script y se vuelve a generar.

Es el hermano del manual de usuario: el manual explica cómo se usa el sistema, esta guía dice qué probar,
con qué datos y qué tiene que pasar. Cuando cambie un flujo, hay que actualizar los dos.
"""
import os
from docx import Document
from docx.shared import Pt, RGBColor, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_COLOR_INDEX

VERDE = RGBColor(0x1B, 0x4D, 0x3E)
VERDE_CLARO = RGBColor(0x2E, 0x7D, 0x32)
GRIS = RGBColor(0x55, 0x55, 0x55)

doc = Document()

for seccion in doc.sections:
    seccion.top_margin = Cm(2.2)
    seccion.bottom_margin = Cm(2.2)
    seccion.left_margin = Cm(2.4)
    seccion.right_margin = Cm(2.4)

normal = doc.styles['Normal']
normal.font.name = 'Segoe UI'
normal.font.size = Pt(10.5)
normal.paragraph_format.space_after = Pt(6)

for nivel, tamano in ((1, 18), (2, 14), (3, 11.5)):
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


# La casilla de cada prueba, para volver a marcar en amarillo las que ya estaban marcadas en el documento anterior.
_casillas = {}
_prueba_actual = None


def prueba(codigo, texto):
    """Encabezado de una prueba, con su código para anotarla si falla."""
    global _prueba_actual
    _prueba_actual = codigo
    doc.add_heading(f'{codigo} · {texto}', level=3)


def esperado(texto):
    """Lo que tiene que pasar. Si no pasa, la prueba falló: anótelo con el código de la prueba."""
    parrafo = doc.add_paragraph()
    etiqueta = parrafo.add_run('Debe pasar: ')
    etiqueta.bold = True
    etiqueta.font.color.rgb = VERDE
    cuerpo = parrafo.add_run(texto)
    cuerpo.font.color.rgb = GRIS
    return parrafo


def tablas(texto):
    """Las tablas que toca la prueba, para poder comprobarlo en la base. «(lee)» es consulta, no escritura."""
    parrafo = doc.add_paragraph()
    etiqueta = parrafo.add_run('Tablas: ')
    etiqueta.bold = True
    etiqueta.font.color.rgb = VERDE_CLARO
    etiqueta.font.size = Pt(9.5)
    cuerpo = parrafo.add_run(texto)
    cuerpo.font.color.rgb = GRIS
    cuerpo.font.size = Pt(9.5)
    return parrafo


def marcar():
    parrafo = doc.add_paragraph()
    corrida = parrafo.add_run('☐ Pasó          ☐ Falló          Observación: ______________________________________________')
    corrida.font.color.rgb = GRIS
    corrida.font.size = Pt(9.5)
    if _prueba_actual is not None:
        _casillas[_prueba_actual] = corrida
    return parrafo


def marcas_anteriores(ruta):
    """
    Las pruebas que quedaron resaltadas en el documento anterior. La guía se genera desde cero cada vez, y quien la está
    usando va marcando lo que ya probó: perder eso en cada regeneración sería borrarle el trabajo.
    """
    if not os.path.exists(ruta):
        return set()

    try:
        anterior = Document(ruta)
    except Exception:
        return set()

    marcadas, codigo = set(), None
    for parrafo in anterior.paragraphs:
        if parrafo.style.name == 'Heading 3' and '·' in parrafo.text:
            codigo = parrafo.text.split('·')[0].strip()
        elif parrafo.text.startswith('☐') and codigo is not None                 and any(c.font.highlight_color is not None for c in parrafo.runs):
            marcadas.add(codigo)

    return marcadas


# ---------------------------------------------------------------- Portada
portada = doc.add_paragraph()
portada.alignment = WD_ALIGN_PARAGRAPH.CENTER
marca = portada.add_run('CG-POS\n')
marca.bold = True
marca.font.size = Pt(34)
marca.font.color.rgb = VERDE
sub = portada.add_run('Guía de pruebas\n')
sub.font.size = Pt(20)
sub.font.color.rgb = VERDE_CLARO
pie = portada.add_run('Qué probar, con qué datos y qué tiene que pasar')
pie.font.size = Pt(11)
pie.font.color.rgb = GRIS

p()
p('Esta guía recorre el sistema entero, de punta a punta, en el orden en que hay que probarlo. Cada bloque depende del '
  'anterior: si se salta uno, el siguiente puede quedarse sin datos.')

titulo('Cómo usar esta guía', 2)
viñeta('Cada prueba tiene un código (A1, B2…). Si algo falla, anote ese código: con él se sabe exactamente qué se estaba haciendo.')
viñeta('«Debe pasar» es el resultado correcto. Si en pantalla ve otra cosa, la prueba falló aunque no salga ningún error.')
viñeta('«Tablas» dice qué se guarda y dónde, para que pueda comprobarlo en la base si algo no cuadra. Las que llevan '
       '«(lee)» solo se consultan: esa prueba no las cambia. Antes del nombre va la base: Central o Caja.')
viñeta('Pruebe con la impresora conectada y con papel: buena parte de lo que hay que revisar sale impreso.')
viñeta('No borre los datos entre bloques. La factura del bloque de ventas es la que se devuelve después, y el turno que se '
       'cierra es el que se cuadra al final.')
nota('Los montos de los ejemplos dan igual; use los de sus artículos. Lo que importa es que el sistema haga la cuenta que '
     'usted espera, no que coincida con un número de este papel.')

titulo('Lo que hace falta antes de empezar', 2)
tabla(['Necesita', 'Para qué'],
      [['El Central instalado y abierto en un navegador', 'Todo se configura ahí primero.'],
       ['Una caja con su servicio instalado', 'Es donde se vende. Puede ser la misma computadora en pruebas.'],
       ['Impresora de tickets con papel', 'Facturas, notas de crédito, retiros y cierres salen impresos.'],
       ['Certificado digital (.p12) y su PIN', 'Sin él no se firma la factura electrónica (bloque G).'],
       ['Un segundo monitor (opcional)', 'Para la pantalla del cliente y la del área de devoluciones.'],
       ['Lector de código de barras (opcional)', 'Todo se puede digitar a mano, pero pruebe también con lector.']],
      anchos=[6.5, 10.5])

doc.add_page_break()

# ---------------------------------------------------------------- A. Central: dejarlo listo
titulo('Bloque A — Dejar el sistema listo (en el Central)')
p('Una instalación nueva llega vacía a propósito: los datos los decide el negocio. Este bloque es, además, la prueba de '
  'que todo lo que hace falta se puede crear desde la pantalla, sin tocar la base de datos.')

prueba('A1', 'Entrar al Central y cambiar la contraseña')
paso('Abra el Central y entre con el usuario ADMIN y la contraseña Admin.CGPOS#2026.')
paso('El sistema pide cambiarla; ponga la suya y anótela.')
esperado('No deja seguir hasta cambiarla, y la nueva sirve para volver a entrar.')
tablas('Central: UsuariosCentral · SesionesCentral · Auditoria')
marcar()

prueba('A2', 'Datos de la empresa')
paso('Vaya a Organización → Empresa (/organizacion/empresa) y complete RNC, razón social, nombre comercial, dirección y teléfono.')
esperado('Los datos se guardan y después salen en el encabezado del ticket y en el XML de la factura electrónica.')
tablas('Central: Empresas')
marcar()

prueba('A3', 'Sucursal y caja')
paso('En Organización → Sucursales cree una sucursal (por ejemplo 01 · Sucursal Principal).')
paso('En Organización → Cajas cree la caja 01 de esa sucursal, con la dirección IP fija del equipo donde va a correr.')
paso('En el menú de esa caja, presione «Emitir credencial» y copie el secreto: se muestra una sola vez.')
esperado('La caja queda listada, habilitada, y usted tiene el secreto guardado para el bloque B.')
tablas('Central: Sucursales · Cajas · CredencialesDispositivo')
marcar()

prueba('A4', 'Secuencias de los documentos del Central')
paso('Vaya a Organización → Secuencias y revise que estén las de factura, nota de crédito, cotización, lista de boda, '
     'promoción, despacho y cierre de sucursal.')
esperado('Cada documento tiene su prefijo y su correlativo. Si falta alguno, el documento correspondiente no se podrá crear, '
         'y el sistema lo dirá con esas palabras.')
tablas('Central: SecuenciasCentral')
marcar()

prueba('A5', 'Catálogos mínimos')
paso('En Maestros → Catálogos revise (o cree) al menos: la moneda local, un impuesto del 18 %, una unidad de medida '
     '«Unidad», un departamento, las formas de pago Efectivo y Tarjeta, un banco, un tipo de tarjeta, los motivos de '
     'descuento y de devolución, y las denominaciones de billetes y monedas.')
esperado('Todo aparece listado y activo. Las denominaciones son las que después usará el supervisor para contar el efectivo.')
tablas('Central: Monedas · Impuestos · UnidadesMedida · Departamentos · Categorias · Marcas · FormasPago · Bancos · TiposTarjeta · Denominaciones · MotivosDescuento · MotivosDevolucion')
marcar()
nota('Sin denominaciones no se puede cuadrar un turno, y sin motivos de devolución no se emite una nota de crédito. '
     'Conviene revisarlos ahora y no cuando la cajera esté esperando.')

prueba('A6', 'Artículos de prueba')
paso('En Maestros → Artículos cree cinco artículos, que son los que usará todo el recorrido:')
tabla(['Para probar', 'Cómo crearlo'],
      [['Artículo normal', 'Tipo Normal, con su código de barras. Es el que se escanea todo el tiempo.'],
       ['Artículo pesado', 'Tipo Pesado, en kilos, con su tara si aplica. Se usa con la balanza.'],
       ['Artículo serializado', 'Tipo Serializado. Al venderlo exige el número de serie.'],
       ['Combo', 'Tipo Combo/Kit, con sus componentes. Nunca toma precio por mayor.'],
       ['Artículo con precio por mayor', 'Normal, pero con precio de mayor y la cantidad desde la que aplica.']],
      anchos=[5.0, 12.0])
esperado('Los cinco quedan creados con su código interno, y el buscador los encuentra por código, por descripción y por '
         'código de barras.')
tablas('Central: Articulos · CodigosArticulo')
marcar()

prueba('A7', 'Precios')
paso('En Precios → Artículos póngale precio de detalle a los cinco, y precio de mayor al que corresponda.')
esperado('El precio queda guardado y el chequeador y la caja lo muestran igual.')
tablas('Central: Articulos (el precio vive en el artículo) · Auditoria')
marcar()

prueba('A8', 'Roles y usuarios de caja')
paso('En Cajas → Roles cree tres roles: CAJERO (abrir y cerrar turno, vender), SUPERVISOR (lo del cajero más autorizar, '
     'descuentos, limpiar la pantalla, retiros, devoluciones y los permisos de Cuadre) y DEVOLUCIONES (abrir y cerrar turno y registrar '
     'devoluciones, sin vender).')
paso('En Cajas → Usuarios cree un usuario para cada rol y asígneles la caja 01.')
esperado('Los tres quedan creados con su clave. El de DEVOLUCIONES se usará en el bloque S.')
tablas('Central: RolesCaja · RolesCajaPermisos · UsuariosCaja · UsuariosCajaCajas')
marcar()

prueba('A9', 'Parámetros del negocio')
paso('En Organización → Parámetros revise los que cambian cómo trabaja la caja: fondo de caja, redondeo del efectivo, '
     'días de vigencia de la nota de crédito, días de retención del ITBIS en devoluciones, monto que exige identificación '
     'y cantidad máxima que el cajero puede digitar.')
esperado('Cada uno se puede cambiar y dice desde cuándo aplica. Anote los valores con los que va a probar: los va a '
         'necesitar para saber si el sistema hizo lo correcto.')
tablas('Central: Parametros')
marcar()

prueba('A10', 'Rango de comprobantes fiscales')
paso('En Fiscal → Secuencias cargue el rango de e-NCF que le dio la DGII para los tipos que va a probar (al menos E32 '
     'consumo y E31 crédito fiscal, y E34 para las notas de crédito).')
esperado('El rango queda con su desde-hasta y su vencimiento, y la caja lo ve en su barra de estado.')
tablas('Central: SecuenciasEcf')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- B. La caja
titulo('Bloque B — Poner la caja a trabajar')

prueba('B1', 'Configurar la caja')
paso('Abra la caja en su equipo. Como es nueva, pide configurarse: escriba qué caja es (sucursal 01, caja 01), la '
     'dirección del Central y el secreto que copió en A3.')
esperado('La caja se identifica contra el Central y queda configurada. Si el secreto está mal, lo dice y no continúa.')
tablas('Caja: ConfiguracionCaja · Central: CredencialesDispositivo · EstadosSincronizacionCaja')
marcar()

prueba('B2', 'Bajada de maestros')
paso('Espere a que la barra de estado deje de decir «Actualizando».')
esperado('La caja tiene ya los artículos, precios, formas de pago y demás que creó en el bloque A, y puede trabajar sin red '
         'desde este momento.')
tablas('Caja: Articulos · CodigosArticulo · FormasPago · Impuestos · Denominaciones · Parametros · Usuarios · Roles · MarcasSincronizacion (y los demás maestros)')
marcar()

prueba('B3', 'Ingreso del cajero')
paso('Entre con el usuario y la clave del CAJERO.')
paso('Pruebe también una clave equivocada tres veces seguidas.')
esperado('Con la clave correcta entra. Con la equivocada, al llegar al máximo de intentos el usuario queda bloqueado por el '
         'tiempo configurado y lo dice.')
tablas('Caja: Usuarios · Auditoria · BandejaSalida · Central: AccesosUsuarioCaja')
marcar()

prueba('B4', 'Abrir turno')
paso('Abra el turno con el fondo que le sugiere la pantalla, o cámbielo.')
paso('Desde otra pantalla intente abrir un segundo turno en la misma caja.')
esperado('El primero abre; el segundo se rechaza diciendo que ya hay un turno abierto.')
tablas('Caja: Turnos · BandejaSalida')
marcar()

prueba('B5', 'Volver a sincronizar una caja desde el Central')
paso('Con la caja ya funcionando, borre a mano unos artículos de su base: DELETE FROM Articulos WHERE Codigo IN (…).')
paso('En la caja, dele a «Sincronizar» en el panel de funciones.')
paso('Ahora en el Central, Organización → Cajas, abra el menú de esa caja y use «Volver a sincronizar esta caja».')
paso('Vuelva a la caja y espere (o dele a «Sincronizar»).')
esperado('Sincronizar por sí solo NO los devuelve: el Central no tiene nada nuevo que mandar, porque allá no pasó nada. '
         'Después del pedido desde el Central, la caja baja el catálogo entero otra vez, los artículos vuelven y los '
         'clientes que ya tenía salen como actualizados, no repetidos.')
tablas('Central: EstadosSincronizacionCaja · Auditoria · Caja: MarcasSincronizacion · Articulos · Clientes')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- C. Venta
titulo('Bloque C — La venta de todos los días')

prueba('C1', 'Escanear y sumar')
paso('Escanee el artículo normal tres veces.')
paso('Escriba «5*» seguido del código y presione Enter.')
esperado('La primera vez aparece una línea que va sumando cantidad; la segunda entra una línea de 5 unidades. El total y el '
         'ITBIS se recalculan solos.')
tablas('Caja: VentasEnProceso · LineasVentaEnProceso')
marcar()

prueba('C2', 'Cambiar la cantidad (F4)')
paso('Seleccione una línea, presione F4 y ponga 2.')
paso('Ahora intente poner una cantidad mayor a la del parámetro (de fábrica, más de 10).')
esperado('La primera cambia. La segunda no se acepta: dice cuál es el máximo que se puede digitar y que lo demás se escanea '
         'artículo por artículo.')
tablas('Caja: LineasVentaEnProceso')
marcar()

prueba('C3', 'Eliminar una línea con autorización')
paso('Con el cajero, intente eliminar una línea.')
paso('Cuando pida autorización, ponga el usuario y la clave del SUPERVISOR. Deje el motivo en blanco: es opcional.')
paso('Elimine otra línea y esta vez escriba un motivo.')
esperado('Sin la clave del supervisor no se elimina; sin motivo sí, porque es opcional. Con la autorización, la línea queda '
         'tachada y en gris —menos su número, que se lee normal— y el total baja. La auditoría guarda siempre qué se '
         'autorizó y quién lo autorizó, y el motivo cuando se escribió.')
tablas('Caja: LineasVentaEnProceso · AutorizacionesOtorgadas · Auditoria')
marcar()

prueba('C4', 'Limpiar la pantalla')
paso('Presione la opción de limpiar. Sale un solo cuadro, el del supervisor: autorice sin escribir motivo.')
paso('Arme otra venta, límpiela de nuevo y esta vez escriba un motivo en ese mismo cuadro.')
esperado('No hay dos cuadros: el motivo se pide dentro del de autorización y es opcional. Las dos veces la venta se descarta '
         'entera y empieza otra vacía, sin dejar factura ni consumir número. El motivo, cuando se escribe, queda guardado en '
         'la auditoría (se comprueba en S3).')
tablas('Caja: VentasEnProceso · LineasVentaEnProceso (las dos filas se borran) · Auditoria')
marcar()

prueba('C5', 'La venta sobrevive a un corte')
paso('Arme una venta con tres líneas y, sin cobrar, cierre la ventana de la caja (o reinicie el servicio).')
paso('Vuelva a entrar con el mismo cajero.')
esperado('La venta aparece tal como la dejó, con sus líneas y sus totales.')
tablas('Caja: VentasEnProceso · LineasVentaEnProceso (lee: es la misma fila de antes del corte)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- D. Cliente y comprobante
titulo('Bloque D — Cliente y tipo de comprobante')

prueba('D1', 'Consumo sin cliente')
paso('Arme una venta pequeña y mire el tipo de comprobante en el encabezado.')
esperado('Sale como consumo (E32), sin cliente.')
tablas('Caja: VentasEnProceso')
marcar()

prueba('D1b', 'Buscar el cliente por nombre')
paso('Presione F12 y toque «Buscar por nombre».')
paso('Escriba con el teclado en pantalla el principio del nombre de un cliente (por ejemplo «Constructora») y busque.')
paso('Toque la fila del cliente y presione Asignar. Pruebe también escribiendo solo dos letras, y escribiendo su documento.')
esperado('Con dos letras avisa que hacen falta al menos tres. Al buscar, el teclado deja su sitio a la lista, que se elige '
         'tocando. Al asignar, el cliente queda en la venta con su comprobante y su lista de precios, igual que si se '
         'hubiera digitado el documento. Escribiendo solo números busca por documento.')
tablas('Caja: Clientes (lee) · VentasEnProceso')
marcar()

prueba('D2', 'Crédito fiscal con RNC (F12)')
paso('Presione F12 y escriba un RNC válido (por ejemplo 401007551).')
esperado('Trae el nombre del contribuyente y el comprobante cambia a crédito fiscal (E31). Con un RNC inventado, lo rechaza '
         'por dígito verificador.')
tablas('Caja: VentasEnProceso · Clientes (lee)')
marcar()

prueba('D3', 'Monto que exige identificación')
paso('Arme una venta que pase el monto configurado en A9 (de fábrica, RD$250,000) sin asignarle cliente e intente cobrar.')
esperado('No deja cobrar sin cédula o RNC, y lo dice con esas palabras.')
tablas('Caja: VentasEnProceso · Parametros (lee)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- E. Artículos especiales
titulo('Bloque E — Artículos que no son normales')

prueba('E0', 'Precio por mayor pasando el artículo varias veces')
paso('Pase por el lector, de uno en uno, tantas unidades del artículo con precio por mayor como pida su cantidad mínima.')
paso('Después pruebe lo mismo escaneando «12*código» en una sola pasada.')
esperado('Las pasadas se van sumando a la misma línea, no se abre una por cada una. Al llegar a la cantidad mínima, la línea '
         'pasa al precio de mayor y aparece la marca «Mayor». Las dos formas terminan igual.')
tablas('Caja: LineasVentaEnProceso · Articulos (lee)')
marcar()

prueba('E1', 'Artículo pesado')
paso('Presione F5 (balanza) con el artículo pesado, o escanee su etiqueta de balanza.')
esperado('Toma el peso y calcula el importe por kilo. Si el artículo tiene tara, la descuenta.')
tablas('Caja: LineasVentaEnProceso · Articulos (lee: peso y tara)')
marcar()

prueba('E2', 'Artículo serializado')
paso('Escanee el artículo serializado.')
paso('Intente vender dos veces el mismo serial en la misma factura.')
esperado('Pide el serial y no deja seguir sin él. El serial repetido se rechaza.')
tablas('Caja: LineasVentaEnProceso (el serial va en la línea)')
marcar()

prueba('E3', 'Combo')
paso('Venda el combo.')
esperado('Entra como un solo artículo con su precio. Aunque compre muchos, nunca toma precio por mayor.')
tablas('Caja: LineasVentaEnProceso')
marcar()

prueba('E4', 'Buscar sin código (F2)')
paso('Presione F2 y busque por parte de la descripción.')
paso('Pruebe también el catálogo en mosaicos.')
esperado('Encuentra el artículo y lo agrega. La búsqueda responde en menos de un segundo aunque el maestro sea grande.')
tablas('Caja: Articulos · CodigosArticulo (lee)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- F. Precios y promociones
titulo('Bloque F — Precios, ofertas y chequeador')

prueba('F1', 'Precio por mayor automático')
paso('Venda el artículo con precio de mayor en una cantidad menor a la que lo activa, y después súbala hasta pasarla.')
esperado('Al pasar la cantidad, el precio baja solo y la columna Promo dice que aplicó el mayor.')
tablas('Caja: LineasVentaEnProceso · Articulos (lee: precio de mayor)')
marcar()

prueba('F2', 'Crear una oferta en el Central')
paso('En Promociones cree una oferta para el artículo normal (por ejemplo 10 % de descuento), vigente hoy, para su sucursal.')
paso('Espere a que baje a la caja y venda ese artículo.')
esperado('El precio sale con el descuento, la columna Promo dice cuál oferta aplicó y la pantalla del cliente muestra el ahorro.')
tablas('Central: Promociones · Caja: Promociones · LineasVentaEnProceso')
marcar()

prueba('F2b', 'Una promoción publicada no se edita: se apaga o se rehace')
paso('En la lista de Promociones abra el menú de la oferta del F2. Compruebe que no hay opción de editarla.')
paso('Use «Rehacer»: cambie el porcentaje y guarde.')
paso('Apague la oferta original y espere a que la caja sincronice; venda el artículo.')
paso('Vuelva a encenderla. Después intente encender una oferta cuya fecha de fin ya pasó.')
esperado('Rehacer abre una promoción nueva con todos los datos de la anterior y un código nuevo de la secuencia; la original '
         'queda como estaba. Apagada, la caja deja de aplicarla tras sincronizar. Se puede volver a encender mientras siga '
         'dentro de sus fechas; una vencida no, y lo dice con esas palabras.')
tablas('Central: Promociones · SecuenciasCentral · Auditoria · Caja: Promociones')
marcar()

prueba('F3', 'Dos ofertas a la vez')
paso('Cree una segunda oferta para el mismo artículo, mejor que la primera (por ejemplo 15 %).')
paso('Venda el artículo otra vez.')
esperado('Aplica solo la que más le conviene al cliente, no las dos. Las ofertas no se suman.')
tablas('Central: Promociones · Caja: Promociones · LineasVentaEnProceso')
marcar()

prueba('F4', 'Chequeador de precios')
paso('Abra el chequeador en la dirección de su sucursal (/chequeador/01) y pase el artículo.')
esperado('Muestra la descripción, el precio grande con el ITBIS incluido y solo la mejor oferta, la misma que le aplicará la '
         'caja. A los pocos segundos se limpia solo.')
tablas('Central: Articulos · Promociones (lee)')
marcar()

prueba('F5', 'Desactivar una promoción en la venta')
paso('Con el artículo en oferta en la venta, desactívele la promoción a esa línea (requiere permiso).')
esperado('Vuelve al precio normal y queda registrado quién la desactivó.')
tablas('Caja: LineasVentaEnProceso · Auditoria')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- G. Descuentos
titulo('Bloque G — Descuentos manuales')

prueba('G1', 'Descuento a una línea')
paso('Toque el precio de una línea y aplique un descuento con su motivo.')
esperado('Pide el motivo, aplica el descuento y recalcula el ITBIS de esa línea.')
tablas('Caja: LineasVentaEnProceso · MotivosDescuento (lee) · Auditoria')
marcar()

prueba('G2', 'Descuento a toda la factura')
paso('Aplique un descuento a la factura completa.')
esperado('Se reparte entre las líneas y la suma de los descuentos cuadra al centavo con el descuento pedido.')
tablas('Caja: VentasEnProceso · LineasVentaEnProceso')
marcar()

prueba('G3', 'Tope y escalamiento')
paso('Intente un descuento mayor al tope del cajero.')
esperado('Pide autorización de alguien con nivel suficiente. Si el supervisor tampoco llega, lo dice y hay que subir de nivel.')
tablas('Caja: TopesDescuento (lee) · AutorizacionesOtorgadas · Auditoria')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- H. Espera y cotizaciones
titulo('Bloque H — Facturas en espera y cotizaciones')

prueba('H1', 'Poner una venta en espera (F7)')
paso('Con una venta armada, presione F7 y escríbale una referencia («Sra. María», «camioneta azul»).')
paso('Atienda a otro cliente y cóbrele.')
paso('Vuelva a F7 y retome la primera.')
esperado('La referencia es obligatoria y no se repite dentro del turno. La venta se retoma completa, y el número de factura '
         'se lo llevó el cliente que cobró primero.')
tablas('Caja: VentasGuardadas · LineasVentaGuardadas (y la venta sale de VentasEnProceso)')
marcar()

prueba('H2', 'Cotizar en el Central')
paso('En una computadora que no sea la caja, entre al Central y cree una cotización (Cotizaciones → Nueva) con dos '
     'artículos y un cliente.')
paso('Descárguela en PDF e imprímala.')
esperado('El PDF sale en hoja carta, con los datos de la empresa, el detalle, los totales, la vigencia y las condiciones.')
tablas('Central: Cotizaciones · LineasCotizacion · SecuenciasCentral')
marcar()

prueba('H3', 'Facturar la cotización en la caja (F9)')
paso('En la caja, presione F9 y digite (o escanee del PDF) el número de la cotización.')
esperado('Las líneas entran a la venta con los precios congelados de la cotización, aunque el precio del artículo haya '
         'cambiado. Al cobrar, la cotización queda marcada como facturada y no se puede volver a usar.')
tablas('Caja: VentasEnProceso · LineasVentaEnProceso · Central: Cotizaciones (queda facturada)')
marcar()

prueba('H4', 'Cotización vencida')
paso('Cambie la fecha de vencimiento de otra cotización a ayer e intente facturarla.')
esperado('No se factura sola: pide autorización de un supervisor con el permiso, y queda registrado quién la revivió.')
tablas('Caja: AutorizacionesOtorgadas · Auditoria')
marcar()

prueba('H5', 'Suspender la caja con su motivo')
paso('Con una venta a medias en pantalla, abra el panel de funciones (☰) y presione «Suspender».')
paso('Elija el motivo «Almuerzo». No debe pedir autorización de supervisor.')
paso('Encienda en el Central el parámetro «Caja.SuspenderRequiereAutorizacion», espere a que la caja sincronice y suspenda otra vez.')
paso('Vuelva un rato después: digite la clave del mismo cajero y presione «Regresar», debajo del conteo.')
paso('Repita con el motivo «Otro» y déjelo sin escribir la explicación.')
esperado('Sin el parámetro no pide autorización; con él encendido sí la pide. La pantalla se bloquea con un cronómetro grande '
         'y la línea «cajero · motivo». Al regresar, la venta que estaba en pantalla sigue ahí completa. El motivo «Otro» no '
         'deja seguir hasta que se escriba en qué consistió. En el panel de funciones ya no hay «Salir».')
tablas('Caja: MotivosSuspension (lee) · SuspensionesCaja · AutorizacionesOtorgadas · Auditoria · BandejaSalida')
marcar()

prueba('H6', 'Nadie volvió a la caja')
paso('Suspenda la caja y, sin reanudar, cierre el turno desde el supervisor.')
esperado('El cierre no se traba: la parada se cierra ahí y queda marcada como cerrada por el cierre del turno, para que no '
         'aparezca de catorce horas en el reporte.')
tablas('Caja: SuspensionesCaja · Turnos · BandejaSalida')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- I. Cobro
titulo('Bloque I — Cobro y ticket')

prueba('I1', 'Efectivo con devuelta')
paso('Presione F8, elija Efectivo, digite un monto mayor al total y cobre.')
esperado('La devuelta se ve grande y clara antes y después de cobrar, la gaveta se abre y el ticket sale impreso.')
tablas('Caja: Ventas · LineasVenta · PagosVenta · SecuenciasCaja · DocumentosElectronicos · BandejaSalida')
marcar()

prueba('I2', 'Pago mixto')
paso('Cobre otra venta con parte en efectivo y el resto con tarjeta.')
esperado('El saldo pendiente se va actualizando con cada pago y no deja cerrar hasta que llega a cero.')
tablas('Caja: PagosVenta')
marcar()

prueba('I3', 'Tarjeta con el terminal conectado, rechazo y contingencia')
paso('Cobre una venta con tarjeta: la caja le manda el monto al terminal y el cliente paga ahí.')
paso('Haga que el terminal rechace un cobro (o simúlelo) y observe qué pasa.')
paso('Desconecte el terminal de la red y pruebe otra vez: use la aprobación manual con el permiso correspondiente.')
esperado('Con el terminal conectado, la aprobación llega sola y nadie digita nada. El rechazo no cobra la venta ni la daña: '
         'se puede reintentar. Si el terminal no responde, la aprobación manual pide autorización de supervisor y queda '
         'marcada para conciliarla después contra el lote.')
tablas('Caja: OperacionesTerminal · PagosVenta · Auditoria')
marcar()

prueba('I3b', 'Caja sin terminal conectado (se cobra en un equipo aparte)')
paso('En la configuración de la caja, ponga el terminal en «Ninguno» y reinicie el servicio.')
paso('Cobre una venta con tarjeta: cobre en el verifone aparte y digite el número de aprobación de su volante y los '
     'últimos cuatro dígitos.')
paso('Intente también cobrar con tarjeta sin escribir el número de aprobación.')
esperado('La pantalla no ofrece «Pasar tarjeta» sino el número de aprobación, y no pide autorización de supervisor: en esa '
         'caja es la forma normal de cobrar. Sin el número no deja cobrar, porque después no habría con qué cuadrar el '
         'turno contra los volantes.')
tablas('Caja: Ventas · LineasVenta · PagosVenta (con la aprobación digitada) · Auditoria')
marcar()
nota('Deje el terminal como estaba antes de seguir. Este modo es para las tiendas que cobran con un equipo inalámbrico que '
     'no habla con la caja; en «Simulado» el sistema aprueba solo, y eso en una tienda registraría cobros que no ocurrieron.')

prueba('I3c', 'Un terminal mal configurado se nota')
paso('Escriba un modelo de terminal que no exista (por ejemplo «Azul») y reinicie el servicio.')
paso('Revise el registro del Agente y la dirección /salud.')
esperado('El log avisa que ese modelo no está implementado y dice cuáles hay. En /salud, la sección Periféricos muestra con '
         'qué quedó configurada la caja: impresora, balanza y terminal, y cuáles están simulados.')
tablas('Ninguna: es configuración del equipo, no datos')
marcar()

prueba('I4', 'Revisar el ticket')
paso('Mire el ticket impreso con calma.')
esperado('Trae los datos de la empresa, el detalle con sus descuentos, el ITBIS, el total, la forma de pago, la devuelta, el '
         'e-NCF con su código de seguridad y el código QR.')
tablas('Ninguna: el ticket se imprime con lo que ya se guardó al cobrar')
marcar()

prueba('I5', 'Reimprimir y abrir gaveta')
paso('Reimprima la última factura y abra la gaveta desde el menú.')
esperado('Las dos cosas piden permiso y quedan en la auditoría. La copia sale marcada como copia.')
tablas('Caja: Auditoria')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- J. e-CF
titulo('Bloque J — Factura electrónica')

prueba('J1', 'Certificado')
paso('Toque el indicador de e-CF en la barra de estado y cargue el PIN del certificado.')
esperado('Dice que el certificado está cargado y cuánto le falta para vencer. El PIN no se guarda en disco: al reiniciar el '
         'servicio lo vuelve a pedir.')
tablas('Ninguna: el PIN del certificado vive solo en memoria')
marcar()

prueba('J2', 'La factura se firma')
paso('Cobre una venta y revise el e-NCF del ticket.')
paso('En el Central, entre a Facturas y búsquela.')
esperado('El e-NCF es correlativo, el ticket trae el código de seguridad y la fecha de firma, y en el Central la factura '
         'aparece con su XML.')
tablas('Caja: DocumentosElectronicos · SecuenciasEcf · Central: ComprobantesRecibidos · VentasCentral')
marcar()

prueba('J3', 'Aviso de secuencia baja y encadenado de rangos')
paso('Deje el rango de e-NCF casi agotado, o baje el parámetro Fiscal.ComprobantesAlertaSecuenciaEcf para que salte antes.')
paso('Cargue un segundo rango del mismo tipo para esa caja, con números siguientes, y facture hasta agotar el primero.')
paso('En el Central, abra el Monitor.')
esperado('La caja avisa cuántos comprobantes quedan al facturar y en su barra de estado. Al agotarse el primer rango, las '
         'facturas siguen con el segundo sin que nadie haga nada, y el rango agotado desaparece de la caja y de la lista de '
         'Fiscal (se ve marcando «Ver agotados»). El Monitor del Central avisa de la caja a la que le queda poco, para que '
         'soporte se entere sin depender de que llamen.')
tablas('Caja: SecuenciasEcf (el agotado se borra) · BandejaSalida · Central: SecuenciasEcf')
marcar()

prueba('J4', 'Estado en la DGII')
paso('En el Central, entre a Monitor → Comprobantes.')
esperado('Cada comprobante muestra si fue aceptado, rechazado o está pendiente, con el mensaje de la DGII cuando lo hay.')
tablas('Central: ComprobantesRecibidos · Caja: HistorialEstadosEcf')
marcar()
nota('Un comprobante rechazado no se corrige desde el POS: eso lo resuelven tecnología y contabilidad. La caja solo lo reenvía.')

doc.add_page_break()

# ---------------------------------------------------------------- K. Devoluciones
titulo('Bloque K — Devoluciones y notas de crédito')

prueba('K1', 'Devolver parte de una factura')
paso('Presione F10 y busque la factura que cobró en el bloque I (por número o escaneando el código del ticket).')
paso('Devuelva una sola de las líneas, con su motivo, e identifique al cliente con cédula o RNC.')
paso('Autorice con el supervisor.')
esperado('Sale la nota de crédito E34 impresa, con referencia al e-NCF de la factura, y con las copias del cliente y de '
         'contabilidad.')
tablas('Caja: Devoluciones · LineasDevolucion · DocumentosElectronicos · SecuenciasEcf · BandejaSalida · Central: NotasCredito')
marcar()

prueba('K2', 'No se puede devolver más de lo vendido')
paso('Busque la misma factura otra vez e intente devolver más cantidad de la que queda.')
esperado('Solo deja devolver lo que falta, descontando lo ya devuelto.')
tablas('Caja: Devoluciones · LineasDevolucion (lee)')
marcar()

prueba('K3', 'Retención del ITBIS fuera de plazo')
paso('Devuelva una factura con más días de los configurados en A9.')
esperado('La nota de crédito retiene el ITBIS y lo dice en pantalla y en el papel.')
tablas('Caja: Devoluciones · Parametros (lee)')
marcar()

prueba('K4', 'Usar la nota de crédito como pago')
paso('Arme una venta nueva y cóbrela con la nota de crédito del K1.')
esperado('Valida vigencia y saldo. Si sobra saldo, imprime el voucher con lo que queda; ese saldo se puede volver a consultar.')
tablas('Caja: ConsumosNotaCredito · PagosVenta · Central: NotasCredito · ConsumosNotaCredito')
marcar()

prueba('K5', 'Nota de crédito interna')
paso('Haga otra devolución marcándola como interna (requiere su permiso).')
esperado('Sale sin comprobante fiscal y no se puede usar como forma de pago.')
tablas('Caja: Devoluciones · Auditoria')
marcar()

prueba('K6', 'Factura de otra caja')
paso('Cobre una factura en la caja 01 y búsquela desde otra caja (o desde la misma, después de que suba al Central).')
esperado('La trae del Central con lo ya devuelto de toda la empresa, y la nota de crédito la emite la caja donde está el '
         'cliente, con su propio rango de e-NCF.')
tablas('Caja: FacturasConsultadas · LineasFacturaConsultada · Central: VentasCentral (lee) · ReservasFactura · LineasReservaFactura')
marcar()

prueba('K7', 'Sin conexión')
paso('Desconecte la caja de la red e intente buscar una factura que no sea suya.')
esperado('Dice que solo se pueden devolver las facturas de esa caja mientras no haya conexión. No promete nada que no pueda '
         'cumplir.')
tablas('Ninguna: sin conexión no se consulta nada, solo se avisa')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- L. Listas de boda
titulo('Bloque L — Lista de boda')

prueba('L1', 'Crear la lista en el Central')
paso('En Listas de boda → Nueva, cree una lista con los novios, la fecha y tres artículos con sus cantidades.')
esperado('La lista queda con su número y se puede imprimir o consultar.')
tablas('Central: ListasBoda · ArticulosListaBoda · SecuenciasCentral')
marcar()

prueba('L2', 'Comprar de la lista en la caja (F6)')
paso('En la caja, presione F6 y asocie la lista por su número.')
paso('Venda uno de los artículos de la lista y cobre.')
esperado('La venta queda asociada a la lista y el ticket lo dice.')
tablas('Caja: Ventas (lleva el número de la lista) · BandejaSalida')
marcar()

prueba('L3', 'La lista baja lo comprado')
paso('Vuelva al Central y abra la lista.')
esperado('El artículo comprado aparece descontado de lo pedido, con la factura que lo compró.')
tablas('Central: ComprasListaBoda · ArticulosListaBoda')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- M. Fidelidad
titulo('Bloque M — Fidelidad')

prueba('M1', 'Inscribir un cliente')
paso('En la caja, inscriba un cliente al programa con su cédula.')
esperado('Queda inscrito y el Central lo ve en Fidelidad → Miembros.')
tablas('Caja: MiembrosFidelidad · BandejaSalida · Central: MiembrosFidelidad')
marcar()

prueba('M2', 'Acumular puntos')
paso('Cóbrele una factura a ese cliente.')
esperado('Los puntos se acumulan según la regla configurada y el ticket dice cuántos ganó y cuántos tiene.')
tablas('Caja: MovimientosPuntos · Central: MovimientosPuntos · SaldosPuntos')
marcar()

prueba('M3', 'Canjear puntos')
paso('En otra venta, canjee puntos como parte del pago (requiere permiso).')
esperado('Descuenta los puntos usados, respeta el tope de canje sin conexión y el saldo queda correcto en el Central.')
tablas('Caja: MovimientosPuntos · PagosVenta · Central: MovimientosPuntos · SaldosPuntos')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- N. Pendientes y despacho
titulo('Bloque N — Mercancía que no se lleva el cliente')

prueba('N1', 'Marcar una línea para entrega')
paso('En una venta, marque una línea como pendiente de entrega o envío, con la dirección y el teléfono del cliente.')
paso('Cobre la venta.')
esperado('Sale el voucher del pendiente con sus datos, y la mercancía queda registrada como no entregada.')
tablas('Caja: DestinosEntregaVenta · LineasDestinoEntrega · PendientesEntrega · LineasPendienteEntrega · Central: PendientesEntrega · LineasPendienteEntrega')
marcar()

prueba('N2', 'Despachar desde el Central')
paso('En Despacho → Pendientes, abra ese pendiente, cámbielo a preparado y después entréguelo, anotando quién lo recibe '
     'con su cédula.')
esperado('La constancia de entrega se imprime en hoja carta y el pendiente queda cerrado.')
tablas('Central: EntregasPendiente · LineasEntregaPendiente · PendientesEntrega')
marcar()

prueba('N3', 'No se devuelve lo que no se ha entregado')
paso('Intente devolver, desde la caja, un artículo pendiente que todavía no se haya entregado.')
esperado('No lo deja, y explica que la mercancía aún no se ha entregado.')
tablas('Caja: PendientesEntrega (lee)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- O. Turno
titulo('Bloque O — Movimientos del turno y cierre')

prueba('O1', 'Retiro de efectivo')
paso('Haga un retiro con su motivo y la autorización del supervisor.')
paso('Intente retirar más efectivo del que hay en la gaveta.')
esperado('El primero imprime su comprobante con las firmas. El segundo se rechaza diciendo cuánto hay.')
tablas('Caja: MovimientosCaja · AutorizacionesOtorgadas · BandejaSalida · Auditoria')
marcar()

prueba('O2', 'Relevo de cajero')
paso('Con una venta a medias, haga un relevo: entra otro cajero con autorización.')
esperado('El turno no se cierra, cambia de operador, y el cajero que entra ve la venta en curso y las facturas en espera '
         'tal como estaban.')
tablas('Caja: Turnos · MovimientosCaja · Auditoria')
marcar()

prueba('O3', 'Pre-cierre')
paso('Pida el pre-cierre con la clave del supervisor.')
esperado('Imprime lo esperado por forma de pago. El cajero por sí solo no lo puede sacar.')
tablas('Caja: Auditoria')
marcar()

prueba('O4', 'Cerrar el lote de tarjetas')
paso('Presione «Cerrar lote».')
esperado('Compara lo aprobado en la caja contra lo que reporta el terminal y lo muestra. Si el terminal no detalla su lote, '
         'lo dice y muestra lo de la caja para comparar contra el comprobante impreso.')
tablas('Caja: LotesTarjetas · LotesTarjetasAprobaciones · OperacionesTerminal (lee) · Auditoria')
marcar()

prueba('O5', 'Lo que impide cerrar el turno')
paso('Deje una factura en espera y una venta a medias con artículos, e intente cerrar el turno.')
esperado('No cierra y lista exactamente qué falta: cuáles facturas en espera (por su referencia) y cuáles transacciones en '
         'curso.')
tablas('Caja: VentasGuardadas · VentasEnProceso · DocumentosElectronicos (lee)')
marcar()

prueba('O6', 'Cerrar el turno')
paso('Limpie lo que faltaba y cierre el turno con la autorización del supervisor.')
esperado('El turno cierra, la caja imprime el cuadre con lo esperado por forma de pago y NO le pide a la cajera declarar '
         'nada. El mensaje le dice que entregue el efectivo y el comprobante del lote al supervisor.')
tablas('Caja: CierresTurno · CierresTurnoFormasPago · Turnos · BandejaSalida · Central: CierresTurno · CierresFormaPago · CierresTurnoMovimientos · CierresTurnoLote · CierresTurnoLoteAprobaciones')
marcar()
nota('Aquí la cajera cuenta su dinero a ciegas, lo mete en su sobre con el comprobante del lote y el cuadre impreso, y se lo '
     'entrega al supervisor. Ella no ve los montos esperados: esa es la idea.')

prueba('O7', 'Turno de un día anterior')
paso('Deje un turno abierto de un día para otro (o cambie la fecha del equipo) e intente vender.')
esperado('No deja vender ni cobrar. Avisa que hay que limpiar las facturas en espera y cerrar el turno, y no hay autorización '
         'que lo salte.')
tablas('Caja: Turnos · Parametros (lee)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- P. Cuadre
titulo('Bloque P — El supervisor cuadra')

prueba('P1', 'Entrar al módulo de cuadre')
paso('En una computadora de la tienda, abra /cuadre y entre con el usuario y la clave de CAJA del supervisor.')
paso('Pruebe también con el usuario del cajero, que no tiene el permiso.')
esperado('El supervisor entra y ve solo su sucursal. El cajero recibe que su usuario no tiene acceso al módulo.')
tablas('Central: UsuariosCaja · RolesCajaPermisos (lee) · Auditoria')
marcar()

prueba('P2', 'Cuadrar el turno')
paso('En «Por cuadrar» abra el cierre del bloque O.')
paso('Cuente el efectivo y digite las cantidades por denominación; escriba el total de las demás formas de pago.')
paso('Antes de guardar, ponga a propósito un efectivo declarado distinto del conteo.')
esperado('No deja guardar mientras el efectivo declarado no cuadre con el conteo. Al corregirlo, guarda y el cierre sale de '
         'pendientes con su diferencia y el nombre de quien cuadró.')
tablas('Central: CierresTurno · CierresFormaPago · CierresTurnoDenominaciones · Auditoria')
marcar()

prueba('P3', 'La diferencia es de la cajera')
paso('Vaya a la pestaña «Diferencias por cajera».')
esperado('El faltante o el sobrante aparece a nombre de la cajera del turno, no del supervisor que lo declaró.')
tablas('Central: CierresTurno (lee)')
marcar()

prueba('P4', 'Corregir un cuadre')
paso('En la pestaña «Cierres», corrija el cuadre de ese turno con un motivo.')
esperado('Pide el motivo, no borra lo anterior y el cierre queda marcado como corregido, con quién lo hizo. Solo puede '
         'hacerlo un rol con el permiso de corregir.')
tablas('Central: AjustesCierreTurno · CierresFormaPago · CierresTurno · Auditoria')
marcar()

prueba('P5', 'Resumen del día y retiros')
paso('Revise las pestañas «Resumen del día» y «Retiros y relevos».')
esperado('El resumen suma el día por forma de pago y avisa cuántas cajas faltan por cuadrar. En retiros aparece el del O1, '
         'con su motivo y quién lo autorizó.')
tablas('Central: CierresTurno · CierresFormaPago · CierresTurnoMovimientos (lee)')
marcar()

prueba('P6', 'Tiempos de caja parada')
paso('Vaya a la pestaña «Caja parada» con las fechas del recorrido.')
esperado('Aparecen las paradas del H5 y del H6: el total del período, el mismo tiempo visto por caja, por cajera y por '
         'motivo, y el detalle de cada una. El almuerzo cuenta como tiempo previsto y el resto como imprevisto; la que '
         'cerró el turno sale marcada.')
tablas('Central: SuspensionesCaja (lee)')
marcar()

prueba('P7', 'Reimprimir el cuadre y ver las tarjetas')
paso('Descargue el PDF del cuadre desde la pestaña «Cierres», y revise la pestaña «Tarjetas».')
esperado('El PDF trae lo esperado y lo declarado, el efectivo billete por billete, los movimientos y las correcciones. En '
         'Tarjetas se ve el lote del O4 con su diferencia y las aprobaciones que aparecen de un solo lado.')
tablas('Central: CierresTurnoDenominaciones · CierresTurnoLote · CierresTurnoLoteAprobaciones (lee)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- Q. Cierre de sucursal y reportes
titulo('Bloque Q — Cierre de la sucursal y reportes')

prueba('Q1', 'No se consolida con cajas sin cuadrar')
paso('Cierre otro turno en la caja y, sin cuadrarlo, vaya a Cierres de sucursal y prepare el día.')
esperado('Avisa que ese turno está sin cuadrar y no deja consolidar hasta que el supervisor lo declare.')
tablas('Central: CierresTurno · VentasCentral (lee)')
marcar()

prueba('Q2', 'Consolidar el día')
paso('Cuadre lo que faltaba, prepare el día otra vez, registre uno o dos depósitos con su banco y boleta, y cierre.')
esperado('Calcula el efectivo a depositar por moneda (las tarjetas no se depositan) y deja a la vista la diferencia entre lo '
         'depositado y lo que había que depositar.')
tablas('Central: CierresSucursal · CierresSucursalFormaPago · DepositosCierreSucursal · SecuenciasCentral')
marcar()

prueba('Q3', 'Un cierre que llega tarde')
paso('Envíe un cierre de caja de ese mismo día después de haber consolidado.')
esperado('El consolidado no cambia, pero la lista lo avisa para que alguien lo revise.')
tablas('Central: CierresTurno · CierresSucursal (lee)')
marcar()

prueba('Q4', 'Reportes')
paso('En Reportes saque Ventas, ITBIS por tasa, Cuadres de caja, e-CF y Formato 607, y descárguelos en Excel y en PDF.')
esperado('Los números cuadran con lo que hizo en el recorrido. En Cuadres se ven lo declarado por la caja y lo corregido, '
         'las dos cifras.')
tablas('Central: VentasCentral · LineasVenta · ImpuestosVenta · PagosVenta · CierresTurno · ComprobantesRecibidos (lee)')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- R. Sin red
titulo('Bloque R — Que se caiga la red')

prueba('R1', 'Vender sin conexión')
paso('Desconecte la caja de la red y venda y cobre tres facturas.')
esperado('La caja sigue vendiendo y facturando con normalidad. La barra de estado dice que no hay comunicación y cuántos '
         'documentos están pendientes de enviar.')
tablas('Caja: Ventas · LineasVenta · BandejaSalida (los mensajes se acumulan)')
marcar()

prueba('R2', 'Reconectar')
paso('Vuelva a conectar la red y espere.')
esperado('Los documentos suben solos, el contador de pendientes baja a cero y en el Central aparecen las tres facturas, sin '
         'duplicados.')
tablas('Caja: BandejaSalida · MarcasSincronizacion · Central: DocumentosRecibidos · VentasCentral · EstadosSincronizacionCaja')
marcar()

prueba('R2b', 'Sincronizar ahora desde la caja')
paso('En el Central, cambie el precio de un artículo.')
paso('En la caja, con la venta vacía, abra el panel de funciones (☰) y presione «Sincronizar».')
paso('Mientras corre, presione «Seguir trabajando» y siga en la pantalla de venta.')
paso('Intente sincronizar otra vez con una venta que tenga artículos.')
esperado('El modal dice en qué anda y al terminar resume qué bajó y qué subió. Al cerrarlo, la caja sigue funcionando y el '
         'artículo ya tiene el precio nuevo. Con una venta empezada no deja sincronizar y explica por qué.')
tablas('Caja: los maestros que hayan cambiado · BandejaSalida · MarcasSincronizacion')
marcar()

prueba('R3', 'Monitor de sincronización')
paso('En el Central, revise Monitor.')
esperado('Muestra la última comunicación de cada caja, los mensajes recibidos y, si hubo, los rechazos y conflictos con su '
         'motivo.')
tablas('Central: EstadosSincronizacionCaja · ConflictosSincronizacion · DocumentosRecibidos (lee)')
marcar()

prueba('R4', 'El Central apagado no traba la caja')
paso('Apague el Central y cierre un turno en la caja.')
esperado('El turno cierra igual y el cuadre queda esperando: cuando el Central vuelva, el cierre sube y aparece en /cuadre.')
tablas('Caja: CierresTurno · BandejaSalida')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- S. Permisos
titulo('Bloque S — Cajas dedicadas y permisos')

prueba('S1', 'Una caja solo de devoluciones')
paso('Entre a la caja con el usuario del rol DEVOLUCIONES que creó en A8.')
esperado('Entra directo a la pantalla de devoluciones. Si va a la pantalla de venta, puede consultar artículos y clientes '
         'pero no escanear ni cobrar, y un aviso lo explica.')
tablas('Caja: Usuarios · Roles · RolesPermisos (lee)')
marcar()

prueba('S2', 'Una caja que no devuelve')
paso('Entre con el CAJERO (que no tiene el permiso de devoluciones) y mire la tecla F10.')
paso('Escriba a mano la dirección /devoluciones en el navegador.')
esperado('F10 está apagada y la dirección no le abre la pantalla: le dice que su usuario no tiene ese permiso y le ofrece '
         'volver a la venta.')
tablas('Caja: Roles · RolesPermisos (lee)')
marcar()

prueba('S3', 'Auditoría')
paso('En el Central, entre a Seguridad → Auditoría y busque el día de las pruebas.')
esperado('Están los ingresos, las autorizaciones del supervisor con su motivo, las eliminaciones de línea, los descuentos, '
         'los retiros, los cierres y los cuadres, cada uno con quién lo hizo.')
tablas('Central: Auditoria · Caja: Auditoria')
marcar()

doc.add_page_break()

# ---------------------------------------------------------------- Si algo falla
titulo('Si algo falla')
p('Antes de reportarlo, junte estas tres cosas: el código de la prueba, la hora exacta y qué esperaba ver.')
tabla(['Dónde mirar', 'Qué encontrará'],
      [['Barra de estado de la caja', 'Si hay comunicación, cuántos documentos faltan por subir y el detalle del último error.'],
       ['Monitor del Central', 'Rechazos y conflictos de sincronización, con el motivo.'],
       ['Seguridad → Auditoría', 'Quién hizo cada operación, con su motivo y quién la autorizó.'],
       ['Monitor → Comprobantes', 'El estado de cada factura en la DGII y su mensaje.'],
       ['C:\\CGPOS\\Logs', 'El registro técnico del servicio de la caja, para tecnología.']],
      anchos=[5.5, 11.5])
nota('Si una prueba falla, siga con las demás del bloque cuando pueda: un fallo suelto se arregla más rápido que un bloque '
     'entero sin probar.')

titulo('Mirar la base de datos', 2)
p('Cada prueba dice qué tablas toca. Para verlas, la base del Central es CgPosCentral y la de cada caja, CgPosCaja en su '
  'propio equipo. Lo más cómodo es ordenar por la fecha y mirar lo último:')
p('    SELECT TOP 20 * FROM Ventas ORDER BY Id DESC;')
p('    SELECT TOP 20 * FROM Auditoria ORDER BY Id DESC;')
nota('Mire, no toque. Corregir a mano en la base deja el sistema diciendo una cosa y la base otra, y a partir de ahí las '
     'pruebas que siguen ya no prueban nada.')

# ---------------------------------------------------------------- Guardar
salida = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'Guia de pruebas CG-POS.docx')

# Se recuperan las marcas de quien estaba usando la guía antes de sobrescribirla.
previas = marcas_anteriores(salida)
for codigo in previas & _casillas.keys():
    _casillas[codigo].font.highlight_color = WD_COLOR_INDEX.YELLOW

try:
    doc.save(salida)
except PermissionError:
    alterno = salida.replace('.docx', ' (nueva).docx')
    doc.save(alterno)
    print(f'El documento está abierto en Word. Se guardó aparte: {alterno}')
    raise SystemExit(0)

if previas:
    print(f'marcas conservadas: {", ".join(sorted(previas))}')
print(f'generado: {salida}')
