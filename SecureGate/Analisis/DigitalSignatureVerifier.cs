using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace SecureGate.Analisis
{
    public class ResultadoFirma
    {
        public bool TieneFirma { get; set; }
        public string Firmante { get; set; } = string.Empty;
        public string Emisor { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    public static class DigitalSignatureVerifier
    {
        public static ResultadoFirma VerificarFirma(string rutaArchivo)
        {
            ResultadoFirma resultado = new ResultadoFirma();

            if (!File.Exists(rutaArchivo))
            {
                resultado.TieneFirma = false;
                resultado.Mensaje = "El archivo no existe.";
                return resultado;
            }

            try
            {
                X509Certificate cert = X509Certificate.CreateFromSignedFile(rutaArchivo);
                X509Certificate2 cert2 = new X509Certificate2(cert);

                resultado.TieneFirma = true;
                resultado.Firmante = cert2.GetNameInfo(X509NameType.SimpleName, false);
                resultado.Emisor = cert2.GetNameInfo(X509NameType.SimpleName, true);
                resultado.Mensaje = "Firma digital válida detectada.";
            }
            catch (Exception)
            {
                resultado.TieneFirma = false;
                resultado.Firmante = "Desconocido / Sin firma";
                resultado.Emisor = "N/A";
                resultado.Mensaje = "El archivo NO tiene firma digital o no se pudo verificar (Mayor Riesgo).";
            }

            return resultado;
        }
    }
}