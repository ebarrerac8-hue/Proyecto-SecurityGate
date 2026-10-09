param(
    [Parameter(Mandatory = $true)][string]$IPServidor,
    [Guid]$AnalisisId = "e4ead7cb-21c5-4e34-967b-199658834363",
    [switch]$PermitirPruebaLocal
)
$ErrorActionPreference = "Stop"
$IPServidor = ([Net.IPAddress]::Parse($IPServidor)).ToString()
if (-not $PermitirPruebaLocal -and (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -eq $IPServidor })) {
    throw "Esta es la computadora servidor. Ejecutá la prueba en el segundo equipo; una consulta local no demuestra conexión remota."
}
Add-Type -AssemblyName System.Net.Http
$token = [Environment]::GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", "User")
if ([string]::IsNullOrWhiteSpace($token)) { throw "Primero ejecutá PrepararCliente.ps1." }
$base = "https://${IPServidor}:5443"
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(15)
function Consultar([string]$ruta, [string]$clave, [int]$esperado) {
    $peticion = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, "$base$ruta")
    if ($clave) { $peticion.Headers.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $clave) }
    $respuesta = $null
    try {
        $respuesta = $http.SendAsync($peticion).GetAwaiter().GetResult()
        if ([int]$respuesta.StatusCode -ne $esperado) { throw "Se esperaba HTTP $esperado; llegó $([int]$respuesta.StatusCode)." }
        return ($respuesta.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json)
    } finally { if ($null -ne $respuesta) { $respuesta.Dispose() }; $peticion.Dispose() }
}
try {
    Consultar "/api/estado" "" 401 | Out-Null
    Write-Host "CORRECTO: acceso sin clave rechazado."
    Consultar "/api/estado" ([Convert]::ToBase64String((New-Object byte[] 32))) 401 | Out-Null
    Write-Host "CORRECTO: clave incorrecta rechazada."
    Consultar "/api/estado" $token 200 | Out-Null
    Write-Host "CORRECTO: HTTPS con certificado y clave válidos."
    $informe = Consultar "/api/analisis/$AnalisisId" $token 200
    if ($informe.analisisId -ne $AnalisisId.ToString() -or $informe.ia.estado -ne "Completada") {
        throw "El informe no coincide o no conserva la explicación de IA."
    }
    Write-Host "CORRECTO: informe y explicación Gemini recuperados desde el servidor."
    Consultar ("/api/analisis/" + [Guid]::NewGuid()) $token 404 | Out-Null
    Write-Host "CORRECTO: análisis inexistente identificado."
    if ($PermitirPruebaLocal) { Write-Host "PRUEBA LOCAL DEL CERTIFICADO COMPLETADA. Falta ejecutarla desde otra computadora." }
    else { Write-Host "PRUEBA ENTRE DOS COMPUTADORAS COMPLETADA. No se abrió Sandbox." }
} finally { $http.Dispose(); $token = $null }
