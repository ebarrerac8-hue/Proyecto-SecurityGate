using Microsoft.Extensions.Options;

namespace SecureGate.Servidor;

public sealed class ServicioLimpieza(LimpiadorDatos limpiador, IOptions<OpcionesServidor> opciones,
    ILogger<ServicioLimpieza> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Value.LimpiezaAutomaticaHabilitada) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var resumen = await limpiador.LimpiarAsync(DateTimeOffset.UtcNow, stoppingToken);
                logger.LogInformation("LIMPIEZA: muestras {Muestras}; huérfanas {Huerfanas}; sesiones {Sesiones}; temporales {Temporales}; omitidas {Omitidas}.",
                    resumen.Muestras, resumen.Huerfanas, resumen.Sesiones, resumen.Temporales, resumen.Omitidas);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning(error, "LIMPIEZA: pasada suspendida. Se volverá a intentar en el siguiente intervalo.");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(opciones.Value.IntervaloLimpiezaMinutos), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
