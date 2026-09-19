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
$sucursal = $configuracion.Caja.Sucursal
$caja = $configuracion.Caja.Codigo
if (-not $urlCentral -or -not $sucursal -or -not $caja) { throw 'La configuración no tiene la caja ni la dirección del Central.' }

# La credencial no está en la configuración: vive cifrada con DPAPI en este equipo, atada a esta máquina. Se descifra aquí
# igual que lo hace el Agente, por eso este script corre como administrador en la propia caja.
$rutaCredencial = if ($configuracion.Central.ArchivoCredencial) { $configuracion.Central.ArchivoCredencial } else { Join-Path $Raiz 'credencial-caja.dat' }
if (-not (Test-Path $rutaCredencial)) { throw "La caja todavía no tiene credencial ($rutaCredencial). Acepte su solicitud en el Central." }

Add-Type -AssemblyName System.Security
$claro = [System.Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($rutaCredencial), $null, 'LocalMachine')
$credencial = [Text.Encoding]::UTF8.GetString($claro) | ConvertFrom-Json
$secreto = $credencial.secreto
if (-not $secreto) { throw 'La caja todavía no recibió su credencial del Central.' }

# La huella se calcula igual que en el Agente: lo que identifica al equipo más el identificador de esta instalación.
$idWindows = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Cryptography' -Name MachineGuid).MachineGuid
$sha = [System.Security.Cryptography.SHA256]::Create()
$huella = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes("$idWindows|$env:COMPUTERNAME|$($credencial.instalacion)")) | ForEach-Object { $_.ToString('X2') })

# Token de dispositivo: la misma credencial con la que la caja sincroniza, y la huella de este equipo.
$token = (Invoke-RestMethod -Method Post -Uri "$urlCentral/api/dispositivos/token" -ContentType 'application/json' `
    -Body (@{ sucursalCodigo = $sucursal; cajaCodigo = $caja; secreto = $secreto; huellaEquipo = $huella } | ConvertTo-Json)).token
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
