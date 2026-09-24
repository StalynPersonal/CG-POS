# -*- coding: utf-8 -*-
"""
Genera los scripts de estructura de las bases de datos a partir del modelo de EF Core.

    python scripts/base-datos/generar-estructura-sql.py

Deja dos archivos en la misma carpeta, uno por base:
    scripts/base-datos/estructura_base_datos_central.sql
    scripts/base-datos/estructura_base_datos_pos.sql

Cada uno crea la base si no existe, la secuencia de Id, todas las tablas con sus llaves primarias,
llaves foráneas, índices y restricciones, y en el Central el único dato inicial: el usuario administrador.
El esquema lo escribe EF (dotnet ef dbcontext script), así que el script siempre corresponde al modelo real.
No hay migraciones: la base se crea y se actualiza con estos scripts.
"""
import base64
import hashlib
import os
import re
import subprocess
import sys

RAIZ = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SALIDA = os.path.join(RAIZ, 'scripts', 'base-datos')

# Datos de la empresa con los que nace la base. El RNC identifica a la empresa ante la DGII; el valor de aquí es un
# marcador que se corrige luego desde el Central, en Organización → Empresa (admite RNC de 9 dígitos o cédula de 11).
EMPRESA = {
    'rnc': '000000002',
    'razon_social': 'CONTRERAS GROUP SRL',
    'nombre_comercial': 'Contreras Group',
    'direccion': 'Indique la dirección de la empresa',
    'telefono': '000-000-0000',
}

# Usuario administrador con el que se entra la primera vez al Central. La contraseña se cambia al ingresar.
ADMIN_CODIGO = 'ADMIN'
ADMIN_NOMBRE = 'Administrador del sistema'
ADMIN_CONTRASENA = 'Admin.CGPOS#2026'
ROL_CODIGO = 'ADMINISTRADOR'
ROL_NOMBRE = 'Administrador'

# Parámetros con los que el sistema arranca usable. Los que dependen de la empresa o del ambiente
# (direcciones de la DGII, tipo de ingresos, textos de la empresa) se dejan sin valor: los pone el
# administrador desde el Central, que avisa cuáles faltan.
PARAMETROS_INICIALES = [
    # Seguridad del Central: sin esto no se puede ni entrar.
    ('Central.Seguridad.IntentosMaximos', '5', 'Intentos de contraseña fallidos que bloquean al usuario'),
    ('Central.Seguridad.MinutosBloqueo', '15', 'Minutos que dura el bloqueo por intentos fallidos'),
    ('Central.Seguridad.MinutosToken', '15', 'Minutos de vigencia del token de acceso'),
    ('Central.Seguridad.MinutosInactividad', '60', 'Minutos sin actividad tras los que vence la sesión'),
    ('Central.Seguridad.HorasSesion', '12', 'Horas máximas de una sesión'),
    ('Central.Seguridad.LargoMinimoContrasena', '10', 'Largo mínimo de las contraseñas de los usuarios del Central'),
    ('Central.Seguridad.LargoMinimoClaveCaja', '6', 'Largo mínimo de la clave de los usuarios de caja'),
    ('Central.Seguridad.ContrasenaCompleja', 'true', 'Exige mayúscula, minúscula, número y símbolo'),
    ('Central.Dispositivos.MinutosToken', '30', 'Minutos de vigencia del token de una caja'),

    # Ritmo de los procesos del Central.
    ('Central.Dgii.SegundosCiclo', '30', 'Segundos entre envíos de e-CF a la DGII'),
    ('Central.Dgii.LoteEnvio', '50', 'Comprobantes por lote de envío a la DGII'),
    ('Central.Dgii.MinutosReintento', '5', 'Minutos antes de reintentar un envío fallido'),
    ('Central.Dgii.MinutosMaximoReintento', '120', 'Tope de espera entre reintentos'),
    ('Central.Dgii.SegundosConsultaEstado', '60', 'Segundos entre consultas del resultado a la DGII'),
    ('Central.Monitor.MinutosSinComunicacion', '30', 'Minutos sin comunicación tras los que una caja es alerta'),
    ('Central.Monitor.MinutosAlertaDgii', '60', 'Minutos sin resultado de la DGII tras los que un e-CF es alerta'),
    ('Central.NotasCredito.MinutosReserva', '10', 'Minutos que se retiene el saldo de una nota mientras la caja cobra'),
    ('Central.Devoluciones.MinutosReserva', '10', 'Minutos que se retienen las líneas de una factura mientras otra tienda le hace la nota de crédito'),
    ('Central.Fidelidad.MinutosCicloVencimiento', '60', 'Minutos entre revisiones de los puntos vencidos'),
    ('Central.Fidelidad.LoteVencimiento', '500', 'Miembros por lote al vencer puntos'),
    ('Central.Cotizaciones.DiasVigencia', '15', 'Días que vale una cotización desde que se hace'),
    ('Central.Despacho.MinutosCicloAvisos', '15', 'Minutos entre avisos de pedidos preparados'),
    ('Central.Despacho.LoteAvisos', '50', 'Avisos por lote'),

    # Seguridad y operación de las cajas.
    ('Seguridad.IntentosMaximosClave', '3', 'Intentos de clave fallidos que bloquean al usuario de la caja'),
    ('Seguridad.MinutosBloqueo', '5', 'Minutos que dura el bloqueo del usuario de la caja'),
    ('Seguridad.MinutosVigenciaAutorizacion', '5', 'Minutos para usar una autorización de supervisor'),
    ('Seguridad.HorasSesion', '12', 'Horas que dura la sesión en la caja'),
    ('General.MonedaLocal', 'DOP', 'Moneda local del negocio'),
    ('Caja.FondoPredeterminado', '0.00', 'Fondo sugerido al abrir turno'),
    ('Ventas.CantidadMaximaDigitada', '10', 'Cantidad máxima que el cajero puede digitar de un artículo de unidad entera'),
    ('Caja.PasoRedondeoEfectivo', '0', 'Múltiplo al que se redondea el cobro en efectivo (0 = sin redondeo)'),
    ('Caja.FondoEnCuadre', 'false', 'El fondo forma parte del efectivo esperado en el cierre'),
    ('Caja.BloquearVentaTurnoDiaAnterior', 'true', 'Con un turno abierto de un día anterior la caja no vende ni cobra'),
    ('Numeracion.DigitosSecuencia', '7', 'Dígitos de la secuencia en el número de los documentos'),

    # Fiscal y devoluciones.
    ('Fiscal.MontoIdentificacionConsumo', '250000', 'Total desde el cual la factura de consumo exige cédula o RNC'),
    ('Fiscal.PorcentajeAlertaSecuenciaEcf', '10', 'Porcentaje restante de un rango de e-CF desde el cual se alerta'),
    ('Fiscal.ComprobantesAlertaSecuenciaEcf', '10', 'Cantidad de comprobantes restantes de un rango de e-CF desde la cual se alerta al facturar'),
    ('Fiscal.DiasAlertaCertificado', '30', 'Días antes del vencimiento del certificado para alertar'),
    ('Fiscal.PorcentajeRetencionLey3223', '0', 'Retención de la Ley 32-23 en facturas gubernamentales E45 (0 = sin retención)'),
    ('Devoluciones.DiasRetencionImpuesto', '30', 'Días desde la factura tras los cuales la devolución retiene el ITBIS'),
    ('Devoluciones.DiasVigenciaNotaCredito', '180', 'Días desde la emisión en que se puede consumir una nota de crédito'),

    # Fidelidad, entregas y mantenimiento.
    ('Fidelidad.ValorPunto', '1', 'Valor en dinero de cada punto al canjearlo'),
    ('Fidelidad.MesesVigenciaPuntos', '12', 'Meses que duran los puntos acumulados'),
    ('Fidelidad.MinimoPuntosCanje', '50', 'Puntos mínimos para poder canjear'),
    ('Fidelidad.MaximoPuntosCanjeSinConexion', '2000', 'Tope de puntos a canjear sin conexión con el Central'),
    ('Sincronizacion.DiasRetencionXmlEnviados', '90', 'Días que se conservan los XML ya enviados'),
    ('Sincronizacion.DiasRetencionMensajesConfirmados', '60', 'Días que se conservan los mensajes confirmados'),
    ('Sincronizacion.AlertaTamanoBaseDatosMb', '8000', 'Tamaño de la base de la caja que dispara alerta'),
    ('Sincronizacion.HorasAlertaPendientes', '24', 'Horas con documentos sin sincronizar que disparan alerta'),
    ('Respaldo.DiasRetencion', '7', 'Días que se conservan los respaldos de la caja'),
    ('Reloj.ToleranciaSegundos', '60', 'Diferencia de hora tolerada contra el servidor NTP'),
    ('Balanza.PrefijoPeso', '21', 'Prefijo de las etiquetas de balanza con peso'),
    ('Balanza.PrefijoPrecio', '22', 'Prefijo de las etiquetas de balanza con precio'),
    ('Balanza.DigitosCodigoArticulo', '5', 'Dígitos del código del artículo en la etiqueta'),
    ('Balanza.DigitosValor', '5', 'Dígitos del valor en la etiqueta'),
    ('Balanza.DecimalesPeso', '3', 'Decimales del peso en la etiqueta'),
    ('Balanza.DecimalesPrecio', '2', 'Decimales del precio en la etiqueta'),
]

# Catálogos iguales en cualquier negocio dominicano. Lo del propio negocio (departamentos, marcas,
# bancos, almacenes, fidelidad) se crea desde el Central: el manual de usuario los sugiere.
MONEDAS = [
    ('DOP', 'Peso dominicano', 'RD$'),
    ('USD', 'Dólar estadounidense', 'US$'),
]

IMPUESTOS = [
    ('ITBIS18', 'ITBIS 18%', '18.00', 1),
    ('ITBIS16', 'ITBIS 16%', '16.00', 2),
    ('ITBIS0', 'ITBIS 0%', '0.00', 3),
    ('EXENTO', 'Exento', '0.00', 4),
]

UNIDADES = [
    (1, 'UND', 'Unidad', 0, 0),
    (2, 'LB', 'Libra', 1, 3),
    (3, 'PIE', 'Pie', 1, 2),
    (4, 'YD', 'Yarda', 1, 2),
    (5, 'GAL', 'Galón', 1, 2),
]

# Tipo: 0 billete, 1 moneda.
DENOMINACIONES = [
    ('DOP', '2000.00', 0), ('DOP', '1000.00', 0), ('DOP', '500.00', 0), ('DOP', '200.00', 0),
    ('DOP', '100.00', 0), ('DOP', '50.00', 0), ('DOP', '25.00', 1), ('DOP', '10.00', 1),
    ('DOP', '5.00', 1), ('DOP', '1.00', 1),
    ('USD', '100.00', 0), ('USD', '50.00', 0), ('USD', '20.00', 0), ('USD', '10.00', 0),
    ('USD', '5.00', 0), ('USD', '1.00', 0),
]

TIPOS_TARJETA = [(1, 'Visa'), (2, 'Mastercard'), (3, 'American Express'), (4, 'Discover')]

MOTIVOS_DESCUENTO = [
    (1, 'Cliente frecuente'), (2, 'Producto con daño o defecto'), (3, 'Ajuste de precio'), (4, 'Autorizado por gerencia'),
]

# Documentos que numera el propio Central. Cada uno con su prefijo, cómo se le llama y cuántos dígitos lleva.
# El código es con lo que el sistema la busca y no cambia; el prefijo es solo cómo se ve el número.
SECUENCIAS_CENTRAL = [
    ('Factura', 'FAC', 'Factura', 6),
    ('NotaCredito', 'NC', 'Nota de crédito', 6),
    ('Despacho', 'DES', 'Despacho', 6),
    ('CierreSucursal', 'CS', 'Cierre de sucursal', 6),
    ('Cotizacion', 'COT', 'Cotización', 6),
    ('ListaBoda', 'LB', 'Lista de boda', 6),
    ('Promocion', 'PRO', 'Promoción', 6),
]

MOTIVOS_DEVOLUCION = [
    (1, 'Artículo defectuoso'), (2, 'Artículo equivocado'), (3, 'Cliente no satisfecho'), (4, 'Garantía'), (5, 'Error de facturación'),
]

# Por qué el cajero deja la caja sola: código, nombre, si es tiempo previsto y si pide explicación.
# El almuerzo se cuenta aparte del baño; «Otro» obliga a escribir en qué consistió.
MOTIVOS_SUSPENSION = [
    (1, 'Baño', 0, 0), (2, 'Almuerzo', 1, 0), (3, 'Receso', 1, 0), (4, 'Llamado del supervisor', 0, 0), (5, 'Otro', 0, 1),
]

# Código, nombre, tipo (TipoFormaPago), moneda, orden, abre gaveta, da devuelta, pide referencia, pide banco, admite comprobante fiscal.
FORMAS_PAGO = [
    ('EFE', 'Efectivo', 0, 'DOP', 1, 1, 1, 0, 0, 1),
    ('TAR', 'Tarjeta', 1, 'DOP', 2, 0, 0, 1, 0, 1),
    ('TRA', 'Transferencia', 2, 'DOP', 3, 0, 0, 1, 1, 1),
    ('CHE', 'Cheque', 3, 'DOP', 4, 0, 0, 1, 1, 1),
    ('USD', 'Dólares', 9, 'USD', 5, 1, 1, 0, 0, 1),
    ('NC', 'Nota de crédito', 5, 'DOP', 6, 0, 0, 1, 0, 1),
    ('BONO', 'Bono de regalo', 4, 'DOP', 7, 0, 0, 1, 0, 0),
    ('GIFT', 'Tarjeta de regalo', 7, 'DOP', 8, 0, 0, 1, 0, 0),
    ('PRE', 'Préstamo bancario', 6, 'DOP', 9, 0, 0, 1, 1, 1),
    ('PUN', 'Puntos', 8, 'DOP', 10, 0, 0, 0, 0, 1),
]

# Permisos del Central: se leen del catálogo del código, para que el script no se desfase.
def permisos_central():
    catalogo = os.path.join(RAIZ, 'src', 'Compartido', 'CgPos.Dominio', 'Seguridad', 'CatalogoPermisosCentral.cs')
    with open(catalogo, encoding='utf-8-sig') as archivo:
        return re.findall(r'public const string \w+ = "([^"]+)"', archivo.read())


ITERACIONES = 600_000


def hash_contrasena(contrasena):
    """Mismo formato que HashContrasenas del Central: PBKDF2-SHA256$iteraciones$sal$hash."""
    sal = os.urandom(16)
    derivado = hashlib.pbkdf2_hmac('sha256', contrasena.encode('utf-8'), sal, ITERACIONES, 32)
    return f'PBKDF2-SHA256${ITERACIONES}${base64.b64encode(sal).decode()}${base64.b64encode(derivado).decode()}'


def esquema(proyecto, arranque, destino):
    """Pide a EF el script de creación del modelo, sin migraciones."""
    comando = [
        'dotnet', 'ef', 'dbcontext', 'script',
        '--project', proyecto,
        '--startup-project', arranque,
        '-o', destino,
    ]
    resultado = subprocess.run(comando, cwd=RAIZ, capture_output=True, text=True)
    if resultado.returncode != 0:
        print(resultado.stdout, resultado.stderr, sep='\n')
        sys.exit(f'No se pudo generar el esquema de {proyecto}')
    with open(destino, encoding='utf-8-sig') as archivo:
        return archivo.read().strip()


def encabezado(base, titulo, descripcion, archivo):
    return f"""/*
    {titulo}
    {descripcion}

    Cómo usarlo:
        sqlcmd -S .\\SQLEXPRESS -E -i {archivo}
    o ábralo en SQL Server Management Studio y ejecútelo.

    Crea la base «{base}» si no existe, la secuencia de Id, todas las tablas con sus llaves,
    índices y restricciones. Volver a ejecutarlo sobre una base que ya tiene las tablas da error:
    es para crear la base desde cero.

    Generado desde el modelo del sistema con scripts/base-datos/generar-estructura-sql.py.
    No editar a mano: se edita el modelo y se vuelve a generar.
*/

IF DB_ID(N'{base}') IS NULL
BEGIN
    PRINT 'Creando la base {base}...';
    EXEC (N'CREATE DATABASE [{base}]');
END
GO

ALTER DATABASE [{base}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

USE [{base}];
GO

/* Los índices filtrados y las restricciones exigen estas opciones; sqlcmd las trae apagadas. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

"""


def escapar(texto):
    """Comillas simples dobladas, como pide SQL."""
    return texto.replace("'", "''")


def siguiente_bloque(cantidad):
    """Primer Id libre después de sembrar: la secuencia entrega bloques de diez, así que se salta a la decena siguiente."""
    return ((cantidad // 10) + 1) * 10 + 1


def catalogos_tecnicos():
    """
    Catálogos que son iguales en cualquier negocio dominicano. Lo propio del negocio (departamentos, marcas,
    bancos, almacenes, fidelidad, artículos) se crea desde el Central; el manual de usuario lo sugiere.
    Cada tabla numera sus Id desde 1, con su propia secuencia.
    """
    partes = []
    momento = "SYSDATETIMEOFFSET()"
    quien = "N'Instalación'"

    def bloque(titulo, tabla, columnas, filas):
        valores = [f"    ({indice + 1}, {fila}, 1, {momento}, {quien})" for indice, fila in enumerate(filas)]
        partes.append(f"/* {titulo} */\nINSERT INTO [{tabla}] ([Id], {columnas}, [ModificadoEn], [ModificadoPor])\nVALUES\n"
                      + ',\n'.join(valores) + ';\n'
                      + f"ALTER SEQUENCE [Secuencia{tabla}] RESTART WITH {siguiente_bloque(len(filas))};\nGO\n")

    bloque('Monedas', 'Monedas', "[Codigo], [Nombre], [Simbolo], [Activa]",
           [f"'{codigo}', N'{nombre}', N'{simbolo}'" for codigo, nombre, simbolo in MONEDAS])

    bloque('Impuestos (ITBIS y exento)', 'Impuestos', "[Codigo], [Nombre], [Porcentaje], [IndicadorFacturacion], [Activo]",
           [f"N'{codigo}', N'{nombre}', {porcentaje}, {indicador}" for codigo, nombre, porcentaje, indicador in IMPUESTOS])

    # Las unidades no llevan «activo»: se escriben aparte.
    valores_unidades = [
        f"    ({indice + 1}, {codigo}, N'{abreviatura}', N'{nombre}', {decimales_si}, {decimales}, {momento}, {quien})"
        for indice, (codigo, abreviatura, nombre, decimales_si, decimales) in enumerate(UNIDADES)]
    partes.append('/* Unidades de medida */\nINSERT INTO [UnidadesMedida] ([Id], [Codigo], [Abreviatura], [Nombre], '
                  '[PermiteDecimales], [Decimales], [ModificadoEn], [ModificadoPor])\nVALUES\n'
                  + ',\n'.join(valores_unidades) + ';\n'
                  + f'ALTER SEQUENCE [SecuenciaUnidadesMedida] RESTART WITH {siguiente_bloque(len(UNIDADES))};\nGO\n')

    bloque('Denominaciones del efectivo (para el cuadre)', 'Denominaciones', "[Moneda], [Valor], [Tipo], [Activa]",
           [f"'{moneda}', {valor}, {tipo}" for moneda, valor, tipo in DENOMINACIONES])

    bloque('Tipos de tarjeta', 'TiposTarjeta', "[Codigo], [Nombre], [Activo]",
           [f"{codigo}, N'{nombre}'" for codigo, nombre in TIPOS_TARJETA])

    bloque('Motivos de descuento', 'MotivosDescuento', "[Codigo], [Nombre], [Activo]",
           [f"{codigo}, N'{nombre}'" for codigo, nombre in MOTIVOS_DESCUENTO])

    bloque('Motivos de devolución', 'MotivosDevolucion', "[Codigo], [Nombre], [Activo]",
           [f"{codigo}, N'{nombre}'" for codigo, nombre in MOTIVOS_DEVOLUCION])

    bloque('Motivos de caja parada', 'MotivosSuspension', "[Codigo], [Nombre], [Programado], [ExigeNota], [Activo]",
           [f"{codigo}, N'{nombre}', {programado}, {nota}" for codigo, nombre, programado, nota in MOTIVOS_SUSPENSION])

    # Las secuencias del Central no llevan Id ni auditoría: su clave es el propio prefijo.
    valores_secuencias = ',\n'.join(
        f"    ('{codigo}', '{prefijo}', N'{documento}', 0, {digitos}, 1)" for codigo, prefijo, documento, digitos in SECUENCIAS_CENTRAL)
    partes.append(
        '/* Numeración de los documentos que emite el Central. Sin la fila de un documento, ese documento no se\n'
        '   puede crear: la numeración es una decisión del negocio, no algo que el sistema invente. */\n'
        'INSERT INTO [SecuenciasCentral] ([Codigo], [Prefijo], [Documento], [Ultimo], [Digitos], [Activa])\nVALUES\n'
        + valores_secuencias + ';\nGO\n')

    bloque('Formas de pago', 'FormasPago',
           "[Codigo], [Nombre], [Tipo], [Moneda], [Orden], [AbreGaveta], [PermiteDevuelta], [RequiereReferencia], "
           "[RequiereBanco], [PermiteComprobanteFiscal], [Activa]",
           [f"N'{codigo}', N'{nombre}', {tipo}, '{moneda}', {orden}, {gaveta}, {devuelta}, {referencia}, {banco}, {fiscal}"
            for codigo, nombre, tipo, moneda, orden, gaveta, devuelta, referencia, banco, fiscal in FORMAS_PAGO])

    return '\n'.join(partes)


def datos_iniciales():
    """El administrador del sistema, los parámetros y los catálogos que son iguales en cualquier negocio."""
    hash_admin = hash_contrasena(ADMIN_CONTRASENA)
    catalogos = catalogos_tecnicos()
    siguiente_parametros = siguiente_bloque(len(PARAMETROS_INICIALES))
    rnc = EMPRESA['rnc']
    razon_social = escapar(EMPRESA['razon_social'])
    nombre_comercial = escapar(EMPRESA['nombre_comercial'])
    direccion = escapar(EMPRESA['direccion'])
    telefono = escapar(EMPRESA['telefono'])
    permisos = ',\n    '.join(f"(1, N'{permiso}')" for permiso in permisos_central())
    parametros = ',\n    '.join(
        f"({indice + 1}, N'{clave}', N'{valor}', N'{escapar(descripcion)}', NULL, NULL, SYSDATETIMEOFFSET(), N'Instalación')"
        for indice, (clave, valor, descripcion) in enumerate(PARAMETROS_INICIALES))

    return f"""

/* ------------------------------------------------------------------------
   Datos iniciales: solo el administrador del sistema.
   Todo lo demás (empresa, sucursales, cajas, maestros, artículos, precios,
   promociones, usuarios de caja y los demás parámetros) se crea desde el
   Central entrando con este usuario.

        Usuario:    {ADMIN_CODIGO}
        Contraseña: {ADMIN_CONTRASENA}

   El RNC sembrado ({rnc}) es un marcador: corríjalo al entrar, en
   Organización -> Empresa.

   El sistema exige cambiarla en el primer ingreso.
   ------------------------------------------------------------------------ */

/* ------------------------------------------------------------------------
   Empresa: es la que sale en las facturas y la que se identifica ante la DGII.
   Todos estos datos, el RNC incluido, se editan luego en el Central, en
   Organización → Empresa. El RNC admite 9 dígitos (RNC) u 11 (cédula, si
   factura una persona física) y se le valida el dígito verificador.
   ------------------------------------------------------------------------ */

INSERT INTO [Empresas] ([Id], [Rnc], [RazonSocial], [NombreComercial], [Direccion], [Telefono], [ModificadoEn], [ModificadoPor])
VALUES (1, '{rnc}', N'{razon_social}', N'{nombre_comercial}', N'{direccion}', N'{telefono}', SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaEmpresas] RESTART WITH 11;
GO

INSERT INTO [RolesCentral] ([Id], [Codigo], [Nombre], [Activo], [ModificadoEn], [ModificadoPor])
VALUES (1, N'{ROL_CODIGO}', N'{ROL_NOMBRE}', 1, SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaRolesCentral] RESTART WITH 11;
GO

INSERT INTO [RolesCentralPermisos] ([RolId], [PermisoCodigo])
VALUES
    {permisos};
GO

INSERT INTO [UsuariosCentral] ([Id], [Codigo], [Nombre], [Correo], [RolId], [Activo], [ContrasenaHash],
                               [DebeCambiarContrasena], [ContrasenaCambiadaEn], [IntentosFallidos],
                               [BloqueadoHasta], [UltimoIngresoEn], [ModificadoEn], [ModificadoPor])
VALUES (1, N'{ADMIN_CODIGO}', N'{ADMIN_NOMBRE}', NULL, 1, 1, '{hash_admin}', 1, NULL, 0, NULL, NULL,
        SYSDATETIMEOFFSET(), N'Instalación');
ALTER SEQUENCE [SecuenciaUsuariosCentral] RESTART WITH 11;
GO

/* Parámetros con los que el sistema arranca usable; se cambian en el Central (Organización → Parámetros).
   Los que dependen de la empresa o del ambiente (direcciones de la DGII, tipo de ingresos, textos y políticas)
   quedan sin valor a propósito: el Central avisa cuáles faltan. */
INSERT INTO [Parametros] ([Id], [Clave], [Valor], [Descripcion], [SucursalId], [CajaId], [ModificadoEn], [ModificadoPor])
VALUES
    {parametros};
ALTER SEQUENCE [SecuenciaParametros] RESTART WITH {siguiente_parametros};
GO

{catalogos}

PRINT 'Base del Central creada. Entre al Central con el usuario {ADMIN_CODIGO} y cambie su contraseña.';
GO
"""


def escribir(contenido, nombre):
    os.makedirs(SALIDA, exist_ok=True)
    ruta = os.path.join(SALIDA, nombre)
    with open(ruta, 'w', encoding='utf-8-sig', newline='\r\n') as archivo:
        archivo.write(contenido)
    print('generado:', os.path.relpath(ruta, RAIZ))


def escribir_carga_clientes_dgii(base):
    """Carga el archivo de contribuyentes de la DGII en la tabla de clientes: actualiza el que existe y agrega el que no."""
    escribir(f"""/*
    CG-POS · Cargar el archivo de la DGII (DGII_RNC.TXT) en los clientes de «{base}»

    Toma el archivo que publica la DGII y lo lleva a la tabla Clientes: si el RNC o la cédula ya existe, le actualiza
    la razón social y el estado; si no existe, lo crea. Los clientes bajan solos a las cajas en la siguiente
    sincronización, como cualquier otro maestro. ACTIVO en la DGII es cliente activo; SUSPENDIDO es cliente inactivo.

        1. Descargue el archivo de la DGII y déjelo en una carpeta del SERVIDOR de base de datos.
        2. Cambie la ruta de @archivo, aquí abajo.
        3. sqlcmd -S .\\SQLEXPRESS -E -d {base} -i cargar-clientes-dgii.sql

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

USE [{base}];
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;

/* ------------------------------------------------------------------------
   Ruta del archivo de la DGII, vista desde el servidor de base de datos.
   ------------------------------------------------------------------------ */
DECLARE @archivo nvarchar(4000) = N'C:\\CGPOS\\DGII_RNC.TXT';

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
""", nombre='cargar-clientes-dgii.sql')


temporal = os.path.join(SALIDA, '_esquema.sql')

# ---------------------------------------------------------------- Central
central = esquema('src/Central/CgPos.Central.Infraestructura', 'src/Central/CgPos.Central.Api', temporal)
escribir(encabezado('CgPosCentral', 'CG-POS · Base de datos del Central',
                    'Estructura completa del servidor corporativo y el usuario administrador.', 'estructura_base_datos_central.sql')
         + central + datos_iniciales(), 'estructura_base_datos_central.sql')
escribir_carga_clientes_dgii('CgPosCentral')

# ---------------------------------------------------------------- Caja
pos = esquema('src/POS/CgPos.Pos.Infraestructura', 'src/POS/CgPos.Pos.Agente', temporal)
escribir(encabezado('CgPosCaja', 'CG-POS · Base de datos de la caja',
                    'Estructura completa de una caja. No lleva datos: todo baja del Central en la primera sincronización.',
                    'estructura_base_datos_pos.sql')
         + pos + """

PRINT 'Base de la caja creada. Al abrirla le pedirá su sucursal, su caja, su IP, el servidor y la credencial.';
GO
""", 'estructura_base_datos_pos.sql')

os.remove(temporal)
