namespace SecureGate.Contratos;

public enum TipoEventoSandbox
{
    Otro = 0,
    ProcesoIniciado = 1,
    ProcesoFinalizado = 2,
    ArchivoCreado = 3,
    ArchivoModificado = 4,
    ArchivoEliminado = 5,
    RegistroModificado = 6,
    ConexionIntentada = 7
}

public sealed class EventoSandbox
{
    public Guid EventoId { get; init; } = Guid.NewGuid();

    public DateTimeOffset FechaUtc { get; init; }

    public TipoEventoSandbox Tipo { get; init; }

    public int? ProcesoId { get; init; }

    public int? ProcesoPadreId { get; init; }

    public string? NombreProceso { get; init; }

    public string? Recurso { get; init; }

    public string Detalle { get; init; } = string.Empty;
}

public sealed class InformeSandbox
{
    public Guid AnalisisId { get; init; }

    public Guid ArchivoId { get; init; }

    public required string Sha256 { get; init; }

    public EstadoComprobacion Estado { get; set; }
        = EstadoComprobacion.NoRealizada;

    // Cuando hay reutilización, los eventos y sus fechas pertenecen a esta observación histórica.
    public Guid? AnalisisOrigenId { get; set; }
    public Guid? ArchivoOrigenId { get; set; }
    public string? NombreArchivoOrigen { get; set; }
    public DateTimeOffset? FechaReutilizacionUtc { get; set; }

    public bool MuestraEjecutada { get; set; }

    public bool ObservadorIniciado { get; set; }

    public bool RedHabilitada { get; set; }

    public DateTimeOffset? InicioUtc { get; set; }

    public DateTimeOffset? FinUtc { get; set; }

    public List<EventoSandbox> Eventos { get; set; } = new();

    public List<string> Limitaciones { get; set; } = new();

    public string? CodigoError { get; set; }
}
