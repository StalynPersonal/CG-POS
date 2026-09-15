# CG-POS

Sistema de punto de venta **offline-first** para Contreras Group, con facturación electrónica **e-CF** firmada en cada caja y sincronización con un servidor **Central**.

Se construye por fases: primero la **caja** (fases C0–C11) y luego el **Central** (fases H1–H7).

**Estado actual:** la caja está completa (C0 a C11: fundaciones, seguridad local, maestros, venta, descuentos, cobro, e-CF offline, turnos, devoluciones, fidelidad, pendientes de entrega y sincronización). Del Central están hechas las fases H1 (fundaciones y seguridad) y H2 (sincronización con las cajas); la H3 (Central Manager) avanza por módulos.

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 (SDK fijado en `global.json`) |
| Caja: servicio local | ASP.NET Core `CgPos.Pos.Agente` (servicio de Windows) |
| Caja: pantallas | Blazor WebAssembly + MudBlazor (servido localmente, sin CDN) |
| Caja: base de datos | SQL Server Express 2019+ con EF Core |
| Central: servicio | ASP.NET Core `CgPos.Central.Api` (HTTPS con TLS 1.2+, JWT con renovación rotativa) |
| Central: base de datos | SQL Server Standard 2019+ con EF Core |
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
src/Central/  CgPos.Central.Api    Servicio del Central: API para el Central Manager y para las cajas
              CgPos.Central.Aplicacion  Casos de uso y abstracciones del Central
              CgPos.Central.Infraestructura  EF Core / SQL Server, migraciones, implementaciones
              CgPos.Central.Web    Central Manager en Blazor WebAssembly + MudBlazor
pruebas/        CgPos.Dominio.Pruebas · CgPos.ECF.Pruebas · CgPos.Pos.Pruebas · CgPos.Central.Pruebas
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

# Central: base de datos y clave de firma de tokens (Base64 de 32 bytes o más)
dotnet user-secrets set "ConnectionStrings:BaseDatosCentral" "Server=localhost;Database=CgPosCentral;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True" --project src/Central/CgPos.Central.Api
dotnet user-secrets set "Seguridad:ClaveFirmaJwt" "<clave-base64>" --project src/Central/CgPos.Central.Api
```

En desarrollo, sin `Seguridad:ClaveFirmaJwt` el Central usa una clave temporal (las sesiones no sobreviven a un reinicio); en producción no arranca sin ella.

En producción, `appsettings.json` usa `.\SQLEXPRESS` con autenticación de Windows.

## Parámetros de negocio

Las reglas de negocio no tienen valores fijos en el código: las configura un usuario en el Central y llegan a la caja como parámetros (con precedencia caja → sucursal → general). El catálogo `CatalogoParametros` (en `CgPos.Dominio`) define cada clave con su tipo, rango y si es obligatoria; el Central Manager solo admite claves del catálogo y valores válidos, y las pruebas verifican que toda clave que leen la caja y el Central esté en él. Si falta uno obligatorio, la operación se rechaza con el mensaje «Falta configurar el parámetro…». En desarrollo están en `datos/carga-inicial.desarrollo.json`.

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
| `Sincronizacion.DiasRetencionXmlEnviados`, `Sincronizacion.DiasRetencionMensajesConfirmados`, `Respaldo.DiasRetencion` | Retención de lo ya confirmado por el Central y de los respaldos (sin ellos no se purga) | No |
| `Sincronizacion.AlertaTamanoBaseDatosMb`, `Sincronizacion.HorasAlertaPendientes`, `Reloj.ToleranciaSegundos` | Umbrales de alerta: tamaño de la base, documentos sin sincronizar y desfase del reloj | No |
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
- **Consumo:** en el cobro, la forma de pago *Nota de crédito* pide el e-NCF; valida que exista en la caja, esté vigente (`Devoluciones.MesesVigenciaNotaCredito`) y tenga saldo. Si queda saldo se imprime un voucher. Cada consumo va al Central (`NotaCredito.Consumida`).
- **Otra sucursal:** las facturas y notas de crédito que no existen en la caja se informan como tales; se validarán con el Central en la Etapa 2.
- **API:** `GET /api/devoluciones/factura/{numero}`, `POST /api/devoluciones`, `GET /api/devoluciones/notas-credito/{codigo}`, `POST /api/devoluciones/{id}/reimprimir`, `GET /api/devoluciones/motivos`.

### Programa de fidelidad

- **Configuración (Central):** `nivelesFidelidad` (factor de acumulación), `reglasAcumulacion` (puntos por cada monto en todo, una familia, un artículo, un día o una promoción; cada línea toma la más favorable) y `miembrosFidelidad` (cédula, nivel y saldo sincronizado con puntos por vencer). Parámetros `Fidelidad.*`.
- **En caja (F12):** la cédula es el ID/PIN del miembro; si no está inscrita se inscribe en el mismo paso. El miembro habilita las ofertas exclusivas de fidelidad.
- **Al cobrar:** acumula sobre lo no pagado con puntos; el canje es la forma de pago *Puntos* (valor del punto, saldo, mínimo y tope sin conexión) con permiso `Fidelidad.CanjearPuntos`. La nota de crédito reversa en proporción lo acumulado.
- **Saldo:** último saldo del Central (sin lo vencido) más los movimientos de la caja posteriores; cada inscripción y movimiento va al Central.
- **API:** `GET /api/fidelidad/miembros/{cedula}`, `POST /api/fidelidad/miembros`, `POST|DELETE /api/ventas/{id}/fidelidad`.

### Pendientes de entrega, envíos y despacho

- **Marcar (tecla «Entrega / envío»):** líneas completas o en parte para retiro en un almacén (`almacenes` en los maestros) o envío a dirección, con fecha comprometida; varios destinos por factura. Requiere `Pendientes.Marcar` o clave de supervisor. Los serializados pueden registrarse sin serial si se entregan después.
- **Al cobrar:** un pendiente por destino (`PE-sucursal-caja-secuencia`) con voucher de código de barras, copia del cliente y del despacho y la política `Entregas.PoliticaPendiente`. Lo pendiente no se devuelve hasta anular el pendiente.
- **Pantalla `/despacho`:** se escanea el voucher o la factura; preparación, entrega total o parcial con quien recibe y serial, constancia impresa, y anulación con motivo y `Pendientes.Anular`. Opera con `Pendientes.Despachar`.
- **Pendiente para el Central (Etapa 2):** pendientes de otras cajas o sucursales, notificación por correo al cliente, documento de entrega o traslado en SAP B1 y reportes.
- **API:** `POST /api/ventas/{id}/entregas`, `DELETE /api/ventas/{id}/entregas/{numero}`, `GET /api/entregas/almacenes`, `GET /api/despacho/pendientes/abiertos`, `GET /api/despacho/pendientes/buscar/{codigo}`, `POST /api/despacho/pendientes/{id}/estado|entregas|anular`.

### Sincronización con el Central y mantenimiento

- **Bandeja de salida:** todo documento (venta, nota de crédito, cierre, pendiente, movimiento de puntos…) se guarda con su mensaje en la misma transacción. Un servicio en segundo plano del Agente lo envía cada `Sincronizacion:IntervaloSegundos` en orden de creación, con el Id del mensaje como clave de idempotencia y el SHA-256 del contenido. Sin comunicación detiene el lote y reintenta con espera progresiva (`EsperaInicialSegundos` duplicada hasta `EsperaMaximaSegundos`); la caja sigue operando.
- **Central:** `Central:Url` para el Central real (`POST api/sincronizacion/mensajes`), con `Caja:Id` y `Central:Secreto` (la credencial que el Central emite para la caja; la caja la cambia por un token de dispositivo), o `Central:Modo = Simulado`, que guarda los mensajes en `Central:CarpetaSimulada` con la misma idempotencia y validación de hash. Sin ninguno, la barra muestra «Sin Central».
- **Maestros del Central:** cada `Sincronizacion:IntervaloMaestrosSegundos` (y al arrancar) la caja pide lo cambiado desde la versión que ya aplicó y lo aplica con las mismas cargas de organización y maestros; la marca solo avanza si todo se aplicó. Una caja nueva con `Central:Url`, `Caja:Id` y `Central:Secreto` se aprovisiona sola en el primer ciclo. Un miembro de fidelidad que llega del Central con una cédula ya inscrita en la caja actualiza el registro local.
- **XML de e-CF:** al confirmarse la venta o la nota de crédito, el e-CF queda *Sincronizado* y su XML pasa de `Pendientes` a `Enviados`; nada sale de `Pendientes` sin confirmación del Central.
- **Mantenimiento** (cada `Mantenimiento:IntervaloMinutos`): verificación de la hora contra `Reloj:ServidorNtp`; respaldo diario de la base desde `Respaldo:Hora` en `Respaldo:Carpeta` (vacía = carpeta de respaldos de la instancia; la cuenta del servicio de SQL Server debe poder escribir en ella); purga de XML enviados, mensajes confirmados y respaldos según los parámetros de retención.
- **Alertas en la barra de estado:** tamaño de la base, documentos atrasados sin sincronizar, hora desfasada y respaldo fallido.

### Pantalla del cliente y monitores

- `http://localhost:5180/cliente` muestra artículos, totales y un carrusel de publicidad en tiempo real (SignalR), sin iniciar sesión. Sin venta en curso muestra la bienvenida.
- Las imágenes (png, jpg, webp o svg) se toman de `Pantallas:CarpetaPublicidad` (`C:\CGPOS\Publicidad` en producción; `datos/publicidad` en desarrollo). El intervalo se configura con `Pantallas:SegundosPorImagen` y el texto con `Pantallas:MensajeBienvenida`.
- `scripts/caja/abrir-pantallas.ps1 -MonitorCajero 1 -MonitorCliente 2 [-MonitorDevoluciones 3]` abre cada pantalla en modo kiosco en su monitor (numerados de izquierda a derecha).

Las migraciones se aplican solas al arrancar. Para aplicarlas manualmente:

```powershell
dotnet ef database update --project src/POS/CgPos.Pos.Infraestructura --startup-project src/POS/CgPos.Pos.Agente
```

## Ejecutar el Central

```powershell
dotnet run --project src/Central/CgPos.Central.Api
```

- API: <http://localhost:5280> en desarrollo (`Central:ExigirHttps = false`). En producción escucha en `https://*:7280` y rechaza las API por HTTP; el certificado del servidor se configura en `Kestrel:Certificates:Default`.
- Salud del servicio y la base: `/salud`.
- Al arrancar aplica las migraciones y `CargaInicial:Archivo` (en desarrollo `datos/central.desarrollo.json`: empresa, sucursal, cajas, parámetros, roles y usuarios del Central). En desarrollo también publica para las cajas `CargaInicialCajas:Archivo` (roles, usuarios y parámetros de caja, `datos/carga-inicial.desarrollo.json`) y `Maestros:Archivo` (`datos/maestros.desarrollo.json`). Usuarios de prueba:

| Usuario | Contraseña | Rol |
|---|---|---|
| ADMIN | Admin.Central2026 | Administrador del Central (todos los permisos) |
| AUDITOR | Auditor.Central2026 | Auditor (debe cambiar la contraseña al primer ingreso) |

### Seguridad del Central

- **Usuarios del Central Manager:** usuario y contraseña (PBKDF2-SHA256, 600,000 iteraciones), roles con permisos de `CatalogoPermisosCentral` (distintos de los de la caja) y bloqueo por intentos. Con una contraseña temporal solo se puede cambiar la contraseña.
- **Sesión:** token de acceso corto y token de renovación de un solo uso. Cada renovación entrega un token nuevo; presentar uno ya usado cierra la sesión completa. Cerrar sesión, cambiar la contraseña o desactivar al usuario invalida también el token de acceso vigente. Todo ingreso, rechazo, bloqueo y reutilización queda en auditoría.
- **Cajas:** cada caja tiene una credencial de dispositivo (el secreto se muestra una sola vez al emitirla y el Central guarda su hash) que cambia por un token de dispositivo de pocos minutos. Emitir otra, revocarla o deshabilitar la caja o su sucursal invalida sus tokens de inmediato.
- **Parámetros del Central** (generales, en la tabla de parámetros):

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Seguridad.IntentosMaximos`, `Central.Seguridad.MinutosBloqueo` | Bloqueo por contraseña incorrecta | Sí |
| `Central.Seguridad.MinutosToken` | Vigencia del token de acceso | Sí |
| `Central.Seguridad.MinutosInactividad`, `Central.Seguridad.HorasSesion` | Vencimiento por inactividad y duración máxima de la sesión | Sí |
| `Central.Seguridad.LargoMinimoContrasena` | Largo mínimo de la contraseña | Sí |
| `Central.Seguridad.ContrasenaCompleja` | Exige mayúsculas, minúsculas, números y símbolos, sin contener el usuario | No |
| `Central.Dispositivos.MinutosToken` | Vigencia del token de las cajas | Sí |

- **API:** `POST /api/sesion/ingreso`, `POST /api/sesion/renovar`, `GET /api/sesion/actual`, `POST /api/sesion/cerrar`, `POST /api/sesion/contrasena`; `POST /api/cajas/{id}/credencial` y `POST /api/cajas/{id}/credencial/revocar` (permiso `Central.Dispositivos.Administrar`); `POST /api/dispositivos/token` y `GET /api/dispositivos/actual` para las cajas.

### Central Manager (web)

- <http://localhost:5280> en desarrollo: Blazor WebAssembly con MudBlazor local y el tema verde, servido por el mismo `CgPos.Central.Api` (se desactiva con `Central:ServirManager = false`). Las rutas `/api` desconocidas responden 404.
- **Sesión:** ingreso con usuario y contraseña. El token de acceso vive en memoria y el de renovación en el almacenamiento de la pestaña: recargar no pide la contraseña, la sesión se renueva sola antes de vencer y cerrar la pestaña la termina en ese equipo. Con contraseña temporal solo se puede cambiarla.
- **Menú según los permisos del rol.** *Seguridad:* usuarios (alta con contraseña temporal, edición, restablecer contraseña, desbloquear, activar o desactivar) y roles con sus permisos agrupados por módulo. Ningún cambio puede dejar al Central sin un usuario activo que administre la seguridad y nadie puede desactivarse a sí mismo; restablecer, desactivar o cambiar el rol cierra las sesiones de ese usuario.
- **API** (permiso `Central.Seguridad.Administrar`): `GET /api/seguridad/permisos`; `GET|POST /api/seguridad/roles`, `PUT /api/seguridad/roles/{id}`, `POST /api/seguridad/roles/{id}/activar|desactivar`; `GET|POST /api/seguridad/usuarios`, `PUT /api/seguridad/usuarios/{id}`, `POST /api/seguridad/usuarios/{id}/contrasena|desbloquear|activar|desactivar`.
- *Organización* (permiso `Central.Organizacion.Administrar`): empresa (sin cambiar el RNC), sucursales y cajas (alta, edición, activación y habilitación) y **parámetros**, elegidos del catálogo con su descripción y tipo, validados antes de guardarse y generales, de una sucursal o de una caja (los del Central solo generales). La moneda local debe estar publicada en el maestro de monedas. Los parámetros no se eliminan, porque las cajas no se enterarían del borrado; la pantalla avisa de los obligatorios sin configurar.
- *Credenciales de las cajas* (permiso `Central.Dispositivos.Administrar`): desde Cajas se emite una credencial nueva (reemplaza la anterior y su secreto se muestra una sola vez, con `Caja:Id` y `Central:Secreto` para configurar la caja) o se revoca con motivo.
- **API de organización:** `GET|PUT /api/organizacion/empresa`; `GET|POST /api/organizacion/sucursales`, `PUT /api/organizacion/sucursales/{id}`, `POST /api/organizacion/sucursales/{id}/activar|desactivar`; `GET|POST /api/organizacion/cajas`, `PUT /api/organizacion/cajas/{id}`, `POST /api/organizacion/cajas/{id}/habilitar|deshabilitar`; `GET /api/organizacion/parametros/catalogo`, `GET|POST /api/organizacion/parametros`, `PUT /api/organizacion/parametros/{id}`.
- *Rangos de e-CF* (permiso `Central.Fiscal.Administrar`): se asignan por caja y tipo (E31, E32, E34, E44 y E45) con inicio, fin y vencimiento. Los rangos del mismo tipo no se solapan entre cajas de la empresa; un rango ya asignado no cambia de caja, tipo ni inicio y solo se amplía, se prorroga o se desactiva, porque la caja pudo haber emitido hasta su final. La lista muestra el último e-NCF recibido de cada rango y cuánto queda.
- *Usuarios y roles de caja* (permiso `Central.UsuariosCaja.Administrar`): roles con nivel (1 a 9) y permisos del catálogo de la caja; usuarios con rol, cajas que operan, PIN (4 a 8 dígitos, obligatorio al crear) y carné opcional. El PIN y el carné se publican solo como hash en el formato de la caja; editar sin PIN o carné nuevo conserva los actuales. Los códigos no cambian y un carné no puede repetirse entre usuarios.
- Los rangos, roles y usuarios pasan por las mismas validaciones que la publicación de maestros y bajan a las cajas en su próxima sincronización.
- *Catálogos de maestros* (permiso `Central.Maestros.Administrar`): monedas, tasas de cambio, familias, unidades de medida, impuestos, formas de pago, denominaciones, bancos, tipos de tarjeta, motivos de descuento y de devolución, y almacenes. Cada registro se guarda en el formato de carga de la caja y pasa por las reglas de su dominio y por las referencias ya publicadas (una forma de pago usa una moneda publicada, un almacén una sucursal existente). Lo que la caja no deja cambiar tampoco se deja en el Central: el código de artículos, monedas, promociones, niveles y reglas de fidelidad y almacenes; el tipo de una forma de pago; la moneda, el valor y el tipo de una denominación. No se borran: se desactivan.
- **API de maestros:** `GET /api/maestros/{catálogo}` y `PUT /api/maestros/{catálogo}/{id}` (crea o cambia el registro con ese Id) para `monedas`, `tasas-cambio`, `familias`, `unidades-medida`, `impuestos`, `formas-pago`, `denominaciones`, `bancos`, `tipos-tarjeta`, `motivos-descuento`, `motivos-devolucion` y `almacenes`; `GET /api/maestros/sucursales` como referencia.
- **API de cajas:** `GET|POST /api/fiscal/secuencias`, `PUT /api/fiscal/secuencias/{id}`; `GET /api/usuarios-caja/permisos`; `GET|POST /api/usuarios-caja/roles`, `PUT /api/usuarios-caja/roles/{id}`; `GET|POST /api/usuarios-caja/usuarios`, `PUT /api/usuarios-caja/usuarios/{id}`.

### Recepción de documentos de las cajas

- `POST /api/sincronizacion/mensajes` (token de dispositivo): el Central valida que el mensaje sea de la caja autenticada, el SHA-256 del contenido y el del XML del e-CF, y guarda el documento una sola vez. Un reenvío con el mismo contenido responde *Duplicado* y la caja lo da por confirmado; lo rechazado responde 422 y la caja reintenta más tarde.
- Los e-CF recibidos quedan pendientes de envío a la DGII (fase H4), con el XML firmado y su hash; un e-NCF se registra una sola vez.
- **Conflictos** (el Central es autoridad sobre maestros y configuración; la caja sobre sus transacciones): otro contenido con el mismo Id, hash o XML alterado, mensaje de otra caja o e-NCF repetido quedan registrados una vez por mensaje (con sus repeticiones), en auditoría y en el log. Un e-NCF repetido no rechaza la transacción.

### Maestros para las cajas

- **Publicación:** el Central guarda los maestros en el mismo formato de carga que aplica la caja (artículos, precios, clientes, formas de pago, promociones, fidelidad, almacenes, rangos de e-CF, roles y usuarios de caja). Antes de publicar valida cada registro con las reglas del dominio, las referencias (familia, unidad e impuesto del artículo, moneda, caja, sucursal, nivel) y que los códigos y códigos de barras no se repitan: un maestro inválido no llega a detener a las cajas. Los PIN y carnés se publican solo como hash, con el formato que verifica la caja.
- **Bajada incremental:** `GET /api/sincronizacion/maestros?desde={versión}` (token de dispositivo) entrega lo cambiado por versión de fila (rowversion) hasta la última versión confirmada; publicar lo mismo no genera versión nueva. Desde 0 es el aprovisionamiento completo de una caja nueva. La respuesta se comprime.
- **Alcance por caja:** baja la organización completa, los parámetros generales, los de su sucursal y los suyos (nunca los `Central.*`) y solo sus rangos de e-CF. El estado de cada caja guarda su última descarga y la versión confirmada y entregada.
- **Inscripciones de fidelidad hechas en caja:** se publican como miembros para todas las cajas con el Id de la caja; si la cédula ya estaba en el Central con otro Id se conserva la del Central y queda un conflicto *MiembroDuplicado*.
- **Pendiente:** el padrón DGII se sigue importando en cada caja desde el archivo de la DGII hasta definir su distribución; los parámetros eliminados en el Central no se borran en las cajas.

```powershell
dotnet ef database update --project src/Central/CgPos.Central.Infraestructura --startup-project src/Central/CgPos.Central.Api
```

## Pruebas

```powershell
dotnet test CgPos.slnx
```

Las pruebas de integración crean una base temporal (`CgPosPruebas_<guid>` para la caja, `CgPosCentralPruebas_<guid>` para el Central) y la eliminan al terminar. Necesitan un servidor configurado; si no lo hay, se omiten. `CgPos.Central.Pruebas` usa los mismos user-secrets que `CgPos.Pos.Pruebas`.

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

El Central se publica con `dotnet publish src/Central/CgPos.Central.Api -c Release` y corre como servicio de Windows `CgPosCentral` (logs en `C:\CGPOS\Central\Logs`). Necesita la cadena `BaseDatosCentral`, la clave `Seguridad:ClaveFirmaJwt` (compartida por todas las instancias) y el certificado HTTPS. El instalador llega en la fase H7.

## Convenciones

- Todo en español: proyectos, namespaces, clases, métodos, variables, parámetros y mensajes. Solo se mantienen siglas técnicas (Id, Api, Json, Pin, Hash, Token, Jwt, Xml, e-CF) y los nombres que exige .NET.
- El servidor corporativo se llama **Central**.
- Las transacciones usan Id GUID v7.
- Todo lo que va al Central se escribe en la bandeja de salida dentro de la misma transacción del documento.
- Seguridad: permisos granulares definidos en `CatalogoPermisos` (caja) y `CatalogoPermisosCentral` (Central); cada permiso es una política de autorización con el mismo nombre.
- Formato RD fijo: `RD$2,175.34` y `dd/MM/yyyy`.
