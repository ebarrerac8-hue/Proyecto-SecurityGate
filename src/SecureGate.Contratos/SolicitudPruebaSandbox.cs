namespace SecureGate.Contratos;

// Solicitud interna del servidor al módulo Sandbox.
public sealed class SolicitudPruebaSandbox
{
    public Guid AnalisisId { get; init; }

    public Guid ArchivoId { get; init; }

    public required string Sha256 { get; init; }

    public required string RutaMuestraServidor { get; init; }

    public int TiempoObservacionSegundos { get; init; } = 120;
}

// Operación que implementará el módulo de Gerson.
public interface IAnalizadorSandbox
{
    Task<InformeSandbox> EjecutarAsync(
        SolicitudPruebaSandbox solicitud,
        CancellationToken cancellationToken = default);
}