using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Cliente;
using SecureGate.Contratos;
using SecureGate.Proteccion;

void Exigir(bool condicion, string detalle)
{
    if (!condicion) throw new InvalidOperationException(detalle);
    Console.WriteLine("CORRECTO: " + detalle);
}
var archivo = new InformacionArchivo { ArchivoId = Guid.NewGuid(), NombreOriginal = "Prueba.exe",
    Extension = ".exe", TamanoBytes = 1, Sha256 = new string('a', 64), FechaRegistroUtc = DateTimeOffset.UtcNow };
var id = Guid.NewGuid();
ResultadoAnalisis Informe() => new()
{
    AnalisisId = id, Archivo = archivo, Estado = EstadoAnalisis.Completado,
    Evaluacion = EvaluacionRiesgo.Incompleto,
    AnalisisLocal = new ResultadoAnalisisLocal
    {
        Archivo = archivo, Antivirus = new ResultadoAntivirus
        { Estado = EstadoComprobacion.Completada, Deteccion = DeteccionAntivirus.SinDetecciones }
    }
};
var validar = typeof(AutorizacionLiberacion).GetMethod("ValidarInforme", BindingFlags.NonPublic | BindingFlags.Static)!;
void Validar(ResultadoAnalisis r, Guid? identidad = null) => validar.Invoke(null,
    new object[] { r, identidad ?? archivo.ArchivoId, id, archivo.Sha256 });
void Rechazo(ResultadoAnalisis r, string detalle, Guid? identidad = null)
{
    bool rechazo = false;
    try { Validar(r, identidad); }
    catch (TargetInvocationException e) when (e.InnerException is UnauthorizedAccessException or InvalidDataException)
    { rechazo = true; }
    Exigir(rechazo, detalle);
}
Validar(Informe());
Exigir(Informe().Evaluacion == EvaluacionRiesgo.Incompleto, "comprobacion no transforma Incompleto en seguro.");
Rechazo(Informe(), "rechaza identidad de otro archivo.", Guid.NewGuid());
var pendiente = Informe(); pendiente.Estado = EstadoAnalisis.EnProceso;
Rechazo(pendiente, "rechaza trabajo pendiente.");
var amenaza = Informe(); amenaza.Evaluacion = EvaluacionRiesgo.AmenazaDetectada;
Rechazo(amenaza, "rechaza amenaza detectada.");
var sinAv = Informe(); sinAv.AnalisisLocal!.Antivirus.Estado = EstadoComprobacion.Fallida;
Rechazo(sinAv, "rechaza antivirus incompleto.");
var avAmenaza = Informe(); avAmenaza.AnalisisLocal!.Antivirus.Deteccion = DeteccionAntivirus.AmenazaDetectada;
Rechazo(avAmenaza, "rechaza deteccion local aunque la evaluacion diga Incompleto.");
var vtAmenaza = Informe(); vtAmenaza.Reputacion = new ResultadoReputacion { Sha256 = archivo.Sha256, MotoresMaliciosos = 1 };
Rechazo(vtAmenaza, "rechaza deteccion de VirusTotal aunque la evaluacion diga Incompleto.");

if (args.Contains("--real") || args.Contains("--recuperar"))
{
    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Prueba real requiere Windows.");
    var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    opciones.Converters.Add(new JsonStringEnumConverter());
    string puntero = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecureGate", "PruebasUI", "ultima-prueba.json");
    using var sesion = JsonDocument.Parse(File.ReadAllText(puntero));
    string raiz = sesion.RootElement.GetProperty("Raiz").GetString()!;
    string monitor = sesion.RootElement.GetProperty("Monitor").GetString()!;
    var archivos = Directory.GetFiles(Path.Combine(raiz, "Envios"), "*.json");
    if (archivos.Length != 1) throw new InvalidDataException("Se requiere la sesion aislada con un envio.");
    var envio = JsonSerializer.Deserialize<EnvioCliente>(File.ReadAllText(archivos[0]), opciones)!;
    string cuarentena = Path.GetDirectoryName(envio.RutaArchivo)!;
    // Solo permitir la muestra benigna ya utilizada en PruebasUI, nunca Descargas habitual.
    string raizPruebas = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecureGate", "PruebasUI") + Path.DirectorySeparatorChar;
    if (!Path.GetFullPath(raiz).StartsWith(raizPruebas, StringComparison.OrdinalIgnoreCase) ||
        !Path.GetFullPath(cuarentena).StartsWith(Path.GetFullPath(raiz) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) ||
        envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal != "PruebaSecureGate_CMD.exe")
        throw new InvalidDataException("La prueba no corresponde a una muestra aislada.");
    string originalWindows = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
    using var original = File.OpenRead(originalWindows);
    string hashWindows = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant();
    if (envio.Solicitud.AnalisisLocal.Archivo.Sha256 != hashWindows)
        throw new InvalidDataException("La muestra no es la copia de cmd.exe actual.");
    var gestor = new GestorCuarentena(cuarentena);
    var registro = await gestor.ConsultarRegistroAsync(envio.Solicitud.AnalisisLocal.Archivo.ArchivoId);
    if (!registro.RutaOriginal.Equals(Path.Combine(monitor, "PruebaSecureGate_CMD.exe"), StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("Ubicacion original no admitida para la prueba.");
    if (args.Contains("--recuperar"))
    {
        Exigir(registro.Estado == EstadoArchivo.Liberado && registro.OperacionPendiente == "" &&
            registro.MetodoAutorizacion == "CredUI+LogonUser" &&
            registro.AnalisisLiberacionId == envio.AnalisisId,
            "recupera la liberacion previamente autorizada sin solicitar otra contrasena.");
        Exigir(File.Exists(registro.RutaOriginal) && !File.Exists(envio.RutaArchivo),
            "copia benigna restaurada y muestra retirada de cuarentena.");
        using var restaurada = File.OpenRead(registro.RutaOriginal);
        Exigir(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(restaurada)).ToLowerInvariant() == registro.Sha256,
            "la recuperacion conserva el SHA-256 original.");
        Exigir(registro.EvaluacionAlLiberar == envio.Resultado!.Evaluacion,
            "la recuperacion no cambia la evaluacion.");
        Console.WriteLine("RECUPERACION COMPLETADA: " + registro.RutaOriginal);
        return;
    }
    var negada = await gestor.LiberarAsync(new SolicitudLiberacion
    { ArchivoId = registro.ArchivoId, Sha256Esperado = registro.Sha256, Motivo = MotivoLiberacion.ExcepcionDelUsuario });
    Exigir(!negada.Realizada && File.Exists(envio.RutaArchivo), "un motivo sin autorizacion no libera.");
    string token = Environment.GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", EnvironmentVariableTarget.User) ?? "";
    using var http = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    using var cliente = new ClienteAnalisisHttp(http, new Uri(envio.Servidor), token);
    Console.WriteLine("PRIMER DIALOGO DE WINDOWS: pulsa Cancelar para comprobar que no libera.");
    var cancelada = await AutorizacionLiberacion.SolicitarWindowsAsync(cliente, registro.ArchivoId,
        envio.AnalisisId!.Value, registro.Sha256, true, IntPtr.Zero);
    Exigir(cancelada is null && File.Exists(envio.RutaArchivo), "cancelar Windows conserva la retencion.");
    Console.WriteLine("SEGUNDO DIALOGO DE WINDOWS: autoriza con la contrasena de la misma cuenta (no PIN). Nunca la pegues en el chat.");
    var permiso = await AutorizacionLiberacion.SolicitarWindowsAsync(cliente, registro.ArchivoId,
        envio.AnalisisId!.Value, registro.Sha256, true, IntPtr.Zero);
    if (permiso is null) throw new OperationCanceledException("Autorizacion cancelada; muestra retenida.");
    var resultado = await gestor.LiberarAutorizadoAsync(permiso);
    Exigir(resultado.Realizada && File.Exists(registro.RutaOriginal) && !File.Exists(envio.RutaArchivo),
        "liberacion autorizada restaura la muestra benigna sin ejecutarla.");
    var recuperada = await new GestorCuarentena(cuarentena).ConsultarRegistroAsync(registro.ArchivoId);
    Exigir(recuperada.Estado == EstadoArchivo.Liberado && recuperada.MetodoAutorizacion == "CredUI+LogonUser" &&
        recuperada.AnalisisLiberacionId == envio.AnalisisId, "decision y autorizacion conservadas al reabrir.");
    Exigir(envio.Resultado!.Evaluacion == recuperada.EvaluacionAlLiberar,
        "liberar no cambia la evaluacion del informe.");
    var otra = await gestor.LiberarAutorizadoAsync(permiso);
    Exigir(!otra.Realizada, "la misma autorizacion no libera nuevamente.");
    // Simular una interrupcion despues del movimiento, antes de restaurar permisos.
    var permisoArchivo = new System.Security.AccessControl.FileSecurity();
    permisoArchivo.SetAccessRuleProtection(true, false);
    using (var identidad = System.Security.Principal.WindowsIdentity.GetCurrent())
    {
        permisoArchivo.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
            identidad.User!, System.Security.AccessControl.FileSystemRights.FullControl,
            System.Security.AccessControl.AccessControlType.Allow));
        permisoArchivo.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
            new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
            System.Security.AccessControl.FileSystemRights.ExecuteFile,
            System.Security.AccessControl.AccessControlType.Deny));
    }
    System.IO.FileSystemAclExtensions.SetAccessControl(new FileInfo(registro.RutaOriginal), permisoArchivo);
    recuperada.Estado = EstadoArchivo.Retenido;
    recuperada.OperacionPendiente = "Liberar";
    recuperada.PermisosAplicados = true;
    string registroJson = Path.Combine(cuarentena, registro.ArchivoId + ".json");
    File.WriteAllText(registroJson, JsonSerializer.Serialize(recuperada));
    var trasInterrupcion = await new GestorCuarentena(cuarentena).ConsultarRegistroAsync(registro.ArchivoId);
    Exigir(trasInterrupcion.Estado == EstadoArchivo.Liberado && trasInterrupcion.OperacionPendiente == "",
        "recupera liberacion autorizada interrumpida despues del movimiento.");
    using (var restaurada = File.OpenRead(registro.RutaOriginal))
        Exigir(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(restaurada)).ToLowerInvariant() == registro.Sha256,
            "liberacion y recuperacion conservan los bytes.");
    // Simular colision antes del movimiento, preservando ambos archivos.
    File.Move(registro.RutaOriginal, envio.RutaArchivo);
    File.WriteAllText(registro.RutaOriginal, "archivo de prueba que no debe sobrescribirse");
    trasInterrupcion.Estado = EstadoArchivo.Retenido;
    trasInterrupcion.OperacionPendiente = "Liberar";
    File.WriteAllText(registroJson, JsonSerializer.Serialize(trasInterrupcion));
    bool colision = false;
    try { await new GestorCuarentena(cuarentena).ConsultarRegistroAsync(registro.ArchivoId); }
    catch (IOException) { colision = true; }
    Exigir(colision && File.Exists(envio.RutaArchivo) &&
        File.ReadAllText(registro.RutaOriginal) == "archivo de prueba que no debe sobrescribirse",
        "destino ocupado no se sobrescribe y conserva la muestra.");
    File.Delete(registro.RutaOriginal); // Solo el archivo benigno que acaba de crear esta prueba.
    await new GestorCuarentena(cuarentena).ConsultarRegistroAsync(registro.ArchivoId);
    Console.WriteLine("La prueba dejo una copia benigna restaurada: " + registro.RutaOriginal);
}
else Console.WriteLine("Pruebas de rechazo completas; --real comprueba autorizacion en Windows sobre la ultima muestra benigna de PruebasUI.");
