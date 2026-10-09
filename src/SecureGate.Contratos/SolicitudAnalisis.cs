namespace SecureGate.Contratos;

// Datos que acompañan al archivo enviado al servidor.
public sealed class SolicitudAnalisis
{
    public Guid SolicitudId { get; init; }

    public required ResultadoAnalisisLocal AnalisisLocal { get; init; }
}

// Respuesta del servidor cuando registra la solicitud.
public sealed class AnalisisAceptado
{
    public Guid SolicitudId { get; init; }

    public Guid AnalisisId { get; init; }

    public Guid ArchivoId { get; init; }

    public EstadoAnalisis Estado { get; init; }
        = EstadoAnalisis.Pendiente;

    public DateTimeOffset FechaRegistroUtc { get; init; }

    public DateTimeOffset? FechaEliminacionMuestraServidorUtc { get; init; }
}