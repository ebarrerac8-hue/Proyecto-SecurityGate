# Solo prueba de archivos en un directorio temporal; no inicia el servidor.
$ErrorActionPreference = 'Stop'
$raiz = Join-Path ([IO.Path]::GetTempPath()) ('SecureGate_Lanzador_' + [Guid]::NewGuid().ToString('N'))
$antes = $env:Servidor__DemostracionComportamiento
try {
    $servidor = Join-Path $raiz 'Servidor'
    New-Item -ItemType Directory -Path $servidor -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $servidor 'SecureGate.Servidor.exe') -Value 'Archivo inerte; nunca ejecutar.'
    $lanzador = & (Join-Path $PSScriptRoot 'IniciarLaboratorio.ps1') -Entrega $raiz -PrepararSolo
    $plantilla = Join-Path $PSScriptRoot '..\SecureGate.Entrega\Plantillas\IniciarServidorPublicado.ps1'
    if (!(Test-Path -LiteralPath $lanzador) -or
        (Get-FileHash -LiteralPath $lanzador).Hash -ne (Get-FileHash -LiteralPath $plantilla).Hash) {
        throw 'No se instalo el lanzador oficial faltante.'
    }
    Set-Content -LiteralPath $lanzador -Value '# Lanzador existente del usuario'
    $hashAntes = (Get-FileHash -LiteralPath $lanzador).Hash
    & (Join-Path $PSScriptRoot 'IniciarLaboratorio.ps1') -Entrega $raiz -PrepararSolo | Out-Null
    if ((Get-FileHash -LiteralPath $lanzador).Hash -ne $hashAntes) { throw 'Se sobrescribio el lanzador existente.' }
    if ($env:Servidor__DemostracionComportamiento -ne $antes) { throw 'Preparar altero el modo del proceso.' }
    Remove-Item -LiteralPath (Join-Path $servidor 'SecureGate.Servidor.exe')
    $rechazado = $false
    try { & (Join-Path $PSScriptRoot 'IniciarLaboratorio.ps1') -Entrega $raiz -PrepararSolo | Out-Null }
    catch { $rechazado = $true }
    if (!$rechazado) { throw 'Se acepto una entrega sin servidor.' }
    Write-Host 'CORRECTO: instala lanzador faltante, conserva existente y rechaza entrega incompleta sin iniciar procesos.'
} finally {
    if (Test-Path -LiteralPath $raiz) { Remove-Item -LiteralPath $raiz -Recurse -Force }
}
