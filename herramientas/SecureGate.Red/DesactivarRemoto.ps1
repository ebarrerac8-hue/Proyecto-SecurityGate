$ErrorActionPreference = "Stop"
Remove-NetFirewallRule -Name "SecureGate-HTTPS-Laboratorio" -ErrorAction SilentlyContinue
foreach ($nombre in @("SECUREGATE_BIND_IP", "SECUREGATE_CERT_THUMBPRINT")) {
    [Environment]::SetEnvironmentVariable($nombre, $null, "User")
    [Environment]::SetEnvironmentVariable($nombre, $null, "Process")
}
Write-Host "Acceso remoto desactivado. Reiniciá el servidor para volver a localhost con el certificado de desarrollo."
