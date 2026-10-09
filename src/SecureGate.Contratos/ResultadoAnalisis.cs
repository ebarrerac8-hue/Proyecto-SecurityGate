namespace SecureGate.Contratos;

public sealed class ResultadoAnalisis
{
    public Guid AnalisisId { get; init; }

    // Copia del servidor no disponible desde esta fecha; el informe permanece.
    public DateTimeOffset? FechaEliminacionMuestraServidorUtc { get; set; }

    public required InformacionArchivo Archivo { get; init; }

    public EstadoAnalisis Estado { get; set; }
        = EstadoAnalisis.Pendiente;

    public EvaluacionRiesgo Evaluacion { get; set; }
        = EvaluacionRiesgo.Incompleto;

    public string Resumen { get; set; } = string.Empty;

    public List<string> Motivos { get; set; } = new();

    public List<string> Limitaciones { get; set; } = new();

    public ResultadoAnalisisLocal? AnalisisLocal { get; set; }

    public ResultadoReputacion? Reputacion { get; set; }

    public InformeSandbox? Sandbox { get; set; }

    public InformeIa? Ia { get; set; }

    public string? CodigoError { get; set; }

    public DateTimeOffset FechaCreacionUtc { get; init; }

    public DateTimeOffset? FechaFinalizacionUtc { get; set; }
}