param(
    [string]$Proyecto = (Get-Location).Path,
    [Parameter(Mandatory=$true)][string]$Entrega,
    [switch]$SoloCliente
)
$ErrorActionPreference = 'Stop'
$Proyecto = (Resolve-Path -LiteralPath $Proyecto).Path
$Entrega = (Resolve-Path -LiteralPath $Entrega).Path
$componentes = if ($SoloCliente) { @('Cliente') } else { @('Cliente','Servidor') }
$rutasRequeridas = @('Cliente\SecureGateIntegrado.exe')
if (!$SoloCliente) { $rutasRequeridas += 'Servidor\SecureGate.Servidor.exe' }
foreach ($ruta in $rutasRequeridas) {
    if (!(Test-Path -LiteralPath (Join-Path $Entrega $ruta))) { throw "No es la entrega esperada: falta $ruta." }
}
$nombresProcesos = if ($SoloCliente) { @('SecureGateIntegrado') } else { @('SecureGateIntegrado','SecureGate.Servidor') }
foreach ($proceso in @(Get-Process -Name $nombresProcesos -ErrorAction SilentlyContinue)) {
    if ($proceso.Path -and $proceso.Path.StartsWith($Entrega + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Cierre las aplicaciones que se van a actualizar antes de continuar. No se modificó la publicación.'
    }
}
Push-Location -LiteralPath $Proyecto
try {
    & dotnet build SecureGate.Equipo.slnx -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw 'La compilación falló. La entrega no se modificó.' }
    & dotnet run -c Release --no-build --project 'pruebas\SecureGate.Decisiones.Pruebas\SecureGate.Decisiones.Pruebas.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Las pruebas de decisión fallaron. La entrega no se modificó.' }
    $id = [Guid]::NewGuid().ToString('N')
    $publicacion = Join-Path $env:LOCALAPPDATA ('SecureGate\Actualizaciones\Decision_' + $id)
    foreach ($item in @(
        @{Nombre='Cliente'; Proyecto='SecureGateIntegrado_NET10 (1)\SecureGateIntegrado\SecureGateIntegrado.csproj'},
        @{Nombre='Servidor'; Proyecto='src\SecureGate.Servidor\SecureGate.Servidor.csproj'}
    )) {
        if ($SoloCliente -and $item.Nombre -eq 'Servidor') { continue }
        & dotnet publish $item.Proyecto -c Release -r win-x64 --self-contained true -o (Join-Path $publicacion $item.Nombre)
        if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar $($item.Nombre). La entrega no se modificó." }
    }
    if (!$SoloCliente -and !(Test-Path -LiteralPath (Join-Path $publicacion 'Servidor\Assets\Guest\Observador.ps1'))) {
        throw 'Falta el observador Sandbox. La entrega no se modificó.'
    }
    foreach ($archivo in Get-ChildItem -LiteralPath $publicacion -Recurse -File) {
        if ($archivo.Extension -in @('.pfx','.p12','.key') -or $archivo.Name -in @('secrets.json','ngrok.yml','ngrok.yaml')) {
            throw 'Se encontró un archivo privado. La entrega no se modificó.'
        }
        if ($archivo.Extension -in @('.json','.config','.ps1','.cmd','.vbs')) {
            $contenido = [IO.File]::ReadAllText($archivo.FullName)
            foreach ($variable in @('SECUREGATE_CLIENT_TOKEN','SECUREGATE_GEMINI_API_KEY','SECUREGATE_VIRUSTOTAL_API_KEY')) {
                $secreto = [Environment]::GetEnvironmentVariable($variable, 'User')
                if (![string]::IsNullOrWhiteSpace($secreto) -and $contenido.Contains($secreto)) {
                    throw 'Se encontró una credencial en la publicación. La entrega no se modificó.'
                }
            }
        }
    }
    $candidato = Join-Path $Entrega ('ActualizacionDecision_' + $id)
    $respaldo = Join-Path $Entrega ('RespaldoDecision_' + $id)
    New-Item -ItemType Directory -Path $candidato,$respaldo | Out-Null
    foreach ($nombre in $componentes) {
        # Preservar lanzadores, accesos y archivos auxiliares de la distribución anterior.
        Copy-Item -LiteralPath (Join-Path $Entrega $nombre) -Destination $candidato -Recurse
        Copy-Item -Path (Join-Path (Join-Path $publicacion $nombre) '*') -Destination (Join-Path $candidato $nombre) -Recurse -Force
    }
    foreach ($archivo in Get-ChildItem -LiteralPath $candidato -Recurse -File) {
        if ($archivo.Extension -in @('.pfx','.p12','.key') -or $archivo.Name -in @('secrets.json','ngrok.yml','ngrok.yaml')) {
            throw 'Hay un archivo privado en la entrega anterior. No se modificó la publicación ni se creó el ZIP.'
        }
        if ($archivo.Extension -in @('.json','.config','.ps1','.cmd','.vbs','.txt','.md')) {
            $contenido = [IO.File]::ReadAllText($archivo.FullName)
            foreach ($variable in @('SECUREGATE_CLIENT_TOKEN','SECUREGATE_GEMINI_API_KEY','SECUREGATE_VIRUSTOTAL_API_KEY')) {
                foreach ($ambito in @('User','Process')) {
                    $secreto = [Environment]::GetEnvironmentVariable($variable, $ambito)
                    if (![string]::IsNullOrWhiteSpace($secreto) -and $contenido.Contains($secreto)) {
                        throw 'Hay una credencial en la entrega. No se modificó la publicación ni se creó el ZIP.'
                    }
                }
            }
        }
    }
    # Crear paquetes nuevos antes de cambiar las carpetas de los accesos existentes.
    foreach ($nombre in $componentes) {
        Compress-Archive -LiteralPath (Join-Path $candidato $nombre) -DestinationPath (Join-Path $candidato ('SecureGate_' + $nombre + '_Actualizado.zip'))
    }
    $movidos = [Collections.Generic.List[string]]::new()
    try {
        foreach ($nombre in $componentes) {
            Move-Item -LiteralPath (Join-Path $Entrega $nombre) -Destination $respaldo
            $movidos.Add($nombre)
            Move-Item -LiteralPath (Join-Path $candidato $nombre) -Destination $Entrega
        }
    } catch {
        foreach ($nombre in $movidos) {
            $destino = Join-Path $Entrega $nombre
            if (Test-Path -LiteralPath $destino) { Move-Item -LiteralPath $destino -Destination $candidato }
            Move-Item -LiteralPath (Join-Path $respaldo $nombre) -Destination $Entrega
        }
        throw 'No se completó la actualización. Se restauraron las carpetas anteriores.'
    }
    Copy-Item -LiteralPath (Join-Path $Proyecto 'README.md') -Destination (Join-Path $Entrega 'ALCANCE_ACTUAL.md') -Force
    foreach ($nombre in $componentes) {
        $zip = 'SecureGate_' + $nombre + '_Actualizado.zip'
        if (Test-Path -LiteralPath (Join-Path $Entrega $zip)) { Move-Item -LiteralPath (Join-Path $Entrega $zip) -Destination $respaldo }
        Move-Item -LiteralPath (Join-Path $candidato $zip) -Destination $Entrega
    }
    Write-Host ('CORRECTO: publicación actualizada: ' + ($componentes -join ', ') + '. Lanzadores y rutas conservados.')
    Write-Host "Respaldo: $respaldo"
    if ($SoloCliente) { Write-Host 'Abra el cliente con su acceso habitual. Puede revisar el informe guardado sin repetir el análisis.' }
    else { Write-Host 'Inicie manualmente servidor, túnel y cliente.' }
    Write-Host ('Paquete para compañeros: ' + (Join-Path $Entrega 'SecureGate_Cliente_Actualizado.zip'))
} finally { Pop-Location }
