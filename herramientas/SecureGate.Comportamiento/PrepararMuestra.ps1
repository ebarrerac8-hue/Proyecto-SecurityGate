param([Parameter(Mandatory=$true)][string]$Destino)
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $csc)) { throw 'No se encontro el compilador de .NET Framework de Windows.' }
$Destino = [IO.Path]::GetFullPath($Destino)
if (Test-Path -LiteralPath $Destino) { throw 'El destino ya existe. No se sobrescribio.' }
New-Item -ItemType Directory -Path (Split-Path -Parent $Destino) -Force | Out-Null
& $csc /nologo /target:winexe /optimize+ "/out:$Destino" (Join-Path $PSScriptRoot 'MuestraControlada.cs')
if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar la muestra controlada.' }
Get-FileHash -LiteralPath $Destino -Algorithm SHA256
Write-Host 'Muestra BENIGNA preparada. Solo realiza acciones en el invitado de Windows Sandbox.'
Write-Host 'No es malware ni una prueba de deteccion universal. Las señales esperadas son SG-B001 y SG-B002.'
