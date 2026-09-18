# -*- coding: utf-8 -*-
"""
Genera los scripts de estructura de las bases de datos a partir del modelo de EF Core.

    python scripts/base-datos/generar-estructura-sql.py

Deja dos archivos, uno por base:
    scripts/base-datos/central/structura_base_datos.sql
    scripts/base-datos/pos/structura_base_datos.sql

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

# Usuario administrador con el que se entra la primera vez al Central. La contraseña se cambia al ingresar.
ADMIN_CODIGO = 'ADMIN'
ADMIN_NOMBRE = 'Administrador del sistema'
ADMIN_CONTRASENA = 'Admin.CGPOS#2026'
ROL_CODIGO = 'ADMINISTRADOR'
ROL_NOMBRE = 'Administrador'

# Parámetros mínimos para poder entrar al Central; el resto se configura desde la propia pantalla de parámetros.
PARAMETROS_INICIALES = [
    ('Central.Seguridad.IntentosMaximos', '5', 'Intentos de contraseña fallidos que bloquean al usuario'),
    ('Central.Seguridad.MinutosBloqueo', '15', 'Minutos que dura el bloqueo por intentos fallidos'),
    ('Central.Seguridad.MinutosToken', '15', 'Minutos de vigencia del token de acceso'),
    ('Central.Seguridad.MinutosInactividad', '60', 'Minutos sin actividad tras los que vence la sesión'),
    ('Central.Seguridad.HorasSesion', '12', 'Horas máximas de una sesión'),
    ('Central.Seguridad.LargoMinimoContrasena', '10', 'Largo mínimo de las contraseñas del Central'),
    ('Central.Seguridad.ContrasenaCompleja', 'true', 'Exige mayúscula, minúscula, número y símbolo'),
    ('Central.Dispositivos.MinutosToken', '30', 'Minutos de vigencia del token de una caja'),
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


def encabezado(base, titulo, descripcion):
    return f"""/*
    {titulo}
    {descripcion}

    Cómo usarlo:
        sqlcmd -S .\\SQLEXPRESS -E -i structura_base_datos.sql
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


def datos_iniciales():
    """Único dato del Central: el rol administrador con todos los permisos y el usuario que lo tiene."""
    hash_admin = hash_contrasena(ADMIN_CONTRASENA)
    permisos = ',\n    '.join(f"(1, N'{permiso}')" for permiso in permisos_central())
    parametros = ',\n    '.join(
        f"({indice + 3}, N'{clave}', N'{valor}', N'{descripcion}', NULL, NULL)"
        for indice, (clave, valor, descripcion) in enumerate(PARAMETROS_INICIALES))

    return f"""

/* ------------------------------------------------------------------------
   Datos iniciales: solo el administrador del sistema.
   Todo lo demás (empresa, sucursales, cajas, maestros, artículos, precios,
   promociones, usuarios de caja y los demás parámetros) se crea desde el
   Central entrando con este usuario.

        Usuario:    {ADMIN_CODIGO}
        Contraseña: {ADMIN_CONTRASENA}

   El sistema exige cambiarla en el primer ingreso.
   ------------------------------------------------------------------------ */

INSERT INTO [RolesCentral] ([Id], [Codigo], [Nombre], [Activo])
VALUES (1, N'{ROL_CODIGO}', N'{ROL_NOMBRE}', 1);
GO

INSERT INTO [RolesCentralPermisos] ([RolId], [PermisoCodigo])
VALUES
    {permisos};
GO

INSERT INTO [UsuariosCentral] ([Id], [Codigo], [Nombre], [Correo], [RolId], [Activo], [ContrasenaHash],
                               [DebeCambiarContrasena], [ContrasenaCambiadaEn], [IntentosFallidos],
                               [BloqueadoHasta], [UltimoIngresoEn])
VALUES (2, N'{ADMIN_CODIGO}', N'{ADMIN_NOMBRE}', NULL, 1, 1, '{hash_admin}', 1, NULL, 0, NULL, NULL);
GO

/* Parámetros mínimos para poder entrar; los demás se configuran en el Central (Organización → Parámetros). */
INSERT INTO [Parametros] ([Id], [Clave], [Valor], [Descripcion], [SucursalId], [CajaId])
VALUES
    {parametros};
GO

/* Los Id de arriba se pusieron a mano: la secuencia arranca después, para que no se repitan. */
ALTER SEQUENCE [EntityFrameworkHiLoSequence] RESTART WITH 101;
GO

PRINT 'Base del Central creada. Entre al Central con el usuario {ADMIN_CODIGO} y cambie su contraseña.';
GO
"""


def escribir(carpeta, contenido):
    destino = os.path.join(SALIDA, carpeta)
    os.makedirs(destino, exist_ok=True)
    ruta = os.path.join(destino, 'structura_base_datos.sql')
    with open(ruta, 'w', encoding='utf-8-sig', newline='\r\n') as archivo:
        archivo.write(contenido)
    print('generado:', os.path.relpath(ruta, RAIZ))


temporal = os.path.join(SALIDA, '_esquema.sql')

# ---------------------------------------------------------------- Central
central = esquema('src/Central/CgPos.Central.Infraestructura', 'src/Central/CgPos.Central.Api', temporal)
escribir('central', encabezado('CgPosCentral', 'CG-POS · Base de datos del Central',
                               'Estructura completa del servidor corporativo y el usuario administrador.')
         + central + datos_iniciales())

# ---------------------------------------------------------------- Caja
pos = esquema('src/POS/CgPos.Pos.Infraestructura', 'src/POS/CgPos.Pos.Agente', temporal)
escribir('pos', encabezado('CgPosCaja', 'CG-POS · Base de datos de la caja',
                           'Estructura completa de una caja. No lleva datos: todo baja del Central en la primera sincronización.')
         + pos + """

PRINT 'Base de la caja creada. Configure la caja con su sucursal, su número y el secreto que emitió el Central.';
GO
""")

os.remove(temporal)
