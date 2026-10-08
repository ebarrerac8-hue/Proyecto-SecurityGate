using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Contratos;

namespace SecureGate.Cliente;

public sealed class EnvioCliente
{
    public required SolicitudAnalisis Solicitud { get; init; }
    public required string RutaArchivo { get; init; }
    public required string Servidor { get; init; }
    public Guid? AnalisisId { get; set; }
    public ResultadoAnalisis? Resultado { get; set; }
    public string EstadoLocal { get; set; } = "Preparado";
    public string? UltimoError { get; set; }
    public DateTimeOffset FechaCreacionUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FechaActualizacionUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool Terminado => Resultado?.Estado is EstadoAnalisis.Completado or
        EstadoAnalisis.Fallido or EstadoAnalisis.Cancelado;
}

public sealed class AlmacenEnviosCliente
{
    private readonly string _carpeta;
    private readonly SemaphoreSlim _control = new(1, 1);
    private static readonly JsonSerializerOptions Json = CrearOpciones();

    public AlmacenEnviosCliente(string? carpeta = null)
    {
        _carpeta = carpeta ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureGate", "Cliente", "Envios");
    }

    public async Task GuardarAsync(EnvioCliente envio, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envio);
        if (envio.Solicitud.SolicitudId == Guid.Empty)
            throw new ArgumentException("SolicitudId no puede estar vacío.");

        await _control.WaitAsync(ct);
        string? temporal = null;
        try
        {
            Directory.CreateDirectory(_carpeta);
            string destino = Path.Combine(_carpeta, envio.Solicitud.SolicitudId.ToString("N") + ".json");
            temporal = destino + "." + Guid.NewGuid().ToString("N") + ".tmp";
            envio.FechaActualizacionUtc = DateTimeOffset.UtcNow;

            await using (var archivo = new FileStream(temporal,
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
            {
                await JsonSerializer.SerializeAsync(archivo, envio, Json, ct);
                await archivo.FlushAsync(ct);
                archivo.Flush(flushToDisk: true);
            }
            File.Move(temporal, destino, overwrite: true);
            temporal = null;
        }
        finally
        {
            if (temporal is not null)
            {
                try { File.Delete(temporal); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _control.Release();
        }
    }

    public async Task<IReadOnlyList<EnvioCliente>> ListarAsync(CancellationToken ct = default)
    {
        await _control.WaitAsync(ct);
        try
        {
            var lista = new List<EnvioCliente>();
            if (!Directory.Exists(_carpeta)) return lista;

            foreach (string ruta in Directory.EnumerateFiles(_carpeta, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                await using var archivo = File.OpenRead(ruta);
                var envio = await JsonSerializer.DeserializeAsync<EnvioCliente>(archivo, Json, ct);
                if (envio is null || envio.Solicitud.SolicitudId == Guid.Empty ||
                    envio.Solicitud.AnalisisLocal?.Archivo is null)
                    throw new InvalidDataException("Registro de envío inválido: " + Path.GetFileName(ruta));
                lista.Add(envio);
            }
            return lista.OrderByDescending(e => e.FechaCreacionUtc).ToList();
        }
        finally { _control.Release(); }
    }

    private static JsonSerializerOptions CrearOpciones()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        return json;
    }
}

