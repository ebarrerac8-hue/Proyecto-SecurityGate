$ErrorActionPreference = "Stop"
$comando = Get-Command cloudflared -ErrorAction SilentlyContinue
$ejecutable = if ($null -ne $comando) { $comando.Source } else { $null }
if (-not $ejecutable) {
    foreach ($ruta in @(
        "$env:LOCALAPPDATA\Microsoft\WinGet\Links\cloudflared.exe"
        "$env:ProgramFiles\cloudflared\cloudflared.exe"
        "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe"
    )) {
        if (Test-Path -LiteralPath $ruta) { $ejecutable = $ruta; break }
    }
}
if (-not $ejecutable) { throw "No se encontró cloudflared. Instalalo con winget o abrí una nueva ventana de PowerShell." }
Add-Type -AssemblyName System.Net.Http
$token = [Environment]::GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", "User")
if ([string]::IsNullOrWhiteSpace($token)) { throw "Falta la clave de acceso de SecureGate." }
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(10)
$http.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $token)
try {
    $respuesta = $http.GetAsync("https://localhost:5443/api/estado").GetAwaiter().GetResult()
    try { if ([int]$respuesta.StatusCode -ne 200) { throw "La API local no aceptó la clave: HTTP $([int]$respuesta.StatusCode)." } }
    finally { $respuesta.Dispose() }
} finally { $http.Dispose(); $token = $null }
# Obtener el certificado real mediante TLS con validación normal de Windows.
$tcp = [Net.Sockets.TcpClient]::new()
$tls = $null
$certificado = $null
try {
    $tcp.Connect("localhost", 5443)
    $tls = [Net.Security.SslStream]::new($tcp.GetStream(), $false)
    $tls.ReadTimeout = 10000
    $tls.WriteTimeout = 10000
    $tls.AuthenticateAsClient("localhost")
    $certificado = [Security.Cryptography.X509Certificates.X509Certificate2]::new($tls.RemoteCertificate)
    $publico = $certificado.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $base64 = [Convert]::ToBase64String($publico, [Base64FormattingOptions]::InsertLineBreaks)
    $carpeta = Join-Path $env:LOCALAPPDATA "SecureGate\Tunel"
    New-Item -ItemType Directory -Path $carpeta -Force | Out-Null
    $pem = Join-Path $carpeta "origen-publico.pem"
    [IO.File]::WriteAllText($pem, "-----BEGIN CERTIFICATE-----`n$base64`n-----END CERTIFICATE-----`n", [Text.Encoding]::ASCII)
} finally {
    if ($null -ne $certificado) { $certificado.Dispose() }
    if ($null -ne $tls) { $tls.Dispose() }
    $tcp.Dispose()
}
Write-Host "CORRECTO: API local y certificado comprobados."
Write-Host "Copiá la URL https://...trycloudflare.com que aparezca y dejá esta ventana abierta."
Write-Host "La API seguirá exigiendo su clave. El túnel termina con Ctrl+C."
& $ejecutable tunnel --url "https://localhost:5443" --origin-server-name "localhost" --origin-ca-pool $pem
