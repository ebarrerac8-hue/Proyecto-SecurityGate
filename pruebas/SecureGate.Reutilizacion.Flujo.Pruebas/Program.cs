using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureGate.Contratos;
using SecureGate.Servidor;
string carpeta = Path.Combine(Path.GetTempPath(), "SecureGate_CacheFlujo_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpeta);
string? claveAnterior = Environment.GetEnvironmentVariable("SECUREGATE_VIRUSTOTAL_API_KEY");
Environment.SetEnvironmentVariable("SECUREGATE_VIRUSTOTAL_API_KEY", "CLAVE_FICTICIA_SIN_VALOR_REAL");
try
{
    byte[] bytes = new byte[] { 1,2,3,4,5,6,7,8 };
    string ruta = Path.Combine(carpeta, "muestra.bytes");
    await File.WriteAllBytesAsync(ruta, bytes);
    string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    var opciones = Options.Create(new OpcionesServidor { CarpetaDatos = carpeta, TiempoObservacionSegundos = 120 });
    var repositorio = new RepositorioAnalisis(opciones, NullLogger<RepositorioAnalisis>.Instance);
    var cola = new ColaAnalisis();
    var http = new FakeHttp();
    var reputacion = new ClienteReputacion(new HttpClient(http), new ConfigurationBuilder().Build());
    var sandbox = new FakeSandbox();
    var gemini = new ClienteGemini(new HttpClient(new NoHttp()), new OpcionesIa());
    using var procesador = new ProcesadorAnalisis(cola, repositorio, reputacion, sandbox, gemini,
        opciones, NullLogger<ProcesadorAnalisis>.Instance);
    await procesador.StartAsync(default);
    async Task<TrabajoAnalisis> Enviar()
    {
        var info = new InformacionArchivo { ArchivoId = Guid.NewGuid(), NombreOriginal = "Prueba.exe",
            Extension = ".exe", TamanoBytes = 8, Sha256 = hash };
        var local = new ResultadoAnalisisLocal { Archivo = info, FinUtc = DateTimeOffset.UtcNow,
            FirmaDigital = new() { Estado = EstadoComprobacion.Completada, Firma = EstadoFirmaDigital.Valida },
            Antivirus = new() { Estado = EstadoComprobacion.Completada, Deteccion = DeteccionAntivirus.SinDetecciones } };
        var trabajo = new TrabajoAnalisis { SolicitudId = Guid.NewGuid(), RutaMuestraServidor = ruta,
            Resultado = new ResultadoAnalisis { AnalisisId = Guid.NewGuid(), Archivo = info,
                AnalisisLocal = local, Estado = EstadoAnalisis.EnCola } };
        await repositorio.GuardarAsync(trabajo);
        cola.Encolar(trabajo.Resultado.AnalisisId);
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var recibido = await repositorio.ObtenerAsync(trabajo.Resultado.AnalisisId, limite.Token);
            if (recibido!.Resultado.Estado == EstadoAnalisis.Completado) return recibido;
            if (recibido.Resultado.Estado == EstadoAnalisis.Fallido) throw new Exception(recibido.Resultado.CodigoError);
            await Task.Delay(50, limite.Token);
        }
    }
    var primero = await Enviar();
    var segundo = await Enviar();
    if (sandbox.Cantidad != 1 || http.Cantidad != 2 ||
        segundo.Resultado.Sandbox?.AnalisisOrigenId != primero.Resultado.AnalisisId ||
        segundo.Resultado.AnalisisId == primero.Resultado.AnalisisId ||
        segundo.Resultado.Archivo.ArchivoId == primero.Resultado.Archivo.ArchivoId ||
        segundo.Resultado.Sandbox.FinUtc != primero.Resultado.Sandbox!.FinUtc)
        throw new Exception("No se conservo reutilizacion, identidad o consulta actual.");
    if (!segundo.Resultado.Ia!.EvidenciasEnviadas.Any(e=>e.Tipo=="ReutilizacionSandbox"))
        throw new Exception("La IA no recibio la procedencia historica.");
    await procesador.StopAsync(default);
    Console.WriteLine("CORRECTO: procesador real atiende dos solicitudes con una ejecucion Sandbox simulada.");
    Console.WriteLine("CORRECTO: VirusTotal simulado consultado dos veces; identidades y origen persistidos.");
    Console.WriteLine("CORRECTO: evidencias para IA indican la reutilizacion historica.");
}
finally
{
    Environment.SetEnvironmentVariable("SECUREGATE_VIRUSTOTAL_API_KEY", claveAnterior);
    Directory.Delete(carpeta, true);
}
sealed class FakeHttp : HttpMessageHandler
{
    public int Cantidad;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Cantidad++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent("{\"data\":{\"attributes\":{\"last_analysis_stats\":{\"malicious\":0,\"suspicious\":0,\"undetected\":1,\"harmless\":0}}}}",Encoding.UTF8,"application/json") });
    }
}
sealed class NoHttp : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        throw new Exception("No se permite ninguna consulta de IA en esta prueba.");
}
sealed class FakeSandbox : IAnalizadorSandbox
{
    public int Cantidad;
    public Task<InformeSandbox> EjecutarAsync(SolicitudPruebaSandbox solicitud, CancellationToken ct = default)
    {
        Cantidad++;
        return Task.FromResult(new InformeSandbox { AnalisisId = solicitud.AnalisisId, ArchivoId = solicitud.ArchivoId,
            Sha256 = solicitud.Sha256, Estado = EstadoComprobacion.Completada, MuestraEjecutada = true,
            ObservadorIniciado = true, InicioUtc = DateTimeOffset.UtcNow.AddMinutes(-2), FinUtc = DateTimeOffset.UtcNow });
    }
}
