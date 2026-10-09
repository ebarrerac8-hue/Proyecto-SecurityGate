$ErrorActionPreference = 'Stop'
try {
    $ngrok = Get-Command ngrok -ErrorAction Stop
    $tcp = [Net.Sockets.TcpClient]::new()
    try {
        $ocupado = $false
        try { $tcp.Connect('localhost',5443); $ocupado = $true } catch { }
    } finally { $tcp.Dispose() }
    if ($ocupado) { throw 'El puerto 5443 ya esta ocupado. Detene el servidor anterior antes de probar el publicado.' }
    if (Get-Process -Name ngrok -ErrorAction SilentlyContinue) { throw 'Ngrok ya esta abierto. Detenelo con Ctrl+C antes de iniciar este acceso.' }
    $script = Join-Path $PSScriptRoot 'IniciarServidorPublicado.ps1'
    $ps = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"
    Start-Process -FilePath $ps -ArgumentList "-NoProfile -NoExit -ExecutionPolicy Bypass -File `"$script`"" | Out-Null
    Add-Type -AssemblyName System.Net.Http
    $token = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN','User')
    if ([string]::IsNullOrWhiteSpace($token)) { throw 'Falta la clave de cliente del servidor.' }
    $http = [Net.Http.HttpClient]::new()
    $http.Timeout = [TimeSpan]::FromSeconds(2)
    $http.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)
    $token = $null
    $listo = $false
    try {
        $limite = [DateTime]::UtcNow.AddSeconds(60)
        do {
            try {
                $respuesta = $http.GetAsync('https://localhost:5443/api/estado').GetAwaiter().GetResult()
                try { $listo = ([int]$respuesta.StatusCode -eq 200) } finally { $respuesta.Dispose() }
            } catch { }
            if (-not $listo) { Start-Sleep -Seconds 1 }
        } while (-not $listo -and [DateTime]::UtcNow -lt $limite)
    } finally { $http.Dispose() }
    if (-not $listo) { throw 'El servidor publicado no respondio. Revisa su ventana; no se inicio ngrok.' }
    # Leer el certificado publico del servidor mediante TLS validado por Windows.
    $tcp = [Net.Sockets.TcpClient]::new('localhost',5443)
    $tls = [Net.Security.SslStream]::new($tcp.GetStream(),$false)
    try {
        $tls.AuthenticateAsClient('localhost')
        $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new($tls.RemoteCertificate)
        try {
            $bytes = $cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert)
            $pem = "-----BEGIN CERTIFICATE-----`r`n" + [Convert]::ToBase64String($bytes,[Base64FormattingOptions]::InsertLineBreaks) + "`r`n-----END CERTIFICATE-----`r`n"
        } finally { $cert.Dispose() }
    } finally { $tls.Dispose(); $tcp.Dispose() }
    $carpetaCert = Join-Path $env:LOCALAPPDATA 'SecureGate\Tunel'
    New-Item -ItemType Directory -Path $carpetaCert -Force | Out-Null
    $rutaCert = Join-Path $carpetaCert 'origen-publico.pem'
    Set-Content -LiteralPath $rutaCert -Value $pem -Encoding ASCII
    Write-Host 'CORRECTO: servidor publicado comprobado; iniciando ngrok.'
    & $ngrok.Source 'http' 'https://localhost:5443' '--url=https://entrust-factual-cozy.ngrok-free.dev' '--upstream-tls-verify' "--upstream-tls-verify-cas=$rutaCert"
    if ($LASTEXITCODE -ne 0) { throw "Ngrok termino con codigo $LASTEXITCODE. El servidor permanece en su ventana." }
} catch { Write-Host $_.Exception.Message -ForegroundColor Red; throw }
