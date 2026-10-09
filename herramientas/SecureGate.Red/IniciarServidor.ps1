param([string]$Proyecto = "C:\Users\usuario\source\repos\Proyecto-SecurityGate")
$ErrorActionPreference = "Stop"
foreach ($nombre in @("SECUREGATE_CLIENT_TOKEN", "SECUREGATE_GEMINI_API_KEY", "SECUREGATE_GEMINI_MODEL", "SECUREGATE_VIRUSTOTAL_API_KEY", "SECUREGATE_BIND_IP", "SECUREGATE_CERT_THUMBPRINT")) {
    [Environment]::SetEnvironmentVariable($nombre, [Environment]::GetEnvironmentVariable($nombre, "User"), "Process")
}
Set-Location -LiteralPath $Proyecto
& dotnet run --no-launch-profile --project ".\src\SecureGate.Servidor\SecureGate.Servidor.csproj"
