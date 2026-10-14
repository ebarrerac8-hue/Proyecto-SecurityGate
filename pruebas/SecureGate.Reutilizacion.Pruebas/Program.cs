using System.Security.Cryptography;
using System.Text.Json;
using SecureGate.Contratos;
using SecureGate.Servidor;

DateTimeOffset ahora = DateTimeOffset.UtcNow;
var opciones = new OpcionesServidor { TiempoObservacionSegundos = 120 };
string hash = new string('a', 64);
TrabajoAnalisis Trabajo(int horas = 1)
{
    var archivo = new InformacionArchivo { ArchivoId = Guid.NewGuid(), NombreOriginal = "Prueba.exe",
        Extension = ".exe", TamanoBytes = 8, Sha256 = hash };
    var local = new ResultadoAnalisisLocal { Archivo = archivo, FinUtc = ahora.AddMinutes(-1),
        FirmaDigital = new() { Estado = EstadoComprobacion.Completada, Firma = EstadoFirmaDigital.Valida },
        Antivirus = new() { Estado = EstadoComprobacion.Completada, Deteccion = DeteccionAntivirus.SinDetecciones } };
    var resultado = new ResultadoAnalisis { AnalisisId = Guid.NewGuid(), Archivo = archivo,
        AnalisisLocal = local, Estado = EstadoAnalisis.Completado, Evaluacion = EvaluacionRiesgo.Incompleto,
        Reputacion = new() { Estado = EstadoComprobacion.Completada, Sha256 = hash, InformeEncontrado = true,
            MotoresMaliciosos = 0, MotoresSospechosos = 0, FechaConsultaUtc = ahora.AddMinutes(-1) } };
    resultado.Sandbox = new InformeSandbox { AnalisisId = resultado.AnalisisId, ArchivoId = archivo.ArchivoId,
        Sha256 = hash, Estado = EstadoComprobacion.Completada, MuestraEjecutada = true, ObservadorIniciado = true,
        InicioUtc = ahora.AddHours(-horas).AddMinutes(-2), FinUtc = ahora.AddHours(-horas), RedHabilitada = false,
        Eventos = new() { new EventoSandbox { Tipo = TipoEventoSandbox.ProcesoIniciado, FechaUtc = ahora.AddHours(-horas), NombreProceso = "Prueba.exe" } } };
    return new TrabajoAnalisis { SolicitudId = Guid.NewGuid(), RutaMuestraServidor = "sin-archivo",
        Resultado = resultado, VersionObservacionSandbox = PoliticaReutilizacionSandbox.VersionActual,
        SegundosObservacionSandbox = 120 };
}
void Ver(bool correcto, string nombre) { if (!correcto) throw new Exception(nombre); Console.WriteLine("CORRECTO: " + nombre); }
bool Reusa(TrabajoAnalisis origen, TrabajoAnalisis actual) =>
    PoliticaReutilizacionSandbox.Seleccionar(new[] { origen }, actual, opciones, ahora) is not null;
var origen = Trabajo();
var actual = Trabajo();
actual.Resultado.Sandbox = null;
Ver(Reusa(origen, actual), "mismo hash con otras identidades reutiliza observacion vigente");
var copia = PoliticaReutilizacionSandbox.Copiar(origen, actual, ahora);
Ver(copia.AnalisisId == actual.Resultado.AnalisisId && copia.ArchivoId == actual.Resultado.Archivo.ArchivoId &&
    copia.AnalisisOrigenId == origen.Resultado.AnalisisId, "copia vincula solicitud nueva y conserva origen");
Ver(copia.FinUtc == origen.Resultado.Sandbox!.FinUtc && copia.InicioUtc == origen.Resultado.Sandbox.InicioUtc,
    "reutilizar no renueva fechas originales");
Ver(!ReferenceEquals(copia.Eventos, origen.Resultado.Sandbox.Eventos) &&
    !ReferenceEquals(copia.Eventos[0], origen.Resultado.Sandbox.Eventos[0]), "copia de eventos independiente");
var persistido = JsonSerializer.Deserialize<TrabajoAnalisis>(JsonSerializer.Serialize(origen))!;
Ver(Reusa(persistido, actual), "registro persistido conserva elegibilidad tras reinicio");
var antiguo = Trabajo(48);
Ver(Reusa(antiguo, actual), "observacion antigua dentro de vigencia admite capas actuales sin detecciones");
actual.Resultado.Reputacion!.InformeEncontrado = false;
Ver(!Reusa(antiguo, actual) && Reusa(origen, actual), "sin informe VirusTotal no reutiliza observacion antigua");
actual.Resultado.Reputacion.InformeEncontrado = true;
Ver(!Reusa(Trabajo(24 * 31), actual), "observacion vencida exige nueva ejecucion");
actual.Resultado.Reputacion.MotoresMaliciosos = 1;
Ver(!Reusa(origen, actual), "deteccion actual VirusTotal impide reutilizacion");
actual.Resultado.Reputacion.MotoresMaliciosos = 0;
actual.Resultado.Reputacion.MotoresSospechosos = 1;
Ver(!Reusa(origen, actual), "nueva senal sospechosa exige revision aislada");
actual.Resultado.Reputacion.MotoresSospechosos = 0;
actual.Resultado.Reputacion.FechaConsultaUtc = ahora.AddHours(-1);
Ver(!Reusa(origen, actual), "consulta VirusTotal antigua no se presenta como actual");
actual.Resultado.Reputacion.FechaConsultaUtc = ahora.AddMinutes(-1);
actual.Resultado.AnalisisLocal!.Antivirus.Estado = EstadoComprobacion.Fallida;
Ver(!Reusa(origen, actual), "antivirus incompleto impide reutilizacion");
actual.Resultado.AnalisisLocal.Antivirus.Estado = EstadoComprobacion.Completada;
actual.Resultado.AnalisisLocal.FinUtc = ahora.AddDays(-2);
Ver(!Reusa(origen, actual), "antivirus local antiguo impide reutilizacion");
actual.Resultado.AnalisisLocal.FinUtc = ahora.AddMinutes(-1);
origen.VersionObservacionSandbox = null;
Ver(!Reusa(origen, actual), "observacion sin version conocida no se reutiliza");
origen.VersionObservacionSandbox = PoliticaReutilizacionSandbox.VersionActual;
origen.SegundosObservacionSandbox = 60;
Ver(!Reusa(origen, actual), "cambio de duracion exige nueva observacion");
origen.SegundosObservacionSandbox = 120;
origen.Resultado.Sandbox!.ObservadorIniciado = false;
Ver(!Reusa(origen, actual), "observador ausente no se reutiliza");
origen.Resultado.Sandbox.ObservadorIniciado = true;
var diferente = Trabajo();
diferente.Resultado = new ResultadoAnalisis { AnalisisId = Guid.NewGuid(), Archivo = new InformacionArchivo {
    ArchivoId = Guid.NewGuid(), NombreOriginal = "Prueba.exe", Extension = ".exe", TamanoBytes = 8, Sha256 = new string('b',64) },
    Estado = EstadoAnalisis.Completado };
Ver(!Reusa(diferente, actual), "hash diferente nunca se reutiliza");
var encadenado = Trabajo();
encadenado.Resultado.Sandbox = copia;
encadenado.Resultado = new ResultadoAnalisis { AnalisisId = copia.AnalisisId, Archivo = actual.Resultado.Archivo,
    AnalisisLocal = actual.Resultado.AnalisisLocal, Estado = EstadoAnalisis.Completado,
    Evaluacion = EvaluacionRiesgo.Incompleto, Sandbox = copia };
var segunda = PoliticaReutilizacionSandbox.Copiar(encadenado, Trabajo(), ahora);
Ver(segunda.AnalisisOrigenId == origen.Resultado.AnalisisId && segunda.FinUtc == copia.FinUtc,
    "cadena de reutilizaciones mantiene primer origen y vigencia");
string carpeta = Path.Combine(Path.GetTempPath(), "SecureGate_Reutilizacion_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(carpeta);
try
{
    string ruta = Path.Combine(carpeta, "muestra.bytes");
    byte[] bytes = new byte[] { 1,2,3,4,5,6,7,8 };
    await File.WriteAllBytesAsync(ruta, bytes);
    var archivo = new InformacionArchivo { ArchivoId = Guid.NewGuid(), NombreOriginal = "Prueba.exe",
        Extension = ".exe", TamanoBytes = 8, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
    var disco = new TrabajoAnalisis { SolicitudId = Guid.NewGuid(), RutaMuestraServidor = ruta,
        Resultado = new ResultadoAnalisis { AnalisisId = Guid.NewGuid(), Archivo = archivo } };
    await PoliticaReutilizacionSandbox.ValidarMuestraAsync(disco, default);
    Ver(true, "comprueba bytes reales antes de reutilizar");
    await File.WriteAllBytesAsync(ruta, new byte[] { 8,7,6,5,4,3,2,1 });
    bool rechazo = false;
    try { await PoliticaReutilizacionSandbox.ValidarMuestraAsync(disco, default); }
    catch (InvalidDataException) { rechazo = true; }
    Ver(rechazo, "rechaza muestra alterada con mismo tamano");
}
finally { Directory.Delete(carpeta, true); }
Console.WriteLine("PRUEBAS DE POLITICA COMPLETADAS. No se abrio Sandbox ni se llamo a APIs.");
