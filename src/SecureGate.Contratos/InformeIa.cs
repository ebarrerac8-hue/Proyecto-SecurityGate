namespace SecureGate.Contratos;

public sealed class EvidenciaIa
{
    public required string Id { get; init; }
    public required string Tipo { get; init; }
    public required string Descripcion { get; init; }
}

public sealed class ObservacionIa
{
    public required string Texto { get; init; }
    public required List<string> Referencias { get; init; }
}

public sealed class InformeIa
{
    public EstadoComprobacion Estado { get; set; } = EstadoComprobacion.NoRealizada;
    public string Proveedor { get; init; } = "Gemini";
    public string Modelo { get; init; } = string.Empty;
    public string VersionInstrucciones { get; init; } = "securegate-ia-1";
    public DateTimeOffset? FechaConsultaUtc { get; init; }
    public DateTimeOffset? FechaFinalizacionUtc { get; set; }
    public string Resumen { get; set; } = string.Empty;
    public List<string> ReferenciasResumen { get; set; } = new();
    public List<ObservacionIa> Observaciones { get; set; } = new();
    public List<EvidenciaIa> EvidenciasEnviadas { get; init; } = new();
    public List<string> Limitaciones { get; set; } = new();
    public string? CodigoError { get; set; }
}

