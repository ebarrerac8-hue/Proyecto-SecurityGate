namespace SecureGate.Contratos;

public sealed class ResultadoAnalisis
{
    public Guid AnalisisId { get; init; }

    public required InformacionArchivo Archivo { get; init; }

    public EstadoAnalisis Estado { get; set; }
        = EstadoAnalisis.Pendiente;

    public EvaluacionRiesgo Evaluacion { get; set; }
        = EvaluacionRiesgo.Incompleto;

    public string Resumen { get; set; } = string.Empty;

    public List<string> Motivos { get; set; } = new();

    public string? CodigoError { get; set; }

    public DateTimeOffset FechaCreacionUtc { get; init; }

    public DateTimeOffset? FechaFinalizacionUtc { get; set; }
}