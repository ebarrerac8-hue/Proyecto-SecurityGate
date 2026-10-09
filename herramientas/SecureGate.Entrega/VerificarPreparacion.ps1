param([string]$Proyecto = (Join-Path $env:USERPROFILE 'source\repos\Proyecto-SecurityGate'))
$ErrorActionPreference = 'Stop'
$config = Join-Path $Proyecto 'herramientas\SecureGate.Entrega\ultima-entrega-local.json'
if (-not (Test-Path -LiteralPath $config)) { throw 'Primero ejecuta PrepararEntrega.ps1.' }
$entrega = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
$procesos = @(Get-Process -Name SecureGate.Servidor -ErrorAction SilentlyContinue)
$esperado = Join-Path $entrega.Servidor 'SecureGate.Servidor.exe'
if ($procesos.Count -ne 1 -or $procesos[0].Path -ne $esperado) { throw 'No se esta ejecutando el servidor de esta entrega. Usa el acceso servidor publicado.' }
Write-Host 'CORRECTO: el servidor activo es el ejecutable publicado fuera del proyecto.'
$logs = Join-Path $env:LOCALAPPDATA 'SecureGate\EvidenciaEntrega'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
foreach ($nombre in @('SecureGate.Retencion.Pruebas','SecureGate.Transporte.Pruebas','SecureGate.Ia.Pruebas','SecureGate.Seguridad.Pruebas')) {
    $proyectoPrueba = Join-Path $Proyecto "pruebas\$nombre\$nombre.csproj"
    if (-not (Test-Path -LiteralPath $proyectoPrueba)) { throw "Falta el proyecto de pruebas: $nombre" }
    Write-Host "Ejecutando $nombre..."
    & dotnet run --project $proyectoPrueba 2>&1 | Tee-Object -FilePath (Join-Path $logs "$nombre.txt")
    if ($LASTEXITCODE -ne 0) { throw "Fallaron las pruebas: $nombre" }
}
Write-Host 'CORRECTO: pruebas aisladas de retencion, transporte, IA y autenticacion aprobadas.'
Write-Host "Resultados guardados en: $logs"
