using SecureGate.Contratos;
using SecureGate.Proteccion;
using System.Diagnostics;
using System.Reflection;

void Exigir(bool condicion, string detalle)
{
    if (!condicion) throw new InvalidOperationException(detalle);
    Console.WriteLine("CORRECTO: " + detalle);
}

var limpio = InterpretadorDefender.Interpretar(0,
    "Scan starting...\nScan finished.\nScanning C:\\prueba.bytes found no threats.\n", "", false);
Exigir(limpio.Estado == EstadoComprobacion.Completada &&
    limpio.Deteccion == DeteccionAntivirus.SinDetecciones, "interpreta escaneo sin detecciones.");
var ambiguo = InterpretadorDefender.Interpretar(2, "Scanning failed.", "Access denied", false);
Exigir(ambiguo.Estado == EstadoComprobacion.Fallida &&
    ambiguo.Deteccion == DeteccionAntivirus.NoDeterminada, "codigo 2 no se convierte en amenaza.");
var vacio = InterpretadorDefender.Interpretar(0, "", "", false);
Exigir(vacio.Estado == EstadoComprobacion.Fallida, "codigo 0 sin evidencia no aprueba el escaneo.");
var amenaza = InterpretadorDefender.Interpretar(2, "Threat Name : Virus:Prueba/Simulada\n", "", false);
Exigir(amenaza.Deteccion == DeteccionAntivirus.AmenazaDetectada &&
    amenaza.Amenazas.Single() == "Virus:Prueba/Simulada", "conserva deteccion explicita simulada.");
var contradictorio = InterpretadorDefender.Interpretar(0,
    "Scanning C:\\prueba.bytes found no threats.\nThreat Name : Virus:Prueba/Simulada", "", false);
Exigir(contradictorio.Estado == EstadoComprobacion.Fallida, "salida contradictoria queda indeterminada.");
var truncado = InterpretadorDefender.Interpretar(0,
    "Scanning C:\\prueba.bytes found no threats.", "", true);
Exigir(truncado.Estado == EstadoComprobacion.Fallida, "salida incompleta no produce aprobacion.");

// Probar el ejecutor real con procesos benignos: salida extensa y tiempo agotado.
var tipoEjecutor = typeof(AnalizadorLocal).Assembly.GetType("SecureGate.Proteccion.AnalizadorDefender")!;
var ejecutar = tipoEjecutor.GetMethod("EjecutarAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
ProcessStartInfo ProcesoPrueba(bool lento)
{
    var p = new ProcessStartInfo
    {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    if (OperatingSystem.IsWindows())
    {
        p.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command",
            lento ? "Start-Sleep -Seconds 10" :
            "[Console]::Out.Write(('x' * 100000)); [Console]::Error.Write(('y' * 100000))" })
            p.ArgumentList.Add(arg);
    }
    else
    {
        p.FileName = "/bin/sh";
        p.ArgumentList.Add("-c");
        p.ArgumentList.Add(lento ? "sleep 10" :
            "head -c 100000 /dev/zero; head -c 100000 /dev/zero >&2");
    }
    return p;
}
var tareaSalida = (Task)ejecutar.Invoke(null,
    new object[] { ProcesoPrueba(false), TimeSpan.FromSeconds(20), CancellationToken.None })!;
await tareaSalida;
var lectura = tareaSalida.GetType().GetProperty("Result")!.GetValue(tareaSalida)!;
var salidaLarga = lectura.GetType().GetProperty("Salida")!.GetValue(lectura)!;
Exigir((bool)salidaLarga.GetType().GetProperty("Truncada")!.GetValue(salidaLarga)! &&
    ((string)salidaLarga.GetType().GetProperty("Texto")!.GetValue(salidaLarga)!).Length == 65536,
    "drena salida y errores extensos sin bloquear ni guardar contenido ilimitado.");
var reloj = Stopwatch.StartNew();
bool tiempoAgotado = false;
try
{
    await (Task)ejecutar.Invoke(null,
        new object[] { ProcesoPrueba(true), TimeSpan.FromMilliseconds(200), CancellationToken.None })!;
}
catch (OperationCanceledException) { tiempoAgotado = true; }
Exigir(tiempoAgotado && reloj.Elapsed < TimeSpan.FromSeconds(8),
    "tiempo agotado interrumpe el proceso de prueba antes de su final natural.");

if (args.Contains("--real"))
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Prueba real requiere Windows.");
    string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecureGate", "PruebasDefender", Guid.NewGuid().ToString("N"));
    string origen = Path.Combine(carpeta, "Entrada", "PruebaWindows.exe");
    string cuarentena = Path.Combine(carpeta, "Cuarentena");
    Directory.CreateDirectory(Path.GetDirectoryName(origen)!);
    File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), origen);
    var gestor = new GestorCuarentena(cuarentena);
    var retenido = await gestor.RetenerAsync(origen);
    var analisis = await new AnalizadorLocal(cuarentena).AnalizarAsync(retenido.ArchivoId);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(analisis,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Exigir(analisis.Archivo.NombreOriginal == "PruebaWindows.exe" && analisis.Archivo.Extension == ".exe",
        "conserva metadatos originales.");
    Exigir(analisis.Antivirus.Estado == EstadoComprobacion.Completada &&
        analisis.Antivirus.Deteccion == DeteccionAntivirus.SinDetecciones,
        "Defender real integrado analiza la copia retenida de cmd.exe.");
    Exigir(File.Exists(Path.Combine(cuarentena, retenido.ArchivoId + ".bytes")) && !File.Exists(origen),
        "muestra permanece retenida tras el escaneo.");
    var repetido = await new AnalizadorLocal(cuarentena).AnalizarAsync(retenido.ArchivoId);
    Exigir(repetido.Archivo.Sha256 == analisis.Archivo.Sha256,
        "bytes conservados y cuarentena recuperable.");
    Console.WriteLine("Carpeta aislada de prueba: " + carpeta);
}
else Console.WriteLine("Pruebas simuladas completas. Usar --real en Windows para comprobar Defender integrado.");
