$ErrorActionPreference = 'Stop'
$base = 'https://entrust-factual-cozy.ngrok-free.dev'
$token = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN','User')
if ([string]::IsNullOrWhiteSpace($token)) {
    $secreto = Read-Host 'Clave de cliente SecureGate recibida por via privada' -AsSecureString
    $token = [Net.NetworkCredential]::new('', $secreto).Password
}
$bytes = [Convert]::FromBase64String($token)
if ($bytes.Length -ne 32 -or $token -ne [Convert]::ToBase64String($bytes)) { throw 'La clave no tiene el formato esperado.' }
Add-Type -AssemblyName System.Net.Http
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(20)
$http.DefaultRequestHeaders.Add('ngrok-skip-browser-warning','SecureGate')
$http.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)
try {
    $respuesta = $http.GetAsync("$base/api/estado").GetAwaiter().GetResult()
    try { if ([int]$respuesta.StatusCode -ne 200) { throw "No se guardo la configuracion. HTTP $([int]$respuesta.StatusCode)." } }
    finally { $respuesta.Dispose() }
} finally { $http.Dispose() }
$carpeta = Join-Path $env:LOCALAPPDATA 'SecureGate\Cliente'
New-Item -ItemType Directory -Path $carpeta -Force | Out-Null
$ruta = Join-Path $carpeta 'configuracion.json'
$ajustes = @{Servidor="$base/"; CarpetaMonitoreo=(Join-Path $env:USERPROFILE 'Downloads')}
if (Test-Path -LiteralPath $ruta) {
    $anterior = Get-Content -LiteralPath $ruta -Raw | ConvertFrom-Json
    if ($anterior.CarpetaMonitoreo) { $ajustes.CarpetaMonitoreo = $anterior.CarpetaMonitoreo }
    Copy-Item -LiteralPath $ruta -Destination "$ruta.bak" -Force
}
[Environment]::SetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN',$token,'User')
$token = $null
$ajustes | ConvertTo-Json | Set-Content -LiteralPath $ruta -Encoding UTF8
Write-Host 'CORRECTO: direccion fija y clave guardadas. Cierra SecureGate mediante Salir y abre AbrirSecureGate.vbs.'
