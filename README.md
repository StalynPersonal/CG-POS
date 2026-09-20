# CG-POS

Sistema de punto de venta **offline-first** para Contreras Group, con facturación electrónica **e-CF** firmada en cada caja y sincronización con un servidor **Central**.

Se construye por fases: primero la **caja** (fases C0–C11) y luego el **Central** (fases H1–H7).

**Estado actual:** la caja está completa (C0 a C11: fundaciones, seguridad local, maestros, venta, descuentos, cobro, e-CF offline, turnos, devoluciones, fidelidad, pendientes de entrega y sincronización). Del Central están hechas las fases H1 (fundaciones y seguridad), H2 (sincronización con las cajas), H3 (Central Manager: seguridad, organización, cajas, maestros, precios y promociones) y H4 (envío de e-CF a la DGII y monitor de sincronización). También están hechas H5 (notas de crédito entre sucursales, saldo central de puntos y despacho), H6 (reportes con exportación a Excel y PDF) y H7 (instalación y actualización remota de las cajas).

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
              CgPos.ECF           Formato, firma y verificación del e-CF (núcleo común)
              CgPos.Interfaz        Tema verde institucional y componentes táctiles (MudBlazor)
src/POS/      CgPos.Pos.Agente     Único servicio de la caja: API local, pantallas y sincronización
              CgPos.Pos.Aplicacion  Casos de uso y abstracciones (BandejaSalida, auditoría)
              CgPos.Pos.Infraestructura  EF Core / SQL Server, migraciones, implementaciones
              CgPos.Pos.ECF       Facturación electrónica de la caja: certificado, emisión y firma
              CgPos.Pos.Web       Pantallas Blazor WebAssembly (cajero, cliente, devoluciones)
src/Central/  CgPos.Central.Api    Servicio del Central: API para el Central Manager y para las cajas
              CgPos.Central.Aplicacion  Casos de uso y abstracciones del Central
              CgPos.Central.Infraestructura  EF Core / SQL Server, migraciones, implementaciones
              CgPos.Central.ECF    Facturación electrónica del Central: envío de los e-CF a la DGII y sus acuses
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
| `Seguridad.IntentosMaximosClave`, `Seguridad.MinutosBloqueo` | Bloqueo por clave incorrecta | Sí |
| `Seguridad.MinutosVigenciaAutorizacion` | Tiempo para usar una autorización de supervisor | Sí |
| `Seguridad.HorasSesion` | Duración de la sesión del usuario | Sí |
| `Fiscal.MontoIdentificacionConsumo` | Total desde el cual la factura de consumo exige cédula o RNC | Sí |
| `Fiscal.PorcentajeAlertaSecuenciaEcf`, `Fiscal.DiasAlertaCertificado` | Alertas de secuencias y certificado (si faltan, la barra de estado lo indica) | Sí |
| `Fiscal.TipoIngresos` | Tipo de ingresos de los e-CF (tabla DGII, 1 a 6) | Sí |
| `Fiscal.PorcentajeRetencionLey3223` | Retención de la Ley 32-23 en facturas gubernamentales (E45) | No (sin configurar, no se retiene) |
| `Caja.PasoRedondeoEfectivo` | Redondeo del cobro en efectivo (`0` = sin redondeo) | Sí |
| `Caja.CierreCiego`, `Caja.FondoEnCuadre` | Modalidad del cierre de turno | Sí |
| `Caja.FondoPredeterminado` | Fondo sugerido al abrir turno | No |
| `Devoluciones.DiasRetencionImpuesto`, `Devoluciones.DiasVigenciaNotaCredito` | Retención del ITBIS y días de vigencia de la nota de crédito desde su emisión | Sí |
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

Otras reglas que dependen de la configuración: sin **topes de descuento** en los maestros no se permite el descuento manual; las **direcciones del timbre** de la DGII (`Ecf.UrlConsultaTimbre` y `Ecf.UrlConsultaTimbreConsumo`, que definen el ambiente del código QR) son obligatorias para emitir; la **dirección** de la empresa o sucursal es obligatoria en el e-CF, las **tasas de ITBIS** del XML salen del maestro de impuestos y el indicador de **bien o servicio** sale del artículo (`esServicio` en los maestros). El nombre y las iniciales de la empresa en la pantalla de venta salen de la configuración de la caja. Las fechas del ticket, del e-CF y los plazos de devolución usan la **zona horaria configurada en el equipo** de la caja (en República Dominicana, UTC-4).

## Ejecutar la caja

```powershell
dotnet build CgPos.slnx
dotnet run --project src/POS/CgPos.Pos.Agente
```

- Pantallas: <http://localhost:5180>
- Salud del servicio y la base: <http://localhost:5180/salud>. Comprueba base de datos, configuración de la caja, Central, certificado e-CF y **publicidad** (la carpeta de las imágenes ya resuelta, la dirección con que se sirven —`/publicidad`— y cuántas hay). Estados en español: *Correcto*, *Con pendientes* y *Con fallas*.
- Logs de desarrollo: `src/POS/CgPos.Pos.Agente/logs/`

En desarrollo, el Agente aplica al arrancar `datos/carga-inicial.desarrollo.json` y opera como la caja que se le indique en su pantalla de configuración (por lo general la 01 de la sucursal 01). Las referencias entre registros de los archivos de datos van por código, nunca por Id (el departamento del artículo por su número, el rol del usuario por su código, sus cajas como sucursal y caja). Los archivos de datos se aplican **solo si cambiaron** desde la última vez (su huella SHA-256 queda en la base). En el Central, además, solo crean lo que no existe: lo que se editó en el Manager nunca se pisa. La caja configurada contra un Central no aplica los archivos de carga inicial ni de maestros: todo le llega del Central. Usuarios de prueba:

| Usuario | Clave | Rol |
|---|---|---|
| C001 | Cajero.2026 | Cajero (nivel 1) |
| S001 | Supervisor.2026 | Supervisor (nivel 2, autoriza operaciones) |
| G001 | Gerente.2026 | Gerente (nivel 3, todos los permisos) |

En la caja se entra solo con usuario y clave; las autorizaciones de supervisor también se dan con usuario y clave.

### Maestros

En desarrollo también se aplican al arrancar:

- `datos/maestros.desarrollo.json`: departamentos, categorías, marcas, unidades, impuestos (ITBIS 18 %, 16 %, 0 % y exento), artículos de ferretería y construcción ficticios, clientes, formas de pago, bancos, tipos de tarjeta y denominaciones.

Ejemplos para probar la API (todas requieren sesión):

| Ruta | Qué devuelve |
|---|---|
| `GET /api/articulos/codigo/7891114119695` | Artículo por código de barras, proveedor, interno o etiqueta de balanza |
| `GET /api/articulos?texto=cemento gris` | Búsqueda por palabras |
| `GET /api/articulos/no-codificados` | Vegetales y especias en orden alfabético |
| `GET /api/documentos/131-24679-6` | RNC/cédula: formato, dígito verificador y cliente registrado |
| `GET /api/catalogos/cobro` | Formas de pago, bancos, tipos de tarjeta y denominaciones |

Importaciones manuales (permiso `Seguridad.AdministrarConfiguracion`, usuario G001 en desarrollo):

- `POST /api/maestros/articulos/csv`: CSV con `;` o `,`. Columnas obligatorias `codigo`, `descripcion`, `departamento`, `unidad`, `impuesto`, `precio_detalle`. Opcionales: `categoria` (del mismo departamento), `marca`, `precio_mayor`, `cantidad_minima_mayor`, `precio_minimo`, `costo`, `tipo`, `referencia`, `codigos_barras` y `codigos_proveedor` (separados por `|`), `ruta_imagen`, `mostrar_en_catalogo` y `activo`. Los números usan punto decimal; las líneas con errores se informan y se omiten.

Etiquetas de balanza: por defecto EAN-13 con prefijo `21` (peso, 3 decimales) o `22` (precio, 2 decimales), 5 dígitos de artículo y 5 de valor. Se ajusta con los parámetros `Balanza.*`.

### Turno y venta

Al ingresar, si la caja no tiene turno abierto se pide el fondo (sugerido por el parámetro `Caja.FondoPredeterminado`). Solo puede haber un turno abierto por caja.

**Numeración de documentos:** cada documento se numera `código de sucursal (2) + código de caja (2) + tipo (1) + secuencia`, sin separadores ni prefijos. El dígito del tipo es `1` factura, `2` nota de crédito y `3` pendiente de entrega (factura de la sucursal 01, caja 01, secuencia 1 con 7 dígitos = `010110000001`; su nota de crédito, `010120000001`). La secuencia es por caja y tipo, y atómica. El número es único en toda la empresa: con él caja y Central identifican el documento, y del número se leen la sucursal, la caja y el tipo (`NumeroDocumento`).
- `Numeracion.DigitosSecuencia` (5 a 12): se puede aumentar en cualquier momento; si una secuencia supera los dígitos, el número crece en vez de reiniciarse.
- `Numeracion.ProximaFactura` y `Numeracion.ProximaNotaCredito` (por caja): mínimo de la próxima secuencia, para continuar la numeración tras reinstalar una caja. Solo la adelantan; un valor menor al ya usado no la hace retroceder.

**Las tres numeraciones del sistema son independientes** y conviene no confundirlas:

| | Quién la lleva | Forma | Dónde se configura |
| --- | --- | --- | --- |
| Documentos de la caja | Cada caja, en su base (`SecuenciasCaja`) | sucursal + caja + tipo + secuencia | `Numeracion.*` |
| Documentos del Central | El Central (`SecuenciasCentral`) | prefijo + correlativo (`COT000001`) | Organización → Secuencias de documentos |
| Comprobantes fiscales | La caja que emite, con su rango de la DGII (`SecuenciasEcf`) | e-NCF (`E320000000001`) | Fiscal → Secuencias de e-CF |

La de la caja va en su base y funciona sin red: la caja factura aunque el Central esté caído. La del Central numera lo que nace allá (cotizaciones, listas de boda). Y el e-NCF es otra cosa: su rango lo asigna la DGII y no se inventa.

**Secuencias de documentos del Central:** cada documento que emite el Central tiene su fila en `SecuenciasCentral`, con su código, el prefijo, cómo se le llama, el último número entregado, los dígitos del correlativo y si está activa. **Sin su secuencia, el documento no se crea**: la operación se rechaza diciendo cuál falta o cuál está apagada, en vez de inventar una numeración. La instalación deja creadas las de cotización y lista de boda; las demás se agregan en Organización → Secuencias de documentos.

El **código** (`Factura`, `Cotizacion`…) es con lo que el sistema la busca y no cambia; el **prefijo** (`FAC`, `COT`) es solo cómo se ve el número y se puede cambiar cuando el negocio quiera, sin que deje de emitirse nada — los documentos ya emitidos conservan el suyo. Dos documentos no pueden compartir prefijo. El contador solo se adelanta: bajarlo repetiría números ya entregados.

**El Central numera lo que le sube la caja.** Una factura llega con el número de la caja (`010110000001`) y el Central le pone además el suyo (`FAC000001`); ambos quedan guardados, y el listado de facturas los muestra juntos. Lo mismo con las notas de crédito, los despachos y el cierre de sucursal. La instalación deja creadas esas seis secuencias.

Si al recibir un documento falta su secuencia o está apagada, **el documento entra igual, sin número del Central**: la caja ya lo emitió y perderlo por una configuración sería peor. Se le puede asignar después. Esto es distinto de crear una cotización, que sí se rechaza: ahí el documento todavía no existe.
- El Central exige que el número de factura y de nota de crédito sea único en la empresa: si llega repetido de otra transacción, el mensaje se guarda, no entra en los reportes y queda un conflicto `NumeroDuplicado` en el monitor.

En la pantalla de venta:

- **Escanear o digitar** el código y Enter. Acepta `cantidad*código` (ej. `12*CEM-425`, que activa el precio mayor).
- **Tocar la cantidad** de una línea (o F4) para cambiarla; **tocar el código** alterna entre el código leído y el interno.
- **F2** busca por descripción, **F11** consulta el precio sin vender.
- **Eliminar línea** (botón ✕ de la línea), **eliminar por escaneo** y **limpiar pantalla** requieren permiso; al cajero se le pide autorización de supervisor (S001 / Supervisor.2026). La autorización es de un solo uso y vence en 5 minutos. La línea eliminada queda tachada y su reverso en rojo debajo.
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

- **Cliente y comprobante (F12 o tocar el encabezado):** RNC o cédula contra los clientes, que bajan del Central. Si no está registrado se pide el nombre. El comprobante toma el habitual del cliente (E31/E32/E44/E45); cambiarlo a mano requiere permiso. E31 y E44 exigen RNC o cédula; E45, RNC.
- **Identificación obligatoria:** una factura de consumo desde `Fiscal.MontoIdentificacionConsumo` (RD$250,000 por defecto) muestra el aviso hasta asignar cédula o RNC.
- **Régimen especial (E44):** la DGII lo define para bienes y servicios **exentos de ITBIS** a quien está acogido a un régimen especial (zonas francas, diplomáticos), y exige que el cliente presente su carné o certificación de exención. Elegir E44 **exenta la factura completa**: cada línea baja a su base, el cliente paga sin ITBIS y el e-CF va con indicador 4 y `MontoExento`. La pantalla de venta, la del cliente y el ticket lo dicen (**EXENTA DE ITBIS · RÉGIMEN ESPECIAL**). Si el comprobante cambia a otro tipo, las líneas vuelven a su precio con impuesto. Un bien que no califique para la exención no se factura con E44: va en un E31 aparte.
- **Gubernamental (E45):** lleva ITBIS como cualquier factura, salvo que la entidad **presente su certificación de exención**: el número se registra en el diálogo del cliente y entonces la factura se emite exenta, igual que el E44 (Norma General 05-19, art. 4). El ticket imprime *EXENTA DE ITBIS - CERTIFICACIÓN &lt;número&gt;* y queda en la auditoría (`Ventas.CertificacionExencion`) quién la registró.
- **Retención de la Ley 32-23 (E45):** con `Fiscal.PorcentajeRetencionLey3223` configurado (ej. 5), una factura **gubernamental (E45)** calcula la retención **sobre el subtotal ya con descuentos** y se la descuenta a lo que el cliente paga en caja; el total de la factura no cambia. Es la retención de ISR que el Estado le hace a su proveedor; un emisor electrónico autorizado está **exento** por el artículo 34 de la Ley 32-23 (reconfirmado en la Ley 30-26), así que lo normal es dejar el parámetro en 0. Se ve en la pantalla de venta, en la del cliente y en el ticket ("RETENCIÓN LEY 32-23" y "TOTAL A PAGAR"), y en el e-CF va como `ValorPagar` junto al `MontoTotal` (las formas de pago cuadran con lo pagado). Al cambiar el comprobante a otro tipo la retención se quita sola; el porcentaje que manda es el vigente al cobrar.
- **Límite de compra (F3):** avisa cuando el total supera el monto que pidió el cliente.
- **Facturas en espera (F7):** quedan ligadas al cajero y al turno; al retomar una, la actual pasa a espera.
- **Anular** (con motivo) y **Suspender** (bloquea la pantalla hasta digitar la clave) están en la segunda página de teclas y requieren permiso o autorización de supervisor.
- **Serializados:** al escanearlos se pide el serial; no se repite en la misma venta.
- **Balanza (F5):** un pesado sin etiqueta toma el peso estable de la balanza menos el peso del empaque configurado en el artículo. En desarrollo está simulada (`Perifericos:BalanzaSimulada:Peso`); en una caja real se conecta por puerto serie (ver *Periféricos configurables*).
- **Catálogo en mosaicos:** botón junto al campo de escaneo; muestra los artículos con `mostrarEnCatalogo`.

### Descuentos y ofertas

- **Ofertas** (`promociones` en los maestros): porcentaje, monto por unidad, precio especial, lleva X paga Y y precio desde una cantidad. Se limitan por artículos, departamentos, categorías o marcas, sucursales, fechas, días de la semana, horas y unidades por cliente. Se recalculan en cada operación sumando todas las líneas del mismo artículo, y si aplican varias gana la más favorable para el cliente. Una oferta solo reemplaza el precio por mayor si deja mejor precio.
- **Columna Promo:** muestra la oferta (ej. `2x1`, `-15%`). Al tocarla se ve el detalle, las ofertas vigentes y la opción de **desactivarla**, que requiere permiso.
- **Descuento a la línea** (tocar el precio) y **a la factura** (segunda página): por porcentaje o monto, con motivo de la lista `motivosDescuento`. Requiere permiso o clave de supervisor. No aplica a artículos en oferta ni a departamentos sin descuento manual (panadería, vegetales). El de factura puede limitarse a líneas elegidas y se prorratea al centavo.
- **Topes** (`topesDescuento`): por nivel de usuario, general, por departamento, categoría, marca o artículo (rige el más específico: artículo, categoría, marca, departamento y por último el general). Si el descuento supera el tope de quien autoriza, se pide la clave de un nivel superior. En desarrollo: supervisor (S001) hasta 10 % o RD$2,000; gerente (G001) hasta 30 % o RD$20,000.
- **Descuento del banco por tarjeta (RF-98)** (`descuentosTarjeta` en los maestros): el Central define qué BIN participan (los primeros 4 a 8 dígitos de la tarjeta), el porcentaje o monto, la compra mínima, el tope del descuento, la vigencia y los días. En el cobro, el terminal lee la tarjeta y entrega su BIN (o el cajero digita los primeros dígitos) y el descuento se aplica **a la factura antes de emitir el e-CF**, para que el comprobante fiscal salga con lo realmente cobrado; si varios bancos cubren el mismo BIN, gana el que más descuenta. Queda auditado como `Ventas.DescuentoTarjeta`.
- Cada línea guarda la oferta aplicada y cada descuento queda auditado con motivo y autorizador.

### Cobro y periféricos

- **F8 Totalizar** abre el cobro: formas de pago del maestro en botones, monto con teclado y billetes rápidos, y en todo momento lo pagado, lo que falta o la devuelta. Admite pago mixto.
- **Efectivo:** es el único medio que da devuelta. Si el parámetro `Caja.PasoRedondeoEfectivo` es mayor que 0, el total se redondea (ej. `1` = al peso).
- **Dólares:** a la tasa del día (`tasasCambio` en los maestros); la devuelta es en pesos.
- **Tarjeta:** "Pasar tarjeta" envía el monto al terminal de pago, simulado en desarrollo con `Perifericos:TerminalSimulado` y `SinConexion: true` para probar la contingencia. Quitar una tarjeta la anula en el terminal. Si la pasarela no responde, se registra la aprobación manual con autorización y queda para conciliar.
- **Transferencia y cheque** piden banco y número; **bonos y tarjetas de regalo** piden serial y no se aceptan con crédito fiscal.
- **Al cobrar:** la venta, sus pagos, el mensaje para el Central (`Venta.Cobrada` en la bandeja de salida) y la auditoría se guardan en una sola transacción. Después se imprime el ticket, se abre la gaveta si hubo un medio físico y empieza otra venta. La pantalla del cliente muestra el pago y la devuelta.
- **Impresora:** `Perifericos:Impresora:Tipo` = `Archivo` deja el ticket en texto y ESC/POS en `Carpeta` (en desarrollo `src/POS/CgPos.Pos.Agente/logs/impresiones`); `Red` lo envía a `Host`:`Puerto` (9100); `Windows` lo manda en crudo a la impresora instalada con ese `Nombre` (USB, serie o compartida), que es lo normal en una caja de mostrador. Reimprimir y abrir la gaveta sin venta (con permiso) están en la segunda página de teclas.

### Periféricos configurables

Balanza y terminal de pago se eligen por configuración, no por código: cada modelo es un **perfil** con lo que cambia entre equipos (qué se le envía y cómo se lee su respuesta). Sustituir un equipo por otro es cambiar el modelo y, si hace falta, ajustar el perfil en `appsettings`.

| Clave | Uso |
| --- | --- |
| `Perifericos:Balanza:Tipo` | `Simulada` (desarrollo) o `Serie` (balanza conectada) |
| `Perifericos:Balanza:Modelo` | Perfil del equipo: `Datalogic Magellan 9556` o vacío para el genérico |
| `Perifericos:Balanza:Puerto`, `Baudios`, `Paridad`, `BitsDatos`, `BitsParada` | Puerto COM y sus parámetros |
| `Perifericos:Balanza:Comando`, `Patron`, `Terminador`, `Unidad`, `Estables`, `MilisegundosEspera` | Ajustes del protocolo si el equipo difiere del perfil |
| `Perifericos:Terminal:Tipo` | `Simulado` (desarrollo) o `Conectado` |
| `Perifericos:Terminal:Modelo` | `CardNet` (Ingenico 7000, protocolo ECRti) o vacío para el terminal genérico |
| `Perifericos:Terminal:Host`, `Puerto` | Dirección del terminal CardNet en la red; el puerto es 7060 |
| `Perifericos:Terminal:ConsultaTarjeta` | `false` si el terminal no tiene activa la consulta de tarjeta (CS00) |
| `Perifericos:Terminal:IdMultiMerchant` | Solo si el terminal trabaja Multi-Merchant |
| `Perifericos:Terminal:Transporte` | Terminal genérico: `Socket` (`Host` y `Puerto`) o `Serie` (`Puerto` y `Baudios`) |
| `Perifericos:Terminal:PlantillaCobro`, `PlantillaAnulacion`, `PlantillaCierreLote`, `PatronRespuesta`, `Aprobadas`, `SegundosEspera` | Terminal genérico: mensajes y lectura de su respuesta |
| `Perifericos:TerminalSimulado:Bin` | Desarrollo: el terminal simulado "lee" esa tarjeta antes de cobrar |

- La **balanza** pide el peso con el comando del perfil y lee peso, unidad y estabilidad de su respuesta; el Datalogic Magellan viene con los valores de su manual (9600 baudios, 7 bits, paridad impar, comando `S`). Si no responde, la caja sigue operando y el cajero digita el peso.
- El **terminal CardNet (Ingenico 7000)** trabaja en *modo caja* (ECRti) por Ethernet: la caja se conecta al puerto 7060 del terminal, saluda con `ENQ`/`ACK`, lo pone a recibir con `SYN` (contesta `EOM` y `ENQ`), envía el mensaje con los campos separados por `FS`, confirma la respuesta con `ACK` (sin ese `ACK` el terminal reversa la transacción) y cierra con `EOT`/`EOM`. Transacciones usadas: `CS00` consulta de tarjeta, `CN00` venta, `CN02` anulación y `CN01` cierre de lote; todo rechazo llega como `99` y una operación apagada en el terminal, como `40`. **La IP de la caja debe estar en la lista blanca del terminal.**
- **BIN automático (RF-98):** al pasar la tarjeta, la caja envía primero `CS00`; el terminal muestra "deslice o inserte la tarjeta" y devuelve los primeros 8 dígitos sin datos sensibles. Con ese BIN se aplica el descuento del banco a la factura y **enseguida** se envía la venta `CN00` ya por el monto rebajado (el terminal guarda la lectura unos 15 segundos). Si la tarjeta no aprueba, el descuento se retira y la venta queda como estaba. El cajero solo digita los dígitos a mano si el terminal no tiene la consulta activa.
- El **terminal genérico** arma sus mensajes con las plantillas del perfil (`{monto}`, `{montoCentavos}`, `{referencia}`, `{aprobacion}`, `{fecha}`) y lee la respuesta con su expresión regular, para integrar otro modelo sin recompilar. Si el terminal no responde, se ofrece la aprobación manual autorizada (RF-213).

### Facturación electrónica (e-CF)

- **Certificado:** `Ecf:Certificado:Ruta` apunta al `.p12` de la caja. El PIN se digita en la pantalla (botón e-CF de la barra de estado, o al cobrar) y queda solo en memoria del Agente: al reiniciarlo se vuelve a pedir. En desarrollo, `Ecf:Certificado:PinDesarrollo` crea un certificado autofirmado en `logs/certificado-desarrollo.p12` y lo carga solo.
- **Secuencias:** los rangos por caja y tipo llegan en `secuenciasEcf` de los maestros. Si un tipo está agotado, vencido o sin rango, el cobro se rechaza y la barra de estado lo alerta (`Ecf.PorcentajeAlertaSecuencia`, `Ecf.DiasAlertaCertificado`).
- **Al cobrar:** dentro de la misma transacción se toma el siguiente e-NCF, se genera y valida el XML, se firma, se calcula el código de seguridad y la URL del timbre, y el XML firmado se guarda en `{Ecf:CarpetaXml}\Pendientes\yyyy\MM\dd\{RNC}{eNCF}.xml` (en desarrollo `logs/ecf`; en producción `C:\CGPOS\eCF`). El documento queda "Pendiente por sincronizar" y el XML viaja al Central en `Venta.Cobrada`. Si algo falla, no se consume la secuencia.
- **Ticket:** e-NCF, vencimiento de la secuencia, código de seguridad, fecha de firma y QR del timbre (ESC/POS nativo).
- **API:** `GET /api/ecf/estado`, `POST /api/ecf/certificado`, `GET /api/ecf/documentos?estado=`.
- **Sin e-NCF no se factura:** si no hay secuencia disponible (agotada, vencida o sin asignar) o la caja no puede firmar (certificado sin cargar o vencido), el cobro se rechaza con el motivo y no se consume nada. No hay comprobantes provisionales.

- **Validación contra los esquemas de la DGII:** los XSD oficiales están en `datos/xsd` y se configuran con `Ecf:CarpetaXsd`. Cada comprobante se valida contra el esquema de su tipo **después de firmarlo** (el esquema exige la firma); si no cumple, no se emite. Las unidades de medida viajan con el código de la tabla de la DGII (`UND` = 43, `LB` = 23…) y la cantidad con dos decimales, como exige el esquema.
- **Resumen de consumo (RFCE):** una factura de consumo que no llega a `Fiscal.MontoIdentificacionConsumo` se le informa a la DGII como **resumen**: totales, formas de pago y el código de seguridad del e-CF, sin las líneas. La caja lo firma y lo envía al Central marcado como resumen, y el Central lo entrega en el servicio de facturas de consumo (`Central.Dgii.UrlRecepcionConsumo`), que responde aceptado o rechazado en el mismo envío, sin trackId. El e-CF completo queda en la caja y es el que se le entrega al cliente.
- **Por confirmar en la certificación:** rutas exactas de los servicios y detalles del resumen de consumo.

### Turno y cierre

- **Relevo:** si la caja tiene abierto el turno de otro cajero, la apertura ofrece *Relevar turno*; con autorización de supervisor el nuevo usuario toma el turno sin cerrarlo ni cuadrar.
- **Retiro** (segunda página de teclas): monto y motivo, autorización de supervisor, comprobante impreso con firmas y apertura de gaveta. No se puede retirar más del efectivo en la gaveta.
- **Cierre de turno:** declaración por forma de pago y conteo del efectivo por denominaciones. Con `Caja.CierreCiego` (por defecto `true`) el cajero no ve lo esperado; *Pre-cierre* imprime lo esperado con clave de supervisor.
- **Lo esperado:** el efectivo cuenta lo recibido menos la devuelta y los retiros; el fondo solo entra si `Caja.FondoEnCuadre` es `true` (por defecto no se mezcla). Moneda extranjera se cuadra en su moneda; los demás medios, por lo aplicado a las facturas.
- **No se cierra** con facturas en espera, transacciones en curso con artículos o ventas sin e-CF firmado; la pantalla lista qué falta. Las transacciones vacías se descartan al cerrar.
- **Al cerrar:** se imprime el reporte (esperado, declarado, diferencia por forma de pago, denominaciones, retiros, reembolsos y relevos) y el cierre queda en la bandeja de salida (`Caja.TurnoCerrado`), igual que retiros y relevos.
- **Cierre de lote (RF-215):** *Cerrar lote* cierra el lote del terminal de pago y cuadra las tarjetas aprobadas del turno con lo que el terminal reporta: si detalla el lote se muestran la diferencia y las autorizaciones que faltan de un lado o del otro; si el modelo no lo detalla, se muestra lo de la caja para compararlo con el comprobante que imprime el terminal.
- **El cierre es definitivo:** la caja no puede reabrirlo ni deshacerlo, ni con autorización. Un cierre mal hecho se corrige en el Central, en *Cierres de caja* (permiso `Central.Cierres.Ajustar`): se corrige **lo declarado en la forma de pago que falló**, con motivo obligatorio, y el cierre recalcula su declarado y su diferencia. Lo que informó la terminal no se borra: queda en el historial del cierre (de cuánto a cuánto, por qué y quién) y en la auditoría (`Reportes.CierreAjustado`). Un día ya consolidado en el cierre de sucursal no se puede corregir, y un reenvío del mismo cierre por parte de la caja no pisa la corrección. En el **reporte de cuadres** y en la pantalla del **cierre de sucursal** se ven las dos cifras, *Declaró la caja* y *Declarado*, con la marca de corregido: lo que se consolida y se deposita es la corregida.
- **API:** `GET /api/caja/turno/resumen`, `POST /api/caja/turno/{precierre|retiros|relevo|cierre}`, `GET /api/caja/cierres`, `POST /api/caja/cierres/{id}/reimprimir`.

### Devoluciones y notas de crédito

- **Pantalla `/devoluciones`:** F10 desde la venta (la venta en curso queda guardada) o una estación dedicada en un tercer monitor. Se escanea el código de barras del ticket (número de transacción) o se digita el e-NCF.
- **Devolución parcial o total:** por línea se ve lo vendido, lo ya devuelto y lo disponible; no se puede devolver más de lo vendido. Los serializados piden el serial vendido. Si la factura no tiene cliente se pide cédula o RNC (el nombre sale del cliente registrado o se digita).
- **Motivo y autorización:** motivo seleccionable (`motivosDevolucion` en los maestros) y clave del encargado (permiso `Devoluciones.Autorizar`), que sale impreso en la nota.
- **Plazo:** pasados `Devoluciones.DiasRetencionImpuesto` días (30 por defecto) se retiene el ITBIS y la nota acredita solo la base.
- **Nota de crédito E34:** se firma en la caja en la misma transacción, con referencia al e-CF de la factura (código 1 si completa la factura, 3 si es parcial), y viaja al Central en `Devolucion.NotaCreditoEmitida`. Se imprimen la copia del cliente (código de barras y política `Devoluciones.PoliticaNotaCredito`) y la de contabilidad.
- **Consumo:** en el cobro, la forma de pago *Nota de crédito* pide el e-NCF; valida que exista en la caja, esté vigente y tenga saldo. La vigencia se cuenta **desde la fecha de emisión con los días configurados hoy** (`Devoluciones.DiasVigenciaNotaCredito`): con 60 días, una nota de 70 días no se puede usar; si se suben los días en el Central, vuelve a poder usarse. La nota no guarda su vencimiento. Si queda saldo se imprime un voucher. Cada consumo va al Central (`NotaCredito.Consumida`).
- **Otra sucursal:** una nota de crédito que no está en la caja se valida y se reserva en el Central al cobrar (ver *Notas de crédito entre sucursales*).
- **La factura siempre sale del Central:** la caja la pide con `GET /api/facturas/{numero}` (por número o por e-NCF) **también cuando la vendió ella misma**, y la guarda en la tabla temporal `FacturasConsultadas`. Una nota de crédito nunca se emite contra una factura que el Central no tenga registrada: es el único que sabe lo **ya devuelto de cada línea en toda la empresa**. Antes de emitir le **retiene esas líneas** (`POST /api/facturas/{numero}/reservas`, con `Central.Devoluciones.MinutosReserva`) para que dos cajas no devuelvan la misma mercancía. Así se devuelve una factura de **cualquier sucursal y cualquier caja**, y la nota sale siempre con el **certificado y el rango de e-NCF de la caja que la emite**. La copia **se borra al emitir la nota** (y las abandonadas, al día). Si la factura la vendió esta caja, se enlaza con su venta para lo que solo ella sabe: los puntos que acumuló y la mercancía pendiente de entregar; y lo devuelto se toma como el mayor entre lo que dice el Central y lo que la caja ya emitió, por si una nota todavía no ha subido. **Sin conexión no se emiten notas de crédito**, y la pantalla lo dice así.
- **Nota de crédito interna (sin comprobante fiscal):** para corregir un problema de la factura sin devolver dinero. Se marca *Nota de crédito interna* en la pantalla de devoluciones: **no consume e-NCF ni se firma** (no va a la DGII ni al 607), **nace sin saldo** y no se acepta como forma de pago ni en esta caja ni en otra sucursal, no admite reembolso y **exige la autorización de un supervisor** (permiso `Devoluciones.AutorizarNotaCreditoInterna`, impreso en la nota). No pide cédula del cliente. Sí descuenta lo devuelto de la factura, viaja al Central en `Devolucion.NotaCreditoEmitida` con `EsInterna` y sale en los reportes de ventas. El ticket sale como *NOTA DE CRÉDITO INTERNA – SIN VALOR FISCAL*. Queda auditada como `Devoluciones.NotaCreditoInterna`.
- **Reembolso (RF-123):** el cliente puede llevarse el dinero en vez del saldo a favor. La nota de crédito E34 se emite igual (la DGII la exige) pero queda sin saldo, y se registra cómo se pagó: **efectivo** de la gaveta (que baja lo esperado del cuadre como un retiro y sale en el reporte del cierre), **a la tarjeta** con la autorización del terminal, o **cheque** con banco y a nombre de quién. Cada forma se habilita por parámetro y el máximo en efectivo se configura; lo que no esté habilitado se rechaza.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Devoluciones.ReembolsoEfectivo`, `Devoluciones.ReembolsoTarjeta`, `Devoluciones.ReembolsoCheque` | Formas de devolver el dinero que el negocio permite | No (sin ellos solo queda saldo en la nota) |
| `Devoluciones.MontoMaximoReembolsoEfectivo` | Tope de la devolución en efectivo | No |
- **API:** `GET /api/devoluciones/factura/{numero}` (la busca en el Central), `POST /api/devoluciones`, `GET /api/devoluciones/notas-credito/{codigo}`, `POST /api/devoluciones/{id}/reimprimir`, `GET /api/devoluciones/motivos`.

### Programa de fidelidad

- **Configuración (Central):** `nivelesFidelidad` (factor de acumulación), `reglasAcumulacion` (puntos por cada monto en todo, un departamento, una categoría, una marca, un artículo, un día o una promoción; cada línea toma la más favorable) y `miembrosFidelidad` (cédula, nivel y saldo sincronizado con puntos por vencer). Parámetros `Fidelidad.*`.
- **En caja (F12):** la cédula es el ID/PIN del miembro; si no está inscrita se inscribe en el mismo paso. El miembro habilita las ofertas exclusivas de fidelidad.
- **Al cobrar:** acumula sobre lo no pagado con puntos; el canje es la forma de pago *Puntos* (valor del punto, saldo, mínimo y tope sin conexión) con permiso `Fidelidad.CanjearPuntos`. La nota de crédito reversa en proporción lo acumulado.
- **Saldo:** último saldo del Central (sin lo vencido) más los movimientos de la caja posteriores; cada inscripción y movimiento va al Central.
- **API:** `GET /api/fidelidad/miembros/{cedula}`, `POST /api/fidelidad/miembros`, `POST|DELETE /api/ventas/{id}/fidelidad`.

### Pendientes de entrega, envíos y despacho

- **Marcar (tecla «Entrega / envío»):** líneas completas o en parte para retiro en un almacén (`almacenes` en los maestros) o envío a dirección, con fecha comprometida; varios destinos por factura. Requiere `Pendientes.Marcar` o clave de supervisor. Los serializados pueden registrarse sin serial si se entregan después.
- **Al cobrar:** un pendiente por destino (número con el tipo `3`) con voucher de código de barras, copia del cliente y del almacén y la política `Entregas.PoliticaPendiente`. El pendiente viaja al Central en `Entregas.PendienteCreado`.
- **La caja no despacha:** ahí termina su parte. Preparar, entregar y anular se hacen en el Central (ver *Despacho en el Central*), porque el despacho no emite comprobante fiscal ni toca la gaveta, y quien atiende a un cliente que llama o que llega a otra tienda tiene que verlos todos. Lo pendiente de entregar tampoco se devuelve, y eso lo comprueba el Central al entregarle la factura a la caja.
- **API:** `POST /api/ventas/{id}/entregas`, `DELETE /api/ventas/{id}/entregas/{numero}`, `GET /api/entregas/almacenes`.

### Sincronización con el Central y mantenimiento

- **Bandeja de salida:** todo documento (venta, nota de crédito, cierre, pendiente, movimiento de puntos…) se guarda con su mensaje en la misma transacción. Un servicio en segundo plano del Agente lo envía cada `Sincronizacion.IntervaloSegundos` en orden de creación, con el Id del mensaje como clave de idempotencia, el número del documento como referencia y el SHA-256 del contenido. Sin comunicación detiene el lote y reintenta con espera progresiva (`Sincronizacion.EsperaInicialSegundos` duplicada hasta `Sincronizacion.EsperaMaximaSegundos`); la caja sigue operando.
- **Central:** la caja no se configura en archivos. Qué caja es, su IP, la dirección del Central y su credencial se escriben **en la pantalla de la caja** la primera vez que se abre, y quedan en su base (tabla `ConfiguracionCaja`), con la credencial cifrada contra ese equipo. En el `appsettings` solo queda `Caja:ValidarIpDelEquipo` (1 exige que la IP configurada sea de ese equipo; 0 no lo mira) y, para trabajar sin servidor, `Central:Modo = Simulado`, que guarda los mensajes en `Central:CarpetaSimulada` con la misma idempotencia y validación de hash.
- **Maestros del Central:** cada `Sincronizacion.IntervaloMaestrosSegundos` (y al arrancar) la caja pide lo cambiado desde la versión que ya aplicó y lo aplica con las mismas cargas de organización y maestros; la marca solo avanza si todo se aplicó. Una caja nueva con `Central:Url`, `Caja:Sucursal` y `Caja:Codigo` se aprovisiona sola en el primer ciclo, en cuanto le aceptan su solicitud. Un miembro de fidelidad que llega del Central con una cédula ya inscrita en la caja actualiza el registro local.
- **XML de e-CF:** al confirmarse la venta o la nota de crédito, el e-CF queda *Sincronizado* y su XML pasa de `Pendientes` a `Enviados`; nada sale de `Pendientes` sin confirmación del Central.
- **Mantenimiento** (cada `Mantenimiento.IntervaloMinutos`): verificación de la hora contra `Reloj.ServidorNtp`; respaldo diario de la base desde `Respaldo.Hora` en `Respaldo:Carpeta` (vacía = carpeta de respaldos de la instancia; la cuenta del servicio de SQL Server debe poder escribir en ella); purga de XML enviados, mensajes confirmados y respaldos según los parámetros de retención.
- **Los ritmos son parámetros, no configuración del equipo:** cada cuánto sincroniza, cada cuánto baja maestros, cuántos mensajes manda por ciclo, cuánto espera entre reintentos, cada cuánto se mantiene, desde qué hora respalda y contra qué servidor verifica la hora se configuran en el Central (módulo *Sincronización y mantenimiento*), en general, por sucursal o por caja. Se leen **en cada ciclo**, así que cambiarlos allá se nota en las cajas sin reiniciar nada. Lo que el Central no tenga configurado toma el valor del `appsettings` de esa instalación y, si tampoco está, el de arranque; un valor mal escrito o fuera de rango se avisa en el registro y se usa el de la caja, que nunca deja de sincronizar por una configuración equivocada. En el `appsettings` solo quedan los datos del equipo: la carpeta de respaldos y la de los XML.
- **Alertas en la barra de estado:** tamaño de la base, documentos atrasados sin sincronizar, hora desfasada y respaldo fallido.

### Pantalla de ventas secundaria

La caja tiene tres pantallas, cada una en su monitor: **ventas principal** (`/`), **ventas secundaria** (`/venta-secundaria`) y **cliente** (`/cliente`). Se abren con `scripts/caja/abrir-pantallas.ps1`; ninguna navega a otra. Cada pantalla que pide sesión lleva al ingreso **sabiendo a cuál se iba** (`/ingreso?pantalla=principal|secundaria|devoluciones`), y al entrar abre esa; la del cliente no pide sesión.

- **Ruta `/venta-secundaria`**, táctil, estilo mostrador de cafetería: a la izquierda la venta con sus líneas, totales y el botón de cobrar; a la derecha el buscador, las pestañas de **departamento** y **categoría**, y los artículos en **mosaicos con imagen y precio**. Un toque agrega el artículo; tocar una línea la selecciona para cambiar cantidad o quitarla (con autorización, igual que en la principal).
- Es **la misma venta** que la principal: el Agente publica cada cambio por SignalR (el mismo canal de la pantalla del cliente) y las dos pantallas se mantienen iguales. Tiene a mano cliente, consulta de precio, facturas en espera y devoluciones.
- Muestra en mosaico los artículos marcados con `mostrarEnCatalogo`; lo demás se encuentra con el buscador por descripción o código.

### Pantalla del cliente y monitores

- `http://localhost:5180/cliente` muestra artículos, totales y un carrusel de publicidad en tiempo real (SignalR), sin iniciar sesión. Sin venta en curso muestra la bienvenida.
- Las imágenes (png, jpg, webp o svg) se toman de `Pantallas:CarpetaPublicidad` (`C:\CGPOS\Publicidad` en producción; `datos/publicidad` en desarrollo). El intervalo se configura con `Pantallas:SegundosPorImagen` y el texto con `Pantallas:MensajeBienvenida`.
- `scripts/caja/abrir-pantallas.ps1 -MonitorCajero 1 -MonitorCliente 2 [-MonitorDevoluciones 3]` abre cada pantalla en modo kiosco en su monitor (numerados de izquierda a derecha).

Las migraciones se aplican solas al arrancar. Las bases de desarrollo creadas antes de los Id enteros deben borrarse: las migraciones de la caja y del Central se regeneraron desde cero (`Inicial`). Para aplicarlas manualmente:

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
- **Cajas:** cada caja tiene una credencial de dispositivo, que cambia por un token de dispositivo de pocos minutos. Emitir otra, revocarla o deshabilitar la caja o su sucursal invalida sus tokens de inmediato.
- **Identidad de la caja:** cada caja se crea en el Central con su código de dos dígitos y su **IP fija**, que es obligatoria y única en toda la empresa. Su credencial se emite desde *Organización → Cajas* y se muestra una sola vez. En la caja, la primera pantalla pide los cinco datos —sucursal, caja, IP, dirección del Central y credencial— más el **usuario y la contraseña** de quien configura, que tiene que ser un usuario del Central con el permiso `Central.Cajas.Configurar`. Antes de guardar nada, la caja le pregunta al Central (`POST /api/dispositivos/configuracion`), que comprueba a la vez a la persona y al equipo: si algo no cuadra se dice en la misma pantalla. Los intentos fallidos cuentan y bloquean igual que en el ingreso normal, y en el Central queda `Dispositivos.CajaConfigurada` con quién y desde dónde. La contraseña no se guarda en la caja: solo el código del usuario. Los cinco datos quedan en su base con la credencial cifrada (DPAPI, atada a ese equipo). **En cada comunicación se validan los cuatro juntos**: sucursal, caja, IP y credencial. Si la conexión llega desde una dirección distinta a la registrada, no se bloquea pero queda en la auditoría. Revocar la credencial, cambiar la IP o eliminar la caja la deja fuera: la caja lo avisa como *Sin conexión (credenciales)*, **sigue vendiendo** y pide que la configuren de nuevo.
- **Parámetros del Central** (generales, en la tabla de parámetros):

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Seguridad.IntentosMaximos`, `Central.Seguridad.MinutosBloqueo` | Bloqueo por contraseña incorrecta | Sí |
| `Central.Seguridad.MinutosToken` | Vigencia del token de acceso | Sí |
| `Central.Seguridad.MinutosInactividad`, `Central.Seguridad.HorasSesion` | Vencimiento por inactividad y duración máxima de la sesión | Sí |
| `Central.Seguridad.LargoMinimoContrasena` | Largo mínimo de la contraseña | Sí |
| `Central.Seguridad.ContrasenaCompleja` | Exige mayúsculas, minúsculas, números y símbolos, sin contener el usuario | No |
| `Central.Dispositivos.MinutosToken` | Vigencia del token de las cajas | Sí |

- **API:** `POST /api/sesion/ingreso`, `POST /api/sesion/renovar`, `GET /api/sesion/actual`, `POST /api/sesion/cerrar`, `POST /api/sesion/contrasena`; `POST /api/cajas/{id}/credencial` y `POST /api/cajas/{id}/credencial/revocar` (permiso `Central.Dispositivos.Administrar`); `POST /api/dispositivos/token` y `GET /api/dispositivos/actual` para las cajas, y `POST /api/dispositivos/configuracion`, con el que una caja que se está configurando comprueba de una vez su credencial y el usuario que la autoriza (permiso `Central.Cajas.Configurar`). En la caja, `GET /api/configuracion` y `POST /api/configuracion` (sin sesión, solo en localhost) son los que usa su pantalla de configuración inicial.

### Central Manager (web)

- <http://localhost:5280> en desarrollo: Blazor WebAssembly con MudBlazor local y el tema verde, servido por el mismo `CgPos.Central.Api` (se desactiva con `Central:ServirManager = false`). Las rutas `/api` desconocidas responden 404.
- **Sesión:** ingreso con usuario y contraseña. El token de acceso vive en memoria y el de renovación en el almacenamiento de la pestaña: recargar no pide la contraseña, la sesión se renueva sola antes de vencer y cerrar la pestaña la termina en ese equipo. Con contraseña temporal solo se puede cambiarla.
- **Menú según los permisos del rol.** *Seguridad:* usuarios (alta con contraseña temporal, edición, restablecer contraseña, desbloquear, activar o desactivar) y roles con sus permisos agrupados por módulo. Ningún cambio puede dejar al Central sin un usuario activo que administre la seguridad y nadie puede desactivarse a sí mismo; restablecer, desactivar o cambiar el rol cierra las sesiones de ese usuario.
- **API** (permiso `Central.Seguridad.Administrar`): `GET /api/seguridad/permisos`; `GET|POST /api/seguridad/roles`, `PUT /api/seguridad/roles/{id}`, `POST /api/seguridad/roles/{id}/activar|desactivar`; `GET|POST /api/seguridad/usuarios`, `PUT /api/seguridad/usuarios/{id}`, `POST /api/seguridad/usuarios/{id}/contrasena|desbloquear|activar|desactivar`.
- *Organización* (permiso `Central.Organizacion.Administrar`): empresa (sin cambiar el RNC) y sucursales con **todos sus datos obligatorios** (razón social, nombre comercial, dirección y teléfono; código, nombre, dirección y teléfono), también NOT NULL en la base; cajas (alta, edición, activación y habilitación) y **parámetros**: la pantalla muestra **todo el catálogo** agrupado por módulo y con su descripción (la clave técnica solo en la edición y en la búsqueda); lo que no tiene valor general aparece *Sin configurar* y se configura desde su fila. *Valor por sucursal o caja* agrega un valor que prevalece sobre el general (los del Central solo son generales). Todo valor se valida según su tipo antes de guardarse. La moneda local debe estar publicada en el maestro de monedas. Los parámetros no se eliminan, porque las cajas no se enterarían del borrado; la pantalla avisa de los obligatorios sin configurar.
- *Último acceso de los usuarios de caja:* cada vez que alguien entra a una caja, la terminal lo avisa al Central (`Seguridad.IngresoUsuario`) y en *Usuarios de caja* se ve la fecha y en qué caja fue. Se guarda en su propia tabla (`AccesosUsuarioCaja`) y no en la fila del usuario: si estuviera ahí, cada ingreso cambiaría la versión del maestro y repartiría los usuarios a todas las terminales.
- *Credenciales de las cajas* (permiso `Central.Dispositivos.Administrar`): desde **Cajas** se emite la credencial de cada caja (se muestra una sola vez) o se revoca con motivo. La IP de la caja se edita en su ficha; cambiarla deja fuera al equipo anterior. El permiso `Central.Cajas.Configurar` es aparte: autoriza a montar una caja desde su propio equipo, sin dar derecho a emitir ni revocar credenciales.
- **API de organización:** `GET|PUT /api/organizacion/empresa`; `GET|POST /api/organizacion/sucursales`, `PUT /api/organizacion/sucursales/{id}`, `POST /api/organizacion/sucursales/{id}/activar|desactivar`; `GET|POST /api/organizacion/cajas`, `PUT /api/organizacion/cajas/{id}`, `POST /api/organizacion/cajas/{id}/habilitar|deshabilitar`; `GET /api/organizacion/parametros/catalogo`, `GET|POST /api/organizacion/parametros`, `PUT /api/organizacion/parametros/{id}`.
- *Rangos de e-CF* (permiso `Central.Fiscal.Administrar`): se asignan por caja y tipo (E31, E32, E34, E44 y E45) con inicio, fin y vencimiento. Los rangos del mismo tipo no se solapan entre cajas de la empresa; un rango ya asignado no cambia de caja, tipo ni inicio y solo se amplía, se prorroga o se desactiva, porque la caja pudo haber emitido hasta su final. La lista muestra el último e-NCF recibido de cada rango y cuánto queda.
- *Usuarios y roles de caja* (permiso `Central.UsuariosCaja.Administrar`): roles con nivel (1 a 9) y permisos del catálogo de la caja; usuarios con rol, cajas que operan y clave (obligatoria al crear, con el largo mínimo de `Central.Seguridad.LargoMinimoContrasena`). La clave se publica solo como hash en el formato de la caja; editar sin clave nueva conserva la actual. Los códigos no cambian.
- Los rangos, roles y usuarios pasan por las mismas validaciones que la publicación de maestros y bajan a las cajas en su próxima sincronización.
- *Catálogos de maestros* (permiso `Central.Maestros.Administrar`): monedas, tasas de cambio, departamentos, categorías (cada una de un departamento), marcas, unidades de medida, impuestos, formas de pago, denominaciones, bancos, tipos de tarjeta, motivos de descuento y de devolución, almacenes, **niveles de fidelidad**, **reglas de acumulación de puntos** y **descuentos por tarjeta** (RF-98). Cada registro se guarda en el formato de carga de la caja y pasa por las reglas de su dominio y por las referencias ya publicadas (una forma de pago usa una moneda publicada, un almacén una sucursal existente). Lo que la caja no deja cambiar tampoco se deja en el Central: el código de artículos, monedas, promociones, niveles y reglas de fidelidad y almacenes; el tipo de una forma de pago; la moneda, el valor y el tipo de una denominación. No se borran: se desactivan.
- **Maestros en tablas:** cada maestro del Central tiene su tabla, con llaves, índices únicos, versión de fila y quién y cuándo lo cambió; los precios del artículo van en su misma fila. Las cajas bajan lo cambiado por versión.
- **Identificación por código:** los Id son internos de cada base y nunca viajan entre la caja y el Central. Cada maestro se identifica por su código: texto en artículos, clientes, impuestos, monedas, bancos, formas de pago, promociones, descuentos por tarjeta, almacenes, roles y usuarios; **número** en departamentos, categorías, marcas, unidades de medida (con su abreviatura, p. ej. `UND`), tipos de tarjeta, motivos de descuento y de devolución, niveles de fidelidad, reglas de acumulación y topes de descuento (el Manager sugiere el siguiente). Sucursales y cajas se numeran de 1 a 99. Lo que no tiene código se identifica por su llave natural: denominación (moneda, valor y tipo), tasa de cambio (moneda y fecha de vigencia), rango de e-CF (tipo y desde), miembro de fidelidad (cédula), parámetro (clave y ámbito), empresa (RNC) y dirección de un cliente (alias). El código no cambia después de creado: crear (POST) con un código existente se rechaza y cambiar (PUT) uno que no existe responde 404.
- **Documentos por número:** lo que la caja envía (`DocumentosCaja` en `CgPos.Contratos`) tampoco lleva Id: la factura, la nota de crédito y el pendiente van por su número; el cierre, el retiro y el relevo por el número del turno en la caja; la inscripción de fidelidad por la cédula; un movimiento de puntos por el documento que lo originó y su tipo; y cada referencia (artículo, forma de pago, cliente, almacén, usuario) por su código. El Central da sus propios Id a lo que registra y rechaza como conflicto un documento cuyo número es de otra caja.
- **Clasificación de artículos:** departamento (obligatorio) → categoría (opcional, siempre del departamento del artículo) y marca (opcional, independiente del departamento). El código interno, los códigos de barras y los de proveedor son únicos en toda la empresa, entre todos ellos: la caja encuentra el artículo por cualquiera.
- *Artículos* (permiso `Central.Maestros.Administrar`): búsqueda paginada en el servidor por código, código de barras, referencia o descripción (sin distinguir acentos solo si la colación de la base no los distingue); alta con precios iniciales y edición de datos, departamento, categoría y marca (opcionales), unidad, impuesto, códigos de barras y de proveedor, peso del empaque (se descuenta al pesar) y presentación. Un artículo ya publicado conserva sus precios aunque se guarde desde aquí.
- *Precios* (permiso `Central.Precios.Administrar`): detalle, por mayor con su cantidad mínima, mínimo y costo, de inmediato o desde una fecha y hora. Todas las cajas registran el cambio en su bitácora con la misma vigencia. *Topes de descuento* por nivel del autorizador, generales, por departamento o por artículo; no puede haber dos del mismo nivel y alcance, y un tope se retira dejándolo en 0.
- *Clientes* (permiso `Central.Maestros.Administrar`): búsqueda paginada por documento, nombre, teléfono o correo; comprobante predeterminado, lista de precios, exoneración de ITBIS, retención y direcciones de envío con su principal. El mismo documento, con o sin guiones, es un solo cliente. **Corregir el documento** (tipo y número mal digitados) requiere el permiso `Central.Clientes.CorregirDocumento` y un motivo: se valida el formato y el dígito verificador, que ningún otro cliente lo tenga, queda en la auditoría con el documento anterior y el nuevo, y baja a las cajas como el mismo cliente (`POST /api/maestros/clientes/{id}/documento`). Las facturas, e-CF, notas de crédito y pendientes ya emitidos conservan los datos del cliente con que se emitieron.
- *Promociones* (permiso `Central.Promociones.Administrar`): porcentaje, monto por unidad, precio especial, lleva X paga Y y precio desde una cantidad; por artículos, departamentos, categorías o marcas, en todas o algunas sucursales, con vigencia, días, horario (puede cruzar la medianoche), límite de unidades y solo para fidelidad. El Central valida que existan los artículos, departamentos y sucursales. La lista muestra el estado (vigente, programada, vencida, inactiva) y la **distribución**: cuántas cajas habilitadas de sus sucursales ya confirmaron tener esa versión.
- *Importación de promociones* desde CSV (separador `;` o `,`): columnas obligatorias `codigo`, `nombre`, `tipo`, `desde`, `hasta`; opcionales `valor`, `articulos`, `departamentos`, `categorias`, `marcas`, `sucursales` (códigos separados por `|`), `lleva`, `paga`, `cantidad_minima`, `limite_cliente`, `dias` (`todos` o `lun|mar|…`), `hora_desde`, `hora_hasta`, `solo_fidelidad`, `activa`. Las fechas se leen en la hora local de quien importa y un `hasta` sin hora incluye todo el día. Se valida el archivo completo y solo se publica si ninguna línea tiene errores; un código existente actualiza esa promoción.
- *Simulación*: para un artículo, cantidad, sucursal, fecha y hora, con o sin fidelidad, muestra cada promoción que lo alcanza, por qué aplica o no y cuál elegiría la caja: la de mayor descuento, solo si deja el importe por debajo del precio por mayor.
- **API de promociones:** `GET /api/promociones`, `PUT /api/promociones/{id}`, `POST /api/promociones/importar`, `POST /api/promociones/simular`; referencias `GET /api/promociones/articulos`, `POST /api/promociones/articulos/por-id`, `GET /api/promociones/departamentos` y `GET /api/promociones/sucursales`.
- **API de artículos y precios:** `GET /api/maestros/clientes?buscar=&pagina=&tamano=` y `PUT /api/maestros/clientes/{id}`; `GET /api/maestros/articulos?buscar=&pagina=&tamano=` y `PUT /api/maestros/articulos/{id}`; `GET /api/precios/articulos?buscar=&pagina=&tamano=` y `PUT /api/precios/articulos/{id}`; `GET /api/precios/departamentos`; `GET /api/precios/topes` y `PUT /api/precios/topes/{id}`.
- **API de maestros:** `GET /api/maestros/{catálogo}` y `PUT /api/maestros/{catálogo}/{id}` (crea o cambia el registro con ese Id) para `monedas`, `tasas-cambio`, `departamentos`, `categorias`, `marcas`, `unidades-medida`, `impuestos`, `formas-pago`, `denominaciones`, `bancos`, `tipos-tarjeta`, `motivos-descuento`, `motivos-devolucion`, `almacenes`, `niveles-fidelidad`, `reglas-acumulacion` y `descuentos-tarjeta`; `GET /api/maestros/sucursales` como referencia.
- **API de cajas:** `GET|POST /api/fiscal/secuencias`, `PUT /api/fiscal/secuencias/{id}`; `GET /api/usuarios-caja/permisos`; `GET|POST /api/usuarios-caja/roles`, `PUT /api/usuarios-caja/roles/{id}`; `GET|POST /api/usuarios-caja/usuarios`, `PUT /api/usuarios-caja/usuarios/{id}`.

### Envío de e-CF a la DGII

- El Central envía a la DGII los e-CF que recibe de las cajas y consulta su resultado (RF-222, RN-18). Un trabajador en segundo plano toma los pendientes cuyo próximo intento ya llegó, los envía y guarda el trackId; después consulta el resultado hasta obtener **aceptado**, **aceptado condicional** o **rechazado**. Los rechazos y las aceptaciones condicionales quedan en la auditoría con su motivo.
- Un envío que no llega (sin conexión, autenticación, error del servicio) sigue pendiente y se reintenta con espera creciente: se duplica desde `Central.Dgii.MinutosReintento` hasta `Central.Dgii.MinutosMaximoReintento`. Cada comprobante guarda sus intentos y el último mensaje.
- **Cliente:** `Dgii:Cliente = Http` (predeterminado) usa la DGII real: pide la semilla, la firma con el certificado del emisor (`Dgii:Certificado:Ruta` y `Dgii:Certificado:Pin` en la configuración segura, nunca en la base de datos), obtiene el token y envía cada XML firmado. `Simulado` (solo en desarrollo) recibe todo y lo acepta. **Por confirmar en la certificación con la DGII (TesteCF):** formatos exactos de las respuestas; las direcciones ya son parámetros.
- **Sin reenvíos duplicados:** tras un fallo de comunicación, antes de reenviar se busca el comprobante en la DGII (por e-NCF con `Central.Dgii.UrlConsultaTrackIds`; el resumen de consumo, por e-NCF y código de seguridad con `Central.Dgii.UrlConsultaConsumo`). Si ya llegó, se retoma su trackId o su resultado.
- **Anulación de e-NCF no utilizados (ANECF):** en *Rangos de e-CF*, un rango desactivado o vencido muestra el botón de anular. Se elige el tramo y el motivo; no se permite si la caja ya envió un e-CF de ese tramo o si solapa otra anulación aceptada. El Central firma el ANECF y lo envía (`Central.Dgii.UrlAnulacion`); cada intento queda registrado con la respuesta de la DGII y en la auditoría.
- **Retorno del estado a la caja (RF-223):** cada resultado de la DGII baja en la sincronización de maestros a la caja que emitió el e-CF (y solo a ella), por versión de fila como los demás maestros. La caja actualiza su documento y su historial de estados, y la barra fiscal alerta al cajero si tiene e-CF rechazados.
- `Dgii:TrabajadorHabilitado = false` desactiva el trabajador (las pruebas ejecutan el despacho a demanda).

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Dgii.Habilitado` | Envía a la DGII los e-CF recibidos; sin él no se envía nada | No |
| `Central.Dgii.UrlSemilla` / `UrlValidarSemilla` | Autenticación: semilla y validación de la semilla firmada | Con el cliente Http |
| `Central.Dgii.UrlRecepcion` / `UrlConsultaResultado` | Recepción de e-CF y consulta de su resultado por trackId | Con el cliente Http |
| `Central.Dgii.UrlConsultaTrackIds` | Busca los envíos de un e-NCF para no reenviarlo | Con el cliente Http |
| `Central.Dgii.UrlRecepcionConsumo` / `UrlConsultaConsumo` | Recepción y consulta de los resúmenes de consumo (RFCE) | Con el cliente Http |
| `Central.Dgii.UrlAnulacion` | Anulación de rangos de e-NCF no utilizados (ANECF) | Para anular |
| `Central.Dgii.SegundosCiclo` | Segundos entre ciclos de envío y consulta | Sí |
| `Central.Dgii.LoteEnvio` | e-CF enviados y resultados consultados por ciclo | Sí |
| `Central.Dgii.MinutosReintento` | Espera tras el primer envío fallido | Sí |
| `Central.Dgii.MinutosMaximoReintento` | Espera máxima entre reintentos | Sí |
| `Central.Dgii.SegundosConsultaEstado` | Segundos entre consultas del resultado | Sí |

### Monitor de sincronización (Central Manager)

- Permiso `Central.Sincronizacion.Monitorear`. **Monitor:** por caja, la última comunicación (mensajes y descargas de maestros), mensajes recibidos, repetidos y rechazados, e-CF sin resultado y rechazados por la DGII, y alertas: nunca se comunicó o lleva más de `Central.Monitor.MinutosSinComunicacion` minutos sin hacerlo, su último mensaje fue rechazado, e-CF sin resultado de la DGII por más de `Central.Monitor.MinutosAlertaDgii` minutos, e-CF rechazados, y conflictos abiertos. Resumen de e-CF por estado, envíos con fallo y el pendiente más antiguo. La pantalla se actualiza sola cada minuto.
- **e-CF y DGII:** búsqueda por e-NCF o trackId, estado, caja y envíos con fallo; detalle con el mensaje de la DGII, los intentos y el XML firmado. **Reenvío dirigido:** un e-CF rechazado o pendiente vuelve a la cola de inmediato (queda en la auditoría); uno aceptado o en proceso no se reenvía.
- **Conflictos:** abiertos y resueltos; resolver exige escribir la resolución y queda con el usuario y la fecha.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Monitor.MinutosSinComunicacion` | Minutos sin mensajes ni descargas tras los que una caja habilitada es alerta | Sí |
| `Central.Monitor.MinutosAlertaDgii` | Minutos sin resultado de la DGII tras los que un e-CF es alerta | Sí |

- **API:** `GET /api/monitor`; `GET /api/monitor/comprobantes?estado=&cajaId=&buscar=&soloConFallo=&pagina=&tamano=`, `GET /api/monitor/comprobantes/{id}/xml`, `POST /api/monitor/comprobantes/{id}/reenviar`; `GET /api/monitor/conflictos?abiertos=`, `POST /api/monitor/conflictos/{id}/resolver`.

### Chequeador de precios (RF-95)

- **Página `/chequeador` del Central**, pensada para la pantalla que se pone en la tienda: el cliente pasa el código por el lector y ve la descripción, el precio grande, el precio por cantidad (desde cuántas unidades) y las **ofertas vigentes** con hasta cuándo duran. La consulta se borra sola a los 20 segundos y el campo queda enfocado para el siguiente cliente.
- **Busca por código interno, de barras, de proveedor o referencia**, y consulta siempre al Central, así que muestra el precio y la oferta del momento sin depender de una caja. Si hay varias sucursales, se elige la del equipo para las ofertas que son de una sola sucursal.
- **Sin sesión, y por eso apagado por defecto:** la consulta (`GET /api/chequeador/articulos/{codigo}`) no pide usuario porque el equipo está a la vista del público, así que solo responde con `Central.Chequeador.Habilitado` encendido y **solo entrega descripción, precio y ofertas** (nunca costo, existencia ni datos internos). Apagado, la página lo avisa y no consulta nada. Las ofertas de fidelidad no se anuncian, porque no son para todo el mundo.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Chequeador.Habilitado` | Enciende la página de consulta de precios de la tienda | No (apagado por defecto) |

### Facturas de las cajas (M16)

- **Pantalla `/facturas`** (permiso `Central.Reportes.Consultar`): todas las facturas y notas de crédito que las cajas subieron al Central, con filtros por rango de días, sucursal, tipo de documento y búsqueda por número, e-NCF, documento o nombre del cliente; muestra el total de lo filtrado y el estado del e-CF en la DGII de cada una.
- **Detalle:** al abrir una se ven sus **líneas tal como las cobró la caja** (código, descripción, cantidad, precio, descuento, ITBIS, importe, serial y oferta aplicada), los totales, el ITBIS por tasa y las formas de pago; en una nota de crédito, además, el comprobante que modifica. Las líneas se guardan al recibir el documento, así que no hace falta abrir el XML; el e-CF firmado se descarga desde el monitor de comprobantes.
- **API:** `GET /api/manager/facturas?desde=&hasta=&sucursalId=&cajaId=&tipo=&buscar=&pagina=&tamano=` y `GET /api/manager/facturas/{id}`.

### Cotizaciones

- **Se hacen en el Central** (`/cotizaciones`, permiso `Central.Cotizaciones.Administrar`): el mostrador que solo cotiza no necesita instalar nada, le basta el navegador. Se elige el cliente, se buscan los artículos en el maestro —por código, barras, referencia o descripción— y cada línea trae **el precio del día**, que se puede cambiar a mano y admite descuento. El Central la numera (`COT000001`).
- **Precios congelados:** lo cotizado se respeta mientras la cotización esté vigente, aunque el artículo suba. La vigencia sale de `Central.Cotizaciones.DiasVigencia` y se puede fijar a mano.
- **El documento** se descarga en PDF tamaño carta, con el encabezado de la empresa, los datos del cliente, el detalle, los totales, hasta cuándo vale y las condiciones (`Central.Cotizaciones.Condiciones`). Se genera sin librerías externas, igual que los reportes.
- **En la caja (F9):** se escanea o digita el número, la caja **consulta al Central** (las cotizaciones no bajan a la caja) y vuelca las líneas con sus precios: esas líneas **no reciben ofertas ni se recalculan**, porque el precio ya se prometió por escrito. Una ya facturada o anulada se rechaza; una **vencida** se puede facturar con autorización de un supervisor (`Ventas.FacturarCotizacionVencida`), que queda auditada. No se monta sobre una venta que ya tiene artículos, y si el artículo no está en esa caja se avisa que sincronice los maestros.
- **Al cobrar**, el número viaja en `Venta.Cobrada`, sale en el ticket, y el Central marca la cotización **facturada** con el número de la factura; el mensaje repetido no la duplica. Si la cotización no existe allá, queda un conflicto de sincronización.
- **El total de la cotización y el de la factura coinciden al centavo:** los dos separan el ITBIS con la misma regla (`CalculoImpuestos`, RF-183).
- **API:** `GET /api/cotizaciones/{numero}` (caja); `GET/POST/PUT /api/manager/cotizaciones`, `POST /api/manager/cotizaciones/{id}/anular`, `GET /api/manager/cotizaciones/{id}/pdf`, `GET /api/manager/cotizaciones/articulos`.

### Listas de boda y de regalos (RF-73)

- **Se crean en el Central** (`/listas-boda`, permiso `Central.ListasBoda.Administrar`): datos del cliente (cédula o RNC, teléfono, correo), del evento (nombre, fecha, lugar) y los artículos pedidos con su cantidad. El Central numera la lista (`LB000001`) y es el número que el cliente da en la tienda. La lista se cierra cuando pasa el evento y se puede reabrir.
- **En la caja (F6):** se digita el número y la caja **consulta al Central** (las listas no se guardan en la caja, así que esto requiere conexión); si existe y está abierta queda asociada a la venta, se ve en el encabezado y sale en el ticket. Una lista cerrada o inexistente se rechaza con su motivo, y vaciar el número la quita.
- **Al cobrar**, la factura viaja al Central con el número de la lista: la compra queda en su historial (factura, caja, monto y fecha) y, **si `Central.ListasBoda.DescontarCompras` está activo**, lo comprado baja las cantidades pedidas; con el parámetro apagado la lista solo guía al cliente. Una factura se registra una sola vez aunque el mensaje se repita, y si la lista no existe en el Central queda un conflicto de sincronización.
- **API:** `GET /api/listas-boda/{numero}` (caja); `GET/POST/PUT /api/manager/listas-boda`, `POST /api/manager/listas-boda/{id}/estado?cerrar=`, `GET /api/manager/listas-boda/configuracion`.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.ListasBoda.DescontarCompras` | Lo comprado baja las cantidades pedidas de la lista | No (apagado, solo guía) |

### Notas de crédito entre sucursales

- Cada nota de crédito que emite una caja se registra en el Central al sincronizar, con su total, cliente y vencimiento. Cualquier caja puede consultar su saldo por e-NCF o número, aunque se haya emitido en otra sucursal (RF-43).
- **Reserva:** antes de cobrar con una nota, la caja pide retener el monto para la factura que cobra; el Central bloquea la fila y entrega lo que haya disponible (o menos, y lo avisa). Reintentar el cobro de la misma factura reemplaza su reserva. La reserva vence sola a los `Central.NotasCredito.MinutosReserva` minutos y la caja puede liberarla si no cobra. Así dos cajas no consumen el mismo saldo.
- **Consumo:** el mensaje de la caja descuenta el saldo y cierra la reserva de esa factura. Una factura consume una nota una sola vez, aunque el mensaje llegue repetido. Un consumo puede llegar antes que la emisión (son cajas distintas): se guarda igual y el saldo se ajusta al registrarla.
- **Sobregiro:** si varias cajas sin conexión consumen más que el total, la nota queda marcada como sobregirada para revisarla con las sucursales; el Central no descarta lo que la caja ya cobró.
- **Central Manager** (permiso `Central.NotasCredito.Administrar`): listado con búsqueda por e-NCF, número, documento o nombre, filtros por estado y sobregiradas, detalle con sus movimientos (consumos y reservas) y su vencimiento calculado con los días configurados. Una nota vencida (RF-40) se habilita **subiendo los días de vigencia** (`Devoluciones.DiasVigenciaNotaCredito`), que aplica a todas las notas; no hay prórroga por nota.
- **En la caja:** al cobrar con la forma de pago *Nota de crédito*, si el e-NCF no está en la caja se consulta al Central y se reserva el monto antes de cobrar. Si el Central no responde no se acepta (solo él conoce el saldo de otras sucursales), si el saldo no alcanza se devuelve la reserva y se indica lo disponible, y si el cobro no se completa (pago rechazado, falta de autorización o falla del e-CF) la reserva se libera. Cobrada la venta, el consumo sale en la bandeja de salida como `NotaCredito.Consumida`.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.NotasCredito.MinutosReserva` | Minutos que se retiene el saldo mientras la caja cobra | Sí |

- **API de las cajas:** `GET /api/notas-credito/{codigo}` (e-NCF o número, sin Id del Central), `POST /api/notas-credito/{numero}/reservas` (con el número de la factura), `DELETE /api/notas-credito/{numero}/reservas/{factura}`. **API del Manager:** `GET /api/manager/notas-credito?buscar=&estado=&soloSobregiradas=`, `GET /api/manager/notas-credito/{id}/movimientos`, `POST /api/manager/notas-credito/{id}/prorrogar`.

### Programa de fidelidad en el Central

- El saldo de puntos lo lleva el Central: cada acumulación, canje o reverso que hace una caja llega en `Fidelidad.MovimientoPuntos` y se registra por la cédula, el documento que lo originó y su tipo, así que un reenvío no acumula dos veces (RF-240). Un movimiento que llega antes que la inscripción se suma al registrarla.
- **Vencimiento (RF-242):** cada acumulación guarda hasta cuándo valen sus puntos. Un canje gasta primero los puntos que vencen antes, para que el cliente no pierda los que pudo usar; los vencidos salen del saldo y quedan a la vista como "vencidos".
- **Publicación:** cada vez que el saldo cambia se reescribe el maestro del miembro (saldo, puntos por vencer y próximo vencimiento), que es como llega a todas las cajas para canjear sin conexión. Un movimiento que llega antes que la inscripción se suma igual: al publicarse el miembro sale ya con su saldo.
- **Vencimiento sin movimientos:** un trabajo en segundo plano recalcula y republica los miembros cuyos puntos ya vencieron, para que la caja no muestre puntos que no valen.
- **Central Manager** (permiso `Central.Fidelidad.Administrar`): miembros con su saldo, filtro de los que tienen puntos, detalle con todos los movimientos de todas las sucursales y **ajuste manual** a favor o en contra, con motivo y responsable, que queda en la auditoría (`Fidelidad.PuntosAjustados`) y baja a las cajas. Un ajuste en contra no deja el saldo en negativo.
- **Saldo en negativo:** si dos cajas sin conexión canjean más de lo que el Central ve, el saldo queda corto hasta que lleguen los demás mensajes; no se descarta lo que la caja ya entregó.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Fidelidad.MinutosCicloVencimiento` | Minutos entre revisiones de los puntos ya vencidos | Sí |
| `Central.Fidelidad.LoteVencimiento` | Máximo de miembros recalculados por ciclo | Sí |

- **API del Manager:** `GET /api/manager/fidelidad/miembros?buscar=&soloConPuntos=`, `GET /api/manager/fidelidad/miembros/{id}`, `GET /api/manager/fidelidad/miembros/{id}/movimientos`, `POST /api/manager/fidelidad/miembros/{id}/ajustes`.

### Despacho en el Central

- **El Central despacha, la caja solo crea:** el pendiente llega en `Entregas.PendienteCreado` y a partir de ahí lo opera el Central, que guarda el documento completo con sus líneas y entregas. Un reenvío del mensaje de la caja **no lo pisa**: borraría las entregas ya registradas.
- **Central Manager** (permiso `Central.Despacho.Operar`): tablero con abiertos, atrasados, retiros, envíos y entregados hoy; listado de todas las sucursales ordenado por fecha comprometida (los atrasados primero), filtros por estado, método, solo abiertos y solo atrasados, y búsqueda por pendiente, factura, cliente, teléfono o destino.
- **Preparación (RF-252):** en preparación → preparado → despachado (esto último solo en envíos). No se saltan pasos.
- **Entrega (RF-253, RF-254):** total o parcial, línea por línea, con el serial de los serializados y el nombre y la cédula de quien recibe. Una entrega parcial deja el pendiente en `Parcial` con lo que falta; al completarse queda `Entregado`. De cada entrega sale una **constancia en PDF tamaño carta** para firmar: el almacén imprime en una impresora normal, no en la de tickets de 42 columnas.
- **Anulación (RF-255):** solo si no se ha entregado nada, con motivo, y con su **propio permiso** `Central.Despacho.Anular` — libera mercancía ya facturada.
- **Lo pendiente no se devuelve (RF-233):** cuando una caja le pide una factura para devolverla, el Central le descuenta lo que todavía está en el almacén, igual que lo ya devuelto. Para devolverlo hay que anular el pendiente primero. La caja no lo comprueba por su cuenta: su copia del pendiente se queda en el estado en que nació.
- **Aviso al cliente (RF-256):** con `Central.Despacho.AvisarPreparado` activo y el correo de la empresa configurado, el Central le escribe al cliente cuando su pedido queda preparado, usando el correo del maestro de clientes. A quien no tiene correo registrado se le marca para llamarlo por teléfono; si el servidor de correo falla, el aviso se reintenta en el próximo ciclo.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Correo.Servidor`, `Central.Correo.Puerto`, `Central.Correo.UsarTls`, `Central.Correo.Usuario`, `Central.Correo.Remitente`, `Central.Correo.NombreRemitente` | Servidor SMTP de la empresa (la contraseña va en `Correo:Contrasena` de la configuración del servidor, nunca en los parámetros) | No |
| `Central.Despacho.AvisarPreparado` | Activa el aviso por correo al cliente | No |
| `Central.Despacho.MinutosCicloAvisos`, `Central.Despacho.LoteAvisos` | Cada cuánto se revisan los pedidos preparados y cuántos se avisan por ciclo | Sí |
- **API del Manager:** `GET /api/manager/despacho/pendientes?buscar=&estado=&metodo=&sucursalId=&soloAtrasados=&soloAbiertos=`, `GET /api/manager/despacho/pendientes/{id}`, `GET /api/manager/despacho/resumen`.

### Reportes del Central

- El Central arma un modelo de lectura con lo que informan las cajas: cada venta cobrada, cada nota de crédito (en negativo) y cada cierre de turno. Los reportes salen de ahí, no de recorrer los mensajes recibidos.
- **Reportes** (permiso `Central.Reportes.Consultar`), todos por rango de días de operación y opcionalmente por sucursal o caja:
  - **Ventas:** por día, sucursal y caja, con facturas, notas de crédito, subtotal, descuento, ITBIS y total.
  - **ITBIS por tasa:** base e impuesto de cada tasa del período.
  - **Formato 607:** un registro por comprobante con e-NCF, e-NCF modificado, RNC o cédula del cliente, monto e ITBIS. Además del listado, se descarga el **archivo de envío** (una línea por comprobante, campos separados por `|`, con el RNC de la empresa y el período).
  - **Cuadres de caja:** turno, cajero, esperado, declarado y diferencia. Un cierre reabierto y vuelto a cerrar actualiza su fila.
  - **e-CF y DGII:** e-NCF, estado, trackId y mensaje de la DGII.
  - **Sincronización:** última comunicación, mensajes, rechazos, conflictos y alertas de cada caja.
- **Exportación:** cada reporte se descarga en **Excel** (.xlsx) y **PDF**, generados en el propio Central sin librerías externas ni internet, con exactamente las mismas filas que la pantalla.
- **API del Manager:** `GET /api/manager/reportes/{tipo}?desde=&hasta=&sucursalId=&cajaId=`, con `/excel` y `/pdf` para descargar, y `GET /api/manager/reportes/formato607/archivo` para el archivo de la DGII.

### Cierre consolidado de sucursal

- **Qué es (RF-264):** al terminar el día, el Central agrupa los cierres de turno de todas las cajas de la sucursal: ventas, lo esperado, lo declarado y la diferencia, y las formas de pago sumadas. Del efectivo declarado (moneda local y extranjera) sale **lo que se debe depositar por moneda**; tarjetas, transferencias y demás no se depositan.
- **Cuándo se puede:** solo si todos los turnos que vendieron ese día ya informaron su cierre y ninguno quedó reabierto sin volver a cerrarse; si no, la pantalla lista lo que falta. Se hace **una vez por sucursal y día** y no se modifica.
- **Depósitos:** se registran uno o varios por moneda con banco (del maestro de bancos), número de boleta, monto y fecha. La diferencia (depositado menos lo que había que depositar) queda guardada y visible, igual que la diferencia de los cierres de caja.
- **Cierres que llegan tarde:** si una caja informa un cierre de ese día después de consolidar, no cambia el consolidado, pero la lista lo avisa.
- **Central Manager** (permiso `Central.CierresSucursal.Operar`): *Cierre de sucursal* prepara el día elegido, muestra cierres, formas de pago, efectivo a depositar y pendientes, registra los depósitos y cierra; debajo, la lista de cierres hechos con su detalle. Queda en la auditoría (`Reportes.CierreSucursal`).
- **API del Manager:** `GET /api/manager/cierres-sucursal/preparar?sucursalId=&fecha=`, `POST /api/manager/cierres-sucursal`, `GET /api/manager/cierres-sucursal?sucursalId=&desde=&hasta=` y `GET /api/manager/cierres-sucursal/{id}`.

### Instalación y actualización de las cajas

- **Instalar una caja** (`scripts/caja/instalar-caja.ps1`, como administrador): verifica SQL Server Express y el paquete, crea `C:\CGPOS` con Agente, Logs, Xml, Respaldos y Certificado, escribe `appsettings.Production.json` (conexión, códigos de sucursal y caja con `-Sucursal` y `-Caja`, URL y secreto del Central, carpetas) con permisos solo para el servicio y los administradores, copia el `.p12` (el PIN nunca se guarda: lo digita el supervisor en la caja), instala el servicio y comprueba `/salud`. La base de datos la crea y migra el propio Agente al arrancar.
- **Actualizar una caja** (`scripts/caja/actualizar-caja.ps1`): pregunta al Central qué versión hay publicada, descarga el paquete con la credencial de la caja, **verifica su SHA-256**, respalda la versión actual, la reemplaza (la configuración, la base de datos y los XML no se tocan), arranca y comprueba `/salud`; si algo falla, **restaura el respaldo** y deja la caja como estaba. Al terminar informa al Central qué versión quedó.
- **Publicar una versión:** se deja el paquete `cgpos-agente-{versión}.zip` en la carpeta configurada y se indican los parámetros; las cajas no se actualizan solas, cada sucursal corre el script cuando el negocio lo permite.
- **Central Manager** (Organización → Actualización de cajas): versión publicada y qué tiene instalada cada caja, con las pendientes marcadas.

| Parámetro | Uso | Obligatorio |
| --- | --- | --- |
| `Central.Actualizaciones.CarpetaPaquetes` | Carpeta del servidor con los paquetes del Agente | No (sin él no hay actualización remota) |
| `Central.Actualizaciones.VersionPublicada` | Versión que deben instalar las cajas | No |

- **API de las cajas:** `GET /api/actualizaciones/caja`, `GET /api/actualizaciones/caja/paquete`, `POST /api/actualizaciones/caja/version`. **API del Manager:** `GET /api/manager/actualizaciones/cajas`, `GET /api/manager/actualizaciones/publicada`.

### Piloto y despliegue gradual

1. **Central:** instalar, cargar empresa, sucursales, cajas (con su IP) , parámetros y maestros, y emitir la credencial de cada caja.
2. **Una caja piloto:** instalarla con `instalar-caja.ps1`, cargar el certificado con su PIN y verificar venta, e-CF, cierre y sincronización de un día completo.
3. **Revisar en el Central:** monitor de sincronización sin alertas, e-CF aceptados por la DGII, cuadre del día y reporte 607 del período.
4. **Resto de la sucursal:** las demás cajas con el mismo paquete; luego sucursal por sucursal, dejando siempre una caja al día antes de seguir.
5. **Actualizaciones:** publicar la versión en el Central y correr `actualizar-caja.ps1` fuera del horario de venta, empezando por una caja de la sucursal piloto.

### Recepción de documentos de las cajas

- `POST /api/sincronizacion/mensajes` (token de dispositivo): el Central valida que el mensaje sea de la caja autenticada, el SHA-256 del contenido y el del XML del e-CF, y guarda el documento una sola vez. Un reenvío con el mismo contenido responde *Duplicado* y la caja lo da por confirmado; lo rechazado responde 422 y la caja reintenta más tarde.
- Los e-CF recibidos quedan pendientes de envío a la DGII (fase H4), con el XML firmado y su hash; un e-NCF se registra una sola vez.
- **Conflictos** (el Central es autoridad sobre maestros y configuración; la caja sobre sus transacciones): otro contenido con el mismo Id de mensaje, hash o XML alterado, mensaje o número de documento de otra caja o e-NCF repetido quedan registrados una vez por mensaje (con sus repeticiones), en auditoría y en el log. Un e-NCF repetido no rechaza la transacción.

### Maestros para las cajas

- **Publicación:** el Central guarda los maestros en el mismo formato de carga que aplica la caja (artículos, precios, clientes, formas de pago, promociones, fidelidad, almacenes, rangos de e-CF, roles y usuarios de caja). Antes de publicar valida cada registro con las reglas del dominio, las referencias (departamento, unidad e impuesto del artículo, moneda, caja, sucursal, nivel) y que los códigos y códigos de barras no se repitan: un maestro inválido no llega a detener a las cajas. Las claves de los usuarios de caja se publican solo como hash, con el formato que verifica la caja.
- **Bajada incremental:** `GET /api/sincronizacion/maestros?desde={versión}` (token de dispositivo) entrega lo cambiado por versión de fila (rowversion) hasta la última versión confirmada; publicar lo mismo no genera versión nueva. Desde 0 es el aprovisionamiento completo de una caja nueva. La respuesta se comprime.
- **Alcance por caja:** de la organización baja solo lo suyo —la empresa, **su** sucursal y **su** caja, con el código de sucursal en la misma fila—, los parámetros generales, los de su sucursal y los suyos (nunca los `Central.*`) y solo sus rangos de e-CF. Los usuarios bajan con sus cajas asignadas, pero en la caja solo queda la asignación a ella. Lo de otras sucursales o cajas (la factura de otra caja para una devolución, por ejemplo) se le pregunta al Central. El estado de cada caja guarda su última descarga y la versión confirmada y entregada.
- **Inscripciones de fidelidad hechas en caja:** se publican como miembros para todas las cajas; si la cédula ya estaba inscrita en el Central se conserva la del Central y queda un conflicto *MiembroDuplicado*.
- **Clientes de la DGII:** el archivo del padrón se carga en el Central como clientes, con `scripts/base-datos/cargar-clientes-dgii.sql`. De ahí en adelante son clientes como cualquier otro y bajan a las cajas con los maestros: la caja no maneja padrón.
- **Bajas:** un parámetro borrado en el Central se borra en la caja: como una baja no viaja en el rango de versiones, cada descarga trae todos los parámetros que hoy aplican a esa caja y la caja elimina lo que sobre.

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
- Los Id son enteros internos de cada base: **cada tabla tiene su propia secuencia** (`SecuenciaArticulos`, `SecuenciaVentas`…), así los números de cada tabla empiezan en 1 y no se mezclan. EF los reserva por bloques (HiLo) al agregar la entidad al contexto, así que una entidad nueva vale 0 hasta entonces y la que la referencia se crea después de agregarla. Entre la caja y el Central solo viajan códigos y números de documento. Siguen siendo Guid, porque no son Id de una base: el Id de cada mensaje de la bandeja de salida (clave de idempotencia en el Central), las autorizaciones de supervisor de un solo uso y las sesiones del Central.
- Todo lo que va al Central se escribe en la bandeja de salida dentro de la misma transacción del documento.
- Seguridad: permisos granulares definidos en `CatalogoPermisos` (caja) y `CatalogoPermisosCentral` (Central); cada permiso es una política de autorización con el mismo nombre.
- Formato RD fijo: `RD$2,175.34` y `dd/MM/yyyy`.
- **Los datos viven en la base, no en archivos:** ni el Central ni las cajas leen JSON de datos, tampoco en desarrollo. La base se crea con su script de estructura, que siembra **la empresa, el administrador, los parámetros de arranque y los catálogos técnicos** (monedas, impuestos, unidades, denominaciones, tipos de tarjeta, motivos y formas de pago); el **RNC de la empresa se revisa en el script antes de instalar**, porque después no se cambia desde el Central. **No siembra artículos, departamentos, clientes, bancos ni promociones**: eso se crea desde el Central Manager y el manual de usuario lo sugiere. Las cajas lo reciben por sincronización. Los JSON de carga quedaron solo como datos de las pruebas (`pruebas/CgPos.Central.Pruebas/Datos`), y si alguien configura `CargaInicial:Archivo`, `CargaInicialCajas:Archivo` o `Maestros:Archivo` fuera de Desarrollo o Pruebas, se ignoran con un aviso en el log.
- **Base de datos:** no hay migraciones de EF. La base se crea con `scripts/base-datos/estructura_base_datos_central.sql` y `scripts/base-datos/estructura_base_datos_pos.sql`, que se regeneran desde el modelo con `python scripts/base-datos/generar-estructura-sql.py` (usa `dotnet ef dbcontext script`, así que el script siempre corresponde al modelo). El script del Central deja el único dato inicial: el usuario `ADMIN` (contraseña `Admin.CGPOS#2026`, que el sistema obliga a cambiar) con todos los permisos, y los parámetros mínimos para poder entrar. Al arrancar, la caja y el Central solo **verifican** que la base exista y tenga sus tablas; si falta algo, avisan con el script que hay que ejecutar. Las pruebas crean sus bases temporales con esos mismos scripts, así que un modelo cambiado sin regenerar el script hace fallar las pruebas.
- **Manual de usuario:** se genera con `python scripts/manual/generar-manual-usuario.py` y queda en `scripts/manual/Manual de usuario CG-POS.docx`, junto al script. El .docx no se edita a mano: se edita el script y se vuelve a generar. Todo cambio que toque pantallas, rutas, teclas, permisos, parámetros visibles o pasos de un flujo se refleja ahí en el mismo trabajo.
