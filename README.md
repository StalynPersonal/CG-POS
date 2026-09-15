# CG-POS

Sistema de punto de venta **offline-first** para Contreras Group, con facturación electrónica **e-CF** firmada en cada caja y sincronización con un servidor **Central**.

Se construye por fases: primero la **caja** (fases C0–C11) y luego el **Central** (fases H1–H7).

**Estado actual:** fases C0 (fundaciones), C1 (configuración y seguridad local), C2 (maestros, precios y padrón DGII), C3 (apertura de turno y pantalla de venta base), C4 (venta avanzada y pantalla del cliente), C5 (descuentos y promociones), C6 (cobro y periféricos), C7 (facturación electrónica offline), C8 (turnos y cierre) y C9 (devoluciones y notas de crédito) completadas.

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

## Parámetros de negocio

Las reglas de negocio no tienen valores fijos en el código: las configura un usuario en el Central y llegan a la caja como parámetros (con precedencia caja → sucursal → general). Si falta uno obligatorio, la operación se rechaza con el mensaje «Falta configurar el parámetro…». En desarrollo están en `datos/carga-inicial.desarrollo.json`.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Seguridad.IntentosMaximosPin`, `Seguridad.MinutosBloqueo` | Bloqueo por PIN incorrecto | Sí |
| `Seguridad.MinutosVigenciaAutorizacion` | Tiempo para usar una autorización de supervisor | Sí |
| `Seguridad.HorasSesion` | Duración de la sesión del usuario | Sí |
| `Fiscal.MontoIdentificacionConsumo` | Total desde el cual la factura de consumo exige cédula o RNC | Sí |
| `Fiscal.PorcentajeAlertaSecuenciaEcf`, `Fiscal.DiasAlertaCertificado` | Alertas de secuencias y certificado (si faltan, la barra de estado lo indica) | Sí |
| `Fiscal.TipoIngresos` | Tipo de ingresos de los e-CF (tabla DGII, 1 a 6) | Sí |
| `Caja.PasoRedondeoEfectivo` | Redondeo del cobro en efectivo (`0` = sin redondeo) | Sí |
| `Caja.CierreCiego`, `Caja.FondoEnCuadre` | Modalidad del cierre de turno | Sí |
| `Caja.FondoPredeterminado` | Fondo sugerido al abrir turno | No |
| `Devoluciones.DiasRetencionImpuesto`, `Devoluciones.MesesVigenciaNotaCredito` | Retención del ITBIS y vigencia de la nota de crédito | Sí |
| `Devoluciones.PoliticaNotaCredito`, `Devoluciones.PoliticaNotaCreditoContabilidad` | Textos impresos en la nota de crédito | No |
| `General.MonedaLocal` | Código ISO de la moneda local (debe existir activa en el maestro `monedas`, con su nombre y símbolo). Cobro, cierre, tickets y pantallas la usan; las demás monedas se cobran a la tasa del día. El e-CF solo se emite con moneda local DOP | Sí |
| `Fidelidad.ValorPunto` | Valor en moneda local de cada punto al canjearlo (sin él no se canjean puntos) | Para canjear |
| `Fidelidad.MesesVigenciaPuntos` | Vigencia de los puntos que acumula la caja (sin él no les pone vencimiento) | No |
| `Fidelidad.MinimoPuntosCanje`, `Fidelidad.MaximoPuntosCanjeSinConexion` | Mínimo por canje y tope por transacción mientras la caja no confirma el saldo con el Central | No |
| `Entregas.PoliticaPendiente` | Texto impreso en el voucher de pendiente de entrega o envío | No |
| `Tickets.MensajePie` | Mensaje al pie del ticket | No |
| `Pantallas.MensajeBienvenida`, `Pantallas.MensajeDespedida` | Mensajes de la pantalla del cliente | No |
| `Pantallas.SegundosPorImagen` | Rotación de la publicidad (sin él no rota) | No |
| `Balanza.PrefijoPeso`, `Balanza.PrefijoPrecio`, `Balanza.DigitosCodigoArticulo`, `Balanza.DigitosValor`, `Balanza.DecimalesPeso`, `Balanza.DecimalesPrecio` | Etiquetas de balanza; sin prefijos la caja no las interpreta | Si hay prefijos |

Otras reglas que dependen de la configuración: sin **topes de descuento** en los maestros no se permite el descuento manual; el **ambiente e-CF** (`Ecf:Ambiente` en la configuración del Agente) es obligatorio para emitir; la **dirección** de la empresa o sucursal es obligatoria en el e-CF, las **tasas de ITBIS** del XML salen del maestro de impuestos y el indicador de **bien o servicio** sale del artículo (`esServicio` en los maestros). El nombre y las iniciales de la empresa en la pantalla de venta salen de la configuración de la caja. Las fechas del ticket, del e-CF y los plazos de devolución usan la **zona horaria configurada en el equipo** de la caja (en República Dominicana, UTC-4).

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

### Venta avanzada

- **Cliente y comprobante (F12 o tocar el encabezado):** RNC o cédula contra el padrón DGII y los clientes registrados. Si no está en ninguno se pide el nombre. El comprobante toma el habitual del cliente (E31/E32/E44/E45); cambiarlo a mano requiere permiso. E31 y E44 exigen RNC o cédula; E45, RNC.
- **Identificación obligatoria:** una factura de consumo desde `Fiscal.MontoIdentificacionConsumo` (RD$250,000 por defecto) muestra el aviso hasta asignar cédula o RNC.
- **Límite de compra (F3):** avisa cuando el total supera el monto que pidió el cliente.
- **Facturas en espera (F7):** quedan ligadas al cajero y al turno; al retomar una, la actual pasa a espera.
- **Anular** (con motivo) y **Suspender** (bloquea la pantalla hasta digitar el PIN) están en la segunda página de teclas y requieren permiso o autorización de supervisor.
- **Serializados:** al escanearlos se pide el serial; no se repite en la misma venta.
- **Balanza (F5):** un pesado sin etiqueta toma el peso estable de la balanza menos la tara del artículo. En desarrollo la balanza está simulada (`Perifericos:BalanzaSimulada:Peso`).
- **Catálogo en mosaicos:** botón junto al campo de escaneo; muestra los artículos con `mostrarEnCatalogo`.

### Descuentos y ofertas

- **Ofertas** (`promociones` en los maestros): porcentaje, monto por unidad, precio especial, lleva X paga Y y precio desde una cantidad. Se limitan por artículos o familias, sucursales, fechas, días de la semana, horas y unidades por cliente. Se recalculan en cada operación sumando todas las líneas del mismo artículo, y si aplican varias gana la más favorable para el cliente. Una oferta solo reemplaza el precio por mayor si deja mejor precio.
- **Columna Promo:** muestra la oferta (ej. `2x1`, `-15%`). Al tocarla se ve el detalle, las ofertas vigentes y la opción de **desactivarla**, que requiere permiso.
- **Descuento a la línea** (tocar el precio) y **a la factura** (segunda página): por porcentaje o monto, con motivo de la lista `motivosDescuento`. Requiere permiso o clave de supervisor. No aplica a artículos en oferta ni a familias sin descuento manual (panadería, vegetales). El de factura puede limitarse a líneas elegidas y se prorratea al centavo.
- **Topes** (`topesDescuento`): por nivel de usuario, general, por familia o por artículo. Si el descuento supera el tope de quien autoriza, se pide la clave de un nivel superior. En desarrollo: supervisor (S001) hasta 10 % o RD$2,000; gerente (G001) hasta 30 % o RD$20,000.
- Cada línea guarda la oferta aplicada y cada descuento queda auditado con motivo y autorizador.

### Cobro y periféricos

- **F8 Totalizar** abre el cobro: formas de pago del maestro en botones, monto con teclado y billetes rápidos, y en todo momento lo pagado, lo que falta o la devuelta. Admite pago mixto.
- **Efectivo:** es el único medio que da devuelta. Si el parámetro `Caja.PasoRedondeoEfectivo` es mayor que 0, el total se redondea (ej. `1` = al peso).
- **Dólares:** a la tasa del día (`tasasCambio` en los maestros); la devuelta es en pesos.
- **Tarjeta:** "Pasar tarjeta" envía el monto al terminal de pago, simulado en desarrollo con `Perifericos:TerminalSimulado` y `SinConexion: true` para probar la contingencia. Quitar una tarjeta la anula en el terminal. Si la pasarela no responde, se registra la aprobación manual con autorización y queda para conciliar.
- **Transferencia y cheque** piden banco y número; **bonos y tarjetas de regalo** piden serial y no se aceptan con crédito fiscal.
- **Al cobrar:** la venta, sus pagos, el mensaje para el Central (`Venta.Cobrada` en la bandeja de salida) y la auditoría se guardan en una sola transacción. Después se imprime el ticket, se abre la gaveta si hubo un medio físico y empieza otra venta. La pantalla del cliente muestra el pago y la devuelta.
- **Impresora:** `Perifericos:Impresora:Tipo` = `Archivo` deja el ticket en texto y ESC/POS en `Carpeta` (en desarrollo `src/POS/CgPos.Pos.Agente/logs/impresiones`); `Red` lo envía a `Host`:`Puerto` (9100). Reimprimir y abrir la gaveta sin venta (con permiso) están en la segunda página de teclas.

### Facturación electrónica (e-CF)

- **Certificado:** `Ecf:Certificado:Ruta` apunta al `.p12` de la caja. El PIN se digita en la pantalla (botón e-CF de la barra de estado, o al cobrar) y queda solo en memoria del Agente: al reiniciarlo se vuelve a pedir. En desarrollo, `Ecf:Certificado:PinDesarrollo` crea un certificado autofirmado en `logs/certificado-desarrollo.p12` y lo carga solo.
- **Secuencias:** los rangos por caja y tipo llegan en `secuenciasEcf` de los maestros. Si un tipo está agotado, vencido o sin rango, el cobro se rechaza y la barra de estado lo alerta (`Ecf.PorcentajeAlertaSecuencia`, `Ecf.DiasAlertaCertificado`).
- **Al cobrar:** dentro de la misma transacción se toma el siguiente e-NCF, se genera y valida el XML, se firma, se calcula el código de seguridad y la URL del timbre, y el XML firmado se guarda en `{Ecf:CarpetaXml}\Pendientes\yyyy\MM\dd\{RNC}{eNCF}.xml` (en desarrollo `logs/ecf`; en producción `C:\CGPOS\eCF`). El documento queda "Pendiente por sincronizar" y el XML viaja al Central en `Venta.Cobrada`. Si algo falla, no se consume la secuencia.
- **Ticket:** e-NCF, vencimiento de la secuencia, código de seguridad, fecha de firma y QR del timbre (ESC/POS nativo).
- **API:** `GET /api/ecf/estado`, `POST /api/ecf/certificado`, `GET /api/ecf/documentos?estado=`.
- **Por confirmar con la DGII:** esquema XML definitivo (colocar los XSD en `Ecf:CarpetaXsd`), código de seguridad y URL del timbre.

### Turno y cierre

- **Relevo:** si la caja tiene abierto el turno de otro cajero, la apertura ofrece *Relevar turno*; con autorización de supervisor el nuevo usuario toma el turno sin cerrarlo ni cuadrar.
- **Retiro** (segunda página de teclas): monto y motivo, autorización de supervisor, comprobante impreso con firmas y apertura de gaveta. No se puede retirar más del efectivo en la gaveta.
- **Cierre de turno:** declaración por forma de pago y conteo del efectivo por denominaciones. Con `Caja.CierreCiego` (por defecto `true`) el cajero no ve lo esperado; *Pre-cierre* imprime lo esperado con clave de supervisor.
- **Lo esperado:** el efectivo cuenta lo recibido menos la devuelta y los retiros; el fondo solo entra si `Caja.FondoEnCuadre` es `true` (por defecto no se mezcla). Moneda extranjera se cuadra en su moneda; los demás medios, por lo aplicado a las facturas.
- **No se cierra** con facturas en espera, transacciones en curso con artículos o ventas sin e-CF firmado; la pantalla lista qué falta. Las transacciones vacías se descartan al cerrar.
- **Al cerrar:** se imprime el reporte (esperado, declarado, diferencia por forma de pago, denominaciones, retiros y relevos) y el cierre queda en la bandeja de salida (`Caja.TurnoCerrado`), igual que retiros, relevos y reaperturas.
- **Reapertura:** desde la apertura, *Reabrir el último cierre* con motivo y autorización de nivel superior (en los datos de desarrollo, el gerente G001). El cierre queda como *Reabierto* y el turno vuelve a su cajero.
- **API:** `GET /api/caja/turno/resumen`, `POST /api/caja/turno/{precierre|retiros|relevo|cierre}`, `GET /api/caja/cierres`, `POST /api/caja/cierres/{id}/{reabrir|reimprimir}`.

### Devoluciones y notas de crédito

- **Pantalla `/devoluciones`:** F10 desde la venta (la venta en curso queda guardada) o una estación dedicada en un tercer monitor. Se escanea el código de barras del ticket (número de transacción) o se digita el e-NCF.
- **Devolución parcial o total:** por línea se ve lo vendido, lo ya devuelto y lo disponible; no se puede devolver más de lo vendido. Los serializados piden el serial vendido. Si la factura no tiene cliente se pide cédula o RNC (el nombre sale del padrón DGII o se digita).
- **Motivo y autorización:** motivo seleccionable (`motivosDevolucion` en los maestros) y clave del encargado (permiso `Devoluciones.Autorizar`), que sale impreso en la nota.
- **Plazo:** pasados `Devoluciones.DiasRetencionImpuesto` días (30 por defecto) se retiene el ITBIS y la nota acredita solo la base.
- **Nota de crédito E34:** se firma en la caja en la misma transacción, con referencia al e-CF de la factura (código 1 si completa la factura, 3 si es parcial), y viaja al Central en `Devolucion.NotaCreditoEmitida`. Se imprimen la copia del cliente (código de barras y política `Devoluciones.PoliticaNotaCredito`) y la de contabilidad.
- **Consumo:** en el cobro, la forma de pago *Nota de crédito* pide el e-NCF; valida que exista en la caja, esté vigente (`Devoluciones.MesesVigenciaNotaCredito`, 6 por defecto) y tenga saldo. Si queda saldo se imprime un voucher. Cada consumo va al Central (`NotaCredito.Consumida`).
- **Otra sucursal:** las facturas y notas de crédito que no existen en la caja se informan como tales; se validarán con el Central en la Etapa 2.
- **API:** `GET /api/devoluciones/factura/{numero}`, `POST /api/devoluciones`, `GET /api/devoluciones/notas-credito/{codigo}`, `POST /api/devoluciones/{id}/reimprimir`, `GET /api/devoluciones/motivos`.

### Pantalla del cliente y monitores

- `http://localhost:5180/cliente` muestra artículos, totales y un carrusel de publicidad en tiempo real (SignalR), sin iniciar sesión. Sin venta en curso muestra la bienvenida.
- Las imágenes (png, jpg, webp o svg) se toman de `Pantallas:CarpetaPublicidad` (`C:\CGPOS\Publicidad` en producción; `datos/publicidad` en desarrollo). El intervalo se configura con `Pantallas:SegundosPorImagen` y el texto con `Pantallas:MensajeBienvenida`.
- `scripts/caja/abrir-pantallas.ps1 -MonitorCajero 1 -MonitorCliente 2 [-MonitorDevoluciones 3]` abre cada pantalla en modo kiosco en su monitor (numerados de izquierda a derecha).

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
