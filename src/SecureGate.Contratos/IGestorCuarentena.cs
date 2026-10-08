namespace SecureGate.Contratos;

public enum EstadoArchivo
{
    NoDeterminado = 0,
    Retenido = 1,
    Liberado = 2,
    Eliminado = 3
}

public enum MotivoLiberacion
{
    NoEspecificado = 0,
    EvaluacionPermitida = 1,
    ExcepcionDelUsuario = 2
}

public sealed class ResultadoOperacionArchivo
{
    public Guid ArchivoId { get; init; }

    public bool Realizada { get; init; }

    public EstadoArchivo EstadoActual { get; init; }
        = EstadoArchivo.NoDeterminado;

    public InformacionArchivo? Archivo { get; init; }

    public string Mensaje { get; init; } = string.Empty;

    public string? CodigoError { get; init; }
}

public sealed class SolicitudLiberacion
{
    public Guid ArchivoId { get; init; }

    public required string Sha256Esperado { get; init; }

    public MotivoLiberacion Motivo { get; init; }
        = MotivoLiberacion.NoEspecificado;
}

public interface IGestorCuarentena
{
    Task<ResultadoOperacionArchivo> RetenerAsync(
        string rutaArchivo,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacionArchivo> EliminarAsync(
        Guid archivoId,
        CancellationToken cancellationToken = default);

    Task<ResultadoOperacionArchivo> LiberarAsync(
        SolicitudLiberacion solicitud,
        CancellationToken cancellationToken = default);
}