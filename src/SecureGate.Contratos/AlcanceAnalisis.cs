namespace SecureGate.Contratos;

// El monitor, la seleccion manual y la API comparten el alcance ejecutable del prototipo.
public static class AlcanceAnalisis
{
    public static bool AdmiteExtension(string? extension) =>
        string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);
}
