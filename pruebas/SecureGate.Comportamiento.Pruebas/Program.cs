using System.Text.Json;
using SecureGate.Contratos;
using SecureGate.Servidor;

static void Exigir(bool valor, string texto) { if (!valor) throw new Exception(texto); Console.WriteLine("CORRECTO: " + texto); }
static ResultadoAnalisis Modelo()
{
    var ahora = DateTimeOffset.UtcNow;
    var r = new ResultadoAnalisis { AnalisisId = Guid.NewGuid(), Archivo = new() { ArchivoId = Guid.NewGuid(),
        NombreOriginal = "Control.exe", Extension = ".exe", TamanoBytes = 10, Sha256 = new string('a', 64) } };
    r.Sandbox = new() { AnalisisId = r.AnalisisId, ArchivoId = r.Archivo.ArchivoId, Sha256 = r.Archivo.Sha256,
        Estado = EstadoComprobacion.Completada, MuestraEjecutada = true, ObservadorIniciado = true,
        InicioUtc = ahora.AddSeconds(-10), FinUtc = ahora.AddSeconds(10) };
    r.Sandbox.Eventos.Add(new() { FechaUtc = ahora.AddSeconds(-1), Tipo = TipoEventoSandbox.ProcesoIniciado,
        ProcesoId = 100, ProcesoPadreId = 10, NombreProceso = "muestra.exe", Recurso = @"C:\Prueba\muestra.exe", EsProcesoMuestra = true, AtribuidoAMuestra = true });
    return r;
}
static EventoSandbox Hijo(string orden, string nombre = "powershell.exe", bool atribuido = true, int padre = 100) => new()
{ FechaUtc = DateTimeOffset.UtcNow, Tipo = TipoEventoSandbox.ProcesoIniciado, ProcesoId = 200,
    ProcesoPadreId = padre, NombreProceso = nombre, AtribuidoAMuestra = atribuido, LineaComandos = orden };
var limpio = Modelo();
limpio.Sandbox!.Eventos.Add(Hijo("powershell.exe -NoProfile -Command Get-Date"));
limpio.Sandbox.Eventos.Add(new() { FechaUtc = DateTimeOffset.UtcNow, Tipo = TipoEventoSandbox.ArchivoCreado, Recurso = @"C:\Users\WDAGUtilityAccount\AppData\Local\Temp\normal.tmp" });
Exigir(AnalizadorComportamientoSandbox.Analizar(limpio).Count == 0, "procesos ordinarios y archivos temporales no generan alerta.");
var codificado = Modelo(); codificado.Sandbox!.Eventos.Add(Hijo("powershell.exe -EncodedCommand ZQBjAGgAbwA="));
EvaluadorAnalisis.Evaluar(codificado);
Exigir(codificado.Evaluacion == EvaluacionRiesgo.Sospechoso && codificado.Motivos.Any(m => m.Contains("SG-B001")), "Sandbox activa precaucion por orden codificada sin Defender, VirusTotal ni firma ausente.");
var noAtribuido = Modelo(); noAtribuido.Sandbox!.Eventos.Add(Hijo("powershell.exe -enc ZQ==", atribuido:false));
Exigir(AnalizadorComportamientoSandbox.Analizar(noAtribuido).Count == 0, "proceso de otro programa no se atribuye a la muestra.");
var padreAjeno = Modelo(); padreAjeno.Sandbox!.Eventos.Add(Hijo("powershell.exe -enc ZQ==", padre:999));
Exigir(AnalizadorComportamientoSandbox.Analizar(padreAjeno).Count == 0, "etiqueta sin cadena de padres no basta.");
var persistencia = Modelo(); persistencia.Sandbox!.Eventos.Add(new() { FechaUtc = DateTimeOffset.UtcNow,
    Tipo = TipoEventoSandbox.RegistroModificado, CambioRegistroObservado = true,
    Recurso = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Control", DatosRegistro = "\"C:\\Prueba\\muestra.exe\" --test" });
Exigir(AnalizadorComportamientoSandbox.Analizar(persistencia).Any(h=>h.Codigo=="SG-B002"), "cambio de inicio automatico apuntando a muestra produce evidencia concreta.");
var ordinario = Modelo(); ordinario.Sandbox!.Eventos.Add(new() { FechaUtc = DateTimeOffset.UtcNow,
    Tipo = TipoEventoSandbox.RegistroModificado, CambioRegistroObservado = true,
    Recurso = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Ajeno", DatosRegistro = @"C:\ProgramaAjeno\normal.exe" });
Exigir(AnalizadorComportamientoSandbox.Analizar(ordinario).Count == 0, "inicio automatico ajeno no activa la regla de la muestra.");
var borrado = Modelo(); borrado.Sandbox!.Eventos.Add(Hijo("vssadmin.exe delete shadows /all", "vssadmin.exe"));
Exigir(AnalizadorComportamientoSandbox.Analizar(borrado).Any(h=>h.Codigo=="SG-B003"), "orden de borrado se reconoce en evidencia simulada sin ejecutarla.");
var identidad = Modelo(); identidad.Sandbox = new() { AnalisisId = Guid.NewGuid(), ArchivoId = identidad.Archivo.ArchivoId,
    Sha256 = identidad.Archivo.Sha256, Estado = EstadoComprobacion.Completada, MuestraEjecutada = true, ObservadorIniciado = true,
    Eventos = codificado.Sandbox.Eventos, InicioUtc=codificado.Sandbox.InicioUtc, FinUtc=codificado.Sandbox.FinUtc };
Exigir(AnalizadorComportamientoSandbox.Analizar(identidad).Count == 0, "informe ajeno no produce deteccion.");
var sinRaiz = Modelo(); sinRaiz.Sandbox!.Eventos.Clear(); sinRaiz.Sandbox.Eventos.Add(Hijo("powershell.exe -enc ZQ=="));
Exigir(AnalizadorComportamientoSandbox.Analizar(sinRaiz).Count == 0, "telemetria antigua sin raiz identificada no inventa atribucion.");
var fueraDeTiempo = Modelo(); var tardio = new EventoSandbox { FechaUtc=DateTimeOffset.UtcNow.AddHours(1), Tipo=TipoEventoSandbox.ProcesoIniciado, ProcesoId=200, ProcesoPadreId=100, AtribuidoAMuestra=true, NombreProceso="powershell.exe", LineaComandos="powershell.exe -enc ZQ==" };
fueraDeTiempo.Sandbox!.Eventos.Add(tardio);
Exigir(AnalizadorComportamientoSandbox.Analizar(fueraDeTiempo).Count == 0, "eventos fuera de la ventana no generan hallazgos.");
var amenaza = JsonSerializer.Deserialize<ResultadoAnalisis>(JsonSerializer.Serialize(codificado))!;
amenaza.Reputacion = new() { Sha256=amenaza.Archivo.Sha256, Estado=EstadoComprobacion.Completada, InformeEncontrado=true, MotoresMaliciosos=1 };
EvaluadorAnalisis.Evaluar(amenaza);
Exigir(amenaza.Evaluacion == EvaluacionRiesgo.AmenazaDetectada, "comportamiento no rebaja una amenaza reportada por otro motor.");
var datos = PreparadorEvidenciasIa.Preparar(codificado);
Exigir(datos.Any(e=>e.Tipo=="Comportamiento" && e.Descripcion.Contains("SG-B001")), "Gemini recibe regla y referencias de eventos reales.");
var copia = JsonSerializer.Deserialize<ResultadoAnalisis>(JsonSerializer.Serialize(codificado))!;
Exigir(AnalizadorComportamientoSandbox.Analizar(copia).Count == 1, "persistencia conserva evidencias y deteccion.");
if (args.Length == 1)
{
    var sb = JsonSerializer.Deserialize<InformeSandbox>(File.ReadAllText(args[0]))!;
    var real = new ResultadoAnalisis { AnalisisId = sb.AnalisisId, Archivo = new() { ArchivoId=sb.ArchivoId,
        Sha256=sb.Sha256, NombreOriginal="MuestraControlada.exe", Extension=".exe", TamanoBytes=1 }, Sandbox=sb };
    EvaluadorAnalisis.Evaluar(real);
    var hallazgos = AnalizadorComportamientoSandbox.Analizar(real);
    Exigir(sb.MuestraEjecutada && sb.ObservadorIniciado && hallazgos.Any(h=>h.Codigo=="SG-B001") && hallazgos.Any(h=>h.Codigo=="SG-B002"),
        "observador REAL en Windows recogio ambas señales de la muestra benigna (CI, no Windows Sandbox).");
    Exigir(real.Evaluacion == EvaluacionRiesgo.Sospechoso, "evaluacion real depende solo de las evidencias de comportamiento.");
}
Console.WriteLine("Pruebas de comportamiento aprobadas.");
