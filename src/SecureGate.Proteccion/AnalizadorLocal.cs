using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SecureGate.Contratos;

namespace SecureGate.Proteccion
{
    public class AnalizadorLocal : IAnalizadorLocal
    {
        public Task<ResultadoAnalisisLocal> AnalizarAsync(Guid archivoId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string directorioCuarentena = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SecureGate",
                    "Cuarentena"
                );
                string rutaArchivo = Path.Combine(directorioCuarentena, $"{archivoId}.bytes");

                if (!File.Exists(rutaArchivo))
                {
                    throw new FileNotFoundException($"No se encontró el archivo con ID {archivoId}");
                }

                var fileInfo = new FileInfo(rutaArchivo);
                var hashSha256 = CalcularSha256(rutaArchivo);

                var archivoInfo = new InformacionArchivo
                {
                    ArchivoId = archivoId,
                    NombreOriginal = fileInfo.Name,
                    Extension = fileInfo.Extension.ToLower(),
                    TamanoBytes = fileInfo.Length,
                    Sha256 = hashSha256,
                    FechaRegistroUtc = DateTimeOffset.UtcNow
                };

                bool firmaValida = VerificarFirmaDigital(rutaArchivo);
                var resultadoAntivirus = EjecutarMicrosoftDefender(rutaArchivo);

                var resultado = new ResultadoAnalisisLocal
                {
                    Archivo = archivoInfo,
                    InicioUtc = DateTimeOffset.UtcNow,

                    FirmaDigital = new ResultadoFirmaDigital
                    {
                        Estado = EstadoComprobacion.Completada,
                        Firma = firmaValida ? EstadoFirmaDigital.Valida : EstadoFirmaDigital.Invalida
                    },

                    Antivirus = resultadoAntivirus,

                    FinUtc = DateTimeOffset.UtcNow
                };

                return resultado;

            }, cancellationToken);
        }

        private ResultadoAntivirus EjecutarMicrosoftDefender(string rutaArchivo)
        {
            var resultado = new ResultadoAntivirus
            {
                Motor = "Microsoft Defender",
                Estado = EstadoComprobacion.NoRealizada,
                Deteccion = DeteccionAntivirus.NoDeterminada
            };

            try
            {
                // Ruta estándar de la utilidad de línea de comandos de Defender
                string defenderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                                   "Windows Defender", "MpCmdRun.exe");

                if (!File.Exists(defenderPath))
                {
                    resultado.Estado = EstadoComprobacion.NoDisponible;
                    resultado.Detalle = "Microsoft Defender no está instalado o accesible en esta ruta.";
                    return resultado;
                }

                // Argumentos: -Scan -ScanType 3 (Custom) -File <ruta> -DisableRemediation (para que no borre nuestro archivo)
                var processInfo = new ProcessStartInfo
                {
                    FileName = defenderPath,
                    Arguments = $"-Scan -ScanType 3 -File \"{rutaArchivo}\" -DisableRemediation",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                    {
                        resultado.Estado = EstadoComprobacion.Fallida;
                        resultado.Detalle = "No se pudo iniciar el proceso del antivirus.";
                        return resultado;
                    }

                    process.WaitForExit(60000); // Esperar máximo 60 segundos

                    // MpCmdRun.exe devuelve 0 si está limpio, 2 si hay amenaza.
                    if (process.ExitCode == 0)
                    {
                        resultado.Estado = EstadoComprobacion.Completada;
                        resultado.Deteccion = DeteccionAntivirus.SinDetecciones;
                        resultado.Detalle = "Análisis completado. Archivo limpio.";
                    }
                    else if (process.ExitCode == 2)
                    {
                        resultado.Estado = EstadoComprobacion.Completada;
                        resultado.Deteccion = DeteccionAntivirus.AmenazaDetectada;
                        resultado.Detalle = "Microsoft Defender detectó una amenaza.";
                        resultado.Amenazas.Add("Amenaza genérica detectada por Defender");
                    }
                    else
                    {
                        resultado.Estado = EstadoComprobacion.Fallida;
                        resultado.Detalle = $"Error durante el análisis. Código de salida: {process.ExitCode}";
                        resultado.CodigoError = process.ExitCode.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                resultado.Estado = EstadoComprobacion.Fallida;
                resultado.Detalle = $"Excepción al ejecutar antivirus: {ex.Message}";
            }

            return resultado;
        }

        private string CalcularSha256(string ruta)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(ruta))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private bool VerificarFirmaDigital(string ruta)
        {
            try
            {
                var cert = X509Certificate.CreateFromSignedFile(ruta);
                var cert2 = new X509Certificate2(cert);
                return cert2.Verify();
            }
            catch
            {
                return false;
            }
        }
    }
}