using System.Collections.Concurrent;
using SecureGate.Contratos;
using SecureGate.Monitoreo;
using SecureGate.Servidor;

void Exigir(bool condicion, string mensaje)
{
    if (!condicion) throw new InvalidOperationException(mensaje);
    Console.WriteLine("CORRECTO: " + mensaje);
}
Exigir(AlcanceAnalisis.AdmiteExtension(".exe") && AlcanceAnalisis.AdmiteExtension(".EXE"),
    "admite ejecutables sin distinguir mayusculas.");
foreach (string? extension in new string?[] { ".ps1", ".msi", ".bat", ".cmd", ".com", ".scr", ".vbs", ".pdf", ".exe.crdownload", "", null })
    Exigir(!AlcanceAnalisis.AdmiteExtension(extension), "rechaza formato ajeno al alcance: " + (extension ?? "null"));

var resultado = new ResultadoAnalisis
{
    AnalisisId = Guid.NewGuid(), Estado = EstadoAnalisis.Completado, Evaluacion = EvaluacionRiesgo.Incompleto,
    Archivo = new InformacionArchivo { ArchivoId = Guid.NewGuid(), NombreOriginal = "prueba.exe", Extension = ".exe", Sha256 = new string('a', 64), TamanoBytes = 3 }
};
var reglas = PreparadorEvidenciasIa.Preparar(resultado).Single(e => e.Tipo == "Reglas").Descripcion;
Exigir(reglas.Contains("liberación excepcional", StringComparison.OrdinalIgnoreCase) &&
    reglas.Contains("autenticación de Windows") && reglas.Contains("no concede permisos") &&
    reglas.Contains("no demuestra que esa autorización") && resultado.Evaluacion == EvaluacionRiesgo.Incompleto,
    "evidencias distinguen liberacion excepcional y evaluacion sin conceder autorizacion.");

// Archivos inertes: se prueba el monitor real, sin ejecutar muestras ni usar la API.
string raiz = Path.Combine(Path.GetTempPath(), "SecureGateAlcance_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(raiz);
try
{
    File.WriteAllText(Path.Combine(raiz, "preexistente.exe"), "contenido inerte");
    var detectados = new ConcurrentBag<string>();
    var errores = new ConcurrentBag<string>();
    using var monitor = new DownloadMonitor();
    monitor.ArchivoDetectado += ruta => detectados.Add(Path.GetFileName(ruta));
    monitor.ErrorMonitoreo += errores.Add;
    monitor.IniciarMonitoreo(raiz);
    File.WriteAllText(Path.Combine(raiz, "herramienta.ps1"), "# contenido inerte");
    File.WriteAllText(Path.Combine(raiz, "documento.pdf"), "contenido inerte");
    File.WriteAllText(Path.Combine(raiz, "nuevo.EXE"), "contenido inerte");
    string temporal = Path.Combine(raiz, "renombrado.exe.crdownload");
    File.WriteAllText(temporal, "contenido inerte");
    await Task.Delay(500);
    File.Move(temporal, Path.Combine(raiz, "renombrado.exe"));
    var limite = DateTimeOffset.UtcNow.AddSeconds(15);
    while (detectados.Count < 2 && DateTimeOffset.UtcNow < limite) await Task.Delay(200);
    await Task.Delay(1000);
    Exigir(errores.IsEmpty, "monitor real no reporta errores.");
    Exigir(detectados.Count == 2 && detectados.Contains("nuevo.EXE") && detectados.Contains("renombrado.exe"),
        "monitor detecta solo el .exe nuevo y la descarga renombrada; ignora scripts, PDF y archivo preexistente.");
    Exigir(File.Exists(Path.Combine(raiz, "herramienta.ps1")), "monitor deja disponible la herramienta .ps1.");
    monitor.DetenerMonitoreo();
}
finally { Directory.Delete(raiz, recursive: true); }
Console.WriteLine("PRUEBAS DE ALCANCE COMPLETADAS SIN CONSULTAS REALES.");
