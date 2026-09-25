# Prueba el terminal de tarjetas (CardNet Ingenico iCT250) sin abrir la caja.
#
# Un ping y un Test-NetConnection solo dicen que el puerto abre. Esto va mas alla: habla el protocolo y dice en que paso
# se queda, que es lo unico que separa "el terminal no me conoce" de "le estoy hablando en el dialecto equivocado".
#
# Prueba las cuatro maneras de arrancar una conversacion, porque el iCT250 acepta una sola y no esta documentado cual:
#   - con saludo o sin el      : el saludo (ENQ -> ACK -> SYN -> ENQ) viene del protocolo por puerto serie; sobre red
#                                muchas instalaciones reciben la trama directa, sin saludar.
#   - trama pelada o enmarcada : enmarcada es STX ... ETX + LRC. La caja hoy ENVIA pelada pero LEE enmarcada.
#
# Manda una consulta de tarjeta (CS00), que hace que el terminal pida pasar la tarjeta pero NO cobra nada.
#
# Uso:
#   .\probar-terminal-cardnet.ps1
#   .\probar-terminal-cardnet.ps1 -Servidor 10.12.3.140 -Puerto 7060

[CmdletBinding()]
param(
    [Alias('Host')]
    [string] $Servidor = '10.12.3.140',
    [int] $Puerto = 7060,
    [int] $SegundosEspera = 8
)

$ErrorActionPreference = 'Stop'

$STX = 0x02; $ETX = 0x03; $ENQ = 0x05; $ACK = 0x06; $NAK = 0x15; $SYN = 0x16; $FS = 0x1C

function Nombre-Byte([int] $valor) {
    switch ($valor) {
        0x02 { 'STX' }; 0x03 { 'ETX' }; 0x04 { 'EOT' }; 0x05 { 'ENQ' }
        0x06 { 'ACK' }; 0x15 { 'NAK' }; 0x16 { 'SYN' }; 0x19 { 'EOM' }
        default { if ($valor -ge 32) { [char] $valor } else { "0x{0:X2}" -f $valor } }
    }
}

# Lee un byte, o -1 si no llega nada dentro del tiempo.
function Leer-Byte($flujo, [int] $milisegundos) {
    $flujo.ReadTimeout = $milisegundos
    try { return $flujo.ReadByte() } catch { return -1 }
}

# Lee todo lo que llegue durante un rato y lo devuelve como lista de bytes.
function Leer-Todo($flujo, [int] $milisegundos) {
    $recibido = New-Object System.Collections.Generic.List[int]
    $hasta = (Get-Date).AddMilliseconds($milisegundos)
    while ((Get-Date) -lt $hasta) {
        $restante = [int] ((New-TimeSpan -Start (Get-Date) -End $hasta).TotalMilliseconds)
        if ($restante -le 0) { break }
        $valor = Leer-Byte $flujo ([Math]::Min($restante, 1500))
        if ($valor -lt 0) { if ($recibido.Count -gt 0) { break } else { continue } }
        $recibido.Add($valor) | Out-Null
        if ($valor -eq $ETX -or $valor -eq 0x04) { Leer-Byte $flujo 400 | Out-Null; break }
    }
    return $recibido
}

# La trama de consulta de tarjeta: no cobra, solo pide pasarla.
function Trama-Consulta([bool] $enmarcada) {
    $cuerpo = [System.Text.Encoding]::ASCII.GetBytes("CS00") + [byte] $FS
    if (-not $enmarcada) { return $cuerpo }

    # LRC: XOR de todo lo que va desde despues del STX hasta el ETX, ambos incluidos.
    $conEtx = $cuerpo + [byte] $ETX
    $lrc = 0
    foreach ($b in $conEtx) { $lrc = $lrc -bxor $b }
    return @([byte] $STX) + $conEtx + @([byte] $lrc)
}

function Probar([bool] $conSaludo, [bool] $enmarcada) {
    $titulo = "{0,-12} + trama {1}" -f $(if ($conSaludo) { 'con saludo' } else { 'sin saludo' }), $(if ($enmarcada) { 'enmarcada' } else { 'pelada  ' })
    Write-Host ("  {0} : " -f $titulo) -NoNewline

    $socket = New-Object System.Net.Sockets.TcpClient
    try {
        $conexion = $socket.BeginConnect($Servidor, $Puerto, $null, $null)
        if (-not $conexion.AsyncWaitHandle.WaitOne(5000, $false)) {
            Write-Host "no conecta" -ForegroundColor Red
            return $false
        }
        $socket.EndConnect($conexion)
        $socket.NoDelay = $true
        $flujo = $socket.GetStream()

        if ($conSaludo) {
            $flujo.Write([byte[]] @($ENQ), 0, 1); $flujo.Flush()
            $respuesta = Leer-Byte $flujo ($SegundosEspera * 1000)
            if ($respuesta -ne $ACK) {
                $texto = if ($respuesta -lt 0) { 'no contesto el saludo' } else { "contesto $(Nombre-Byte $respuesta) al saludo" }
                Write-Host $texto -ForegroundColor DarkYellow
                return $false
            }
            $flujo.Write([byte[]] @($SYN), 0, 1); $flujo.Flush()
            if ((Leer-Byte $flujo ($SegundosEspera * 1000)) -ne $ENQ) {
                Write-Host "saludo bien, pero no se puso a la escucha" -ForegroundColor DarkYellow
                return $false
            }
        }

        $trama = Trama-Consulta $enmarcada
        $flujo.Write($trama, 0, $trama.Length); $flujo.Flush()

        $recibido = Leer-Todo $flujo ($SegundosEspera * 1000)
        if ($recibido.Count -eq 0) {
            Write-Host "silencio" -ForegroundColor DarkGray
            return $false
        }

        $legible = ($recibido | ForEach-Object { Nombre-Byte $_ }) -join ''
        Write-Host "RESPONDE -> $legible" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host ("error: " + $_.Exception.Message) -ForegroundColor Red
        return $false
    }
    finally {
        $socket.Close()
    }
}

Write-Host ""
Write-Host "Terminal CardNet Ingenico iCT250 en ${Servidor}:${Puerto}" -ForegroundColor Cyan
Write-Host ("-" * 62)

$socket = New-Object System.Net.Sockets.TcpClient
try {
    $conexion = $socket.BeginConnect($Servidor, $Puerto, $null, $null)
    if (-not $conexion.AsyncWaitHandle.WaitOne(5000, $false)) {
        Write-Host "No conecta al puerto. Revise el cable, la direccion y que este equipo tenga una direccion" -ForegroundColor Red
        Write-Host "de la misma subred (10.12.3.x) en su tarjeta Ethernet." -ForegroundColor Red
        exit 1
    }
    $socket.EndConnect($conexion)
    Write-Host ("Conecta al puerto. Esta caja se ve como {0}" -f $socket.Client.LocalEndPoint.Address) -ForegroundColor Green
}
finally { $socket.Close() }

Write-Host ""
Write-Host "Deje el terminal en su pantalla de espera. Si alguna prueba lo despierta, no cobra nada: solo pide la tarjeta."
Write-Host ""

$gano = $null
foreach ($conSaludo in @($true, $false)) {
    foreach ($enmarcada in @($false, $true)) {
        if (Probar $conSaludo $enmarcada) {
            $gano = @{ Saludo = $conSaludo; Enmarcada = $enmarcada }
            break
        }
        Start-Sleep -Milliseconds 400
    }
    if ($gano) { break }
}

Write-Host ""
if ($gano) {
    Write-Host "El terminal habla: saludo=$($gano.Saludo), trama enmarcada=$($gano.Enmarcada)." -ForegroundColor Green
    Write-Host "Digame este resultado y ajusto el POS a esa manera." -ForegroundColor Green
    exit 0
}

Write-Host "El terminal no contesta de ninguna de las cuatro maneras." -ForegroundColor Yellow
Write-Host ""
Write-Host "El puerto abre pero nadie habla. Lo mas probable, en orden:" -ForegroundColor Yellow
Write-Host "  1. La direccion de esta caja no esta autorizada en el terminal (lo configura CardNet)."
Write-Host "  2. El terminal no esta en su pantalla de espera: sacarlo de cualquier menu y repetir."
Write-Host "  3. La aplicacion de integracion no esta activa en el terminal (la enciende CardNet)."
Write-Host "  4. El que escucha en el puerto no es el terminal, sino otro equipo con esa direccion."
exit 2
