param(
    [Parameter(Mandatory = $true)][string]$IPServidor,
    [Parameter(Mandatory = $true)][string]$Certificado,
    [Parameter(Mandatory = $true)][string]$Sha256Certificado
)
$ErrorActionPreference = "Stop"
$ip = [Net.IPAddress]::Parse($IPServidor)
if ($ip.GetAddressBytes().Length -ne 4) { throw "Usá la IPv4 del servidor." }
$IPServidor = $ip.ToString()
$Certificado = (Resolve-Path -LiteralPath $Certificado).Path
if ((Get-FileHash -LiteralPath $Certificado -Algorithm SHA256).Hash -ne $Sha256Certificado.Trim()) {
    throw "El certificado no coincide con la huella mostrada por el servidor. No se importó nada."
}
Import-Certificate -FilePath $Certificado -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
$secreto = Read-Host "Ingresá la clave SECUREGATE_CLIENT_TOKEN que te entregó el responsable del servidor" -AsSecureString
$credencial = [Net.NetworkCredential]::new("", $secreto).Password
$bytes = [Convert]::FromBase64String($credencial)
if ($bytes.Length -ne 32 -or $credencial -ne [Convert]::ToBase64String($bytes)) { throw "La clave no tiene el formato esperado." }
Add-Type -AssemblyName System.Net.Http
$cliente = [Net.Http.HttpClient]::new()
$cliente.Timeout = [TimeSpan]::FromSeconds(15)
$cliente.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $credencial)
try {
    $respuesta = $cliente.GetAsync("https://${IPServidor}:5443/api/estado").GetAwaiter().GetResult()
    try { if ([int]$respuesta.StatusCode -ne 200) { throw "Conexión rechazada: HTTP $([int]$respuesta.StatusCode). No se guardó la clave." } }
    finally { $respuesta.Dispose() }
} finally { $cliente.Dispose() }
[Environment]::SetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", $credencial, "User")
$env:SECUREGATE_CLIENT_TOKEN = $credencial
$credencial = $null
$carpeta = Join-Path $env:LOCALAPPDATA "SecureGate\Cliente"
New-Item -ItemType Directory -Path $carpeta -Force | Out-Null
$ruta = Join-Path $carpeta "configuracion.json"
$configuracion = @{ Servidor = "https://${IPServidor}:5443/"; CarpetaMonitoreo = "$env:USERPROFILE\Downloads" }
if (Test-Path -LiteralPath $ruta) {
    $anterior = Get-Content -LiteralPath $ruta -Raw | ConvertFrom-Json
    if ($anterior.CarpetaMonitoreo) { $configuracion.CarpetaMonitoreo = $anterior.CarpetaMonitoreo }
    Copy-Item -LiteralPath $ruta -Destination "$ruta.bak" -Force
}
$configuracion | ConvertTo-Json | Set-Content -LiteralPath $ruta -Encoding UTF8
Write-Host "CORRECTO: cliente conectado por HTTPS; certificado validado y acceso autorizado."
Write-Host "Abrí la interfaz con IniciarInterfaz.ps1 para que cargue la clave."
