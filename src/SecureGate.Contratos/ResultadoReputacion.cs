namespace SecureGate.Contratos;

public sealed class ResultadoReputacion
{
    public required string Sha256 { get; init; }

    public EstadoComprobacion Estado { get; set; }
        = EstadoComprobacion.NoRealizada;

    public bool? InformeEncontrado { get; set; }

    public int? MotoresMaliciosos { get; set; }

    public int? MotoresSospechosos { get; set; }

    public int? MotoresSinDeteccion { get; set; }

    public DateTimeOffset? FechaConsultaUtc { get; set; }

    public DateTimeOffset? FechaInformeUtc { get; set; }

    public string Detalle { get; set; } = string.Empty;

    public string? CodigoError { get; set; }
}