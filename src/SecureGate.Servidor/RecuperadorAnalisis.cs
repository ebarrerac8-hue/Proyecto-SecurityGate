using SecureGate.Contratos;

namespace SecureGate.Servidor;

public sealed class RecuperadorAnalisis : IHostedService
{
    private readonly RepositorioAnalisis _repositorio;
    private readonly ColaAnalisis _cola;
    private readonly ILogger<RecuperadorAnalisis> _logger;

    public RecuperadorAnalisis(
        RepositorioAnalisis repositorio,
        ColaAnalisis cola,
        ILogger<RecuperadorAnalisis> logger)
    {
        _repositorio = repositorio;
        _cola = cola;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var pendientes = await _repositorio.ListarPendientesAsync(
            cancellationToken);

        int recuperados = 0;

        foreach (var trabajo in pendientes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            trabajo.Resultado.Estado = EstadoAnalisis.EnCola;
            trabajo.Resultado.Resumen =
                "Trabajo recuperado después del inicio del servidor. " +
                "En espera de procesamiento.";

            await _repositorio.GuardarAsync(
                trabajo,
                cancellationToken);

            if (_cola.Encolar(trabajo.Resultado.AnalisisId))
            {
                recuperados++;
            }
        }

        _logger.LogInformation(
            "RECUPERACION: {Cantidad} trabajo(s) agregado(s) a la cola.",
            recuperados);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}