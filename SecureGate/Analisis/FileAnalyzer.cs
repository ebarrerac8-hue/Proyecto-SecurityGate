using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SecureGate.Analisis
{
    public class InformacionArchivo
    {
        public string Nombre { get; set; } = string.Empty;
        public string RutaCompleta { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long TamañoBytes { get; set; }
        public string TamañoFormateado { get; set; } = string.Empty;
        public string HashSHA256 { get; set; } = string.Empty;

        // Propiedades de Firma Digital (Paso 7)
        public bool TieneFirma { get; set; }
        public string Firmante { get; set; } = string.Empty;
        public string EstadoFirma { get; set; } = string.Empty;
    }

    public static class FileAnalyzer
    {
        public static InformacionArchivo AnalizarArchivo(string rutaArchivo)
        {
            if (!File.Exists(rutaArchivo))
            {
                throw new FileNotFoundException("El archivo especificado no existe.", rutaArchivo);
            }

            FileInfo info = new FileInfo(rutaArchivo);
            ResultadoFirma firma = DigitalSignatureVerifier.VerificarFirma(rutaArchivo);

            InformacionArchivo resultado = new InformacionArchivo
            {
                Nombre = info.Name,
                RutaCompleta = info.FullName,
                Extension = info.Extension.ToLower(),
                TamañoBytes = info.Length,
                TamañoFormateado = FormatearTamaño(info.Length),
                HashSHA256 = CalcularSHA256(rutaArchivo),

                // Asignar datos de la firma
                TieneFirma = firma.TieneFirma,
                Firmante = firma.Firmante,
                EstadoFirma = firma.Mensaje
            };

            return resultado;
        }

        private static string CalcularSHA256(string rutaArchivo)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                using (FileStream stream = File.OpenRead(rutaArchivo))
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    StringBuilder builder = new StringBuilder();
                    foreach (byte b in hashBytes)
                    {
                        builder.Append(b.ToString("x2"));
                    }
                    return builder.ToString();
                }
            }
        }

        private static string FormatearTamaño(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            }
            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F2} KB";
            }
            return $"{bytes} Bytes";
        }
    }
}