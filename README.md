# CG-POS

Sistema de punto de venta **offline-first** para Contreras Group, con facturación electrónica **e-CF** firmada en cada caja y sincronización con un servidor **Central**.

Se construye por fases: primero la **caja** (fases C0–C11) y luego el **Central** (fases H1–H7).

**Estado actual:** fases C0 (fundaciones), C1 (configuración y seguridad local), C2 (maestros, precios y padrón DGII) y C3 (apertura de turno y pantalla de venta base) completadas.

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 (SDK fijado en `global.json`) |
| Caja: servicio local | ASP.NET Core `CgPos.Pos.Agente` (servicio de Windows) |
| Caja: pantallas | Blazor WebAssembly + MudBlazor (servido localmente, sin CDN) |
| Caja: base de datos | SQL Server Express 2019+ con EF Core |
| Facturación electrónica | XMLDSig (RSA-SHA256) con `System.Security.Cryptography.Xml` |
| Logs | Serilog |
| Pruebas | xUnit |

## Estructura

```
src/Compartido/   CgPos.Dominio        Entidades y reglas comunes (formato RD, auditoría)
              CgPos.Contratos     Datos, estados y formato JSON compartidos caja ↔ Central
              CgPos.ECF           Firma y verificación de e-CF
              CgPos.Interfaz        Tema verde institucional y componentes táctiles (MudBlazor)
src/POS/      CgPos.Pos.Agente     Único servicio de la caja: API local, pantallas y sincronización
              CgPos.Pos.Aplicacion  Casos de uso y abstracciones (BandejaSalida, auditoría)
              CgPos.Pos.Infraestructura  EF Core / SQL Server, migraciones, implementaciones
              CgPos.Pos.Web       Pantallas Blazor WebAssembly (cajero, cliente, devoluciones)
pruebas/        CgPos.Dominio.Pruebas · CgPos.ECF.Pruebas · CgPos.Pos.Pruebas
scripts/caja/ Instalación del Agente como servicio de Windows
datos/        Carga inicial de desarrollo (empresa, cajas, roles y usuarios ficticios)
```

## Requisitos de desarrollo

- .NET SDK 10.0.401 o superior dentro de la serie 10.
- SQL Server 2019+ (Developer o Express). Con una versión anterior, ajustar el nivel de compatibilidad (ver abajo).
- Herramienta EF Core: `dotnet tool install -g dotnet-ef`.

## Configuración local

Las credenciales **no** van en el repositorio. Se configuran con *user-secrets* (se guardan en el perfil del usuario).

```powershell
# Conexión de la base local de la caja
dotnet user-secrets set "ConnectionStrings:BaseDatosPos" "Server=localhost;Database=CgPosCaja;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True" --project src/POS/CgPos.Pos.Agente

# Solo si el SQL Server es anterior a 2019 (ej. 2014 = 120)
dotnet user-secrets set "BaseDatos:NivelCompatibilidad" "120" --project src/POS/CgPos.Pos.Agente
```

En producción, `appsettings.json` usa `.\SQLEXPRESS` con autenticación de Windows.

## Ejecutar la caja

```powershell
dotnet build CgPos.slnx
dotnet run --project src/POS/CgPos.Pos.Agente
```

- Pantallas: <http://localhost:5180>
- Salud del servicio y la base: <http://localhost:5180/salud>
- Logs de desarrollo: `src/POS/CgPos.Pos.Agente/logs/`

En desarrollo, el Agente aplica al arrancar `datos/carga-inicial.desarrollo.json` y opera como la Caja 01. Usuarios de prueba:

| Usuario | PIN | Carné | Rol |
|---|---|---|---|
| C001 | 1111 | CGP-C001 | Cajero (nivel 1) |
| S001 | 2222 | CGP-S001 | Supervisor (nivel 2, autoriza operaciones) |
| G001 | 3333 | — | Gerente (nivel 3, todos los permisos) |

La huella está simulada: identifica siempre al usuario de `Perifericos:HuellaSimulada:CodigoUsuario` (C001 en desarrollo).

### Maestros y padrón DGII

En desarrollo también se aplican al arrancar:

- `datos/maestros.desarrollo.json`: familias, unidades, impuestos (ITBIS 18 %, 16 %, 0 % y exento), artículos de ferretería y construcción ficticios, clientes, formas de pago, bancos, tipos de tarjeta y denominaciones.
- `datos/padron-dgii.desarrollo.txt`: padrón de ejemplo con el formato de la DGII (`RNC|razón social|nombre comercial|…|ESTADO|RÉGIMEN`).

Ejemplos para probar la API (todas requieren sesión):

| Ruta | Qué devuelve |
|---|---|
| `GET /api/articulos/codigo/7891114119695` | Artículo por código de barras, proveedor, interno o etiqueta de balanza |
| `GET /api/articulos?texto=cemento gris` | Búsqueda por palabras |
| `GET /api/articulos/no-codificados` | Vegetales y especias en orden alfabético |
| `GET /api/documentos/131-24679-6` | RNC/cédula: validez, padrón DGII y cliente registrado |
| `GET /api/catalogos/cobro` | Formas de pago, bancos, tipos de tarjeta y denominaciones |

Importaciones manuales (permiso `Seguridad.AdministrarConfiguracion`, usuario G001 en desarrollo):

- `POST /api/maestros/articulos/csv`: CSV con `;` o `,`. Columnas obligatorias `codigo`, `descripcion`, `familia`, `unidad`, `impuesto`, `precio_detalle`. Opcionales: `precio_mayor`, `cantidad_minima_mayor`, `precio_minimo`, `costo`, `tipo`, `referencia`, `codigos_barras` y `codigos_proveedor` (separados por `|`), `ruta_imagen`, `mostrar_en_catalogo` y `activo`. Los números usan punto decimal; las líneas con errores se informan y se omiten.
- `POST /api/maestros/padron-dgii`: el archivo del padrón completo; inserta los nuevos y actualiza los que cambiaron.

Etiquetas de balanza: por defecto EAN-13 con prefijo `21` (peso, 3 decimales) o `22` (precio, 2 decimales), 5 dígitos de artículo y 5 de valor. Se ajusta con los parámetros `Balanza.*`.

### Turno y venta

Al ingresar, si la caja no tiene turno abierto se pide el fondo (sugerido por el parámetro `Caja.FondoPredeterminado`). Solo puede haber un turno abierto por caja.

En la pantalla de venta:

- **Escanear o digitar** el código y Enter. Acepta `cantidad*código` (ej. `12*CEM-425`, que activa el precio mayor).
- **Tocar la cantidad** de una línea (o F4) para cambiarla; **tocar el código** alterna entre el código leído y el interno.
- **F2** busca por descripción, **F11** consulta el precio sin vender.
- **Eliminar línea** (botón ✕ de la línea), **eliminar por escaneo** y **limpiar pantalla** requieren permiso; al cajero se le pide autorización de supervisor (S001/2222). La autorización es de un solo uso y vence en 5 minutos. La línea eliminada queda tachada y su reverso en rojo debajo.
- La venta se guarda en cada operación: si la caja se apaga, al volver a ingresar se recupera tal como estaba.

| Ruta | Uso |
|---|---|
| `GET /api/turnos/actual` · `POST /api/turnos` | Estado y apertura del turno |
| `GET /api/ventas/actual` | Venta en curso del usuario (la crea si no hay) |
| `POST /api/ventas/{id}/lineas` | Agregar artículo (`codigo`, `cantidad` opcional) |
| `PUT /api/ventas/{id}/lineas/{n}/cantidad` | Cambiar cantidad |
| `POST /api/ventas/{id}/lineas/{n}/eliminar` · `/eliminar-por-codigo` · `/limpiar` | Con `autorizacionId` si hace falta |
| `GET /api/sincronizacion/estado` | Indicador de conexión y documentos pendientes |

Los rechazos de negocio responden 422 (409 si ya hay turno abierto) con `resultado`, `mensaje` y la venta actual.

Las migraciones se aplican solas al arrancar. Para aplicarlas manualmente:

```powershell
dotnet ef database update --project src/POS/CgPos.Pos.Infraestructura --startup-project src/POS/CgPos.Pos.Agente
```

## Pruebas

```powershell
dotnet test CgPos.slnx
```

Las pruebas de integración crean una base temporal `CgPosPruebas_<guid>` y la eliminan al terminar. Necesitan un servidor configurado; si no lo hay, se omiten.

```powershell
dotnet user-secrets set "ConnectionStrings:ServidorPruebas" "Server=localhost;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True" --project pruebas/CgPos.Pos.Pruebas
```

En CI se pueden usar variables de entorno con prefijo `CGPOS_`, por ejemplo `CGPOS_ConnectionStrings__ServidorPruebas`.

## Publicar e instalar en una caja

```powershell
dotnet publish src/POS/CgPos.Pos.Agente -c Release -o C:\CGPOS\Agente
# En la caja, como administrador:
.\scripts\caja\instalar-agente.ps1 -RutaEjecutable "C:\CGPOS\Agente\CgPos.Pos.Agente.exe"
```

El servicio `CgPosAgente` depende de SQL Server Express y se reinicia automáticamente ante fallos. Logs: `C:\CGPOS\Logs`.

## Convenciones

- Todo en español: proyectos, namespaces, clases, métodos, variables, parámetros y mensajes. Solo se mantienen siglas técnicas (Id, Api, Json, Pin, Hash, Token, Jwt, Xml, e-CF) y los nombres que exige .NET.
- El servidor corporativo se llama **Central**.
- Las transacciones usan Id GUID v7.
- Todo lo que va al Central se escribe en la bandeja de salida dentro de la misma transacción del documento.
- Seguridad: permisos granulares definidos en `CatalogoPermisos`; cada permiso es una política de autorización con el mismo nombre.
- Formato RD fijo: `RD$2,175.34` y `dd/MM/yyyy`.
