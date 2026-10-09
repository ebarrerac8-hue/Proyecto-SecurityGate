using SecureGate.Contratos;

namespace SecureGate.Servidor;

// Registro interno: no se devuelve completo al usuario.
public sealed class TrabajoAnalisis
{
    public required Guid SolicitudId { get; init; }

    public required string RutaMuestraServidor { get; init; }

    // Punto de recuperación: evidencias guardadas antes de consultar Gemini.
    public EstadoAnalisis? EstadoTrasEvidencias { get; set; }

    public required ResultadoAnalisis Resultado { get; set; }
}