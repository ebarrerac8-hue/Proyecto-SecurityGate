param(
    [Parameter(Mandatory=$true)][string]$Entrega,
    [Parameter(Mandatory=$true)][string]$ServidorPublico
)
$ErrorActionPreference = 'Stop'
$Entrega = (Resolve-Path -LiteralPath $Entrega).Path
$cliente = Join-Path $Entrega 'Cliente'
if (!(Test-Path -LiteralPath (Join-Path $cliente 'SecureGateIntegrado.exe'))) {
    throw 'No se encontro el cliente publicado.'
}
$uri = [Uri]$ServidorPublico
if (!$uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or $uri.IsLoopback -or
    $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
    throw 'Se necesita la URL publica HTTPS del servidor, sin rutas adicionales.'
}
$clave = [Environment]::GetEnvironmentVariable('SECUREGATE_CLIENT_TOKEN', 'User')
if ([string]::IsNullOrWhiteSpace($clave)) { throw 'No se encontro la clave del servidor.' }
$bytes = [Convert]::FromBase64String($clave)
if ($bytes.Length -ne 32 -or $clave -ne [Convert]::ToBase64String($bytes)) { throw 'La clave no tiene el formato esperado.' }
Add-Type -AssemblyName System.Net.Http
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(20)
$http.DefaultRequestHeaders.Add('ngrok-skip-browser-warning', 'SecureGate')
$http.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $clave)
try {
    $respuesta = $http.GetAsync($uri.AbsoluteUri.TrimEnd('/') + '/api/estado').GetAwaiter().GetResult()
    try { if ([int]$respuesta.StatusCode -ne 200) { throw "El servidor no valido el acceso: HTTP $([int]$respuesta.StatusCode)." } }
    finally { $respuesta.Dispose() }
} finally { $http.Dispose() }
$temporal = Join-Path ([IO.Path]::GetTempPath()) ('SecureGateCliente_' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $temporal | Out-Null
    $destino = Join-Path $temporal 'SecureGate'
    New-Item -ItemType Directory -Path $destino | Out-Null
    # Solo componentes publicados; excluir lanzadores y configuraciones antiguas.
    foreach ($archivo in Get-ChildItem -LiteralPath $cliente -Recurse -File) {
        if ($archivo.Extension -notin @('.exe','.dll','.json','.config','.dat','.bin','.xml','.txt')) { continue }
        if ($archivo.Name -eq 'conexion-cliente.privado.json') { continue }
        $relativa = $archivo.FullName.Substring($cliente.Length + 1)
        $objetivo = Join-Path $destino $relativa
        New-Item -ItemType Directory -Path (Split-Path -Parent $objetivo) -Force | Out-Null
        Copy-Item -LiteralPath $archivo.FullName -Destination $objetivo
    }
    # Rechazar claves de proveedores y certificados; la unica credencial distribuida es la del cliente.
    foreach ($archivo in Get-ChildItem -LiteralPath $destino -Recurse -File) {
        if ($archivo.Extension -in @('.json','.config','.txt','.xml')) {
            $contenido = [IO.File]::ReadAllText($archivo.FullName)
            foreach ($nombre in @('SECUREGATE_GEMINI_API_KEY','SECUREGATE_VIRUSTOTAL_API_KEY')) {
                foreach ($ambito in @('User','Process')) {
                    $proveedor = [Environment]::GetEnvironmentVariable($nombre, $ambito)
                    if ($proveedor -and $contenido.Contains($proveedor)) { throw 'Se encontro una clave de proveedor en el paquete.' }
                }
            }
        }
    }
    @{Servidor=$uri.AbsoluteUri; ClaveCliente=$clave} | ConvertTo-Json |
        Set-Content -LiteralPath (Join-Path $destino 'conexion-cliente.privado.json') -Encoding UTF8
    @'
SECUREGATE
Extraiga toda la carpeta y abra SecureGateIntegrado.exe. No se necesita configurar nada.
SecureGate comprueba la conexion y protege las nuevas descargas .exe automaticamente.
Puede minimizar o cerrar el cuadro de progreso: el analisis continua en segundo plano.
Al terminar aparecera el resultado. El icono junto al reloj permite abrir la interfaz o salir.
Este paquete contiene acceso al servidor. Compartalo solo con los participantes de la prueba.
'@ | Set-Content -LiteralPath (Join-Path $destino 'LEEME.txt') -Encoding UTF8
    $zip = Join-Path $Entrega ('SecureGate_Cliente_Listo_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.zip')
    Compress-Archive -LiteralPath $destino -DestinationPath $zip
    Write-Host "CORRECTO: paquete listo para abrir, con conexion verificada: $zip"
} finally {
    $clave = $null
    if (Test-Path -LiteralPath $temporal) { Remove-Item -LiteralPath $temporal -Recurse -Force }
}
