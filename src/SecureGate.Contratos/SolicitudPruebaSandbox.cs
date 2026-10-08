namespace SecureGate.Contratos;

public sealed class SolicitudPruebaSandbox
{
    public Guid AnalisisId { get; init; }
    public Guid ArchivoId { get; init; }
    public required string Sha256 { get; init; }
    public required string RutaMuestraServidor { get; init; }
    public string ExtensionMuestra { get; init; } = string.Empty;
    public int TiempoObservacionSegundos { get; init; } = 120;
}

public interface IAnalizadorSandbox
{
    Task<InformeSandbox> EjecutarAsync(
        SolicitudPruebaSandbox solicitud,
        CancellationToken cancellationToken = default);
}
