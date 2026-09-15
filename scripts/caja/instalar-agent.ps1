#requires -RunAsAdministrator
<#
.SYNOPSIS
    Instala CG-POS Agent como servicio de Windows en una caja.
.DESCRIPTION
    - Crea el servicio "CgPosAgent" con inicio automático retrasado.
    - Lo hace depender de la instancia de SQL Server Express local.
    - Configura reinicio automático ante fallos.
.EXAMPLE
    .\instalar-agent.ps1 -RutaEjecutable "C:\CGPOS\Agent\CgPos.Pos.Agent.exe"
#>
param(
    [string] $RutaEjecutable = "C:\CGPOS\Agent\CgPos.Pos.Agent.exe",
    [string] $ServicioSql = 'MSSQL$SQLEXPRESS'
)

$ErrorActionPreference = 'Stop'
$nombre = 'CgPosAgent'

if (-not (Test-Path $RutaEjecutable)) { throw "No existe el ejecutable: $RutaEjecutable" }
if (Get-Service -Name $nombre -ErrorAction SilentlyContinue) { throw "El servicio $nombre ya está instalado. Use desinstalar-agent.ps1 primero." }
if (-not (Get-Service -Name $ServicioSql -ErrorAction SilentlyContinue)) { throw "No se encontró el servicio de SQL Server '$ServicioSql'." }

New-Item -ItemType Directory -Force -Path 'C:\CGPOS\Logs' | Out-Null

sc.exe create $nombre binPath= "`"$RutaEjecutable`"" start= delayed-auto depend= $ServicioSql DisplayName= "CG-POS Agent" | Out-Null
sc.exe description $nombre "Servicio local de la caja CG-POS: pantallas, API local y sincronizacion con el Central." | Out-Null
# Reinicia a los 5 s, 10 s y 30 s tras cada fallo; el contador se reinicia cada 24 h.
sc.exe failure $nombre reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Start-Service -Name $nombre
Get-Service -Name $nombre | Format-Table Name, Status, StartType -AutoSize
Write-Host "Verifique: http://localhost:5080/salud"
