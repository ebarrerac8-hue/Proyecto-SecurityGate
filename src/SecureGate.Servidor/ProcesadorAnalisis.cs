using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecureGate.Contratos;

namespace SecureGate.Servidor;

public sealed class ProcesadorAnalisis : BackgroundService
{
    private readonly ColaAnalisis _cola;
    private readonly RepositorioAnalisis _repositorio;
    private readonly ClienteReputacion _reputacion;
    private readonly IAnalizadorSandbox _sandbox;
    private readonly OpcionesServidor _opciones;
    private readonly ILogger<ProcesadorAnalisis> _logger;

    public ProcesadorAnalisis(
        ColaAnalisis cola,
        RepositorioAnalisis repositorio,
        ClienteReputacion reputacion,
        IAnalizadorSandbox sandbox,
        IOptions<OpcionesServidor> opciones,
        ILogger<ProcesadorAnalisis> logger)
    {
        _cola = cola;
        _repositorio = repositorio;
        _reputacion = reputacion;
        _sandbox = sandbox;
        _opciones = opciones.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var id in _cola.LeerAsync(stoppingToken))
            {
                TrabajoAnalisis? trabajo = null;
                try
                {
                    trabajo = await _repositorio.ObtenerAsync(id, stoppingToken);
                    if (trabajo is null || trabajo.Resultado.Estado is
                        EstadoAnalisis.Completado or EstadoAnalisis.Fallido or EstadoAnalisis.Cancelado)
                        continue;

                    var resultado = trabajo.Resultado;
                    resultado.Estado = EstadoAnalisis.EnProceso;
                    resultado.Evaluacion = EvaluacionRiesgo.Incompleto;
                    resultado.FechaFinalizacionUtc = null;
                    resultado.CodigoError = null;
                    resultado.Resumen = "Consultando la reputación del archivo.";
                    await _repositorio.GuardarAsync(trabajo, stoppingToken);
                    _logger.LogInformation("PROCESANDO: {AnalisisId}", id);

                    // No repetir una consulta ya guardada si se interrumpió Sandbox.
                    if (resultado.Reputacion?.Estado != EstadoComprobacion.Completada ||
                        !string.Equals(resultado.Reputacion.Sha256, resultado.Archivo.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        resultado.Reputacion = await _reputacion.ConsultarAsync(
                            resultado.Archivo.Sha256, stoppingToken);
                    }

                    _logger.LogInformation("REPUTACION: {AnalisisId}; {Detalle}",
                        id, resultado.Reputacion!.Detalle);
                    EvaluadorAnalisis.Evaluar(resultado);

                    // La recomendación ya es de no ejecutar si se reportaron amenazas.
                    // No iniciar automáticamente otra ejecución de esa muestra.
                    if (resultado.Evaluacion == EvaluacionRiesgo.AmenazaDetectada)
                    {
                        AgregarLimitacion(resultado,
                            "Sandbox se omitió porque ya se reportaron detecciones de amenaza.");
                        resultado.Estado = EstadoAnalisis.Completado;
                    }
                    else
                    {
                        resultado.Resumen = "Preparando prueba aislada en Windows Sandbox.";
                        await _repositorio.GuardarAsync(trabajo, stoppingToken);
                        _logger.LogInformation("SANDBOX INICIO: {AnalisisId}", id);

                        resultado.Sandbox = await _sandbox.EjecutarAsync(
                            new SolicitudPruebaSandbox
                            {
                                AnalisisId = id,
                                ArchivoId = resultado.Archivo.ArchivoId,
                                Sha256 = resultado.Archivo.Sha256,
                                RutaMuestraServidor = trabajo.RutaMuestraServidor,
                                ExtensionMuestra = resultado.Archivo.Extension,
                                TiempoObservacionSegundos = _opciones.TiempoObservacionSegundos
                            }, stoppingToken);

                        foreach (string detalle in resultado.Sandbox.Limitaciones)
                            AgregarLimitacion(resultado, detalle);

                        EvaluadorAnalisis.Evaluar(resultado);
                        bool reputacionTerminada =
                            resultado.Reputacion.Estado == EstadoComprobacion.Completada;
                        bool sandboxTerminado =
                            resultado.Sandbox.Estado == EstadoComprobacion.Completada;

                        resultado.Estado = reputacionTerminada && sandboxTerminado
                            ? EstadoAnalisis.Completado : EstadoAnalisis.Fallido;
                        resultado.CodigoError = resultado.Sandbox.CodigoError
                            ?? resultado.Reputacion.CodigoError;

                        _logger.LogInformation(
                            "SANDBOX FIN: {AnalisisId}; estado: {Estado}; eventos: {Cantidad}; error: {Error}",
                            id, resultado.Sandbox.Estado, resultado.Sandbox.Eventos.Count,
                            resultado.Sandbox.CodigoError ?? "ninguno");
                    }

                    resultado.FechaFinalizacionUtc = DateTimeOffset.UtcNow;
                    await _repositorio.GuardarAsync(trabajo, stoppingToken);
                    _logger.LogInformation("RESULTADO: {AnalisisId}; {Estado}; {Evaluacion}",
                        id, resultado.Estado, resultado.Evaluacion);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // El registro EnProceso se recuperará en el siguiente inicio.
                    throw;
                }
                catch (Exception error)
                {
                    _logger.LogError(error, "ERROR PROCESANDO: {AnalisisId}", id);
                    if (trabajo is not null)
                    {
                        trabajo.Resultado.Estado = EstadoAnalisis.Fallido;
                        trabajo.Resultado.Evaluacion = EvaluacionRiesgo.Incompleto;
                        trabajo.Resultado.CodigoError = "PROCESAMIENTO_ERROR";
                        trabajo.Resultado.Resumen = "No se pudo terminar el procesamiento.";
                        trabajo.Resultado.FechaFinalizacionUtc = DateTimeOffset.UtcNow;
                        try
                        {
                            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await _repositorio.GuardarAsync(trabajo, limite.Token);
                        }
                        catch (Exception errorGuardado)
                        {
                            _logger.LogError(errorGuardado,
                                "No se pudo guardar el fallo de {AnalisisId}.", id);
                        }
                    }
                }
                finally
                {
                    _cola.Liberar(id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Procesador detenido.");
        }
    }

    private static void AgregarLimitacion(ResultadoAnalisis resultado, string detalle)
    {
        if (!resultado.Limitaciones.Contains(detalle))
            resultado.Limitaciones.Add(detalle);
    }
}
