using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Cliente;
using SecureGate.Contratos;

if (args.Contains("--persistencia"))
{
    await PruebasPersistencia.EjecutarAsync();
    return;
}

if (args.Contains("--autopruebas"))
{
    await Autopruebas.EjecutarAsync();
    return;
}

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
json.Converters.Add(new JsonStringEnumConverter());

string carpeta = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SecureGate", "Servidor", "Trabajos");
string ruta = args.Length >= 2 && args[0] == "--registro"
    ? args[1] : Path.Combine(carpeta, "e8c774ea5e7d4711ba265b6f920ba436.json");

if (!File.Exists(ruta))
    throw new FileNotFoundException("No se encontró el registro de demostración. Use --registro RUTA_JSON.", ruta);

var guardado = JsonSerializer.Deserialize<TrabajoLocal>(
    await File.ReadAllTextAsync(ruta), json)
    ?? throw new InvalidDataException("Registro vacío.");

if (guardado.Resultado.Estado is not (EstadoAnalisis.Completado or EstadoAnalisis.Fallido))
    throw new InvalidOperationException("Esta prueba utiliza un trabajo que ya terminó.");
if (guardado.Resultado.AnalisisLocal is null)
    throw new InvalidDataException("El registro no contiene AnalisisLocal.");

using var cliente = new ClienteAnalisisHttp(new Uri("https://localhost:5443"));
Guid id = guardado.Resultado.AnalisisId;

Console.WriteLine("PRUEBA 1: consultar el informe con el cliente C#.");
var antes = await cliente.ConsultarAsync(id);
Console.WriteLine($"CORRECTO: {antes.AnalisisId}; {antes.Estado}; {antes.Evaluacion}");
Console.WriteLine(antes.Resumen);
string sandboxAntes = JsonSerializer.Serialize(antes.Sandbox, json);
var fechaAntes = antes.Reputacion?.FechaConsultaUtc;

var solicitud = new SolicitudAnalisis
{
    SolicitudId = guardado.SolicitudId,
    AnalisisLocal = guardado.Resultado.AnalisisLocal
};

Console.WriteLine("PRUEBA 2: reenviar el trabajo terminado y conservar el stream.");
await using (var archivo = File.OpenRead(guardado.RutaMuestraServidor))
{
    var aceptado = await cliente.EnviarAsync(solicitud, archivo);
    if (aceptado.AnalisisId != id || !archivo.CanRead)
        throw new Exception("El identificador cambió o el cliente cerró el stream.");
}
Console.WriteLine("CORRECTO: mismo AnalisisId; stream del llamador conservado.");

Console.WriteLine("PRUEBA 3: usar el envío por ruta con reintentos.");
var porRuta = await cliente.EnviarArchivoAsync(solicitud, guardado.RutaMuestraServidor);
if (porRuta.AnalisisId != id) throw new Exception("Se creó un trabajo diferente.");
var despues = await cliente.EsperarResultadoAsync(id);
if (despues.Reputacion?.FechaConsultaUtc != fechaAntes ||
    JsonSerializer.Serialize(despues.Sandbox, json) != sandboxAntes)
    throw new Exception("El reintento modificó el informe anterior.");
Console.WriteLine("CORRECTO: mismo trabajo y mismas evidencias, sin repetir Sandbox.");

Console.WriteLine("PRUEBA 4: reconocer el conflicto HTTP 409.");
var original = solicitud.AnalisisLocal.Archivo;
var distinta = new SolicitudAnalisis
{
    SolicitudId = solicitud.SolicitudId,
    AnalisisLocal = new ResultadoAnalisisLocal
    {
        Archivo = new InformacionArchivo
        {
            ArchivoId = Guid.NewGuid(),
            NombreOriginal = original.NombreOriginal,
            Extension = original.Extension,
            Sha256 = original.Sha256,
            TamanoBytes = original.TamanoBytes,
            FechaRegistroUtc = original.FechaRegistroUtc
        }
    }
};
try
{
    await cliente.EnviarArchivoAsync(distinta, guardado.RutaMuestraServidor);
    throw new Exception("Se esperaba un conflicto.");
}
catch (ErrorClienteAnalisis error) when (
    error.EstadoHttp == HttpStatusCode.Conflict &&
    error.Codigo == "SOLICITUD_REUTILIZADA" && !error.Reintentable)
{
    Console.WriteLine("CORRECTO: conflicto identificado sin reintentar.");
}

Console.WriteLine("PRUEBA 5: reconocer HTTP 404.");
try
{
    await cliente.ConsultarAsync(Guid.NewGuid());
    throw new Exception("Se esperaba HTTP 404.");
}
catch (ErrorClienteAnalisis error) when (
    error.EstadoHttp == HttpStatusCode.NotFound && !error.Reintentable)
{
    Console.WriteLine("CORRECTO: análisis inexistente identificado.");
}

Console.WriteLine("LAS CINCO PRUEBAS REALES DEL CLIENTE TERMINARON CORRECTAMENTE.");

internal sealed class TrabajoLocal
{
    public Guid SolicitudId { get; init; }
    public required string RutaMuestraServidor { get; init; }
    public required ResultadoAnalisis Resultado { get; init; }
}

