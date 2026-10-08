namespace SecureGate.Contratos;

public interface IClienteAnalisis
{
    Task<AnalisisAceptado> EnviarAsync(
        SolicitudAnalisis solicitud,
        Stream contenidoArchivo,
        CancellationToken cancellationToken = default);

    Task<ResultadoAnalisis> ConsultarAsync(
        Guid analisisId,
        CancellationToken cancellationToken = default);
}