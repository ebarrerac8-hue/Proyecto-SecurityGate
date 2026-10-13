namespace SecureGate;

internal static class EntornoCliente
{
    private static string? Aislado => Environment.GetEnvironmentVariable("SECUREGATE_CLIENT_DATA_DIR");
    internal static bool EsPrueba => !string.IsNullOrWhiteSpace(Aislado);
    internal static string CarpetaCliente => EsPrueba ? Path.GetFullPath(Aislado!) : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureGate", "Cliente");
    internal static string? CarpetaCuarentena => EsPrueba ? Path.Combine(CarpetaCliente, "Cuarentena") : null;
    internal static string? CarpetaHistorial => EsPrueba ? Path.Combine(CarpetaCliente, "HistorialAuxiliar") : null;
    internal static void ValidarRutaPrueba(string ruta)
    {
        if (!EsPrueba) return;
        string relativa = Path.GetRelativePath(CarpetaCliente, Path.GetFullPath(ruta));
        if (Path.IsPathRooted(relativa) || relativa == ".." || relativa.StartsWith(".." + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("En modo de prueba solo se admiten rutas de la carpeta aislada.");
    }
    internal static void MonitorListo(string carpeta)
    {
        if (!EsPrueba) return;
        ValidarRutaPrueba(carpeta);
        File.WriteAllText(Path.Combine(CarpetaCliente, "monitor-listo.json"),
            System.Text.Json.JsonSerializer.Serialize(new { Pid = Environment.ProcessId, Carpeta = carpeta }));
    }
}
