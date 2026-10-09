$ErrorActionPreference = "Stop"

try {
    $proyecto = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $iniciarServidor = Join-Path $PSScriptRoot "IniciarServidorPublico.ps1"
    $certificado = Join-Path $env:LOCALAPPDATA "SecureGate\Tunel\origen-publico.pem"

    if (-not (Get-Command ngrok -ErrorAction SilentlyContinue)) {
        throw "No se encontro ngrok. Comproba su instalacion."
    }

    if (-not (Test-Path -LiteralPath $certificado)) {
        throw "No se encontro el certificado publico del servidor."
    }

    if (-not (Test-Path -LiteralPath $iniciarServidor)) {
        throw "No se encontro IniciarServidorPublico.ps1."
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "Este inicio desde el proyecto necesita el SDK de .NET."
    }

    $clave = [Environment]::GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", "User")
    if ([string]::IsNullOrWhiteSpace($clave)) {
        throw "Falta configurar la clave del cliente en este servidor."
    }

    Add-Type -AssemblyName System.Net.Http
    $http = [System.Net.Http.HttpClient]::new()
    $http.Timeout = [TimeSpan]::FromSeconds(2)
    $http.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $clave)
    $clave = $null

    function ComprobarServidor {
        try {
            $respuesta = $http.GetAsync("https://localhost:5443/api/estado").GetAwaiter().GetResult()
            try { return ([int]$respuesta.StatusCode -eq 200) }
            finally { $respuesta.Dispose() }
        }
        catch { return $false }
    }

    try {
        if (-not (ComprobarServidor)) {
            Write-Host "Iniciando servidor..."
            $argumentos = "-NoProfile -NoExit -ExecutionPolicy Bypass -File `"$iniciarServidor`" -Proyecto `"$proyecto`""
            Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList $argumentos | Out-Null

            $limite = [DateTime]::UtcNow.AddSeconds(60)
            $listo = $false

            do {
                Start-Sleep -Seconds 1
                $listo = ComprobarServidor
            } while (-not $listo -and [DateTime]::UtcNow -lt $limite)

            if (-not $listo) {
                throw "El servidor no respondio. Revisa su ventana."
            }
        }

        Write-Host "Servidor comprobado. Iniciando conexion publica..."
    }
    finally {
        $http.Dispose()
    }

    & ngrok http https://localhost:5443 --url=https://entrust-factual-cozy.ngrok-free.dev --upstream-tls-verify "--upstream-tls-verify-cas=$certificado"

    if ($LASTEXITCODE -ne 0) {
        throw "Ngrok termino con un error. Revisa el mensaje anterior."
    }
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
}
