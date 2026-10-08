namespace SecureGate.Servidor;

public sealed class OpcionesServidor
{
    // Almacenamiento fuera del repositorio.
    public string CarpetaDatos { get; set; } = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
        "SecureGate",
        "Servidor");

    // Límite inicial: 100 MiB por archivo.
    public long TamanoMaximoArchivoBytes { get; set; }
        = 100L * 1024 * 1024;

    // Duración solicitada para observar la muestra.
    public int TiempoObservacionSegundos { get; set; } = 120;
}