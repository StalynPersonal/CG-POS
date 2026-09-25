# Descubre como esta conectado un terminal de tarjetas del que no se sabe nada.
#
# Sirve para cualquier marca (CardNet, Azul, otra) y no asume ningun protocolo: solo mira que hay y como reacciona.
# Es el paso previo a implementar un terminal nuevo en el POS.
#
# Uso:
#   .\descubrir-terminal.ps1                      -> lista los puertos serie del equipo
#   .\descubrir-terminal.ps1 -Servidor 10.12.3.140 -> busca puertos abiertos en ese equipo y los sondea
#   .\descubrir-terminal.ps1 -Servidor 10.12.3.140 -Puertos 7060,8080
#
# Lo que reporta:
#   - que puertos TCP acepta conexion
#   - si al conectar el terminal habla primero (algunos saludan solos)
#   - si contesta a un saludo ENQ, y con que
#
# Nada de lo que manda cobra dinero.

[CmdletBinding()]
param(
    [Alias('Host')]
    [string] $Servidor,
    [int[]] $Puertos = @(7060, 1500, 2000, 5000, 6000, 8080, 9100, 10000, 12000, 20002),
    [int] $SegundosEspera = 4
)

$ErrorActionPreference = 'Stop'
$ENQ = 0x05

function Nombre-Byte([int] $valor) {
    switch ($valor) {
        0x02 { 'STX' }; 0x03 { 'ETX' }; 0x04 { 'EOT' }; 0x05 { 'ENQ' }
        0x06 { 'ACK' }; 0x15 { 'NAK' }; 0x16 { 'SYN' }; 0x19 { 'EOM' }; 0x1C { 'FS' }
        default { if ($valor -ge 32) { [char] $valor } else { "<0x{0:X2}>" -f $valor } }
    }
}

function Leer-Todo($flujo, [int] $milisegundos) {
    $recibido = New-Object System.Collections.Generic.List[int]
    $flujo.ReadTimeout = $milisegundos
    $hasta = (Get-Date).AddMilliseconds($milisegundos)
    while ((Get-Date) -lt $hasta) {
        try { $valor = $flujo.ReadByte() } catch { break }
        if ($valor -lt 0) { break }
        $recibido.Add($valor) | Out-Null
        $flujo.ReadTimeout = 700
    }
    return $recibido
}

# ---------------------------------------------------------------- Puertos serie
Write-Host ""
Write-Host "Puertos serie de este equipo" -ForegroundColor Cyan
Write-Host ("-" * 62)
$serie = [System.IO.Ports.SerialPort]::GetPortNames()
if ($serie.Count -eq 0) {
    Write-Host "  ninguno. Si el terminal va por cable serie o USB, conectelo y repita." -ForegroundColor DarkGray
}
else {
    foreach ($puerto in $serie) { Write-Host "  $puerto" -ForegroundColor Green }
    Write-Host ""
    Write-Host "  Si el terminal es de estos, digame cual y lo sondeo por serie." -ForegroundColor DarkGray
}

if (-not $Servidor) {
    Write-Host ""
    Write-Host "Para sondear por red, vuelva a correrlo con -Servidor <direccion del terminal>." -ForegroundColor DarkGray
    exit 0
}

# ---------------------------------------------------------------- Red
Write-Host ""
Write-Host "Puertos abiertos en $Servidor" -ForegroundColor Cyan
Write-Host ("-" * 62)

$abiertos = @()
foreach ($puerto in $Puertos) {
    $socket = New-Object System.Net.Sockets.TcpClient
    try {
        $conexion = $socket.BeginConnect($Servidor, $puerto, $null, $null)
        if ($conexion.AsyncWaitHandle.WaitOne(1200, $false)) {
            try {
                $socket.EndConnect($conexion)
                Write-Host ("  {0,-6} abierto" -f $puerto) -ForegroundColor Green
                $abiertos += $puerto
            }
            catch { }
        }
    }
    catch { }
    finally { $socket.Close() }
}

if ($abiertos.Count -eq 0) {
    Write-Host "  ninguno de los puertos probados." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Si el terminal responde a 'arp -a' pero no abre puertos, casi siempre es que no sabe contestarle" -ForegroundColor Yellow
    Write-Host "  a la direccion de este equipo: revise la mascara de subred del terminal." -ForegroundColor Yellow
    Write-Host "  Si conoce otro puerto, pruebelo con -Puertos 1234" -ForegroundColor Yellow
    exit 2
}

Write-Host ""
Write-Host "Como reacciona cada puerto abierto" -ForegroundColor Cyan
Write-Host ("-" * 62)

foreach ($puerto in $abiertos) {
    Write-Host ("  Puerto {0}:" -f $puerto) -ForegroundColor White
    $socket = New-Object System.Net.Sockets.TcpClient
    try {
        $conexion = $socket.BeginConnect($Servidor, $puerto, $null, $null)
        if (-not $conexion.AsyncWaitHandle.WaitOne(2000, $false)) { continue }
        $socket.EndConnect($conexion)
        $socket.NoDelay = $true
        $flujo = $socket.GetStream()

        # 1. Algunos terminales saludan solos al conectar.
        $espontaneo = Leer-Todo $flujo 2000
        if ($espontaneo.Count -gt 0) {
            $texto = ($espontaneo | ForEach-Object { Nombre-Byte $_ }) -join ''
            Write-Host "    habla primero -> $texto" -ForegroundColor Green
        }
        else {
            Write-Host "    callado al conectar" -ForegroundColor DarkGray
        }

        # 2. Un saludo ENQ, que es el arranque mas comun.
        $flujo.Write([byte[]] @($ENQ), 0, 1); $flujo.Flush()
        $respuesta = Leer-Todo $flujo ($SegundosEspera * 1000)
        if ($respuesta.Count -gt 0) {
            $texto = ($respuesta | ForEach-Object { Nombre-Byte $_ }) -join ''
            Write-Host "    contesta al saludo -> $texto" -ForegroundColor Green
        }
        else {
            Write-Host "    no contesta al saludo" -ForegroundColor DarkYellow
        }
    }
    catch {
        Write-Host ("    " + $_.Exception.Message) -ForegroundColor Red
    }
    finally { $socket.Close() }
}

Write-Host ""
Write-Host "Digame que salio y con eso decido como implementar este terminal." -ForegroundColor Cyan
