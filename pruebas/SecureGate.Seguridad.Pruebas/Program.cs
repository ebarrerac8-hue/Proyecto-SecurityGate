using Microsoft.AspNetCore.Http;
using SecureGate.Servidor;
using SecureGate.Cliente;
using System.Net;

string token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
var acceso = new AccesoCliente(token);
async Task Probar(string nombre, string esquema, string[]? cabeceras, int esperado, bool debeContinuar)
{
    var contexto = new DefaultHttpContext();
    contexto.Request.Scheme = esquema;
    contexto.Response.Body = new MemoryStream();
    if (cabeceras is not null) contexto.Request.Headers.Authorization = cabeceras;
    bool continuo = false;
    await acceso.ProcesarAsync(contexto, () => { continuo = true; return Task.CompletedTask; });
    if (contexto.Response.StatusCode != esperado || continuo != debeContinuar)
        throw new Exception("FALLÓ: " + nombre);
    Console.WriteLine("CORRECTO: " + nombre);
}
await Probar("sin clave rechazada antes del endpoint", "https", null, 401, false);
await Probar("clave incorrecta rechazada", "https", ["Bearer " + Convert.ToBase64String(new byte[32])], 401, false);
await Probar("cabeceras duplicadas rechazadas", "https", ["Bearer " + token, "Bearer " + token], 401, false);
await Probar("clave válida permite el endpoint", "https", ["Bearer " + token], 200, true);
await Probar("HTTP rechazado incluso con clave válida", "http", ["Bearer " + token], 403, false);
try
{
    using var h = new HttpClient();
    using var c = new ClienteAnalisisHttp(h, new Uri("http://localhost:5080"), token);
    throw new Exception("El cliente aceptó HTTP.");
}
catch (ArgumentException) { Console.WriteLine("CORRECTO: cliente rechaza URL HTTP"); }
int llamadas = 0;
using var http = new HttpClient(new Simulado(request =>
{
    llamadas++;
    if (request.Headers.Authorization?.Scheme != "Bearer" || request.Headers.Authorization.Parameter != token)
        throw new Exception("No se envió la credencial por petición.");
    return new HttpResponseMessage(HttpStatusCode.Unauthorized)
    {
        Content = new StringContent("{\"codigo\":\"CLIENTE_NO_AUTORIZADO\",\"mensaje\":\"Acceso rechazado\"}")
    };
}));
using var cliente = new ClienteAnalisisHttp(http, new Uri("https://localhost:5443"), token);
try
{
    await cliente.ConsultarAsync(Guid.NewGuid());
    throw new Exception("El cliente aceptó un 401.");
}
catch (ErrorClienteAnalisis error) when (error.Codigo == "CLIENTE_NO_AUTORIZADO" && !error.Reintentable && llamadas == 1)
{
    Console.WriteLine("CORRECTO: credencial enviada y 401 identificado sin reintento");
}
Console.WriteLine("LAS SIETE PRUEBAS DE SEGURIDAD TERMINARON CORRECTAMENTE.");

sealed class Simulado(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(responder(request));
}
