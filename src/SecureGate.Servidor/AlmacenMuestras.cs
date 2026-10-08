using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using SecureGate.Contratos;

namespace SecureGate.Servidor;

public sealed class AlmacenMuestras
{
    private readonly OpcionesServidor _opciones;
    private readonly ILogger<AlmacenMuestras> _logger;

    public AlmacenMuestras(
        IOptions<OpcionesServidor> opciones,
        ILogger<AlmacenMuestras> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task<string> GuardarAsync(
        Stream contenido,
        InformacionArchivo archivo,
        CancellationToken cancellationToken = default)
    {
        // Validar los datos antes de guardar.
        if (archivo.ArchivoId == Guid.Empty)
            throw new InvalidDataException(
                "El identificador del archivo está vacío.");

        if (archivo.TamanoBytes <= 0 ||
            archivo.TamanoBytes > _opciones.TamanoMaximoArchivoBytes)
        {
            throw new InvalidDataException(
                "El tamaño del archivo no está permitido.");
        }

        if (string.IsNullOrWhiteSpace(archivo.Sha256) ||
            archivo.Sha256.Length != 64 ||
            !archivo.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException(
                "El SHA-256 debe contener 64 caracteres hexadecimales.");
        }

        // La ubicación la genera el servidor, no el usuario.
        string carpeta = Path.Combine(
            Path.GetFullPath(_opciones.CarpetaDatos),
            "Muestras",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(carpeta);

        string rutaTemporal = Path.Combine(carpeta, "recepcion.tmp");
        string rutaFinal = Path.Combine(carpeta, "muestra.bin");

        try
        {
            using var hash = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

            byte[] buffer = new byte[81920];
            long total = 0;

            await using (var destino = new FileStream(
                rutaTemporal,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                buffer.Length,
                useAsync: true))
            {
                while (true)
                {
                    int leidos = await contenido.ReadAsync(
                        buffer.AsMemory(),
                        cancellationToken);

                    if (leidos == 0)
                        break;

                    total += leidos;

                    // Comprobar el tamaño real mientras llegan los datos.
                    if (total > _opciones.TamanoMaximoArchivoBytes ||
                        total > archivo.TamanoBytes)
                    {
                        throw new InvalidDataException(
                            "El contenido supera el tamaño permitido o declarado.");
                    }

                    hash.AppendData(buffer, 0, leidos);

                    await destino.WriteAsync(
                        buffer.AsMemory(0, leidos),
                        cancellationToken);
                }

                await destino.FlushAsync(cancellationToken);
            }

            if (total != archivo.TamanoBytes)
                throw new InvalidDataException(
                    "El tamaño recibido no coincide con el declarado.");

            string hashCalculado = Convert.ToHexString(
                hash.GetHashAndReset()).ToLowerInvariant();

            if (!string.Equals(
                hashCalculado,
                archivo.Sha256,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "El SHA-256 recibido no coincide con el contenido.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Conservar la copia únicamente después de verificarla.
            File.Move(rutaTemporal, rutaFinal);

            return rutaFinal;
        }
        catch
        {
            // Intentar retirar la copia incompleta o rechazada.
            try
            {
                Directory.Delete(carpeta, recursive: true);
            }
            catch (Exception errorLimpieza)
            {
                _logger.LogWarning(
                    errorLimpieza,
                    "No se pudo limpiar una recepción fallida.");
            }

            throw;
        }
    }
}