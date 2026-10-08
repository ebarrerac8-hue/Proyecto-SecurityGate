$ErrorActionPreference = "Stop"
$solicitud = Get-Content "C:\Entrada\solicitud.json" -Raw | ConvertFrom-Json
$maxEventos = 2000

$informe = [ordered]@{
    AnalisisId = $solicitud.AnalisisId
    ArchivoId = $solicitud.ArchivoId
    Sha256 = $solicitud.Sha256
    Estado = 2
    MuestraEjecutada = $false
    ObservadorIniciado = $true
    RedHabilitada = $false
    InicioUtc = [DateTimeOffset]::UtcNow.ToString("o")
    FinUtc = $null
    Eventos = [System.Collections.Generic.List[object]]::new()
    Limitaciones = [System.Collections.Generic.List[string]]::new()
    CodigoError = $null
}

function Guardar-Informe([string]$Nombre) {
    $temporal = "C:\Salida\$Nombre.tmp"
    $destino = "C:\Salida\$Nombre.json"
    $informe | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $temporal -Encoding UTF8
    Move-Item -LiteralPath $temporal -Destination $destino -Force
}

function Agregar-Evento([int]$Tipo, $ProcesoId, $PadreId,
    [string]$Nombre, [string]$Recurso, [string]$Detalle) {
    if ($informe.Eventos.Count -ge $maxEventos) {
        if (-not $informe.Limitaciones.Contains("Se alcanzó el límite de 2000 eventos.")) {
            $informe.Limitaciones.Add("Se alcanzó el límite de 2000 eventos.")
        }
        return
    }
    $informe.Eventos.Add([ordered]@{
        EventoId = [Guid]::NewGuid().ToString()
        FechaUtc = [DateTimeOffset]::UtcNow.ToString("o")
        Tipo = $Tipo
        ProcesoId = $ProcesoId
        ProcesoPadreId = $PadreId
        NombreProceso = $Nombre
        Recurso = $Recurso
        Detalle = $Detalle
    })
}

function Obtener-Procesos {
    $mapa = @{}
    foreach ($p in @(Get-CimInstance Win32_Process -ErrorAction Stop)) {
        $clave = "$($p.ProcessId)|$($p.CreationDate)"
        $mapa[$clave] = $p
    }
    return ,$mapa
}

$watcher = $null
$fuentes = @("SGCreado", "SGModificado", "SGEliminado", "SGError")
$procesoMuestra = $null

try {
    # Confirmar que el observador arrancó dentro del invitado.
    Guardar-Informe "inicio"
    New-Item -ItemType Directory -Path "C:\Prueba" -Force | Out-Null
    Copy-Item "C:\Entrada\muestra.exe" "C:\Prueba\muestra.exe"

    $hash = (Get-FileHash "C:\Prueba\muestra.exe" -Algorithm SHA256).Hash
    if ($hash -ne $solicitud.Sha256) {
        $informe.CodigoError = "SANDBOX_HASH_INVITADO_NO_COINCIDE"
        throw "El SHA-256 de la copia dentro de Sandbox no coincide."
    }

    # Observación limitada al perfil; no incluye registro ni todo el disco.
    $watcher = [System.IO.FileSystemWatcher]::new($env:USERPROFILE)
    $watcher.IncludeSubdirectories = $true
    $watcher.InternalBufferSize = 32768
    Register-ObjectEvent $watcher Created -SourceIdentifier "SGCreado" | Out-Null
    Register-ObjectEvent $watcher Changed -SourceIdentifier "SGModificado" | Out-Null
    Register-ObjectEvent $watcher Deleted -SourceIdentifier "SGEliminado" | Out-Null
    Register-ObjectEvent $watcher Error -SourceIdentifier "SGError" | Out-Null
    $watcher.EnableRaisingEvents = $true

    $anteriores = Obtener-Procesos
    if ($solicitud.ModoDemostracion -eq $true) {
        # Acciones fijas de demostración, ejecutadas por la muestra cmd.exe.
        $acciones = '/d /c echo Creado dentro de Sandbox > "%USERPROFILE%\Desktop\SecureGate_Prueba.txt" & ping -n 4 127.0.0.1 > nul & echo Modificado dentro de Sandbox >> "%USERPROFILE%\Desktop\SecureGate_Prueba.txt" & ping -n 4 127.0.0.1 > nul & del "%USERPROFILE%\Desktop\SecureGate_Prueba.txt" & ping -n 4 127.0.0.1 > nul'
        $informe.Limitaciones.Add("Demostración con cmd.exe de Windows y acciones predeterminadas; no es el análisis de un instalador.")
        $procesoMuestra = Start-Process -FilePath "C:\Prueba\muestra.exe" -ArgumentList $acciones -WorkingDirectory "C:\Prueba" -PassThru -ErrorAction Stop
    }
    else {
        $procesoMuestra = Start-Process -FilePath "C:\Prueba\muestra.exe" -WorkingDirectory "C:\Prueba" -PassThru -ErrorAction Stop
    }
    $informe.MuestraEjecutada = $true
    Agregar-Evento 1 $procesoMuestra.Id $PID "muestra.exe" "C:\Prueba\muestra.exe" "Windows inició el proceso de la muestra."

    $finObservacion = [DateTimeOffset]::UtcNow.AddSeconds([int]$solicitud.TiempoObservacionSegundos)
    $salidaRegistrada = $false

    while ([DateTimeOffset]::UtcNow -lt $finObservacion) {
        foreach ($evento in @(Get-Event -ErrorAction SilentlyContinue)) {
            if ($fuentes -notcontains $evento.SourceIdentifier) { continue }
            if ($evento.SourceIdentifier -eq "SGError") {
                $informe.Limitaciones.Add("El observador de archivos perdió eventos o reportó un error.")
            }
            else {
                $tipo = switch ($evento.SourceIdentifier) {
                    "SGCreado" { 3 }
                    "SGModificado" { 4 }
                    "SGEliminado" { 5 }
                }
                Agregar-Evento $tipo $null $null "" $evento.SourceEventArgs.FullPath "Evento de archivo del perfil; autor no identificado."
            }
            Remove-Event -EventIdentifier $evento.EventIdentifier
        }

        $actuales = Obtener-Procesos
        foreach ($clave in $actuales.Keys) {
            if (-not $anteriores.ContainsKey($clave)) {
                $p = $actuales[$clave]
                Agregar-Evento 1 ([int]$p.ProcessId) ([int]$p.ParentProcessId) $p.Name $p.ExecutablePath "Proceso nuevo observado; no necesariamente pertenece a la muestra."
            }
        }
        foreach ($clave in $anteriores.Keys) {
            if (-not $actuales.ContainsKey($clave)) {
                $p = $anteriores[$clave]
                Agregar-Evento 2 ([int]$p.ProcessId) ([int]$p.ParentProcessId) $p.Name $p.ExecutablePath "El proceso dejó de aparecer en el muestreo."
            }
        }
        $anteriores = $actuales
        $procesoMuestra.Refresh()
        if (-not $salidaRegistrada -and $procesoMuestra.HasExited) {
            Agregar-Evento 0 $procesoMuestra.Id $PID "muestra.exe" "" "El proceso inicial terminó. Código de salida: $($procesoMuestra.ExitCode)."
            $salidaRegistrada = $true
        }
        Start-Sleep -Milliseconds 500
    }

    if (-not $procesoMuestra.HasExited) {
        $informe.Limitaciones.Add("El proceso inicial seguía abierto al terminar la observación. Puede requerir interacción; no se confirmó la finalización de la instalación.")
    }
    $informe.Estado = 1
}
catch {
    $informe.Estado = 2
    if (-not $informe.CodigoError) {
        $informe.CodigoError = "SANDBOX_OBSERVADOR_ERROR"
        $causa = $_.Exception
        while ($null -ne $causa) {
            if ($causa -is [System.ComponentModel.Win32Exception] -and
                $causa.NativeErrorCode -in @(4551, 1260)) {
                $informe.CodigoError = "SANDBOX_EJECUCION_BLOQUEADA"
                break
            }
            $causa = $causa.InnerException
        }
    }
    $informe.Limitaciones.Add($_.Exception.Message)
}
finally {
    if ($null -ne $watcher) {
        $watcher.EnableRaisingEvents = $false
        $watcher.Dispose()
    }
    foreach ($fuente in $fuentes) {
        Unregister-Event -SourceIdentifier $fuente -ErrorAction SilentlyContinue
    }
    if ($null -ne $procesoMuestra) { $procesoMuestra.Dispose() }
    $informe.FinUtc = [DateTimeOffset]::UtcNow.ToString("o")
    Guardar-Informe "informe"
    # Apaga únicamente el invitado donde se está ejecutando este script.
    & "$env:SystemRoot\System32\shutdown.exe" /s /t 0
}
