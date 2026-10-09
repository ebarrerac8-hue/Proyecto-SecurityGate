param([Parameter(Mandatory = $true)][string]$IPServidor)
$ErrorActionPreference = "Stop"
$identidad = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identidad)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Abrí PowerShell como administrador con tu mismo usuario Windows para preparar el certificado y la regla de red."
}
$ip = [Net.IPAddress]::Parse($IPServidor)
$b = $ip.GetAddressBytes()
if ($b.Length -ne 4 -or -not ($b[0] -eq 10 -or ($b[0] -eq 172 -and $b[1] -ge 16 -and $b[1] -le 31) -or ($b[0] -eq 192 -and $b[1] -eq 168))) {
    throw "Usá una IPv4 de red privada."
}
$IPServidor = $ip.ToString()
$interfaz = Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -eq $IPServidor } | Select-Object -First 1
if ($null -eq $interfaz) { throw "Esa dirección no pertenece a esta computadora." }
$perfil = Get-NetConnectionProfile -InterfaceIndex $interfaz.InterfaceIndex
if ($perfil.NetworkCategory -ne "Private") {
    throw "La conexión elegida no está marcada como red privada. Usá una red de laboratorio de confianza; no se cambió el perfil."
}
$carpeta = Join-Path $env:LOCALAPPDATA "SecureGate\Servidor\Certificados"
New-Item -ItemType Directory -Path $carpeta -Force | Out-Null
$certificado = New-SelfSignedCertificate `
    -Subject "CN=SecureGate-Laboratorio" `
    -Type SSLServerAuthentication `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
    -KeyExportPolicy NonExportable `
    -NotAfter (Get-Date).AddDays(60) `
    -TextExtension @("2.5.29.17={text}DNS=localhost&IPAddress=127.0.0.1&IPAddress=$IPServidor")
$archivo = Join-Path $carpeta "SecureGateServidor.cer"
Export-Certificate -Cert $certificado -FilePath $archivo -Force | Out-Null
Import-Certificate -FilePath $archivo -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
$regla = Get-NetFirewallRule -Name "SecureGate-HTTPS-Laboratorio" -ErrorAction SilentlyContinue
if ($null -ne $regla) { Remove-NetFirewallRule -Name "SecureGate-HTTPS-Laboratorio" }
New-NetFirewallRule -Name "SecureGate-HTTPS-Laboratorio" `
    -DisplayName "SecureGate HTTPS laboratorio" -Direction Inbound -Action Allow `
    -Protocol TCP -LocalPort 5443 -LocalAddress $IPServidor `
    -RemoteAddress LocalSubnet -Profile Private -EdgeTraversalPolicy Block | Out-Null
[Environment]::SetEnvironmentVariable("SECUREGATE_BIND_IP", $IPServidor, "User")
[Environment]::SetEnvironmentVariable("SECUREGATE_CERT_THUMBPRINT", $certificado.Thumbprint, "User")
Write-Host "CORRECTO: servidor preparado para https://${IPServidor}:5443/"
Write-Host "Certificado público para el cliente:" $archivo
Write-Host "SHA256 del certificado (comparar en el cliente):" (Get-FileHash -LiteralPath $archivo -Algorithm SHA256).Hash
Write-Host "Reiniciá el servidor desde PowerShell normal con tu mismo usuario. La clave privada no se exportó."
