using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SecureGate.Contratos;

namespace SecureGate.Servidor;

public sealed class ClienteReputacion
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuracion;
    private readonly SemaphoreSlim _control = new(1, 1);

    private DateTimeOffset _proximaConsultaUtc =
        DateTimeOffset.MinValue;

    public ClienteReputacion(
        HttpClient http,
        IConfiguration configuracion)
    {
        _http = http;
        _configuracion = configuracion;
    }

    public async Task<ResultadoReputacion> ConsultarAsync(
        string sha256,
        CancellationToken cancellationToken = default)
    {
        var resultado = new ResultadoReputacion
        {
            Sha256 = sha256 ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(sha256) ||
            sha256.Length != 64 ||
            !sha256.All(Uri.IsHexDigit))
        {
            return ConError(
                resultado,
                EstadoComprobacion.Fallida,
                "VT_HASH_INVALIDO",
                "El SHA-256 no tiene el formato esperado.");
        }

        string? clave = (Environment.GetEnvironmentVariable("SECUREGATE_VIRUSTOTAL_API_KEY") ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(clave))
        {
            return ConError(
                resultado,
                EstadoComprobacion.NoDisponible,
                "VT_SIN_CLAVE",
                "La clave de VirusTotal no está configurada.");
        }

        await _control.WaitAsync(cancellationToken);

        try
        {
            TimeSpan espera =
                _proximaConsultaUtc - DateTimeOffset.UtcNow;

            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            resultado.FechaConsultaUtc = DateTimeOffset.UtcNow;

            // Separar los intentos de consulta de esta instancia.
            _proximaConsultaUtc =
                DateTimeOffset.UtcNow.AddSeconds(16);

            string direccion =
                "https://www.virustotal.com/api/v3/files/" +
                sha256.ToLowerInvariant();

            using var peticion =
                new HttpRequestMessage(HttpMethod.Get, direccion);

            peticion.Headers.Add("x-apikey", clave);

            using var respuesta = await _http.SendAsync(
                peticion,
                cancellationToken);

            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                resultado.Estado = EstadoComprobacion.Completada;
                resultado.InformeEncontrado = false;
                resultado.Detalle =
                    "VirusTotal no tiene un informe para este hash.";

                return resultado;
            }

            if ((int)respuesta.StatusCode == 429)
            {
                return ConError(
                    resultado,
                    EstadoComprobacion.NoDisponible,
                    "VT_LIMITE_CONSULTAS",
                    "VirusTotal rechazó la consulta por su límite de uso.");
            }

            if (respuesta.StatusCode is
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
            {
                return ConError(
                    resultado,
                    EstadoComprobacion.NoDisponible,
                    "VT_ACCESO_RECHAZADO",
                    "VirusTotal rechazó las credenciales o el acceso.");
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                return ConError(
                    resultado,
                    EstadoComprobacion.Fallida,
                    "VT_ERROR_HTTP",
                    "VirusTotal respondió con HTTP " +
                    (int)respuesta.StatusCode + ".");
            }

            string contenido =
                await respuesta.Content.ReadAsStringAsync(
                    cancellationToken);

            using var documento = JsonDocument.Parse(contenido);

            var atributos = documento.RootElement
                .GetProperty("data")
                .GetProperty("attributes");

            var estadisticas =
                atributos.GetProperty("last_analysis_stats");

            int? maliciosos = LeerContador(
                estadisticas, "malicious");

            int? sospechosos = LeerContador(
                estadisticas, "suspicious");

            int? sinDeteccion = LeerContador(
                estadisticas, "undetected");

            int? inofensivos = LeerContador(
                estadisticas, "harmless");

            if (maliciosos is null ||
                sospechosos is null ||
                sinDeteccion is null ||
                inofensivos is null ||
                (long)sinDeteccion.Value + inofensivos.Value >
                    int.MaxValue)
            {
                return ConError(
                    resultado,
                    EstadoComprobacion.Fallida,
                    "VT_ESTADISTICAS_INVALIDAS",
                    "El informe no contiene estadísticas válidas.");
            }

            resultado.Estado = EstadoComprobacion.Completada;
            resultado.InformeEncontrado = true;
            resultado.MotoresMaliciosos = maliciosos.Value;
            resultado.MotoresSospechosos = sospechosos.Value;
            resultado.MotoresSinDeteccion =
                sinDeteccion.Value + inofensivos.Value;

            if (atributos.TryGetProperty(
                    "last_analysis_date", out var fecha) &&
                fecha.ValueKind == JsonValueKind.Number &&
                fecha.TryGetInt64(out long segundos) &&
                segundos >= 0 &&
                segundos <= 253402300799L)
            {
                resultado.FechaInformeUtc =
                    DateTimeOffset.FromUnixTimeSeconds(segundos);
            }

            resultado.Detalle =
                $"Informe encontrado: {maliciosos.Value} motores " +
                $"maliciosos, {sospechosos.Value} sospechosos y " +
                $"{resultado.MotoresSinDeteccion} sin detección.";

            return resultado;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ConError(
                resultado,
                EstadoComprobacion.Fallida,
                "VT_TIEMPO_AGOTADO",
                "La consulta de VirusTotal excedió el tiempo de espera.");
        }
        catch (HttpRequestException)
        {
            return ConError(
                resultado,
                EstadoComprobacion.Fallida,
                "VT_ERROR_CONEXION",
                "No se pudo establecer la conexión con VirusTotal.");
        }
        catch (JsonException)
        {
            return RespuestaInvalida(resultado);
        }
        catch (KeyNotFoundException)
        {
            return RespuestaInvalida(resultado);
        }
        catch (InvalidOperationException)
        {
            return RespuestaInvalida(resultado);
        }
        finally
        {
            _control.Release();
        }
    }

    private static int? LeerContador(
        JsonElement estadisticas,
        string nombre)
    {
        if (estadisticas.TryGetProperty(nombre, out var valor) &&
            valor.ValueKind == JsonValueKind.Number &&
            valor.TryGetInt32(out int cantidad) &&
            cantidad >= 0)
        {
            return cantidad;
        }

        return null;
    }

    private static ResultadoReputacion RespuestaInvalida(
        ResultadoReputacion resultado)
    {
        return ConError(
            resultado,
            EstadoComprobacion.Fallida,
            "VT_RESPUESTA_INVALIDA",
            "VirusTotal devolvió un informe con formato inesperado.");
    }

    private static ResultadoReputacion ConError(
        ResultadoReputacion resultado,
        EstadoComprobacion estado,
        string codigo,
        string detalle)
    {
        resultado.Estado = estado;
        resultado.CodigoError = codigo;
        resultado.Detalle = detalle;

        return resultado;
    }
}