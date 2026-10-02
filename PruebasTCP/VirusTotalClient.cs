using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

namespace Seguregate
{
    public class VirusTotalClient
    {
        private static readonly HttpClient cliente = new HttpClient(
            new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private static readonly SemaphoreSlim turno = new SemaphoreSlim(1, 1);
        private static readonly Stopwatch reloj = Stopwatch.StartNew();
        private static TimeSpan proximaConsulta = TimeSpan.Zero;

        public async Task<ResultadoReputacion> ConsultarHashAsync(
            string sha256, string claveApi
            
            )


        {


            sha256 = sha256.Trim().ToLowerInvariant();

            if (!Regex.IsMatch(sha256, @"\A[0-9a-f]{64}\z"))
                throw new ArgumentException("El SHA-256 no es válido.");

            if (string.IsNullOrWhiteSpace(claveApi))
                throw new ArgumentException("Falta la clave de API.");


            var resultado = new ResultadoReputacion
            {
                Sha256 = sha256,
                Estado = EstadoConsulta.NoDisponible
            };
            await turno.WaitAsync();
            try
            {
                TimeSpan espera = proximaConsulta - reloj.Elapsed;

                if (espera > TimeSpan.Zero)
                {
                    await Task.Delay(espera);
                }
                string direccion =
                    $"https://www.virustotal.com/api/v3/files/{sha256}";

                using var solicitud =
                    new HttpRequestMessage(HttpMethod.Get, direccion);

                solicitud.Version = HttpVersion.Version11;
                solicitud.VersionPolicy =
                    HttpVersionPolicy.RequestVersionExact;

                solicitud.Headers.Add("x-apikey", claveApi.Trim());
                proximaConsulta = reloj.Elapsed + TimeSpan.FromSeconds(16);
                using var respuesta = await cliente.SendAsync(solicitud);

                if (respuesta.StatusCode == HttpStatusCode.NotFound)
                {
                    resultado.Estado = EstadoConsulta.Desconocido;
                    resultado.Mensaje = "VirusTotal no conoce esta huella.";
                    return resultado;
                }

                if (!respuesta.IsSuccessStatusCode)
                {
                    resultado.Mensaje = respuesta.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized =>
                            "VirusTotal rechazó la clave de API.",
                        HttpStatusCode.Forbidden =>
                            "La cuenta no tiene acceso a esta consulta.",
                        HttpStatusCode.TooManyRequests =>
                            "Se alcanzó el límite de consultas.",
                        _ =>
                            $"VirusTotal respondió con error HTTP {(int)respuesta.StatusCode}."
                    };

                    return resultado;
                }

                string informe = await respuesta.Content.ReadAsStringAsync();
                using var documento = JsonDocument.Parse(informe);

                var estadisticas = documento.RootElement
                    .GetProperty("data")
                    .GetProperty("attributes")
                    .GetProperty("last_analysis_stats");


                int maliciosos =
                    estadisticas.GetProperty("malicious").GetInt32();
                int sospechosos =
                    estadisticas.GetProperty("suspicious").GetInt32();
                int sinDeteccion =
                    estadisticas.GetProperty("undetected").GetInt32();


                resultado.Maliciosos = maliciosos;
                resultado.Sospechosos = sospechosos;
                resultado.SinDeteccion = sinDeteccion;
                resultado.Estado = EstadoConsulta.InformeDisponible;
                resultado.Mensaje = "Informe de VirusTotal disponible.";
            }
            catch (HttpRequestException)
            {
                resultado.Mensaje =
                    "No se pudo establecer la comunicación con VirusTotal.";
            }
            catch (TaskCanceledException)
            {
                resultado.Mensaje = "La consulta superó el tiempo de espera.";
            }
            catch (Exception error) when (
                error is JsonException ||
                error is KeyNotFoundException ||
                error is InvalidOperationException ||
                error is FormatException)
            {
                resultado.Mensaje =
                    "La respuesta no contiene un informe válido para procesar.";
            }

            finally
            {
                turno.Release();
            }
            return resultado;
        }
    }
}