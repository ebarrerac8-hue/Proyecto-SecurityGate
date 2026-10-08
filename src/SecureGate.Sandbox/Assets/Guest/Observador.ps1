param (
    [string]$RutaEjecutable = "C:\Muestra\muestra.exe",
    [int]$TiempoEsperaSegundos = 30
)

$RutaSalidaJson = "C:\Muestra\evidencias.json"
$Eventos = @()

$Eventos += [PSCustomObject]@{
    EventoId        = [guid]::NewGuid().ToString()
    FechaUtc        = [DateTime]::UtcNow.ToString("o")
    Tipo            = 0 
    ProcesoId       = $PID
    ProcesoPadreId  = $null
    NombreProceso   = "powershell.exe"
    Recurso         = $null
    Detalle         = "Iniciando monitoreo de la muestra"
}

$ProcesosIniciales = Get-Process | Select-Object -ExpandProperty Id

if (Test-Path $RutaEjecutable) {
    $proc = Start-Process -FilePath $RutaEjecutable -PassThru -ErrorAction SilentlyContinue
    
    $Eventos += [PSCustomObject]@{
        EventoId        = [guid]::NewGuid().ToString()
        FechaUtc        = [DateTime]::UtcNow.ToString("o")
        Tipo            = 1
        ProcesoId       = if ($proc) { $proc.Id } else { $null }
        ProcesoPadreId  = $PID
        NombreProceso   = [System.IO.Path]::GetFileName($RutaEjecutable)
        Recurso         = $RutaEjecutable
        Detalle         = "Se ejecuto la muestra en $RutaEjecutable"
    }
} else {
    $Eventos += [PSCustomObject]@{
        EventoId        = [guid]::NewGuid().ToString()
        FechaUtc        = [DateTime]::UtcNow.ToString("o")
        Tipo            = 0
        ProcesoId       = $null
        ProcesoPadreId  = $null
        NombreProceso   = $null
        Recurso         = $RutaEjecutable
        Detalle         = "No se encontro la muestra en $RutaEjecutable"
    }
}

Start-Sleep -Seconds $TiempoEsperaSegundos

$ProcesosNuevos = Get-Process | Where-Object { $ProcesosIniciales -notcontains $_.Id }

foreach ($p in $ProcesosNuevos) {
    $Eventos += [PSCustomObject]@{
        EventoId        = [guid]::NewGuid().ToString()
        FechaUtc        = [DateTime]::UtcNow.ToString("o")
        Tipo            = 1
        ProcesoId       = $p.Id
        ProcesoPadreId  = $null
        NombreProceso   = $p.Name
        Recurso         = $null
        Detalle         = "Proceso hijo o nuevo detectado: $($p.Name) (PID: $($p.Id))"
    }
}

$Eventos | ConvertTo-Json -Depth 3 | Out-File -FilePath $RutaSalidaJson -Encoding utf8