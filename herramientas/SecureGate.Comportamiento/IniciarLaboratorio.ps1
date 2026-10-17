param([Parameter(Mandatory=$true)][string]$Entrega)
$ErrorActionPreference = 'Stop'
$script = Join-Path $Entrega 'Servidor\IniciarServidorPublicado.ps1'
if (!(Test-Path -LiteralPath $script)) { throw 'No se encontro el lanzador del servidor publicado.' }
if (Get-Process -Name 'SecureGate.Servidor' -ErrorAction SilentlyContinue) { throw 'Cierre el servidor anterior antes de iniciar el laboratorio.' }
$anterior = $env:Servidor__DemostracionComportamiento
try {
    $env:Servidor__DemostracionComportamiento = 'true'
    Write-Host 'LABORATORIO EXPLICITO: solo cmd.exe del servidor ejecutara acciones benignas predeterminadas.'
    Write-Host 'No se modifican Defender, Control de aplicaciones ni sus exclusiones. Ctrl+C termina esta sesion.'
    & $script
} finally { $env:Servidor__DemostracionComportamiento = $anterior }
