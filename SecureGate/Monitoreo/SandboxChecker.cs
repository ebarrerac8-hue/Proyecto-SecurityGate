using System;
using System.Diagnostics;
using System.IO;

namespace SecureGate.Monitoreo
{
    public enum EstadoEntorno
    {
        Listo,
        SandboxNoInstalado,
        SinDerechosAdministrador,
        SistemaNoCompatible
    }

    public class ResultadoVerificacion
    {
        public EstadoEntorno Estado { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool SandboxDisponible { get; set; }
    }

    public static class SandboxChecker
    {
        /// <summary>
        /// Comprueba si el ejecutable de Windows Sandbox existe en la carpeta System32
        /// </summary>
        public static ResultadoVerificacion VerificarEstadoSandbox()
        {
            string rutaSandbox = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe"
            );

            bool existe = File.Exists(rutaSandbox);

            if (existe)
            {
                return new ResultadoVerificacion
                {
                    Estado = EstadoEntorno.Listo,
                    SandboxDisponible = true,
                    Mensaje = "Windows Sandbox está instalado y habilitado en el sistema."
                };
            }
            else
            {
                return new ResultadoVerificacion
                {
                    Estado = EstadoEntorno.SandboxNoInstalado,
                    SandboxDisponible = false,
                    Mensaje = "Windows Sandbox no está habilitado o no está presente en esta edición de Windows."
                };
            }
        }
    }
}