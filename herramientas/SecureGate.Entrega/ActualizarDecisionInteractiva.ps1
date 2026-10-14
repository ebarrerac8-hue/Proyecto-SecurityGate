param(
    [string]$Proyecto = (Get-Location).Path,
    [Parameter(Mandatory=$true)][string]$Entrega
)
$ErrorActionPreference = 'Stop'
$Proyecto = (Resolve-Path -LiteralPath $Proyecto).Path
$Entrega = (Resolve-Path -LiteralPath $Entrega).Path
foreach ($ruta in @('Cliente\SecureGateIntegrado.exe','Servidor\SecureGate.Servidor.exe')) {
    if (!(Test-Path -LiteralPath (Join-Path $Entrega $ruta))) { throw "No es la entrega esperada: falta $ruta." }
}
foreach ($proceso in @(Get-Process -Name SecureGateIntegrado,SecureGate.Servidor -ErrorAction SilentlyContinue)) {
    if ($proceso.Path -and $proceso.Path.StartsWith($Entrega + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Cierra el cliente con Salir y detén el servidor de esta entrega antes de actualizar. No se modificó la publicación.'
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
        & dotnet publish $item.Proyecto -c Release -r win-x64 --self-contained true -o (Join-Path $publicacion $item.Nombre)
        if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar $($item.Nombre). La entrega no se modificó." }
    }
    if (!(Test-Path -LiteralPath (Join-Path $publicacion 'Servidor\Assets\Guest\Observador.ps1'))) {
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
    foreach ($nombre in @('Cliente','Servidor')) {
        # Preservar lanzadores, accesos y archivos auxiliares de la distribución anterior.
        Copy-Item -LiteralPath (Join-Path $Entrega $nombre) -Destination $candidato -Recurse
        Copy-Item -Path (Join-Path (Join-Path $publicacion $nombre) '*') -Destination (Join-Path $candidato $nombre) -Recurse -Force
    }
    # Crear paquetes nuevos antes de cambiar las carpetas de los accesos existentes.
    foreach ($nombre in @('Cliente','Servidor')) {
        Compress-Archive -LiteralPath (Join-Path $candidato $nombre) -DestinationPath (Join-Path $candidato ('SecureGate_' + $nombre + '_Actualizado.zip'))
    }
    $movidos = [Collections.Generic.List[string]]::new()
    try {
        foreach ($nombre in @('Cliente','Servidor')) {
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
    foreach ($nombre in @('Cliente','Servidor')) {
        $zip = 'SecureGate_' + $nombre + '_Actualizado.zip'
        if (Test-Path -LiteralPath (Join-Path $Entrega $zip)) { Move-Item -LiteralPath (Join-Path $Entrega $zip) -Destination $respaldo }
        Move-Item -LiteralPath (Join-Path $candidato $zip) -Destination $Entrega
    }
    Write-Host 'CORRECTO: cliente y servidor actualizados; lanzadores y rutas conservados.'
    Write-Host "Respaldo: $respaldo"
    Write-Host 'Inicia manualmente servidor, túnel y cliente. La prueba real del cuadro requiere una nueva descarga .exe.'
    Write-Host ('Paquete para compañeros: ' + (Join-Path $Entrega 'SecureGate_Cliente_Actualizado.zip'))
} finally { Pop-Location }
