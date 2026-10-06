namespace SecureGate.Contratos;

public enum EstadoEjecucionSandbox
{
    Completado = 0,
    Timeout = 1,
    ErrorArranque = 2,
    FaltaRecursos = 3,
    NoIniciado = 4
}

public class EventoComportamiento
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string TipoEvento { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}

public class InformeSandbox
{
    public Guid SolicitudId { get; set; }
    public EstadoEjecucionSandbox Estado { get; set; }
    public TimeSpan DuracionEfectiva { get; set; }
    public List<EventoComportamiento> Eventos { get; set; } = new();
    public string? Observaciones { get; set; }
    public bool InteraccionRequerida { get; set; }
}
