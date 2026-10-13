using System;
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

                var resultado = new ResultadoAnalisisLocal
                {
                    Archivo = archivoInfo,
                    InicioUtc = DateTimeOffset.UtcNow,

                    FirmaDigital = new ResultadoFirmaDigital
                    {
                        Estado = EstadoComprobacion.Completada,
                        Firma = firmaValida ? EstadoFirmaDigital.Valida : EstadoFirmaDigital.Invalida
                    },

                    Antivirus = new ResultadoAntivirus
                    {
                        Estado = EstadoComprobacion.NoRealizada,
                        Deteccion = DeteccionAntivirus.NoDeterminada,
                        Detalle = "Análisis antivirus local no implementado"
                    },

                    FinUtc = DateTimeOffset.UtcNow
                };

                return resultado;

            }, cancellationToken);
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