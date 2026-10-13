param(
    [string]$Proyecto = (Get-Location).Path,
    [ValidateSet('Nueva', 'Reabrir')][string]$Modo = 'Nueva'
)
$ErrorActionPreference = 'Stop'
$Proyecto = [IO.Path]::GetFullPath($Proyecto)
$exe = Join-Path $Proyecto 'SecureGateIntegrado_NET10 (1)\SecureGateIntegrado\bin\Debug\net10.0-windows\SecureGateIntegrado.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Falta compilar el cliente actualizado.' }
foreach ($p in @(Get-Process -Name SecureGateIntegrado -ErrorAction SilentlyContinue)) {
    if ($p.Path -eq $exe) { throw 'Este cliente ya esta abierto. Usa Salir desde su icono junto al reloj antes de reabrir.' }
}
$puntero = Join-Path $env:LOCALAPPDATA 'SecureGate\PruebasUI\ultima-prueba.json'
if ($Modo -eq 'Nueva') {
    $raiz = Join-Path $env:LOCALAPPDATA ('SecureGate\PruebasUI\Sesion_' + [Guid]::NewGuid().ToString('N'))
    $monitor = Join-Path $raiz 'DescargasPrueba'
    New-Item -ItemType Directory -Path $monitor | Out-Null
    $servidor = 'https://localhost:5443/'
    $ajustesHabituales = Join-Path $env:LOCALAPPDATA 'SecureGate\Cliente\configuracion.json'
    if (Test-Path -LiteralPath $ajustesHabituales) {
        $guardado = Get-Content -LiteralPath $ajustesHabituales -Raw | ConvertFrom-Json
        if (![string]::IsNullOrWhiteSpace($guardado.Servidor)) { $servidor = $guardado.Servidor }
    }
    $sesion = [pscustomobject]@{ Raiz = $raiz; Monitor = $monitor; Servidor = $servidor; Proyecto = $Proyecto; Pid = 0 }
    $config = @{ Servidor = $servidor; CarpetaMonitoreo = $monitor; RetencionAutomatica = $true } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $raiz 'configuracion.json'), $config, [Text.UTF8Encoding]::new($false))
} else {
    if (!(Test-Path -LiteralPath $puntero)) { throw 'Primero ejecuta una prueba Nueva.' }
    $sesion = Get-Content -LiteralPath $puntero -Raw | ConvertFrom-Json
    if ($sesion.Proyecto -ne $Proyecto -or !(Test-Path -LiteralPath $sesion.Raiz)) {
        throw 'La prueba guardada corresponde a otra copia o ya no existe.'
    }
}
$uri = [Uri]$sesion.Servidor
if ($uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) {
    throw 'Direccion HTTPS no admitida.'
}
$token = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN', 'User')
if ([string]::IsNullOrWhiteSpace($token)) { throw 'Falta la clave de cliente guardada para este usuario.' }
$consulta = $sesion.Servidor.TrimEnd('/') + '/api/analisis/' + [Guid]::NewGuid().ToString()
try {
    Invoke-WebRequest -UseBasicParsing -Uri $consulta -Headers @{ Authorization = "Bearer $token"; 'ngrok-skip-browser-warning' = 'SecureGate' } -TimeoutSec 15 | Out-Null
    throw 'La API respondio inesperadamente para un identificador inexistente.'
} catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) {
        throw 'No se confirmo acceso a la API. Revisa servidor, tunel y clave; todavia no se crea la muestra.'
    }
}
$antesDatos = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_DATA_DIR', 'Process')
$antesToken = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN', 'Process')
try {
    [Environment]::SetEnvironmentVariable('SECUREGATE_CLIENT_DATA_DIR', $sesion.Raiz, 'Process')
    [Environment]::SetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN', $token.Trim(), 'Process')
    $p = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru
} finally {
    [Environment]::SetEnvironmentVariable('SECUREGATE_CLIENT_DATA_DIR', $antesDatos, 'Process')
    [Environment]::SetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN', $antesToken, 'Process')
    $token = $null
}
$sesion.Pid = $p.Id
$limite = [DateTime]::UtcNow.AddSeconds(40)
$listo = $false
while ([DateTime]::UtcNow -lt $limite) {
    $p.Refresh()
    if ($p.HasExited) { throw 'El cliente se cerro antes de iniciar el monitor.' }
    $senal = Join-Path $sesion.Raiz 'monitor-listo.json'
    if (Test-Path -LiteralPath $senal) {
        try {
            $dato = Get-Content -LiteralPath $senal -Raw | ConvertFrom-Json
            if ($dato.Pid -eq $p.Id) { $listo = $true; break }
        } catch { }
    }
    Start-Sleep -Milliseconds 500
}
if (!$listo) { throw 'No se confirmo el inicio del monitor. Revisa el mensaje de la interfaz.' }
New-Item -ItemType Directory -Path (Split-Path $puntero -Parent) -Force | Out-Null
[IO.File]::WriteAllText($puntero, ($sesion | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
if ($Modo -eq 'Nueva') {
    $original = Join-Path $env:SystemRoot 'System32\cmd.exe'
    $muestra = Join-Path $sesion.Monitor 'PruebaSecureGate_CMD.exe'
    Copy-Item -LiteralPath $original -Destination $muestra
    Write-Host 'CORRECTO: interfaz aislada abierta y muestra de demostracion copiada a su carpeta monitoreada.'
    Write-Host 'No ejecutes la muestra en el anfitrion. SecureGate debe detectarla automaticamente.'
} else {
    Write-Host 'CORRECTO: cliente reabierto con los mismos datos aislados. No se creo otra muestra.'
}
Write-Host "Servidor: $($sesion.Servidor)"
Write-Host "Carpeta de prueba: $($sesion.Raiz)"
Write-Host 'La configuracion y las Descargas habituales permanecen separadas de esta prueba.'
