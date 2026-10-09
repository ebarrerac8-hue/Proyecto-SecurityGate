namespace SecureGate.Servidor;

public sealed class OpcionesIa
{
    public string Clave { get; init; } = string.Empty;
    public string Modelo { get; init; } = string.Empty;
    public int TiempoMaximoSegundos { get; init; } = 20;

    public static OpcionesIa DesdeEntorno() => new()
    {
        Clave = (Environment.GetEnvironmentVariable("SECUREGATE_GEMINI_API_KEY") ?? "").Trim(),
        Modelo = (Environment.GetEnvironmentVariable("SECUREGATE_GEMINI_MODEL") ?? "").Trim()
    };
}

