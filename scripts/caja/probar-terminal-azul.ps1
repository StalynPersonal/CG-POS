# Habla con el terminal Azul (Verifone V200c) por red, para descubrir y probar su protocolo antes de tocar el POS.
#
# El terminal escucha en el puerto 2000 y no usa el saludo ENQ/ACK de CardNet: espera una trama directa. Como la
# especificacion de Azul no esta en el repositorio, este script sirve para dos cosas:
#
#   1. Probar una trama concreta cuando se tenga la documentacion:
#        .\probar-terminal-azul.ps1 -Mensaje "0200|100.00|..." -Enmarcar
#
#   2. Ver en crudo lo que conteste, en hexadecimal y en texto, que es lo que hace falta para implementarlo.
#
# CUIDADO: una trama puede iniciar un cobro de verdad. Este script NO inventa tramas por su cuenta: manda solo lo que
# se le indique. El modo -Sondear envia unicamente lineas vacias y saludos inofensivos para ver si algo responde.
#
# Uso:
#   .\probar-terminal-azul.ps1 -Sondear                        (inofensivo: mira como reacciona)
#   .\probar-terminal-azul.ps1 -Mensaje "TEXTO DE LA TRAMA"
#   .\probar-terminal-azul.ps1 -Mensaje "TEXTO" -Enmarcar      (le pone STX ... ETX + LRC)
#   .\probar-terminal-azul.ps1 -Hex "02 30 32 30 30 03 1A"     (bytes exactos, en hexadecimal)

[CmdletBinding()]
param(
    [Alias('Host')]
    [string] $Servidor = '10.14.3.115',
    [int] $Puerto = 2000,
    [string] $Mensaje,
    [string] $Hex,
    [switch] $Enmarcar,
    [switch] $Sondear,
    [int] $SegundosEspera = 20
)

$ErrorActionPreference = 'Stop'
$STX = 0x02; $ETX = 0x03; $ENQ = 0x05; $LF = 0x0A; $CR = 0x0D

function Mostrar-Bytes($bytes, [string] $titulo) {
    if ($bytes.Count -eq 0) { return }
    $hex = ($bytes | ForEach-Object { "{0:X2}" -f $_ }) -join ' '
    $txt = ($bytes | ForEach-Object { if ($_ -ge 32 -and $_ -lt 127) { [char] $_ } else { '.' } }) -join ''
    Write-Host "    $titulo hex : $hex" -ForegroundColor Gray
    Write-Host "    $titulo txt : $txt" -ForegroundColor White
}

function Leer-Todo($flujo, [int] $milisegundos) {
    $recibido = New-Object System.Collections.Generic.List[int]
    $flujo.ReadTimeout = $milisegundos
    while ($true) {
        try { $valor = $flujo.ReadByte() } catch { break }
        if ($valor -lt 0) { break }
        $recibido.Add($valor) | Out-Null
        $flujo.ReadTimeout = 900   # ya empezo a hablar: se espera menos entre byte y byte
    }
    return $recibido
}

# Le pone el marco STX ... ETX + LRC, que es lo mas comun en terminales de pago.
function Enmarcar-Trama($cuerpo) {
    $conEtx = $cuerpo + [byte] $ETX
    $lrc = 0
    foreach ($b in $conEtx) { $lrc = $lrc -bxor $b }
    return @([byte] $STX) + $conEtx + @([byte] $lrc)
}

function Enviar($descripcion, $bytes) {
    Write-Host ""
    Write-Host "  $descripcion" -ForegroundColor Cyan

    $socket = New-Object System.Net.Sockets.TcpClient
    try {
        $conexion = $socket.BeginConnect($Servidor, $Puerto, $null, $null)
        if (-not $conexion.AsyncWaitHandle.WaitOne(5000, $false)) {
            Write-Host "    no conecta" -ForegroundColor Red
            return
        }
        $socket.EndConnect($conexion)
        $socket.NoDelay = $true
        $flujo = $socket.GetStream()

        # Algunos terminales hablan primero.
        $antes = Leer-Todo $flujo 1500
        if ($antes.Count -gt 0) { Mostrar-Bytes $antes 'saluda' }

        if ($bytes -and $bytes.Count -gt 0) {
            Mostrar-Bytes $bytes 'envio '
            $flujo.Write($bytes, 0, $bytes.Length)
            $flujo.Flush()
        }

        $respuesta = Leer-Todo $flujo ($SegundosEspera * 1000)
        if ($respuesta.Count -eq 0) {
            Write-Host "    sin respuesta en $SegundosEspera s" -ForegroundColor DarkYellow
            return
        }

        Write-Host "    RESPONDE:" -ForegroundColor Green
        Mostrar-Bytes $respuesta 'recibo'
    }
    catch {
        Write-Host ("    " + $_.Exception.Message) -ForegroundColor Red
    }
    finally { $socket.Close() }
}

Write-Host ""
Write-Host "Terminal Azul (Verifone V200c) en ${Servidor}:${Puerto}" -ForegroundColor Cyan
Write-Host ("-" * 62)

if ($Hex) {
    $bytes = [byte[]] (($Hex -split '[\s,]+' | Where-Object { $_ }) | ForEach-Object { [Convert]::ToByte($_, 16) })
    Enviar "Bytes exactos" $bytes
}
elseif ($Mensaje) {
    $cuerpo = [System.Text.Encoding]::ASCII.GetBytes($Mensaje)
    $bytes = if ($Enmarcar) { Enmarcar-Trama $cuerpo } else { $cuerpo }
    Enviar $(if ($Enmarcar) { "Trama enmarcada (STX/ETX/LRC)" } else { "Trama tal cual" }) $bytes
}
elseif ($Sondear) {
    Write-Host "Sondeo inofensivo: nada de esto puede cobrar." -ForegroundColor DarkGray
    Enviar "Solo conectar y escuchar"            @()
    Enviar "Un salto de linea"                   ([byte[]] @($CR, $LF))
    Enviar "Un saludo ENQ"                       ([byte[]] @($ENQ))
    Enviar "Una trama vacia enmarcada"           (Enmarcar-Trama ([byte[]] @()))
    Write-Host ""
    Write-Host "Si ninguno contesta, el terminal espera una trama con su formato propio: hace falta la" -ForegroundColor Yellow
    Write-Host "especificacion de integracion de Azul para el V200c. Con ella, use -Mensaje o -Hex." -ForegroundColor Yellow
}
else {
    Write-Host "Indique -Sondear, -Mensaje o -Hex. Vea los ejemplos al inicio de este archivo." -ForegroundColor Yellow
}

Write-Host ""
