using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureGate.Servidor;
using SecureGate.Contratos;

var ahora = DateTimeOffset.UtcNow;
int pruebas = 0;
async Task Probar(string nombre, Func<Entorno, Task> probar, bool sandbox = false)
{
    using var entorno = new Entorno(ahora, sandbox);
    await probar(entorno);
    Console.WriteLine("CORRECTO: " + nombre); pruebas++;
}
void Exigir(bool condicion) { if (!condicion) throw new Exception("La prueba de retención falló."); }
foreach (var estado in new[] { EstadoAnalisis.Pendiente, EstadoAnalisis.EnCola, EstadoAnalisis.EnProceso })
    await Probar("no elimina muestras " + estado, async e =>
    {
        var t = await e.Crear(estado, 20);
        await e.Limpiar(); Exigir(File.Exists(t.RutaMuestraServidor));
    });
await Probar("conserva terminales recientes", async e =>
{
    var t = await e.Crear(EstadoAnalisis.Completado, 2);
    await e.Limpiar(); Exigir(File.Exists(t.RutaMuestraServidor));
});
await Probar("elimina copia vencida, conserva informe, IA y solicitud", async e =>
{
    var t = await e.Crear(EstadoAnalisis.Completado, 20);
    string ia = JsonSerializer.Serialize(t.Resultado.Ia);
    string sb = JsonSerializer.Serialize(t.Resultado.Sandbox);
    var r = await e.Limpiar();
    var recuperado = await e.Repo.BuscarPorSolicitudAsync(t.SolicitudId);
    Exigir(r.Muestras == 1 && !File.Exists(t.RutaMuestraServidor) && recuperado is not null);
    Exigir(recuperado!.Resultado.AnalisisId == t.Resultado.AnalisisId &&
        recuperado.Resultado.FechaEliminacionMuestraServidorUtc == ahora &&
        recuperado.Resultado.Evaluacion == t.Resultado.Evaluacion &&
        recuperado.Resultado.Resumen == t.Resultado.Resumen &&
        JsonSerializer.Serialize(recuperado.Resultado.Ia) == ia && JsonSerializer.Serialize(recuperado.Resultado.Sandbox) == sb);
    Exigir((await e.Limpiar()).Muestras == 0);
});
foreach (var estado in new[] { EstadoAnalisis.Fallido, EstadoAnalisis.Cancelado })
    await Probar("elimina terminal vencida " + estado, async e =>
    {
        var t = await e.Crear(estado, 20);
        await e.Limpiar(); Exigir(!File.Exists(t.RutaMuestraServidor));
    });
await Probar("elimina huérfanas antiguas y conserva nuevas", async e =>
{
    string vieja = e.Huerfana(2), nueva = e.Huerfana(0);
    var r = await e.Limpiar(); Exigir(r.Huerfanas == 1 && !Directory.Exists(vieja) && Directory.Exists(nueva));
});
await Probar("registro corrupto suspende el borrado", async e =>
{
    string muestra = e.Huerfana(2);
    Directory.CreateDirectory(Path.Combine(e.Raiz, "Trabajos"));
    await File.WriteAllTextAsync(Path.Combine(e.Raiz, "Trabajos", Guid.NewGuid().ToString("N") + ".json"), "{");
    bool suspendida = false;
    try { await e.Limpiar(); } catch (JsonException) { suspendida = true; }
    Exigir(suspendida && Directory.Exists(muestra));
});
await Probar("no elimina temporales bloqueados", async e =>
{
    string carpeta = Path.Combine(e.Raiz, "Trabajos"); Directory.CreateDirectory(carpeta);
    string ruta = Path.Combine(carpeta, Guid.NewGuid().ToString("N") + ".tmp");
    await File.WriteAllTextAsync(ruta, "temporal"); File.SetLastWriteTimeUtc(ruta, ahora.AddDays(-2).UtcDateTime);
    using (File.Open(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    { await e.Limpiar(); Exigir(File.Exists(ruta)); }
    Exigir((await e.Limpiar()).Temporales == 1 && !File.Exists(ruta));
});
await Probar("sesión antigua eliminada; sesión activa conservada", async e =>
{
    var terminada = await e.Crear(EstadoAnalisis.Completado, 20);
    var activa = await e.Crear(EstadoAnalisis.EnProceso, 20);
    string antigua = e.Sesion(terminada.Resultado.AnalisisId), protegida = e.Sesion(activa.Resultado.AnalisisId);
    var r = await e.Limpiar(); Exigir(r.Sesiones == 1 && !Directory.Exists(antigua) && Directory.Exists(protegida));
});
await Probar("Sandbox abierto suspende limpieza de sesiones", async e =>
{
    var t = await e.Crear(EstadoAnalisis.Completado, 20);
    string sesion = e.Sesion(t.Resultado.AnalisisId);
    Exigir((await e.Limpiar()).Sesiones == 0 && Directory.Exists(sesion));
}, sandbox: true);
await Probar("rutas fuera del almacén suspenden la limpieza", async e =>
{
    var t = await e.Crear(EstadoAnalisis.Completado, 20);
    var invalido = new TrabajoAnalisis
    {
        SolicitudId = t.SolicitudId, Resultado = t.Resultado,
        RutaMuestraServidor = Path.Combine(e.Raiz, "fuera.bin")
    };
    await e.Repo.GuardarAsync(invalido);
    bool suspendida = false;
    try { await e.Limpiar(); } catch (InvalidDataException) { suspendida = true; }
    Exigir(suspendida && File.Exists(t.RutaMuestraServidor));
});
await Probar("limpieza espera a que termine un registro concurrente", async e =>
{
    string muestra = e.Huerfana(2);
    await e.Registro.WaitAsync();
    var limpieza = e.Limpiar(); Exigir(!limpieza.IsCompleted && Directory.Exists(muestra));
    e.Registro.Release();
    await limpieza; Exigir(!Directory.Exists(muestra));
});
Console.WriteLine($"LAS {pruebas} PRUEBAS DE RETENCIÓN TERMINARON CORRECTAMENTE. No se usaron tus datos reales.");

sealed class Entorno : IDisposable
{
    public string Raiz { get; } = Path.Combine(Path.GetTempPath(), "SecureGate_Retencion_Pruebas", Guid.NewGuid().ToString("N"));
    public RepositorioAnalisis Repo { get; }
    public SemaphoreSlim Registro { get; } = new(1, 1);
    private readonly LimpiadorDatos _limpiador;
    private readonly DateTimeOffset _ahora;
    public Entorno(DateTimeOffset ahora, bool abierto)
    {
        _ahora = ahora; Directory.CreateDirectory(Raiz);
        var opciones = Options.Create(new OpcionesServidor { CarpetaDatos = Raiz });
        Repo = new RepositorioAnalisis(opciones, NullLogger<RepositorioAnalisis>.Instance);
        _limpiador = new LimpiadorDatos(opciones, Repo, Registro, NullLogger<LimpiadorDatos>.Instance, () => abierto);
    }
    public Task<ResumenLimpieza> Limpiar() => _limpiador.LimpiarAsync(_ahora);
    public async Task<TrabajoAnalisis> Crear(EstadoAnalisis estado, int dias)
    {
        string carpeta = Huerfana(dias);
        var archivo = new InformacionArchivo
        {
            ArchivoId = Guid.NewGuid(), NombreOriginal = "prueba.exe", Extension = ".exe", TamanoBytes = 3,
            Sha256 = new string('a', 64), FechaRegistroUtc = _ahora.AddDays(-dias)
        };
        var t = new TrabajoAnalisis
        {
            SolicitudId = Guid.NewGuid(), RutaMuestraServidor = Path.Combine(carpeta, "muestra.bin"),
            Resultado = new ResultadoAnalisis
            {
                AnalisisId = Guid.NewGuid(), Archivo = archivo, Estado = estado, Evaluacion = EvaluacionRiesgo.Incompleto,
                Resumen = "Informe de prueba", FechaCreacionUtc = _ahora.AddDays(-dias), FechaFinalizacionUtc = _ahora.AddDays(-dias),
                Ia = new InformeIa { Estado = EstadoComprobacion.Completada, Resumen = "Explicación conservada" },
                Sandbox = new InformeSandbox { Sha256 = archivo.Sha256, Estado = EstadoComprobacion.Completada }
            }
        };
        await Repo.GuardarAsync(t); return t;
    }
    public string Huerfana(int dias)
    {
        string carpeta = Path.Combine(Raiz, "Muestras", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta); File.WriteAllText(Path.Combine(carpeta, "muestra.bin"), "abc");
        Envejecer(carpeta, dias); return carpeta;
    }
    public string Sesion(Guid id)
    {
        string carpeta = Path.Combine(Raiz, "SesionesSandbox", id.ToString("N"));
        string salida = Path.Combine(carpeta, Guid.NewGuid().ToString("N"), "Salida");
        Directory.CreateDirectory(salida); File.WriteAllText(Path.Combine(salida, "informe.json"), "{}");
        Envejecer(carpeta, 20); return carpeta;
    }
    private void Envejecer(string carpeta, int dias)
    {
        foreach (string p in Directory.EnumerateFiles(carpeta, "*", SearchOption.AllDirectories))
            File.SetLastWriteTimeUtc(p, _ahora.AddDays(-dias).UtcDateTime);
        foreach (string p in Directory.EnumerateDirectories(carpeta, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length))
            Directory.SetLastWriteTimeUtc(p, _ahora.AddDays(-dias).UtcDateTime);
        Directory.SetLastWriteTimeUtc(carpeta, _ahora.AddDays(-dias).UtcDateTime);
    }
    public void Dispose() { Directory.Delete(Raiz, recursive: true); Registro.Dispose(); }
}
