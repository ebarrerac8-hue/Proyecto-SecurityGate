param([Parameter(Mandatory=$true)][string]$Entrega, [switch]$PrepararSolo)
$ErrorActionPreference = 'Stop'
$Entrega = (Resolve-Path -LiteralPath $Entrega).Path
$exe = Join-Path $Entrega 'Servidor\SecureGate.Servidor.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'No se encontro el servidor publicado en la entrega.' }
$script = Join-Path $Entrega 'Servidor\IniciarServidorPublicado.ps1'
if (!(Test-Path -LiteralPath $script)) {
    $plantilla = Join-Path $PSScriptRoot '..\SecureGate.Entrega\Plantillas\IniciarServidorPublicado.ps1'
    if (!(Test-Path -LiteralPath $plantilla -PathType Leaf)) { throw 'Falta la plantilla del lanzador; actualice el repositorio.' }
    Copy-Item -LiteralPath $plantilla -Destination $script -ErrorAction Stop
}
if ($PrepararSolo) { Write-Output $script; return }
if (Get-Process -Name 'SecureGate.Servidor' -ErrorAction SilentlyContinue) { throw 'Cierre el servidor anterior antes de iniciar el laboratorio.' }
$anterior = $env:Servidor__DemostracionComportamiento
try {
    $env:Servidor__DemostracionComportamiento = 'true'
    Write-Host 'LABORATORIO EXPLICITO: solo cmd.exe del servidor ejecutara acciones benignas predeterminadas.'
    Write-Host 'No se modifican Defender, Control de aplicaciones ni sus exclusiones. Ctrl+C termina esta sesion.'
    & $script
} finally { $env:Servidor__DemostracionComportamiento = $anterior }
