param (
    [string]$RutaEjecutable = "C:\Muestra\muestra.exe",
    [int]$TiempoEsperaSegundos = 30
)

$RutaSalidaJson = "C:\Muestra\evidencias.json"
$Eventos = @()

$Eventos += [PSCustomObject]@{
    TimestampUtc = [DateTime]::UtcNow.ToString("o")
    TipoEvento   = "ObservadorIniciado"
    Detalle      = "Iniciando monitoreo de la muestra"
}

$ProcesosIniciales = Get-Process | Select-Object -ExpandProperty Id

if (Test-Path $RutaEjecutable) {
    Start-Process -FilePath $RutaEjecutable -ErrorAction SilentlyContinue
    $Eventos += [PSCustomObject]@{
        TimestampUtc = [DateTime]::UtcNow.ToString("o")
        TipoEvento   = "ProcesoIniciado"
        Detalle      = "Se ejecuto $RutaEjecutable"
    }
} else {
    $Eventos += [PSCustomObject]@{
        TimestampUtc = [DateTime]::UtcNow.ToString("o")
        TipoEvento   = "Error"
        Detalle      = "No se encontro la muestra en $RutaEjecutable"
    }
}

Start-Sleep -Seconds $TiempoEsperaSegundos

$ProcesosNuevos = Get-Process | Where-Object { $ProcesosIniciales -notcontains $_.Id }
foreach ($p in $ProcesosNuevos) {
    $Eventos += [PSCustomObject]@{
        TimestampUtc = [DateTime]::UtcNow.ToString("o")
        TipoEvento   = "ProcesoCreado"
        Detalle      = "Proceso detectado: $($p.Name) (PID: $($p.Id))"
    }
}

$Eventos | ConvertTo-Json | Out-File -FilePath $RutaSalidaJson -Encoding utf8