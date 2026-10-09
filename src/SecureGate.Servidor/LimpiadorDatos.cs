using Microsoft.Extensions.Options;
using SecureGate.Contratos;

namespace SecureGate.Servidor;

public sealed record ResumenLimpieza(int Muestras, int Huerfanas, int Sesiones, int Temporales, int Omitidas);

public sealed class LimpiadorDatos
{
    private readonly OpcionesServidor _opciones;
    private readonly RepositorioAnalisis _repositorio;
    private readonly SemaphoreSlim _registro;
    private readonly ILogger<LimpiadorDatos> _logger;
    private readonly Func<bool> _sandboxAbierto;

    public LimpiadorDatos(IOptions<OpcionesServidor> opciones, RepositorioAnalisis repositorio,
        SemaphoreSlim registro, ILogger<LimpiadorDatos> logger)
        : this(opciones, repositorio, registro, logger, HaySandboxAbierto) { }

    // El observador sustituible permite probar sin iniciar Sandbox.
    public LimpiadorDatos(IOptions<OpcionesServidor> opciones, RepositorioAnalisis repositorio,
        SemaphoreSlim registro, ILogger<LimpiadorDatos> logger, Func<bool> sandboxAbierto)
    {
        _opciones = opciones.Value; _repositorio = repositorio; _registro = registro;
        _logger = logger; _sandboxAbierto = sandboxAbierto;
    }

    public async Task<ResumenLimpieza> LimpiarAsync(DateTimeOffset ahora, CancellationToken ct = default)
    {
        await _registro.WaitAsync(ct);
        try
        {
            string raiz = Path.GetFullPath(_opciones.CarpetaDatos);
            ComprobarAncestros(raiz);
            // Si no se pueden leer TODOS los registros, no se deduce que una muestra sea huérfana.
            var trabajos = await _repositorio.ListarTodosAsync(ct);
            string muestras = Path.Combine(raiz, "Muestras");
            var referencias = new Dictionary<string, List<TrabajoAnalisis>>(StringComparer.OrdinalIgnoreCase);
            foreach (var trabajo in trabajos)
            {
                string ruta = Path.GetFullPath(trabajo.RutaMuestraServidor);
                string? carpeta = Path.GetDirectoryName(ruta);
                if (carpeta is null || !string.Equals(Path.GetDirectoryName(carpeta), muestras, StringComparison.OrdinalIgnoreCase) ||
                    !Guid.TryParseExact(Path.GetFileName(carpeta), "N", out _) || Path.GetFileName(ruta) != "muestra.bin")
                    throw new InvalidDataException("Un registro tiene una ruta de muestra ajena al almacén esperado.");
                if (!referencias.TryGetValue(carpeta, out var lista)) referencias[carpeta] = lista = new();
                lista.Add(trabajo);
            }
            int eliminadas = 0, huerfanas = 0, sesiones = 0, temporales = 0, omitidas = 0;
            DateTimeOffset vencidas = ahora.AddDays(-_opciones.RetencionMuestrasDias);
            DateTimeOffset abandonadas = ahora.AddHours(-_opciones.TemporalesAbandonadosHoras);
            if (Directory.Exists(muestras))
            {
                ComprobarAncestros(muestras);
                foreach (string carpeta in Directory.EnumerateDirectories(muestras))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Guid.TryParseExact(Path.GetFileName(carpeta), "N", out _)) { omitidas++; continue; }
                    if (referencias.TryGetValue(carpeta, out var relacionados))
                    {
                        // Incluye Pendiente, EnCola, EnProceso y cualquier estado desconocido.
                        if (relacionados.Any(t => !Vencido(t, vencidas))) continue;
                        if (!EliminarArbol(carpeta, ct, soloMuestra: true)) { omitidas++; continue; }
                        eliminadas++;
                        foreach (var trabajo in relacionados)
                        {
                            trabajo.Resultado.FechaEliminacionMuestraServidorUtc ??= ahora;
                            await _repositorio.GuardarAsync(trabajo, ct);
                        }
                    }
                    else
                    {
                        if (!EsAntiguo(carpeta, abandonadas, ct)) continue;
                        if (EliminarArbol(carpeta, ct, soloMuestra: true)) huerfanas++; else omitidas++;
                    }
                }
            }
            // Completar la marca si el proceso se interrumpió después del borrado físico.
            foreach (var trabajo in trabajos.Where(t => Vencido(t, vencidas) &&
                t.Resultado.FechaEliminacionMuestraServidorUtc is null && !File.Exists(t.RutaMuestraServidor)))
            {
                trabajo.Resultado.FechaEliminacionMuestraServidorUtc = ahora;
                await _repositorio.GuardarAsync(trabajo, ct);
            }
            string raizSesiones = Path.Combine(raiz, "SesionesSandbox");
            if (Directory.Exists(raizSesiones) && !_sandboxAbierto())
            {
                ComprobarAncestros(raizSesiones);
                var porId = trabajos.ToDictionary(t => t.Resultado.AnalisisId);
                foreach (string carpeta in Directory.EnumerateDirectories(raizSesiones))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Guid.TryParseExact(Path.GetFileName(carpeta), "N", out var id)) { omitidas++; continue; }
                    bool elegible = porId.TryGetValue(id, out var trabajo)
                        ? Vencido(trabajo, vencidas) && EsAntiguo(carpeta, vencidas, ct)
                        : EsAntiguo(carpeta, abandonadas, ct);
                    if (!elegible) continue;
                    if (EliminarArbol(carpeta, ct)) sesiones++; else omitidas++;
                }
            }
            string registros = Path.Combine(raiz, "Trabajos");
            if (Directory.Exists(registros))
            {
                ComprobarAncestros(registros);
                foreach (string ruta in Directory.EnumerateFiles(registros, "*.tmp"))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(ruta), "N", out _) ||
                        File.GetLastWriteTimeUtc(ruta) > abandonadas.UtcDateTime) continue;
                    try
                    {
                        ComprobarAncestros(ruta);
                        // Un escritor activo mantiene el archivo con FileShare.None.
                        using (File.Open(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                        File.Delete(ruta); temporales++;
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { omitidas++; }
                }
            }
            return new ResumenLimpieza(eliminadas, huerfanas, sesiones, temporales, omitidas);
        }
        finally { _registro.Release(); }
    }

    private static bool Vencido(TrabajoAnalisis trabajo, DateTimeOffset limite) =>
        (trabajo.Resultado.Estado is EstadoAnalisis.Completado or EstadoAnalisis.Fallido or EstadoAnalisis.Cancelado) &&
        trabajo.Resultado.FechaFinalizacionUtc is { } fin && fin <= limite;

    private static void ComprobarAncestros(string ruta)
    {
        for (string? actual = Path.GetFullPath(ruta); actual is not null; actual = Path.GetDirectoryName(actual))
            if ((Directory.Exists(actual) || File.Exists(actual)) &&
                (File.GetAttributes(actual) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("La limpieza no sigue enlaces ni puntos de reanálisis.");
    }

    private static List<string> Recorrer(string carpeta, CancellationToken ct)
    {
        ComprobarAncestros(carpeta);
        var elementos = new List<string> { carpeta };
        var pendientes = new Stack<string>(); pendientes.Push(carpeta);
        while (pendientes.TryPop(out var actual))
            foreach (string ruta in Directory.EnumerateFileSystemEntries(actual))
            {
                ct.ThrowIfCancellationRequested();
                var atributos = File.GetAttributes(ruta);
                if ((atributos & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Se omite una carpeta que contiene enlaces.");
                elementos.Add(ruta);
                if ((atributos & FileAttributes.Directory) != 0) pendientes.Push(ruta);
            }
        return elementos;
    }

    private static bool EsAntiguo(string carpeta, DateTimeOffset limite, CancellationToken ct)
    {
        try { return Recorrer(carpeta, ct).All(p => File.GetLastWriteTimeUtc(p) <= limite.UtcDateTime); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private bool EliminarArbol(string carpeta, CancellationToken ct, bool soloMuestra = false)
    {
        try
        {
            var elementos = Recorrer(carpeta, ct);
            if (soloMuestra && elementos.Skip(1).Any(p => Directory.Exists(p) ||
                Path.GetFileName(p) is not ("muestra.bin" or "recepcion.tmp"))) return false;
            foreach (string ruta in elementos.OrderByDescending(p => p.Length))
            {
                ct.ThrowIfCancellationRequested();
                ComprobarAncestros(ruta);
                if (Directory.Exists(ruta)) Directory.Delete(ruta, recursive: false);
                else File.Delete(ruta);
            }
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("LIMPIEZA: se omitió una carpeta por acceso, bloqueo o enlace.");
            return false;
        }
    }

    private static bool HaySandboxAbierto()
    {
        try
        {
            foreach (string nombre in new[] { "WindowsSandbox", "WindowsSandboxClient" })
            {
                var procesos = System.Diagnostics.Process.GetProcessesByName(nombre);
                bool existe = procesos.Length > 0;
                foreach (var proceso in procesos) proceso.Dispose();
                if (existe) return true;
            }
            return false;
        }
        catch { return true; }
    }
}
