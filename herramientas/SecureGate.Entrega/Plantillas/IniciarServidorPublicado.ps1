$ErrorActionPreference = 'Stop'
try {
    $exe = Join-Path $PSScriptRoot 'SecureGate.Servidor.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Falta SecureGate.Servidor.exe. Ejecuta PrepararEntrega.ps1.' }
    foreach ($nombre in @('SECUREGATE_CLIENT_TOKEN','SECUREGATE_GEMINI_API_KEY','SECUREGATE_GEMINI_MODEL','SECUREGATE_VIRUSTOTAL_API_KEY')) {
        [Environment]::SetEnvironmentVariable($nombre, [Environment]::GetEnvironmentVariable($nombre,'User'),'Process')
    }
    if ([string]::IsNullOrWhiteSpace($env:SECUREGATE_CLIENT_TOKEN)) { throw 'Falta la clave de cliente guardada en Windows.' }
    $env:SECUREGATE_BIND_IP = $null
    $env:SECUREGATE_CERT_THUMBPRINT = $null
    $env:Servidor__TamanoMaximoArchivoBytes = '26214400'
    $env:ASPNETCORE_ENVIRONMENT = 'Production'
    $observador = Join-Path $PSScriptRoot 'Assets\Guest\Observador.ps1'
    if (-not (Test-Path -LiteralPath $observador)) { throw 'Falta Assets/Guest/Observador.ps1 en el servidor publicado.' }
    Set-Location -LiteralPath $PSScriptRoot
    Write-Host 'SecureGate: servidor publicado. Ctrl+C para detenerlo.'
    & $exe
    if ($LASTEXITCODE -ne 0) { throw "El servidor termino con codigo $LASTEXITCODE." }
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; throw }
