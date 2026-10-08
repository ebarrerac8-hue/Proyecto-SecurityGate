using System.Text.Json;
using Microsoft.Extensions.Options;
using SecureGate.Contratos;

namespace SecureGate.Servidor;

public sealed class RepositorioAnalisis
{
    private readonly string _carpeta;
    private readonly ILogger<RepositorioAnalisis> _logger;
    private readonly SemaphoreSlim _control = new(1, 1);

    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true
    };

    public RepositorioAnalisis(
        IOptions<OpcionesServidor> opciones,
        ILogger<RepositorioAnalisis> logger)
    {
        _carpeta = Path.Combine(
            Path.GetFullPath(opciones.Value.CarpetaDatos),
            "Trabajos");

        _logger = logger;
    }

    public async Task GuardarAsync(
        TrabajoAnalisis trabajo,
        CancellationToken cancellationToken = default)
    {
        Guid id = trabajo.Resultado.AnalisisId;

        if (id == Guid.Empty || trabajo.SolicitudId == Guid.Empty)
        {
            throw new InvalidDataException(
                "El análisis y la solicitud necesitan identificadores.");
        }

        await _control.WaitAsync(cancellationToken);

        string? temporal = null;

        try
        {
            Directory.CreateDirectory(_carpeta);

            string destino = Path.Combine(
                _carpeta,
                $"{id:N}.json");

            temporal = Path.Combine(
                _carpeta,
                $"{Guid.NewGuid():N}.tmp");

            await using (var archivo = new FileStream(
                temporal,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    archivo,
                    trabajo,
                    _json,
                    cancellationToken);

                await archivo.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(temporal, destino, overwrite: true);
        }
        finally
        {
            try
            {
                if (temporal is not null && File.Exists(temporal))
                {
                    File.Delete(temporal);
                }
            }
            catch (Exception errorLimpieza)
            {
                _logger.LogWarning(
                    errorLimpieza,
                    "No se pudo eliminar un registro temporal.");
            }

            _control.Release();
        }
    }

    public async Task<TrabajoAnalisis?> ObtenerAsync(
        Guid analisisId,
        CancellationToken cancellationToken = default)
    {
        if (analisisId == Guid.Empty)
        {
            return null;
        }

        await _control.WaitAsync(cancellationToken);

        try
        {
            string ruta = Path.Combine(
                _carpeta,
                $"{analisisId:N}.json");

            if (!File.Exists(ruta))
            {
                return null;
            }

            await using var archivo = new FileStream(
                ruta,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<TrabajoAnalisis>(
                archivo,
                _json,
                cancellationToken)
                ?? throw new InvalidDataException(
                    "El registro del análisis está vacío.");
        }
        finally
        {
            _control.Release();
        }
    }

    public async Task<IReadOnlyList<TrabajoAnalisis>> ListarPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        await _control.WaitAsync(cancellationToken);

        try
        {
            var pendientes = new List<TrabajoAnalisis>();

            if (!Directory.Exists(_carpeta))
            {
                return pendientes;
            }

            foreach (string ruta in Directory.EnumerateFiles(
                _carpeta,
                "*.json",
                SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var archivo = new FileStream(
                    ruta,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    useAsync: true);

                var trabajo =
                    await JsonSerializer.DeserializeAsync<TrabajoAnalisis>(
                        archivo,
                        _json,
                        cancellationToken)
                    ?? throw new InvalidDataException(
                        "Se encontró un registro de análisis vacío.");

                if (trabajo.Resultado.Estado is
                    EstadoAnalisis.Pendiente or
                    EstadoAnalisis.EnCola or
                    EstadoAnalisis.EnProceso)
                {
                    pendientes.Add(trabajo);
                }
            }

            return pendientes
                .OrderBy(trabajo => trabajo.Resultado.FechaCreacionUtc)
                .ThenBy(trabajo => trabajo.Resultado.AnalisisId)
                .ToList();
        }
        finally
        {
            _control.Release();
        }
    }

    public async Task<TrabajoAnalisis?> BuscarPorSolicitudAsync(
        Guid solicitudId,
        CancellationToken cancellationToken = default)
    {
        if (solicitudId == Guid.Empty)
        {
            return null;
        }

        await _control.WaitAsync(cancellationToken);

        try
        {
            if (!Directory.Exists(_carpeta))
            {
                return null;
            }

            foreach (string ruta in Directory.EnumerateFiles(
                _carpeta,
                "*.json",
                SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var archivo = new FileStream(
                    ruta,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    useAsync: true);

                var trabajo =
                    await JsonSerializer.DeserializeAsync<TrabajoAnalisis>(
                        archivo,
                        _json,
                        cancellationToken)
                    ?? throw new InvalidDataException(
                        "Se encontró un registro de análisis vacío.");

                if (trabajo.SolicitudId == solicitudId)
                {
                    return trabajo;
                }
            }

            return null;
        }
        finally
        {
            _control.Release();
        }
    }
}