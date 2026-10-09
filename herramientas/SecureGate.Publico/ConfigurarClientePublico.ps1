param([Parameter(Mandatory = $true)][string]$Servidor)
$ErrorActionPreference = "Stop"
$uri = [Uri]::new($Servidor)
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne "https" -or
    $uri.Host -notmatch "^[a-z0-9-]+\.trycloudflare\.com$" -or
    $uri.AbsolutePath -ne "/" -or $uri.Query -or $uri.Fragment -or $uri.UserInfo -or $uri.Port -ne 443) {
    throw "Ingresá la URL HTTPS del túnel que te dio el responsable del servidor."
}
$base = $uri.AbsoluteUri.TrimEnd('/')
$secreto = Read-Host "Ingresá la clave de acceso SECUREGATE_CLIENT_TOKEN recibida por vía privada" -AsSecureString
$token = [Net.NetworkCredential]::new("", $secreto).Password
$bytes = [Convert]::FromBase64String($token)
if ($bytes.Length -ne 32 -or $token -ne [Convert]::ToBase64String($bytes)) { throw "La clave no tiene el formato esperado." }
Add-Type -AssemblyName System.Net.Http
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(20)
$http.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $token)
try {
    $respuesta = $http.GetAsync("$base/api/estado").GetAwaiter().GetResult()
    try { if ([int]$respuesta.StatusCode -ne 200) { throw "Conexión rechazada: HTTP $([int]$respuesta.StatusCode). No se guardó la configuración." } }
    finally { $respuesta.Dispose() }
} finally { $http.Dispose() }
$carpeta = Join-Path $env:LOCALAPPDATA "SecureGate\Cliente"
New-Item -ItemType Directory -Path $carpeta -Force | Out-Null
$ruta = Join-Path $carpeta "configuracion.json"
$configuracion = @{ Servidor = "$base/"; CarpetaMonitoreo = "$env:USERPROFILE\Downloads" }
if (Test-Path -LiteralPath $ruta) {
    $anterior = Get-Content -LiteralPath $ruta -Raw | ConvertFrom-Json
    if ($anterior.CarpetaMonitoreo) { $configuracion.CarpetaMonitoreo = $anterior.CarpetaMonitoreo }
    Copy-Item -LiteralPath $ruta -Destination "$ruta.bak" -Force
}
[Environment]::SetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", $token, "User")
$env:SECUREGATE_CLIENT_TOKEN = $token
$token = $null
$configuracion | ConvertTo-Json | Set-Content -LiteralPath $ruta -Encoding UTF8
Write-Host "CORRECTO: cliente conectado a la API pública con certificado y clave válidos."
Write-Host "Cerrá y volvé a iniciar la interfaz para que cargue la dirección."
