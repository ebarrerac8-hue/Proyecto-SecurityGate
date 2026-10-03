using System.Collections.Concurrent;
using System.Runtime.InteropServices;
namespace SecureGate.Monitoreo;

// El monitor comunica eventos; la interfaz decide cómo mostrarlos.
public sealed class DownloadMonitor : IDisposable
{
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _cancelacion;
    private readonly ConcurrentDictionary<string, byte> _pendientes = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".msi", ".bat", ".ps1", ".vbs", ".cmd", ".com", ".scr" };
    public event Action<string>? ArchivoDetectado;
    public event Action<string>? ErrorMonitoreo;
    public string CarpetaActual { get; private set; } = "";

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr ruta);
    public static string ObtenerDescargas()
    {
        var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        int resultado = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var puntero);
        try
        {
            if (resultado == 0 && puntero != IntPtr.Zero)
                return Marshal.PtrToStringUni(puntero)!;
        }
        finally { if (puntero != IntPtr.Zero) Marshal.FreeCoTaskMem(puntero); }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }
    public void IniciarMonitoreo(string? carpeta = null)
    {
        DetenerMonitoreo();
        string ruta = Path.GetFullPath(carpeta ?? ObtenerDescargas());
        if (!Directory.Exists(ruta)) throw new DirectoryNotFoundException("No se encontró la carpeta de descargas: " + ruta);
        CarpetaActual = ruta;
        _cancelacion = new CancellationTokenSource();
        var token = _cancelacion.Token;
        _watcher = new FileSystemWatcher(ruta)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false
        };
        _watcher.Created += (_, e) => Encolar(e.FullPath, token);
        _watcher.Renamed += (_, e) => Encolar(e.FullPath, token);
        _watcher.Error += (_, e) => ErrorMonitoreo?.Invoke("El monitor perdió eventos: " + e.GetException().Message);
        _watcher.EnableRaisingEvents = true;
    }
    private void Encolar(string ruta, CancellationToken token)
    {
        if (!Extensiones.Contains(Path.GetExtension(ruta)) || !_pendientes.TryAdd(ruta, 0)) return;
        _ = EsperarAsync(ruta, token);
    }
    private async Task EsperarAsync(string ruta, CancellationToken token)
    {
        try
        {
            long tamañoAnterior = -1;
            DateTime fechaAnterior = DateTime.MinValue;
            int estable = 0;
            for (int i = 0; i < 120; i++)
            {
                await Task.Delay(1000, token);
                token.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(ruta);
                    if (!info.Exists) return;
                    long tamaño = info.Length;
                    DateTime fecha = info.LastWriteTimeUtc;
                    using var lectura = File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.None);
                    estable = tamaño == tamañoAnterior && fecha == fechaAnterior ? estable + 1 : 0;
                    tamañoAnterior = tamaño;
                    fechaAnterior = fecha;
                    if (estable >= 2)
                    {
                        token.ThrowIfCancellationRequested();
                        ArchivoDetectado?.Invoke(ruta);
                        return;
                    }
                }
                catch (IOException) { estable = 0; }
            }
            ErrorMonitoreo?.Invoke("No se pudo analizar automáticamente " + Path.GetFileName(ruta) + ". Podés seleccionarlo manualmente cuando termine la descarga.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!token.IsCancellationRequested) ErrorMonitoreo?.Invoke(ex.Message); }
        finally { _pendientes.TryRemove(ruta, out _); }
    }
    public void DetenerMonitoreo()
    {
        _watcher?.Dispose();
        _watcher = null;
        _cancelacion?.Cancel();
        _cancelacion?.Dispose();
        _cancelacion = null;
    }
    public void Dispose() => DetenerMonitoreo();
}
