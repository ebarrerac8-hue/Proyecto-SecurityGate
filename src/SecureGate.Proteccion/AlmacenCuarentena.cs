using System.Security.Cryptography;
using System.Text.Json;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

public sealed class RegistroCuarentena
{
    public int Version { get; set; } = 1;
    public Guid ArchivoId { get; set; }
    public string NombreOriginal { get; set; } = "";
    public string Extension { get; set; } = "";
    public string RutaOriginal { get; set; } = "";
    public long TamanoBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTimeOffset FechaRegistroUtc { get; set; }
    public DateTimeOffset FechaActualizacionUtc { get; set; }
    public EstadoArchivo Estado { get; set; }
    public string OperacionPendiente { get; set; } = "Retener";

    public InformacionArchivo Informacion() => new()
    {
        ArchivoId = ArchivoId, NombreOriginal = NombreOriginal,
        Extension = Extension, TamanoBytes = TamanoBytes, Sha256 = Sha256,
        FechaRegistroUtc = FechaRegistroUtc
    };
}

internal sealed class AlmacenCuarentena
{
    internal string Carpeta { get; }
    internal AlmacenCuarentena(string? carpeta)
    {
        Carpeta = Path.GetFullPath(carpeta ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureGate", "Cuarentena"));
        Directory.CreateDirectory(Carpeta);
        ComprobarRuta(Carpeta);
    }
    internal static void ComprobarRuta(string ruta)
    {
        for (string? actual = Path.GetFullPath(ruta); actual is not null;
             actual = Path.GetDirectoryName(actual))
        {
            if ((File.Exists(actual) || Directory.Exists(actual)) &&
                (File.GetAttributes(actual) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("No se admiten enlaces ni puntos de reanalisis en esta ruta.");
        }
    }
    internal string Muestra(Guid id) => Path.Combine(Carpeta, $"{id}.bytes");
    private string Registro(Guid id) => Path.Combine(Carpeta, $"{id}.json");
    internal async Task<FileStream> AbrirSesionAsync(CancellationToken ct, string nombre = ".operacion.lock")
    {
        if (nombre != ".operacion.lock" && nombre != ".preparacion-envios.lock")
            throw new ArgumentException("Nombre de bloqueo no admitido.");
        var ruta = Path.Combine(Carpeta, nombre);
        ComprobarRuta(ruta);
        for (int intento = 0; ; intento++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(ruta, FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (intento < 300)
            { await Task.Delay(100, ct); }
        }
    }
    internal RegistroCuarentena Leer(Guid id)
    {
        if (id == Guid.Empty) throw new InvalidDataException("Identificador vacio.");
        string ruta = Registro(id);
        ComprobarRuta(ruta);
        var r = JsonSerializer.Deserialize<RegistroCuarentena>(File.ReadAllText(ruta))
            ?? throw new InvalidDataException("Registro vacio.");
        if (r.Version != 1 || r.ArchivoId != id || r.TamanoBytes <= 0 ||
            r.Sha256.Length != 64 || !r.Sha256.All(Uri.IsHexDigit) ||
            string.IsNullOrWhiteSpace(r.NombreOriginal) ||
            Path.GetFileName(r.NombreOriginal) != r.NombreOriginal ||
            !Path.IsPathFullyQualified(r.RutaOriginal) ||
            !Enum.IsDefined(r.Estado) ||
            r.Extension != Path.GetExtension(r.NombreOriginal) ||
            (r.OperacionPendiente != "" && r.OperacionPendiente != "Retener" &&
             r.OperacionPendiente != "Eliminar"))
            throw new InvalidDataException("Registro de cuarentena inconsistente.");
        return r;
    }
    internal IEnumerable<Guid> Identificadores()
    {
        foreach (var ruta in Directory.EnumerateFiles(Carpeta, "*.json"))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(ruta), out var id))
                throw new InvalidDataException("Nombre de registro desconocido.");
            yield return id;
        }
    }
    internal void Guardar(RegistroCuarentena r)
    {
        string destino = Registro(r.ArchivoId);
        ComprobarRuta(destino);
        string temporal = destino + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporal, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, r);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporal, destino, overwrite: true);
        }
        finally { if (File.Exists(temporal)) File.Delete(temporal); }
    }
    internal static async Task<string> HashAsync(string ruta, CancellationToken ct)
    {
        ComprobarRuta(ruta);
        await using var stream = new FileStream(ruta, FileMode.Open, FileAccess.Read,
            FileShare.None, 65536, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct))
            .ToLowerInvariant();
    }
    internal async Task ValidarAsync(RegistroCuarentena r, CancellationToken ct)
    {
        string ruta = Muestra(r.ArchivoId);
        string hash = await HashAsync(ruta, ct);
        if (new FileInfo(ruta).Length != r.TamanoBytes ||
            !hash.Equals(r.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La muestra retenida no coincide con su registro.");
    }
    internal async Task<RegistroCuarentena> RecuperarAsync(Guid id, CancellationToken ct)
    {
        var r = Leer(id);
        string muestra = Muestra(id);
        ComprobarRuta(muestra);
        if (r.OperacionPendiente == "Retener" && !File.Exists(muestra))
        {
            ComprobarRuta(r.RutaOriginal);
            if (!File.Exists(r.RutaOriginal))
                throw new InvalidDataException("No se encontro la muestra ni su ubicacion original: " + id);
            string originalHash = await HashAsync(r.RutaOriginal, ct);
            if (!originalHash.Equals(r.Sha256, StringComparison.OrdinalIgnoreCase) ||
                new FileInfo(r.RutaOriginal).Length != r.TamanoBytes)
                throw new InvalidDataException("La retencion pendiente no coincide con el archivo original.");
            ct.ThrowIfCancellationRequested();
            File.Move(r.RutaOriginal, muestra, overwrite: false);
        }
        if (r.OperacionPendiente == "Eliminar" && !File.Exists(muestra))
        {
            r.Estado = EstadoArchivo.Eliminado;
            r.OperacionPendiente = "";
            r.FechaActualizacionUtc = DateTimeOffset.UtcNow;
            Guardar(r);
        }
        else if (r.OperacionPendiente == "Retener" && File.Exists(muestra))
        {
            await ValidarAsync(r, ct);
            r.Estado = EstadoArchivo.Retenido;
            r.OperacionPendiente = "";
            r.FechaActualizacionUtc = DateTimeOffset.UtcNow;
            Guardar(r);
        }
        if (r.Estado == EstadoArchivo.Retenido) await ValidarAsync(r, ct);
        return r;
    }
}
