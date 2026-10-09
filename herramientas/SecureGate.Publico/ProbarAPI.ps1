param(
    [Parameter(Mandatory = $true)][string]$Servidor,
    [Guid]$AnalisisId = "e4ead7cb-21c5-4e34-967b-199658834363"
)
$ErrorActionPreference = "Stop"
$uri = [Uri]::new($Servidor)
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne "https" -or $uri.Host -notmatch "^[a-z0-9-]+\.trycloudflare\.com$" -or $uri.AbsolutePath -ne "/" -or $uri.Query -or $uri.Fragment -or $uri.UserInfo -or $uri.Port -ne 443) {
    throw "Usá la URL HTTPS del túnel, sin rutas adicionales."
}
$base = $uri.AbsoluteUri.TrimEnd('/')
Add-Type -AssemblyName System.Net.Http
$token = [Environment]::GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", "User")
if (-not $token) { throw "Falta configurar la clave de acceso del cliente." }
$http = [Net.Http.HttpClient]::new()
$http.Timeout = [TimeSpan]::FromSeconds(20)
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
    Write-Host "CORRECTO: API pública rechaza acceso sin clave."
    Consultar "/api/estado" ([Convert]::ToBase64String((New-Object byte[] 32))) 401 | Out-Null
    Write-Host "CORRECTO: clave incorrecta rechazada."
    Consultar "/api/estado" $token 200 | Out-Null
    Write-Host "CORRECTO: HTTPS público y acceso autorizado."
    $informe = Consultar "/api/analisis/$AnalisisId" $token 200
    if ($informe.analisisId -ne $AnalisisId.ToString() -or $informe.ia.estado -ne "Completada") { throw "El informe no coincide o no conserva la IA." }
    Write-Host "CORRECTO: informe real con Gemini recuperado."
    Consultar ("/api/analisis/" + [Guid]::NewGuid()) $token 404 | Out-Null
    Write-Host "CORRECTO: análisis inexistente identificado."
    Write-Host "PRUEBA DE LA URL PÚBLICA COMPLETADA. Falta demostrar el envío completo desde la PC del compañero."
} finally { $http.Dispose(); $token = $null }
