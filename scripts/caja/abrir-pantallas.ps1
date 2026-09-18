<#
.SYNOPSIS
    Abre las pantallas de la caja en modo kiosco, cada una en su monitor.

.DESCRIPTION
    Las pantallas de la caja, cada una en su monitor:
    - Ventas principal   (/)                  : venta con lector, cobro y turnos.
    - Ventas secundaria  (/venta-secundaria)  : táctil, con el catálogo en mosaicos (opcional).
    - Cliente            (/cliente)           : lo que el cliente ve: artículos, total y publicidad.
    - Devoluciones       (/devoluciones)      : opcional, para una estación dedicada.

    Los monitores se numeran de izquierda a derecha empezando en 1; 0 significa "no abrir".
    Cada pantalla usa su propio perfil de Edge para que las ventanas no se mezclen.
    Se puede programar al iniciar sesión de Windows (Programador de tareas o carpeta Inicio).

.EXAMPLE
    .\abrir-pantallas.ps1 -MonitorVentaPrincipal 1 -MonitorCliente 2
.EXAMPLE
    .\abrir-pantallas.ps1 -MonitorVentaPrincipal 1 -MonitorVentaSecundaria 2 -MonitorCliente 3
#>
param(
    [int] $MonitorVentaPrincipal = 1,
    [int] $MonitorCliente = 2,
    [int] $MonitorDevoluciones = 0,
    [int] $MonitorVentaSecundaria = 0,
    [string] $Url = 'http://localhost:5180',
    [string] $Navegador = "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

if (-not (Test-Path $Navegador)) {
    throw "No se encontró Microsoft Edge en '$Navegador'. Indique la ruta con -Navegador."
}

$monitores = @([System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.Bounds.X }, { $_.Bounds.Y })
Write-Host "Monitores detectados: $($monitores.Count)"

function Abrir-Pantalla([string] $ruta, [int] $numero, [string] $perfil) {
    if ($numero -le 0) { return }

    if ($numero -gt $monitores.Count) {
        Write-Warning "El monitor $numero no existe (hay $($monitores.Count)). '$ruta' se abre en el monitor 1."
        $numero = 1
    }

    $area = $monitores[$numero - 1].Bounds
    $datosPerfil = Join-Path $env:LOCALAPPDATA "CgPos\Kiosco\$perfil"

    Start-Process -FilePath $Navegador -ArgumentList @(
        '--kiosk', "$Url$ruta",
        '--edge-kiosk-type=fullscreen',
        '--no-first-run',
        "--user-data-dir=`"$datosPerfil`"",
        "--window-position=$($area.X),$($area.Y)",
        "--window-size=$($area.Width),$($area.Height)"
    )

    Write-Host "Pantalla '$ruta' abierta en el monitor $numero ($($area.Width)x$($area.Height))."
}

Abrir-Pantalla '/' $MonitorVentaPrincipal 'venta-principal'
Abrir-Pantalla '/cliente' $MonitorCliente 'cliente'
Abrir-Pantalla '/devoluciones' $MonitorDevoluciones 'devoluciones'
Abrir-Pantalla '/venta-secundaria' $MonitorVentaSecundaria 'venta-secundaria'
