# CG-POS

Sistema de punto de venta **offline-first** para Contreras Group, con facturación electrónica **e-CF** firmada en cada caja y sincronización con un servidor **Central**.

Se construye por fases: primero la **caja** (fases C0–C11) y luego el **Central** (fases H1–H7).

**Estado actual:** Fase C0 (fundaciones de la caja) completada.

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 (SDK fijado en `global.json`) |
| Caja: servicio local | ASP.NET Core `CgPos.Pos.Agent` (servicio de Windows) |
| Caja: pantallas | Blazor WebAssembly + MudBlazor (servido localmente, sin CDN) |
| Caja: base de datos | SQL Server Express 2019+ con EF Core |
| Facturación electrónica | XMLDSig (RSA-SHA256) con `System.Security.Cryptography.Xml` |
| Logs | Serilog |
| Pruebas | xUnit |

## Estructura

```
src/Shared/   CgPos.Domain        Entidades y reglas comunes (formato RD, auditoría)
              CgPos.Contracts     DTOs, estados y formato JSON compartidos caja ↔ Central
              CgPos.ECF           Firma y verificación de e-CF
              CgPos.UI.Kit        Tema verde institucional y componentes táctiles (MudBlazor)
src/POS/      CgPos.Pos.Agent     Único servicio de la caja: API local, pantallas y sincronización
              CgPos.Pos.Application  Casos de uso y abstracciones (Outbox, auditoría)
              CgPos.Pos.Infrastructure  EF Core / SQL Server, migraciones, implementaciones
              CgPos.Pos.Web       Pantallas Blazor WebAssembly (cajero, cliente, devoluciones)
tests/        CgPos.Domain.Tests · CgPos.ECF.Tests · CgPos.Pos.Tests
scripts/caja/ Instalación del Agent como servicio de Windows
```

## Requisitos de desarrollo

- .NET SDK 10.0.401 o superior dentro de la serie 10.
- SQL Server 2019+ (Developer o Express). Con una versión anterior, ajustar el nivel de compatibilidad (ver abajo).
- Herramienta EF Core: `dotnet tool install -g dotnet-ef`.

## Configuración local

Las credenciales **no** van en el repositorio. Se configuran con *user-secrets* (se guardan en el perfil del usuario).

```powershell
# Conexión de la base local de la caja
dotnet user-secrets set "ConnectionStrings:PosDb" "Server=localhost;Database=CgPosCaja;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True" --project src/POS/CgPos.Pos.Agent

# Solo si el SQL Server es anterior a 2019 (ej. 2014 = 120)
dotnet user-secrets set "BaseDatos:NivelCompatibilidad" "120" --project src/POS/CgPos.Pos.Agent
```

En producción, `appsettings.json` usa `.\SQLEXPRESS` con autenticación de Windows.

## Ejecutar la caja

```powershell
dotnet build CgPos.slnx
dotnet run --project src/POS/CgPos.Pos.Agent
```

- Pantallas: <http://localhost:5080>
- Salud del servicio y la base: <http://localhost:5080/salud>
- Logs de desarrollo: `src/POS/CgPos.Pos.Agent/logs/`

Las migraciones se aplican solas al arrancar. Para aplicarlas manualmente:

```powershell
dotnet ef database update --project src/POS/CgPos.Pos.Infrastructure --startup-project src/POS/CgPos.Pos.Agent
```

## Pruebas

```powershell
dotnet test CgPos.slnx
```

Las pruebas de integración crean una base temporal `CgPosPruebas_<guid>` y la eliminan al terminar. Necesitan un servidor configurado; si no lo hay, se omiten.

```powershell
dotnet user-secrets set "ConnectionStrings:ServidorPruebas" "Server=localhost;User Id=<usuario>;Password=<clave>;TrustServerCertificate=True" --project tests/CgPos.Pos.Tests
```

En CI se pueden usar variables de entorno con prefijo `CGPOS_`, por ejemplo `CGPOS_ConnectionStrings__ServidorPruebas`.

## Publicar e instalar en una caja

```powershell
dotnet publish src/POS/CgPos.Pos.Agent -c Release -o C:\CGPOS\Agent
# En la caja, como administrador:
.\scripts\caja\instalar-agent.ps1 -RutaEjecutable "C:\CGPOS\Agent\CgPos.Pos.Agent.exe"
```

El servicio `CgPosAgent` depende de SQL Server Express y se reinicia automáticamente ante fallos. Logs: `C:\CGPOS\Logs`.

## Convenciones

- El código, los nombres y los mensajes están en español.
- El servidor corporativo se llama **Central**.
- Las transacciones usan Id GUID v7.
- Todo lo que va al Central se escribe en el Outbox dentro de la misma transacción del documento.
- Formato RD fijo: `RD$2,175.34` y `dd/MM/yyyy`.
