#requires -RunAsAdministrator
<#
.SYNOPSIS
    Actualiza el Agente de esta caja con la versión que publicó el Central, y avisa qué versión quedó instalada.

.DESCRIPTION
    - Pregunta al Central qué versión hay publicada; si es la misma que está instalada, no hace nada.
    - Descarga el paquete y verifica su SHA-256 antes de tocar nada.
    - Respalda la carpeta actual, detiene el servicio, copia la versión nueva y lo arranca.
    - Si /salud no responde, restaura el respaldo y deja la caja como estaba.
    - Al terminar, reporta la versión al Central para seguir el despliegue.
    La base de datos, los XML y la configuración no se tocan: la actualización solo reemplaza el programa.

.EXAMPLE
    .\actualizar-caja.ps1
.EXAMPLE
    .\actualizar-caja.ps1 -Forzar
#>
param(
    [string] $Raiz = 'C:\CGPOS',
    [int] $Puerto = 5180,
    [switch] $Forzar
)

$ErrorActionPreference = 'Stop'
function Escribir($mensaje) { Write-Host "[CG-POS] $mensaje" }

$carpetaAgente = Join-Path $Raiz 'Agente'
$rutaConfiguracion = Join-Path $carpetaAgente 'appsettings.Production.json'
if (-not (Test-Path $rutaConfiguracion)) { throw "No se encontró $rutaConfiguracion. ¿Está instalada la caja?" }

$configuracion = Get-Content $rutaConfiguracion -Raw | ConvertFrom-Json
$urlCentral = $configuracion.Central.Url.TrimEnd('/')
$cajaId = $configuracion.Caja.Id
$secreto = $configuracion.Central.Secreto
if (-not $urlCentral -or -not $cajaId -or -not $secreto) { throw 'La configuración no tiene la caja o la credencial del Central.' }

# Token de dispositivo: la misma credencial con la que la caja sincroniza.
$token = (Invoke-RestMethod -Method Post -Uri "$urlCentral/api/dispositivos/token" -ContentType 'application/json' `
    -Body (@{ cajaId = $cajaId; secreto = $secreto } | ConvertTo-Json)).token
$encabezados = @{ Authorization = "Bearer $token" }

$publicada = Invoke-RestMethod -Uri "$urlCentral/api/actualizaciones/caja" -Headers $encabezados
if (-not $publicada) { Escribir 'El Central no tiene ninguna versión publicada.'; return }

$versionInstalada = (Get-Item (Join-Path $carpetaAgente 'CgPos.Pos.Agente.exe')).VersionInfo.ProductVersion
if (-not $Forzar -and $versionInstalada -eq $publicada.version) {
    Escribir "La caja ya tiene la versión $versionInstalada."
    return
}

Escribir "Descargando la versión $($publicada.version) ($([math]::Round($publicada.tamano / 1MB, 1)) MB)"
$temporal = Join-Path $env:TEMP $publicada.archivo
Invoke-WebRequest -Uri "$urlCentral/api/actualizaciones/caja/paquete" -Headers $encabezados -OutFile $temporal

$hash = (Get-FileHash -Path $temporal -Algorithm SHA256).Hash
if ($hash -ne $publicada.hash) { throw "El paquete descargado no coincide con el hash publicado. No se instaló nada." }

$respaldo = Join-Path $Raiz ("Respaldos\agente-{0:yyyyMMdd-HHmmss}" -f (Get-Date))
Escribir "Respaldando la versión actual en $respaldo"
Copy-Item -Path $carpetaAgente -Destination $respaldo -Recurse -Force

$servicio = 'CgPosAgente'
Escribir 'Deteniendo el servicio'
Stop-Service -Name $servicio -Force

try {
    # La configuración se conserva: solo se reemplaza el programa.
    Get-ChildItem -Path $carpetaAgente -Exclude 'appsettings.Production.json' | Remove-Item -Recurse -Force
    Expand-Archive -Path $temporal -DestinationPath $carpetaAgente -Force
    Start-Service -Name $servicio

    $intentos = 0
    do {
        Start-Sleep -Seconds 3
        $intentos++
        try { $salud = Invoke-RestMethod -Uri "http://localhost:$Puerto/salud" -TimeoutSec 5 } catch { $salud = $null }
    } while (-not $salud -and $intentos -lt 10)

    if (-not $salud) { throw 'El Agente no respondió tras la actualización.' }
} catch {
    Escribir "Falló la actualización: $($_.Exception.Message). Restaurando la versión anterior."
    Stop-Service -Name $servicio -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $carpetaAgente -Exclude 'appsettings.Production.json' | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $respaldo '*') -Destination $carpetaAgente -Recurse -Force -Exclude 'appsettings.Production.json'
    Start-Service -Name $servicio
    throw
}

$nueva = (Get-Item (Join-Path $carpetaAgente 'CgPos.Pos.Agente.exe')).VersionInfo.ProductVersion
Invoke-RestMethod -Method Post -Uri "$urlCentral/api/actualizaciones/caja/version" -Headers $encabezados -ContentType 'application/json' `
    -Body (@{ version = $nueva } | ConvertTo-Json) | Out-Null

Remove-Item $temporal -Force -ErrorAction SilentlyContinue
Escribir "Caja actualizada a la versión $nueva."
