#requires -RunAsAdministrator
<#
.SYNOPSIS
    Instala una caja CG-POS completa: carpetas, configuración, certificado, base de datos y el servicio del Agente.

.DESCRIPTION
    Pensado para una caja nueva en una sucursal. Deja el equipo listo para vender:
      1. Verifica SQL Server (Express) y el paquete publicado del Agente.
      2. Crea C:\CGPOS con Agente, Logs, Xml y Respaldos.
      3. Copia el Agente y escribe appsettings.Production.json con la conexión, la caja y el Central. No lleva ningún
         secreto: al arrancar, la caja le pide su credencial al Central y alguien la acepta allá (Solicitudes de cajas).
         La credencial queda cifrada en este equipo y solo sirve aquí.
      4. Copia el certificado .p12 de la empresa (el PIN no se guarda: lo digita el supervisor en la caja).
      5. Instala el servicio con instalar-agente.ps1, lo arranca y comprueba /salud.
    La base de datos NO la crea este script: créela antes con scripts\base-datos\pos\estructura_base_datos.sql.

.EXAMPLE
    .\instalar-caja.ps1 -Paquete C:\temp\cgpos-agente-1.0.0.zip -Sucursal 1 -Caja 1 `
        -UrlCentral https://central.contrerasgroup.com.do -Certificado C:\temp\empresa.p12
#>
param(
    [Parameter(Mandatory = $true)][string] $Paquete,
    [Parameter(Mandatory = $true)][ValidateRange(1, 99)][int] $Sucursal,
    [Parameter(Mandatory = $true)][ValidateRange(1, 99)][int] $Caja,
    [Parameter(Mandatory = $true)][string] $UrlCentral,
    [string] $Certificado,
    [string] $InstanciaSql = '.\SQLEXPRESS',
    [string] $ServicioSql = 'MSSQL$SQLEXPRESS',
    [string] $BaseDatos = 'CgPosCaja',
    [string] $Raiz = 'C:\CGPOS',
    [int] $Puerto = 5180
)

$ErrorActionPreference = 'Stop'

function Escribir($mensaje) { Write-Host "[CG-POS] $mensaje" }

if (-not (Test-Path $Paquete)) { throw "No existe el paquete: $Paquete" }
if (-not (Get-Service -Name $ServicioSql -ErrorAction SilentlyContinue)) {
    throw "No se encontró el servicio de SQL Server '$ServicioSql'. Instale SQL Server Express antes de continuar."
}
if ($Certificado -and -not (Test-Path $Certificado)) { throw "No existe el certificado: $Certificado" }

$carpetaAgente = Join-Path $Raiz 'Agente'
foreach ($carpeta in @($Raiz, $carpetaAgente, (Join-Path $Raiz 'Logs'), (Join-Path $Raiz 'Xml'), (Join-Path $Raiz 'Respaldos'), (Join-Path $Raiz 'Certificado'))) {
    New-Item -ItemType Directory -Force -Path $carpeta | Out-Null
}

Escribir "Extrayendo el Agente en $carpetaAgente"
Expand-Archive -Path $Paquete -DestinationPath $carpetaAgente -Force

$ejecutable = Join-Path $carpetaAgente 'CgPos.Pos.Agente.exe'
if (-not (Test-Path $ejecutable)) { throw "El paquete no contiene CgPos.Pos.Agente.exe" }

if ($Certificado) {
    $destinoCertificado = Join-Path $Raiz 'Certificado\empresa.p12'
    Copy-Item -Path $Certificado -Destination $destinoCertificado -Force
    Escribir "Certificado copiado en $destinoCertificado (el PIN se digita en la caja, no se guarda)"
}

# La configuración de producción: la caja, su credencial ante el Central y dónde viven XML y respaldos.
$configuracion = [ordered]@{
    ConnectionStrings = [ordered]@{
        BaseDatosPos = "Server=$InstanciaSql;Database=$BaseDatos;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
    }
    Kestrel           = [ordered]@{ Endpoints = [ordered]@{ Http = [ordered]@{ Url = "http://localhost:$Puerto" } } }
    Caja              = [ordered]@{ Sucursal = $Sucursal; Codigo = $Caja }
    Central           = [ordered]@{ Url = $UrlCentral; ArchivoCredencial = (Join-Path $Raiz 'credencial-caja.dat') }
    Ecf               = [ordered]@{
        Certificado = [ordered]@{ Ruta = (Join-Path $Raiz 'Certificado\empresa.p12') }
        CarpetaXml  = (Join-Path $Raiz 'Xml')
    }
    Respaldo          = [ordered]@{ Carpeta = (Join-Path $Raiz 'Respaldos') }
}

$rutaConfiguracion = Join-Path $carpetaAgente 'appsettings.Production.json'
$configuracion | ConvertTo-Json -Depth 6 | Out-File -FilePath $rutaConfiguracion -Encoding utf8
Escribir "Configuración escrita en $rutaConfiguracion"

# Solo el servicio y los administradores leen la configuración y la credencial cifrada de la caja.
icacls $rutaConfiguracion /inheritance:r /grant:r "SYSTEM:(R)" "Administrators:(F)" | Out-Null

$env:ASPNETCORE_ENVIRONMENT = 'Production'
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'Machine')

Escribir 'Instalando el servicio del Agente'
& (Join-Path $PSScriptRoot 'instalar-agente.ps1') -RutaEjecutable $ejecutable -ServicioSql $ServicioSql

Escribir 'Comprobando la salud del Agente'
$intentos = 0
do {
    Start-Sleep -Seconds 3
    $intentos++
    try {
        $salud = Invoke-RestMethod -Uri "http://localhost:$Puerto/salud" -TimeoutSec 5
    } catch {
        $salud = $null
    }
} while (-not $salud -and $intentos -lt 10)

if (-not $salud) { throw "El Agente no respondió en http://localhost:$Puerto/salud. Revise C:\CGPOS\Logs." }

Escribir "Caja instalada. Estado: $($salud.estado)"
if ($salud.pendientes) { $salud.pendientes | ForEach-Object { Escribir "  PENDIENTE - $_" } }
Escribir "Siguiente paso: abrir las pantallas con abrir-pantallas.ps1 y cargar el certificado con su PIN desde la caja."
