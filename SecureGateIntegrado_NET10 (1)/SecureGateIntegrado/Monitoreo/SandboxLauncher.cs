using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace SecureGate.Monitoreo
{
    public static class SandboxLauncher
    {
        public static bool EjecutarEnSandbox(
            string rutaArchivo,
            out string mensajeError,
            string? hashEsperado = null)
        {
            mensajeError = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(rutaArchivo))
                {
                    mensajeError = "No se proporcionó un archivo.";
                    return false;
                }

                string rutaOriginal = Path.GetFullPath(rutaArchivo);

                if (!File.Exists(rutaOriginal))
                {
                    mensajeError = "El archivo a probar no existe.";
                    return false;
                }

                string carpetaPrueba = Path.Combine(
                    Path.GetTempPath(),
                    "SecureGate",
                    "Sandbox",
                    Guid.NewGuid().ToString("N")
                );

                string carpetaMuestra = Path.Combine(
                    carpetaPrueba,
                    "Muestra"
                );

                Directory.CreateDirectory(carpetaMuestra);

               
                string copiaArchivo = Path.Combine(
                    carpetaMuestra,
                    Path.GetFileName(rutaOriginal)
                );

                File.Copy(rutaOriginal, copiaArchivo);
                if (hashEsperado != null)
                {
                    using var lectura = File.OpenRead(copiaArchivo);
                    string hashActual = Convert.ToHexString(SHA256.HashData(lectura));
                    if (!string.Equals(hashActual, hashEsperado, StringComparison.OrdinalIgnoreCase))
                    {
                        lectura.Dispose();
                        Directory.Delete(carpetaPrueba, true);
                        mensajeError = "El archivo cambió desde el análisis. Volvé a analizarlo antes de probarlo.";
                        return false;
                    }
                }

                string rutaWsb = Path.Combine(
                    carpetaPrueba,
                    "Prueba.wsb"
                );

                
                XDocument configuracion = new XDocument(
                    new XElement("Configuration",
                        new XElement("vGPU", "Disable"),
                        new XElement("Networking", "Disable"),
                        new XElement("ClipboardRedirection", "Disable"),
                        new XElement("AudioInput", "Disable"),
                        new XElement("VideoInput", "Disable"),
                        new XElement("PrinterRedirection", "Disable"),

                        new XElement("MappedFolders",
                            new XElement("MappedFolder",
                                new XElement(
                                    "HostFolder",
                                    carpetaMuestra
                                ),
                                new XElement(
                                    "SandboxFolder",
                                    @"C:\SecureGate\Muestra"
                                ),
                                new XElement("ReadOnly", "true")
                            )
                        ),

                        new XElement("LogonCommand",
                            new XElement(
                                "Command",
                                @"explorer.exe C:\SecureGate\Muestra"
                            )
                        )
                    )
                );

                configuracion.Save(rutaWsb);

                GestorSandbox gestor = new GestorSandbox();

                ResultadoSandbox resultado = gestor.Iniciar(
                    rutaWsb,
                    autorizadoPorUsuario: true
                );

                if (resultado.Estado != EstadoSandbox.InicioSolicitado)
                {
                    mensajeError = resultado.Mensaje;
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                mensajeError =
                    "No se pudo preparar la prueba: " + error.Message;

                return false;
            }
        }
    }
}