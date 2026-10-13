param([ValidateSet('Inicial', 'Recuperacion')][string]$Modo = 'Inicial')
$ErrorActionPreference = 'Stop'
function ConvertirInformeCanonico($valor, [bool]$RepararCodificacion = $false) {
    if ($null -eq $valor) { return 'null' }
    if ($valor -is [DateTime] -or $valor -is [DateTimeOffset]) {
        return ConvertTo-Json -InputObject (([DateTimeOffset]$valor).UtcDateTime.ToString('o')) -Compress
    }
    if ($valor -is [string]) {
        if ($RepararCodificacion -and $valor -match '[^\x00-\x7F]') {
            try {
                $cp = [Text.Encoding]::GetEncoding(1252, [Text.EncoderFallback]::ExceptionFallback, [Text.DecoderFallback]::ExceptionFallback)
                $utf8Estricto = [Text.UTF8Encoding]::new($false, $true)
                $valor = $utf8Estricto.GetString($cp.GetBytes($valor))
            } catch { }
        }
        if ($valor -match '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}') {
            try { $valor = ([DateTimeOffset]::Parse($valor, [Globalization.CultureInfo]::InvariantCulture)).UtcDateTime.ToString('o') }
            catch { }
        }
        return ConvertTo-Json -InputObject $valor -Compress
    }
    if ($valor -is [System.Collections.IDictionary]) {
        $partes = @()
        foreach ($clave in @($valor.Keys | Sort-Object)) {
            $partes += (ConvertTo-Json -InputObject ([string]$clave) -Compress) + ':' +
                (ConvertirInformeCanonico $valor[$clave] $RepararCodificacion)
        }
        return '{' + ($partes -join ',') + '}'
    }
    if ($valor -is [System.Management.Automation.PSCustomObject]) {
        $partes = @()
        foreach ($prop in @($valor.PSObject.Properties | Sort-Object -Property Name)) {
            $partes += (ConvertTo-Json -InputObject $prop.Name -Compress) + ':' +
                (ConvertirInformeCanonico $prop.Value $RepararCodificacion)
        }
        return '{' + ($partes -join ',') + '}'
    }
    if ($valor -is [System.Collections.IEnumerable]) {
        $partes = @()
        foreach ($elemento in $valor) { $partes += ConvertirInformeCanonico $elemento $RepararCodificacion }
        return '[' + ($partes -join ',') + ']'
    }
    return ConvertTo-Json -InputObject $valor -Depth 100 -Compress
}
$puntero = Join-Path $env:LOCALAPPDATA 'SecureGate\PruebasUI\ultima-prueba.json'
$sesion = Get-Content -LiteralPath $puntero -Raw -Encoding UTF8 | ConvertFrom-Json
$carpetaEnvios = Join-Path $sesion.Raiz 'Envios'
$limite = [DateTime]::UtcNow.AddMinutes(4)
$envio = $null
while ([DateTime]::UtcNow -lt $limite) {
    $archivos = @(Get-ChildItem -LiteralPath $carpetaEnvios -Filter '*.json' -ErrorAction SilentlyContinue)
    if ($archivos.Count -gt 1) { throw 'La prueba genero mas de un envio; revisar duplicacion.' }
    if ($archivos.Count -eq 1) {
        $envio = Get-Content -LiteralPath $archivos[0].FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $envio.Resultado -and $envio.Resultado.Estado -in @('Completado', 'Fallido', 'Cancelado')) { break }
        Write-Host "Estado local: $($envio.EstadoLocal)"
    } else { Write-Host 'Esperando deteccion y preparacion del archivo...' }
    Start-Sleep -Seconds 3
}
if ($null -eq $envio -or $null -eq $envio.Resultado) {
    throw 'Todavia no hay informe. Revisa la interfaz; la muestra debe permanecer retenida.'
}
if ($envio.Resultado.Estado -ne 'Completado') {
    throw "El servidor no termino la demostracion: $($envio.Resultado.CodigoError). La muestra sigue retenida."
}
if ($envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal -ne 'PruebaSecureGate_CMD.exe' -or
    $envio.Solicitud.AnalisisLocal.Archivo.Extension -ne '.exe' -or
    !(Test-Path -LiteralPath $envio.RutaArchivo) -or
    (Test-Path -LiteralPath (Join-Path $sesion.Monitor 'PruebaSecureGate_CMD.exe'))) {
    throw 'La retencion o los metadatos no corresponden a la muestra de prueba.'
}
$hash = (Get-FileHash -LiteralPath $envio.RutaArchivo -Algorithm SHA256).Hash
if ($hash -ne $envio.Solicitud.AnalisisLocal.Archivo.Sha256) { throw 'Hash retenido diferente.' }
if ($envio.Resultado.Archivo.ArchivoId -ne $envio.Solicitud.AnalisisLocal.Archivo.ArchivoId -or
    [string]::IsNullOrWhiteSpace($envio.AnalisisId)) { throw 'Identidad de respuesta diferente.' }
if ($envio.Solicitud.AnalisisLocal.FirmaDigital.Estado -ne 'Completada') {
    throw "La firma no pudo verificarse: $($envio.Solicitud.AnalisisLocal.FirmaDigital.CodigoError)."
}
if ($envio.Resultado.Sandbox.MuestraEjecutada -ne $true -or
    $envio.Resultado.Sandbox.ObservadorIniciado -ne $true) {
    throw 'Sandbox no confirmo ejecucion y observacion.'
}
$instantanea = Join-Path $sesion.Raiz 'verificacion-inicial.json'
$resultadoJson = $envio.Resultado | ConvertTo-Json -Depth 100 -Compress
if ($Modo -eq 'Inicial') {
    $registro = @{
        Pid = $sesion.Pid
        FechaUtc = [DateTime]::UtcNow.ToString('o')
        SolicitudId = $envio.Solicitud.SolicitudId
        AnalisisId = $envio.AnalisisId
        InicioLocal = $envio.Solicitud.AnalisisLocal.InicioUtc
        ResultadoJson = $resultadoJson
    } | ConvertTo-Json -Depth 100
    [IO.File]::WriteAllText($instantanea, $registro, [Text.UTF8Encoding]::new($false))
    Write-Host 'CORRECTO: deteccion automatica, retencion, firma y envio real desde la interfaz.'
    Write-Host 'CORRECTO: resultado real con ejecucion y observacion Sandbox; la muestra sigue retenida.'
} else {
    $anterior = Get-Content -LiteralPath $instantanea -Raw -Encoding UTF8 | ConvertFrom-Json
    $proceso = Get-Process -Id $sesion.Pid
    if ($sesion.Pid -eq $anterior.Pid -or $proceso.StartTime.ToUniversalTime() -le [DateTime]::Parse($anterior.FechaUtc).ToUniversalTime()) {
        throw 'Primero cierra el cliente mediante Salir y reabre la misma prueba.'
    }
    if ($envio.Solicitud.SolicitudId -ne $anterior.SolicitudId -or
        $envio.AnalisisId -ne $anterior.AnalisisId -or
        ([DateTimeOffset]$envio.Solicitud.AnalisisLocal.InicioUtc).UtcDateTime.Ticks -ne ([DateTimeOffset]$anterior.InicioLocal).UtcDateTime.Ticks -or
        (ConvertirInformeCanonico $envio.Resultado) -ne (ConvertirInformeCanonico ($anterior.ResultadoJson | ConvertFrom-Json))) {
        throw 'Los identificadores, comprobaciones o el informe cambiaron tras reabrir.'
    }
    Write-Host 'CORRECTO: cliente reiniciado con misma solicitud, analisis, comprobacion local e informe.'
}
Write-Host "AnalisisId: $($envio.AnalisisId)"
Write-Host "Firma: $($envio.Solicitud.AnalisisLocal.FirmaDigital.Firma)"
Write-Host "Evaluacion: $($envio.Resultado.Evaluacion)"
Write-Host "IA: $($envio.Resultado.Ia.Estado); $($envio.Resultado.Ia.CodigoError)"
if ($envio.Resultado.Ia.Estado -ne 'Completada') { Write-Host 'PENDIENTE: revisar fallo o ausencia de explicacion IA.' }
