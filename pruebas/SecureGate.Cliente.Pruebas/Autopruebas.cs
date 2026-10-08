using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Cliente;
using SecureGate.Contratos;

internal static class Autopruebas
{
    private static readonly JsonSerializerOptions Json = Opciones();

    public static async Task EjecutarAsync()
    {
        var solicitud = CrearSolicitud();
        var aceptado = new AnalisisAceptado
        {
            SolicitudId = solicitud.SolicitudId,
            ArchivoId = solicitud.AnalisisLocal.Archivo.ArchivoId,
            AnalisisId = Guid.NewGuid(),
            Estado = EstadoAnalisis.EnCola,
            FechaRegistroUtc = DateTimeOffset.UtcNow
        };
        byte[] bytes = Encoding.UTF8.GetBytes("abc");
        string temporal = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        await File.WriteAllBytesAsync(temporal, bytes);

        try
        {
            int llamadas = 0;
            string? jsonInicial = null;
            using (var http = new HttpClient(new Simulado(async (peticion, ct) =>
            {
                llamadas++;
                var partes = ((MultipartFormDataContent)peticion.Content!).ToArray();
                string datos = await partes[0].ReadAsStringAsync(ct);
                byte[] contenido = await partes[1].ReadAsByteArrayAsync(ct);
                Comprobar(contenido.SequenceEqual(bytes), "El reintento cambió el contenido.");
                jsonInicial ??= datos;
                Comprobar(datos == jsonInicial, "El reintento cambió la solicitud.");
                if (llamadas == 1)
                    throw new HttpRequestException("Desconexión simulada después de leer el envío.");
                return Respuesta(HttpStatusCode.Accepted, aceptado);
            })))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            {
                var respuesta = await cliente.EnviarArchivoAsync(solicitud, temporal);
                Comprobar(llamadas == 2 && respuesta.AnalisisId == aceptado.AnalisisId,
                    "No recuperó el envío después de la desconexión.");
                Console.WriteLine("CORRECTO: reintento después de consumir el archivo conserva JSON, contenido e identificador.");
            }

            using (var http = new HttpClient(new Simulado((_, _) =>
                Task.FromResult(Respuesta(HttpStatusCode.Accepted, aceptado)))))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            using (var stream = new MemoryStream(bytes))
            {
                await cliente.EnviarAsync(solicitud, stream);
                Comprobar(stream.CanRead, "El cliente eliminó el stream del llamador.");
                Console.WriteLine("CORRECTO: el stream del llamador queda abierto.");
            }

            llamadas = 0;
            using (var http = new HttpClient(new Simulado((_, _) =>
            {
                llamadas++;
                return Task.FromResult(Respuesta(HttpStatusCode.Conflict,
                    new { codigo = "SOLICITUD_REUTILIZADA", mensaje = "Conflicto de prueba." }));
            })))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            {
                try
                {
                    await cliente.EnviarArchivoAsync(solicitud, temporal);
                    throw new Exception("No detectó el conflicto.");
                }
                catch (ErrorClienteAnalisis error) when (error.EstadoHttp == HttpStatusCode.Conflict)
                {
                    Comprobar(llamadas == 1 && !error.Reintentable, "Reintentó un conflicto.");
                    Console.WriteLine("CORRECTO: HTTP 409 no se reintenta.");
                }
            }

            llamadas = 0;
            using (var http = new HttpClient(new Simulado((_, _) =>
            {
                llamadas++;
                return Task.FromResult(Respuesta(HttpStatusCode.Accepted, aceptado));
            })))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            using (var cancelar = new CancellationTokenSource())
            {
                cancelar.Cancel();
                try
                {
                    await cliente.EnviarArchivoAsync(solicitud, temporal, cancelar.Token);
                    throw new Exception("No respetó la cancelación.");
                }
                catch (OperationCanceledException)
                {
                    Comprobar(llamadas == 0, "Envió una solicitud después de cancelar.");
                    Console.WriteLine("CORRECTO: cancelación previa sin envío.");
                }
            }

            var resultado = new ResultadoAnalisis
            {
                AnalisisId = aceptado.AnalisisId,
                Archivo = solicitud.AnalisisLocal.Archivo,
                Estado = EstadoAnalisis.Fallido,
                Evaluacion = EvaluacionRiesgo.Incompleto,
                CodigoError = "SANDBOX_PRUEBA",
                Resumen = "Fallo simulado."
            };
            using (var http = new HttpClient(new Simulado((_, _) =>
                Task.FromResult(Respuesta(HttpStatusCode.OK, resultado)))))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            {
                var final = await cliente.EsperarResultadoAsync(resultado.AnalisisId);
                Comprobar(final.Estado == EstadoAnalisis.Fallido && final.CodigoError == "SANDBOX_PRUEBA",
                    "No conservó el informe fallido.");
                Console.WriteLine("CORRECTO: un trabajo Fallido se devuelve con su informe.");
            }

            using (var http = new HttpClient(new Simulado((_, _) =>
                Task.FromResult(Respuesta(HttpStatusCode.OK, resultado)))))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            {
                try
                {
                    await cliente.ConsultarAsync(Guid.NewGuid());
                    throw new Exception("Aceptó un resultado de otro trabajo.");
                }
                catch (ErrorClienteAnalisis error) when (error.Codigo == "RESPUESTA_INVALIDA")
                {
                    Console.WriteLine("CORRECTO: rechaza un resultado con otro AnalisisId.");
                }
            }

            llamadas = 0;
            using (var http = new HttpClient(new Simulado((_, _) =>
            {
                llamadas++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("JSON inválido")
                });
            })))
            using (var cliente = new ClienteAnalisisHttp(http, new Uri("http://localhost:5080")))
            {
                try
                {
                    await cliente.EsperarResultadoAsync(aceptado.AnalisisId);
                    throw new Exception("Aceptó una respuesta inválida.");
                }
                catch (ErrorClienteAnalisis error) when (error.Codigo == "RESPUESTA_INVALIDA")
                {
                    Comprobar(llamadas == 1, "Reintentó una respuesta inválida.");
                    Console.WriteLine("CORRECTO: JSON inválido identificado sin reintentar.");
                }
            }

            Console.WriteLine("LAS SIETE AUTOPRUEBAS TERMINARON CORRECTAMENTE.");
        }
        finally { File.Delete(temporal); }
    }

    private static SolicitudAnalisis CrearSolicitud() => new()
    {
        SolicitudId = Guid.NewGuid(),
        AnalisisLocal = new ResultadoAnalisisLocal
        {
            Archivo = new InformacionArchivo
            {
                ArchivoId = Guid.NewGuid(),
                NombreOriginal = "prueba.exe",
                Extension = ".exe",
                TamanoBytes = 3,
                Sha256 = new string('a', 64)
            }
        }
    };

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, object cuerpo) =>
        new(codigo)
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo, Json),
                Encoding.UTF8, "application/json")
        };

    private static JsonSerializerOptions Opciones()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        json.Converters.Add(new JsonStringEnumConverter());
        return json;
    }

    private static void Comprobar(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception(mensaje);
    }

    private sealed class Simulado(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> ejecutar)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage peticion, CancellationToken ct) => ejecutar(peticion, ct);
    }
}
