param([string]$Proyecto = "C:\Users\usuario\source\repos\Proyecto-SecurityGate")
$ErrorActionPreference = "Stop"
foreach ($nombre in @("SECUREGATE_CLIENT_TOKEN", "SECUREGATE_GEMINI_API_KEY", "SECUREGATE_GEMINI_MODEL", "SECUREGATE_VIRUSTOTAL_API_KEY")) {
    [Environment]::SetEnvironmentVariable($nombre, [Environment]::GetEnvironmentVariable($nombre, "User"), "Process")
}
# El túnel conecta al servicio local; no necesita abrir una escucha de red.
$env:SECUREGATE_BIND_IP = $null
$env:SECUREGATE_CERT_THUMBPRINT = $null
# Límite de la sesión de demostración: 25 MiB por archivo.
$env:Servidor__TamanoMaximoArchivoBytes = "26214400"
Set-Location -LiteralPath $Proyecto
& dotnet run --no-launch-profile --project ".\src\SecureGate.Servidor\SecureGate.Servidor.csproj"
