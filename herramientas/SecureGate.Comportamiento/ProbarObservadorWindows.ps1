# Prueba exclusiva de CI sobre un equipo efimero. No inicia Windows Sandbox.
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Esta prueba solo se ejecuta en CI efimero.' }
$rutas = @('C:\Entrada','C:\Prueba','C:\Salida')
foreach ($ruta in $rutas) { if (Test-Path -LiteralPath $ruta) { throw "Directorio ocupado; no se modifica: $ruta" } }
$escritorio = [Environment]::GetFolderPath('Desktop')
$marcador = Join-Path $escritorio 'SecureGate_PruebaComportamiento.txt'
if (Test-Path -LiteralPath $marcador) { throw 'El marcador de prueba ya existe.' }
$antes = $env:SECUREGATE_OBSERVADOR_QA
try {
    foreach ($ruta in $rutas) { New-Item -ItemType Directory -Path $ruta | Out-Null }
    New-Item -ItemType Directory -Path $escritorio -Force | Out-Null
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    & $csc /nologo /target:winexe /define:SECUREGATE_QA '/out:C:\Entrada\muestra.exe' (Join-Path $PSScriptRoot 'MuestraControlada.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion del fixture de CI.' }
    $hash = (Get-FileHash -LiteralPath 'C:\Entrada\muestra.exe' -Algorithm SHA256).Hash.ToLowerInvariant()
    @{AnalisisId=[Guid]::NewGuid().ToString(); ArchivoId=[Guid]::NewGuid().ToString(); Sha256=$hash;
        TiempoObservacionSegundos=20; ModoDemostracion=$false} | ConvertTo-Json |
        Set-Content -LiteralPath 'C:\Entrada\solicitud.json' -Encoding UTF8
    $env:SECUREGATE_OBSERVADOR_QA = '1'
    $observador = Join-Path $PSScriptRoot '..\..\src\SecureGate.Sandbox\Assets\Guest\Observador.ps1'
    & powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File $observador -PruebaObservador
    if (!(Test-Path -LiteralPath 'C:\Salida\informe.json')) { throw 'No llego el informe del observador.' }
    $proyecto = Join-Path $PSScriptRoot '..\..\pruebas\SecureGate.Comportamiento.Pruebas\SecureGate.Comportamiento.Pruebas.csproj'
    & dotnet run -c Release --no-build --project $proyecto -- 'C:\Salida\informe.json'
    if ($LASTEXITCODE -ne 0) { throw 'El observador no produjo las señales esperadas.' }
    if ($env:SECUREGATE_DECISION_QA_DIR) {
        New-Item -ItemType Directory -Path $env:SECUREGATE_DECISION_QA_DIR -Force | Out-Null
        Copy-Item -LiteralPath 'C:\Salida\informe.json' -Destination (Join-Path $env:SECUREGATE_DECISION_QA_DIR 'observador-comportamiento-ci.json')
    }
} finally {
    $env:SECUREGATE_OBSERVADOR_QA = $antes
    foreach ($ruta in $rutas) { if (Test-Path -LiteralPath $ruta) { Remove-Item -LiteralPath $ruta -Recurse -Force } }
    # El fixture crea solo entradas con este prefijo y un GUID; se retiran si se interrumpio.
    $clave = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\Microsoft\Windows\CurrentVersion\Run', $true)
    if ($clave) { try { foreach ($nombre in $clave.GetValueNames()) {
        if ($nombre -match '^SecureGate_PruebaControlada_[a-f0-9]{32}$') { $clave.DeleteValue($nombre, $false) }
    } } finally { $clave.Dispose() } }
    if (Test-Path -LiteralPath $marcador) { Remove-Item -LiteralPath $marcador -Force }
}
