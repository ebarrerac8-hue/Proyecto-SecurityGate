param([string]$Proyecto = (Join-Path $env:USERPROFILE 'source\repos\Proyecto-SecurityGate'))
$ErrorActionPreference = 'Stop'
$Proyecto = (Resolve-Path -LiteralPath $Proyecto).Path
$clienteOrigen = Join-Path $Proyecto 'entrega\Cliente'
$servidorOrigen = Join-Path $Proyecto 'entrega\Servidor'
foreach ($ruta in @((Join-Path $clienteOrigen 'SecureGateIntegrado.exe'),(Join-Path $servidorOrigen 'SecureGate.Servidor.exe'),(Join-Path $servidorOrigen 'Assets\Guest\Observador.ps1'))) {
    if (-not (Test-Path -LiteralPath $ruta)) { throw "Falta un archivo publicado: $ruta. Revisa la publicacion antes de continuar." }
}
$raiz = Join-Path ([Environment]::GetFolderPath('MyDocuments')) ('SecureGate_Entrega_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss'))
if (Test-Path -LiteralPath $raiz) { throw 'La carpeta de destino ya existe. Espera un segundo y repeti.' }
$cliente = Join-Path $raiz 'Cliente'
$servidor = Join-Path $raiz 'Servidor'
New-Item -ItemType Directory -Path $cliente,$servidor -Force | Out-Null
Copy-Item -Path (Join-Path $clienteOrigen '*') -Destination $cliente -Recurse -Force
Copy-Item -Path (Join-Path $servidorOrigen '*') -Destination $servidor -Recurse -Force
foreach ($nombre in @('ConfigurarCliente.ps1','ConfigurarCliente.cmd','AbrirSecureGate.vbs')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Plantillas\$nombre") -Destination $cliente
}
foreach ($nombre in @('IniciarTodo.ps1','IniciarTodo.cmd','IniciarServidorPublicado.ps1','IniciarServidorPublicado.cmd')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Plantillas\$nombre") -Destination $servidor
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PruebasFinales.ps1') -Destination $raiz
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VerificarPreparacion.ps1') -Destination $raiz
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEEME.md') -Destination $raiz

# Versionar solamente scripts y guia. Los ejecutables y las evidencias quedan fuera de Git.
$herramientas = Join-Path $Proyecto 'herramientas\SecureGate.Entrega'
if (Test-Path -LiteralPath $herramientas) {
    $respaldo = Join-Path $env:LOCALAPPDATA ('SecureGate\Respaldos\Entrega_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss'))
    New-Item -ItemType Directory -Path $respaldo -Force | Out-Null
    Copy-Item -LiteralPath $herramientas -Destination $respaldo -Recurse
}
New-Item -ItemType Directory -Path $herramientas -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PrepararEntrega.ps1') -Destination $herramientas -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PruebasFinales.ps1') -Destination $herramientas -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VerificarPreparacion.ps1') -Destination $herramientas -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LEEME.md') -Destination $herramientas -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Plantillas') -Destination $herramientas -Recurse -Force
$gitignore = Join-Path $Proyecto '.gitignore'
if (-not (Test-Path -LiteralPath $gitignore) -or -not (Select-String -LiteralPath $gitignore -SimpleMatch '/entrega/' -Quiet)) {
    Add-Content -LiteralPath $gitignore -Value @('','# Salidas publicadas locales de SecureGate','/entrega/') -Encoding UTF8
}

# Excluir de la distribucion cualquier configuracion sensible agregada al publicar.
$archivos = @(Get-ChildItem -LiteralPath $cliente,$servidor -File -Recurse)
foreach ($f in $archivos) {
    if ($f.Extension -in @('.pfx','.p12','.key') -or $f.Name -in @('ngrok.yml','ngrok.yaml','secrets.json')) { throw ('Archivo privado en la publicacion: ' + $f.Name) }
    if ($f.Extension -in @('.json','.config','.ps1','.cmd','.vbs')) {
        $texto = Get-Content -LiteralPath $f.FullName -Raw
        foreach ($nombre in @('SECUREGATE_CLIENT_TOKEN','SECUREGATE_GEMINI_API_KEY','SECUREGATE_VIRUSTOTAL_API_KEY')) {
            $secreto = [Environment]::GetEnvironmentVariable($nombre,'User')
            if (-not [string]::IsNullOrWhiteSpace($secreto) -and $texto.Contains($secreto)) { throw ('Se encontro una credencial en: ' + $f.Name) }
        }
    }
}
Compress-Archive -LiteralPath $cliente -DestinationPath (Join-Path $raiz 'SecureGate_Cliente.zip')
Compress-Archive -LiteralPath $servidor -DestinationPath (Join-Path $raiz 'SecureGate_Servidor.zip')

# Accesos: servidor visible; cliente sin consola de PowerShell.
$wsh = New-Object -ComObject WScript.Shell
$escritorio = [Environment]::GetFolderPath('Desktop')
$acceso = $wsh.CreateShortcut((Join-Path $escritorio 'SecureGate - servidor publicado.lnk'))
$acceso.TargetPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$scriptInicio = Join-Path $servidor 'IniciarTodo.ps1'
$acceso.Arguments = '-NoProfile -NoExit -ExecutionPolicy Bypass -File "' + $scriptInicio + '"'
$acceso.WorkingDirectory = $servidor
$acceso.Save()
$acceso = $wsh.CreateShortcut((Join-Path $escritorio 'SecureGate - cliente publicado.lnk'))
$acceso.TargetPath = Join-Path $env:WINDIR 'System32\wscript.exe'
$acceso.Arguments = '"' + (Join-Path $cliente 'AbrirSecureGate.vbs') + '"'
$acceso.WorkingDirectory = $cliente
$acceso.Save()
[pscustomobject]@{Carpeta=$raiz; Cliente=$cliente; Servidor=$servidor; Pruebas=(Join-Path $raiz 'PruebasFinales.ps1')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $herramientas 'ultima-entrega-local.json') -Encoding UTF8
# Ruta personal: archivo de conveniencia que no se sube al repositorio.
$regla = '/herramientas/SecureGate.Entrega/ultima-entrega-local.json'
if (-not (Select-String -LiteralPath $gitignore -SimpleMatch $regla -Quiet)) { Add-Content -LiteralPath $gitignore -Value $regla -Encoding UTF8 }
Write-Host 'CORRECTO: paquete preparado sin incluir claves ni certificados privados.'
Write-Host "Carpeta de entrega: $raiz"
Write-Host 'Accesos creados: SecureGate - servidor publicado y SecureGate - cliente publicado.'
Write-Host 'Envia solamente SecureGate_Cliente.zip al companero. La clave se entrega por via privada.'
Invoke-Item -LiteralPath $raiz
