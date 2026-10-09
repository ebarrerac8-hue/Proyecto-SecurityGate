using System.Net;
using System.Text;
using System.Text.Json;
using SecureGate.Contratos;
using SecureGate.Servidor;

static ResultadoAnalisis Resultado() => new()
{
    AnalisisId = Guid.NewGuid(),
    Archivo = new InformacionArchivo
    {
        ArchivoId = Guid.NewGuid(), NombreOriginal = "privado.exe",
        Extension = ".exe", Sha256 = new string('a', 64), TamanoBytes = 3
    },
    Evaluacion = EvaluacionRiesgo.Incompleto,
    Resumen = "Resumen de reglas que debe conservarse."
};

static object Explicacion(string referencia = "E0001", string evaluacion = "Incompleto",
    string resumen = "La evaluación está incompleta. No se autoriza la ejecución.") => new
{
    evaluacionRecibida = evaluacion, resumen,
    referenciasResumen = new[] { referencia },
    observaciones = new[] { new { texto = "Faltan comprobaciones locales.", referencias = new[] { "E0003", "E0004" } } },
    limitaciones = new[] { "La observación es limitada." }
};

static HttpResponseMessage Respuesta(object explicacion) => new(HttpStatusCode.OK)
{
    Content = new StringContent(JsonSerializer.Serialize(new
    {
        candidates = new[] { new
        {
            finishReason = "STOP",
            content = new { parts = new[] { new { text = JsonSerializer.Serialize(explicacion) } } }
        } }
    }), Encoding.UTF8, "application/json")
};

static ClienteGemini Cliente(HttpClient http, string clave = "clave-de-prueba") => new(http,
    new OpcionesIa { Clave = clave, Modelo = "gemini-3.8-flash" });

static void Comprobar(bool condicion, string mensaje)
{
    if (!condicion) throw new Exception(mensaje);
}

using (var handler = new Simulador((_, _) => Task.FromResult(Respuesta(Explicacion()))))
using (var http = new HttpClient(handler))
{
    var original = Resultado();
    var informe = await Cliente(http).ExplicarAsync(original);
    Comprobar(informe.Estado == EstadoComprobacion.Completada &&
        informe.Observaciones.Count == 1 && original.Evaluacion == EvaluacionRiesgo.Incompleto &&
        original.Resumen == "Resumen de reglas que debe conservarse.", "Cambió las reglas o perdió la explicación.");
    Console.WriteLine("CORRECTO 1: acepta explicación con referencias y conserva la evaluación y resumen de reglas.");
}

foreach (var caso in new[]
{
    (Explicacion("E9999"), "referencia inventada"),
    (Explicacion(evaluacion: "SinIndicadoresDetectados"), "cambio de evaluación"),
    (Explicacion(resumen: "El archivo es seguro y puedes ejecutar."), "aprobación de ejecución")
})
{
    using var handler = new Simulador((_, _) => Task.FromResult(Respuesta(caso.Item1)));
    using var http = new HttpClient(handler);
    var informe = await Cliente(http).ExplicarAsync(Resultado());
    Comprobar(informe.CodigoError == "IA_RESPUESTA_INVALIDA" && informe.Resumen == "",
        "Aceptó " + caso.Item2);
    Console.WriteLine("CORRECTO: rechaza " + caso.Item2 + ".");
}

using (var handler = new Simulador((_, _) =>
    Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))))
using (var http = new HttpClient(handler))
{
    var informe = await Cliente(http).ExplicarAsync(Resultado());
    Comprobar(informe.CodigoError == "IA_CUOTA_AGOTADA" && handler.Intentos == 1, "Reintentó 429.");
    Console.WriteLine("CORRECTO 5: registra cuota agotada sin repetir la consulta.");
}

using (var handler = new Simulador((_, _) => throw new Exception("No debe enviar sin clave.")))
using (var http = new HttpClient(handler))
{
    var informe = await Cliente(http, "").ExplicarAsync(Resultado());
    Comprobar(informe.Estado == EstadoComprobacion.NoDisponible && handler.Intentos == 0,
        "Envió sin clave.");
    Console.WriteLine("CORRECTO 6: informa falta de clave sin enviar.");
}

using (var handler = new Simulador((_, _) => throw new Exception("No debe enviar cancelado.")))
using (var http = new HttpClient(handler))
using (var cancelacion = new CancellationTokenSource())
{
    cancelacion.Cancel();
    try
    {
        await Cliente(http).ExplicarAsync(Resultado(), cancelacion.Token);
        throw new Exception("No propagó la cancelación.");
    }
    catch (OperationCanceledException) when (cancelacion.IsCancellationRequested) { }
    Comprobar(handler.Intentos == 0, "Envió pese a cancelación previa.");
    Console.WriteLine("CORRECTO 7: conserva la cancelación del servidor.");
}

using (var handler = new Simulador(async (peticion, ct) =>
{
    string cuerpo = await peticion.Content!.ReadAsStringAsync(ct);
    Comprobar(!cuerpo.Contains("persona_privada") && !cuerpo.Contains("DETALLE_PRIVADO") &&
        !cuerpo.Contains(new string('a', 64)) && !cuerpo.Contains("privado.exe"),
        "Envió datos innecesarios.");
    return Respuesta(Explicacion());
}))
using (var http = new HttpClient(handler))
{
    var original = Resultado();
    original.Sandbox = new InformeSandbox
    {
        AnalisisId = original.AnalisisId, ArchivoId = original.Archivo.ArchivoId,
        Sha256 = original.Archivo.Sha256,
        Eventos = new List<EventoSandbox>
        {
            new()
            {
                Tipo = TipoEventoSandbox.ArchivoCreado,
                Recurso = @"C:\Users\persona_privada\Desktop\prueba.txt",
                Detalle = "DETALLE_PRIVADO"
            }
        }
    };
    var informe = await Cliente(http).ExplicarAsync(original);
    Comprobar(informe.Estado == EstadoComprobacion.Completada &&
        informe.EvidenciasEnviadas.Any(e => e.Descripcion.Contains("prueba.txt")),
        "Perdió el nombre mínimo de evidencia.");
    Console.WriteLine("CORRECTO 8: omite rutas completas, hash, nombre original y detalle libre.");
}

Console.WriteLine("LAS OCHO PRUEBAS DE IA TERMINARON CORRECTAMENTE (SIN CONSULTAS REALES).");

internal sealed class Simulador(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
{
    public int Intentos { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Intentos++;
        return responder(request, ct);
    }
}

