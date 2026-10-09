param(
    [ValidateSet('Concurrencia','PrepararRecuperacion','VerificarRecuperacion','ConsultaPublica')][string]$Modo = 'ConsultaPublica',
    [string]$Servidor = 'https://localhost:5443',
    [string]$RutaMuestra = (Join-Path $env:USERPROFILE 'Documents\SecureGate_Prueba_Remota\PruebaSecureGate_CMD.exe')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$token = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN','User')
if ([string]::IsNullOrWhiteSpace($token)) { throw 'Falta la clave de cliente guardada en Windows.' }
$uri = [Uri]::new($Servidor)
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') { throw 'Usa una direccion base HTTPS.' }
$base = $uri.AbsoluteUri.TrimEnd('/')
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(30)
$http.DefaultRequestHeaders.Add('ngrok-skip-browser-warning','SecureGate')
$carpeta = Join-Path $env:LOCALAPPDATA 'SecureGate\EvidenciaEntrega'
New-Item -ItemType Directory -Path $carpeta -Force | Out-Null
$estadoRecuperacion = Join-Path $carpeta 'recuperacion-pendiente.json'

function Leer-Informe([string]$Id) {
    $r = $http.GetAsync("$base/api/analisis/$Id").GetAwaiter().GetResult()
    try {
        if ([int]$r.StatusCode -ne 200) { throw "Consulta de ${Id}: HTTP $([int]$r.StatusCode)." }
        return ($r.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
    } finally { $r.Dispose() }
}
function Iniciar-Envio([string]$Json,[string]$Ruta) {
    $form = [Net.Http.MultipartFormDataContent]::new()
    try {
        $datos = [Net.Http.StringContent]::new($Json,[Text.Encoding]::UTF8,'application/json')
        $form.Add($datos,'solicitud')
        $contenido = [Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($Ruta))
        $form.Add($contenido,'archivo',[IO.Path]::GetFileName($Ruta))
        return [pscustomobject]@{Formulario=$form; Tarea=$http.PostAsync("$base/api/analisis",$form)}
    } catch { $form.Dispose(); throw }
}
function Terminar-Envio($Envio) {
    try {
        $r = $Envio.Tarea.GetAwaiter().GetResult()
        try {
            $texto = $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ([int]$r.StatusCode -ne 202) { throw "Envio rechazado: HTTP $([int]$r.StatusCode); $texto" }
            return ($texto | ConvertFrom-Json)
        } finally { $r.Dispose() }
    } finally { $Envio.Formulario.Dispose() }
}
function Nueva-Solicitud($Informe) {
    $local = ($Informe.analisisLocal | ConvertTo-Json -Depth 32 | ConvertFrom-Json)
    if ($null -eq $local.archivo) { throw 'El informe base no conserva AnalisisLocal.Archivo.' }
    $local.archivo.archivoId = [Guid]::NewGuid().ToString()
    return (@{solicitudId=[Guid]::NewGuid().ToString(); analisisLocal=$local} | ConvertTo-Json -Depth 32 -Compress)
}
function Esperar-Final([string]$Id) {
    $limite = [DateTime]::UtcNow.AddMinutes(10)
    $ultimo = ''
    do {
        $r = Leer-Informe $Id
        if ($r.estado -ne $ultimo) { Write-Host "$Id : $($r.estado)"; $ultimo = $r.estado }
        if ($r.estado -in @('Completado','Fallido','Cancelado')) { return $r }
        Start-Sleep -Seconds 3
    } while ([DateTime]::UtcNow -lt $limite)
    throw "Tiempo agotado esperando $Id. El trabajo permanece guardado; no se borro."
}
function Validar-Demo($r) {
    if ($r.estado -ne 'Completado' -or $r.sandbox.estado -ne 'Completada' -or -not $r.sandbox.muestraEjecutada -or -not $r.sandbox.observadorIniciado -or $r.sandbox.redHabilitada -or @($r.sandbox.eventos).Count -eq 0) {
        throw "La demostracion $($r.analisisId) no termino correctamente: $($r.codigoError)."
    }
}
function Guardar-Evidencia($r,[string]$Nombre) {
    $r | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $carpeta $Nombre) -Encoding UTF8
}

try {
    if ($Modo -eq 'ConsultaPublica') {
        $base = 'https://entrust-factual-cozy.ngrok-free.dev'
        $r = $http.GetAsync("$base/api/estado").GetAwaiter().GetResult()
        try { if ([int]$r.StatusCode -ne 401) { throw "Sin clave se esperaba 401; llego $([int]$r.StatusCode)." } } finally { $r.Dispose() }
        Write-Host 'CORRECTO: URL fija rechaza acceso sin clave.'
    }
    $http.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$token)
    $token = $null
    $r = $http.GetAsync("$base/api/estado").GetAwaiter().GetResult()
    try { if ([int]$r.StatusCode -ne 200) { throw "API no disponible: HTTP $([int]$r.StatusCode)." } } finally { $r.Dispose() }
    $modelo = Leer-Informe 'bfafb3f9-12ed-4944-8a66-f3071b76575c'
    if ($Modo -eq 'ConsultaPublica') {
        if ($modelo.ia.estado -ne 'Completada') { throw 'El informe base no conserva la IA completada.' }
        Guardar-Evidencia $modelo 'consulta-publica.json'
        Write-Host 'CORRECTO: informe real e IA recuperados por la direccion fija.'
        exit 0
    }
    if ($Modo -eq 'VerificarRecuperacion') {
        if (-not (Test-Path -LiteralPath $estadoRecuperacion)) { throw 'Primero ejecuta PrepararRecuperacion.' }
        $pendiente = Get-Content -LiteralPath $estadoRecuperacion -Raw | ConvertFrom-Json
        $RutaMuestra = $pendiente.rutaMuestra
    }
    if (-not (Test-Path -LiteralPath $RutaMuestra -PathType Leaf)) { throw "No se encontro la muestra de demostracion: $RutaMuestra" }
    $hash = (Get-FileHash -LiteralPath $RutaMuestra -Algorithm SHA256).Hash
    if ($hash -ne $modelo.archivo.sha256 -or (Get-Item -LiteralPath $RutaMuestra).Length -ne $modelo.archivo.tamanoBytes) { throw 'La muestra no coincide con la demostracion anterior. No se envio ni ejecuto.' }

    if ($Modo -eq 'Concurrencia') {
        $jsonA = Nueva-Solicitud $modelo
        $jsonB = Nueva-Solicitud $modelo
        $envios = @()
        try {
            # Se inician las cuatro solicitudes antes de esperar sus respuestas.
            $envios += Iniciar-Envio $jsonA $RutaMuestra
            $envios += Iniciar-Envio $jsonA $RutaMuestra
            $envios += Iniciar-Envio $jsonA $RutaMuestra
            $envios += Iniciar-Envio $jsonB $RutaMuestra
            $aceptados = @($envios | ForEach-Object { Terminar-Envio $_ })
        } finally { foreach ($e in $envios) { $e.Formulario.Dispose() } }
        if (@($aceptados[0..2].analisisId | Select-Object -Unique).Count -ne 1 -or @($aceptados[0..2].fechaRegistroUtc | Select-Object -Unique).Count -ne 1) { throw 'Las solicitudes simultaneas iguales no devolvieron el mismo trabajo y fecha.' }
        if ($aceptados[0].analisisId -eq $aceptados[3].analisisId) { throw 'Dos solicitudes distintas devolvieron el mismo trabajo.' }
        Write-Host 'CORRECTO: tres envios simultaneos iguales recuperaron un solo identificador; otro envio creo un trabajo distinto.'
        $a = Esperar-Final $aceptados[0].analisisId
        $b = Esperar-Final $aceptados[3].analisisId
        Guardar-Evidencia $a 'concurrencia-A.json'
        Guardar-Evidencia $b 'concurrencia-B.json'
        Validar-Demo $a
        Validar-Demo $b
        $orden = @(@($a,$b) | Sort-Object { [DateTimeOffset]$_.sandbox.inicioUtc })
        if ([DateTimeOffset]$orden[1].sandbox.inicioUtc -lt [DateTimeOffset]$orden[0].sandbox.finUtc) { throw 'Las dos observaciones Sandbox se superpusieron.' }
        Write-Host 'CORRECTO: ambos trabajos terminaron y sus intervalos de Sandbox no se superpusieron.'
        Write-Host 'PRUEBA REAL DE CONCURRENCIA COMPLETADA.'
    }
    elseif ($Modo -eq 'PrepararRecuperacion') {
        if (Test-Path -LiteralPath $estadoRecuperacion) {
            $anterior = Get-Content -LiteralPath $estadoRecuperacion -Raw | ConvertFrom-Json
            $viejo = Leer-Informe $anterior.analisisId
            if ($viejo.estado -notin @('Completado','Fallido','Cancelado')) { throw 'Ya hay una prueba de recuperacion pendiente. Usa VerificarRecuperacion.' }
        }
        $json = Nueva-Solicitud $modelo
        $aceptado = Terminar-Envio (Iniciar-Envio $json $RutaMuestra)
        $limite = [DateTime]::UtcNow.AddSeconds(60)
        do {
            $r = Leer-Informe $aceptado.analisisId
            if ($r.estado -eq 'EnProceso' -and $r.resumen -like '*prueba aislada*') { break }
            if ($r.estado -in @('Completado','Fallido','Cancelado')) { throw 'El trabajo termino antes de la interrupcion. No se ha demostrado recuperacion.' }
            Start-Sleep -Milliseconds 500
        } while ([DateTime]::UtcNow -lt $limite)
        if ($r.estado -ne 'EnProceso' -or $r.resumen -notlike '*prueba aislada*') { throw 'No se alcanzo la fase Sandbox a tiempo. No se declaro exito.' }
        $servidores = @(Get-Process -Name SecureGate.Servidor -ErrorAction SilentlyContinue)
        if ($servidores.Count -ne 1) { throw 'La prueba requiere exactamente un servidor publicado SecureGate.Servidor.exe.' }
        @{analisisId=$aceptado.analisisId; solicitudJson=$json; rutaMuestra=(Resolve-Path -LiteralPath $RutaMuestra).Path; fechaRegistroUtc=$aceptado.fechaRegistroUtc; preparadoUtc=[DateTimeOffset]::UtcNow.ToString('o'); pidServidor=$servidores[0].Id; inicioServidorUtc=$servidores[0].StartTime.ToUniversalTime().ToString('o')} | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $estadoRecuperacion -Encoding UTF8
        Write-Host "AnalisisId: $($aceptado.analisisId)"
        Write-Host 'AHORA: Ctrl+C SOLO en la ventana del servidor. Espera su cierre y que se cierre Sandbox.'
        Write-Host 'Reinicia SOLO el servidor con IniciarServidorPublicado.cmd. Ngrok puede permanecer abierto.'
        Write-Host 'Luego ejecuta VerificarRecuperacion. Esta salida todavia no confirma recuperacion.'
    }
    elseif ($Modo -eq 'VerificarRecuperacion') {
        $servidores = @(Get-Process -Name SecureGate.Servidor -ErrorAction SilentlyContinue)
        if ($servidores.Count -ne 1 -or $servidores[0].StartTime.ToUniversalTime() -le ([DateTimeOffset]$pendiente.preparadoUtc).UtcDateTime) { throw 'No se detecto un nuevo inicio del servidor posterior a la preparacion. No se declaro recuperacion.' }
        $registro = Join-Path $env:LOCALAPPDATA ('SecureGate\Servidor\Trabajos\' + ([Guid]$pendiente.analisisId).ToString('N') + '.json')
        if (-not (Test-Path -LiteralPath $registro)) { throw 'No se encontro el registro local esperado. Revisa si configuraste otra carpeta de datos.' }
        # La consulta final puede haber terminado antes de este script: se comprueba el nuevo inicio en los eventos Sandbox.
        $final = Esperar-Final $pendiente.analisisId
        Guardar-Evidencia $final 'recuperacion-final.json'
        Validar-Demo $final
        if ([DateTimeOffset]$final.sandbox.inicioUtc -le [DateTimeOffset]$pendiente.preparadoUtc) { throw 'El inicio final de Sandbox no es posterior a la preparacion. No se demostro reinicio durante la prueba.' }
        $reintento = Terminar-Envio (Iniciar-Envio $pendiente.solicitudJson $RutaMuestra)
        if ($reintento.analisisId -ne $pendiente.analisisId -or $reintento.fechaRegistroUtc -ne $pendiente.fechaRegistroUtc) { throw 'El reintento no conservo el identificador y la fecha.' }
        $otra = Leer-Informe $pendiente.analisisId
        if (($final.sandbox | ConvertTo-Json -Depth 40 -Compress) -ne ($otra.sandbox | ConvertTo-Json -Depth 40 -Compress) -or ($final.ia | ConvertTo-Json -Depth 40 -Compress) -ne ($otra.ia | ConvertTo-Json -Depth 40 -Compress)) { throw 'El reintento cambio las evidencias o la IA.' }
        Write-Host 'CORRECTO: trabajo interrumpido recuperado con el mismo identificador.'
        Write-Host 'CORRECTO: reintento conserva fecha, Sandbox e IA.'
        Write-Host 'PRUEBA REAL DE RECUPERACION COMPLETADA.'
    }
    Write-Host "Evidencias guardadas en: $carpeta"
} finally { $http.Dispose() }

