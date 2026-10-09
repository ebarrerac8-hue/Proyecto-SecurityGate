namespace SecureGate.Servidor;

public sealed class OpcionesServidor
{
    public bool LimpiezaAutomaticaHabilitada { get; set; } = true;
    public int RetencionMuestrasDias { get; set; } = 7;
    public int TemporalesAbandonadosHoras { get; set; } = 24;
    public int IntervaloLimpiezaMinutos { get; set; } = 60;

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