param([Parameter(Mandatory=$true)][string]$Destino)
$ErrorActionPreference = 'Stop'
$original = Join-Path $env:WINDIR 'System32\cmd.exe'
$Destino = [IO.Path]::GetFullPath($Destino)
if (Test-Path -LiteralPath $Destino) { throw 'El destino ya existe; no se sobrescribio.' }
if ((Get-AuthenticodeSignature -LiteralPath $original).Status -ne 'Valid') { throw 'La firma de cmd.exe no se pudo validar.' }
New-Item -ItemType Directory -Path (Split-Path -Parent $Destino) -Force | Out-Null
Copy-Item -LiteralPath $original -Destination $Destino
Get-FileHash -LiteralPath $Destino -Algorithm SHA256
Write-Host 'Copia de cmd.exe firmada. Generar en el SERVIDOR; compartir esta misma copia con los participantes.'
Write-Host 'No abrir manualmente. El laboratorio solo se activa si el SHA-256 coincide con cmd.exe del servidor.'
