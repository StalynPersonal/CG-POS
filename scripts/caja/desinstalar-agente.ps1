#requires -RunAsAdministrator
<#
.SYNOPSIS
    Detiene y elimina el servicio de Windows CG-POS Agente. No borra la base de datos, los XML ni los logs.
#>
$ErrorActionPreference = 'Stop'
$nombre = 'CgPosAgente'

$servicio = Get-Service -Name $nombre -ErrorAction SilentlyContinue
if (-not $servicio) { Write-Host "El servicio $nombre no está instalado."; return }

if ($servicio.Status -ne 'Stopped') { Stop-Service -Name $nombre -Force }
sc.exe delete $nombre | Out-Null
Write-Host "Servicio $nombre eliminado."
