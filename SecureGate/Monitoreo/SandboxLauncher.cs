using System;
using System.Diagnostics;
using System.IO;

namespace SecureGate.Monitoreo
{
    public static class SandboxLauncher
    {
        /// <summary>
        /// Genera un archivo .wsb temporal que comparte la carpeta del archivo descargado
        /// y ejecuta Windows Sandbox de forma aislada.
        /// </summary>
        public static bool EjecutarEnSandbox(string rutaArchivo, out string mensajeError)
        {
            mensajeError = string.Empty;

            if (!File.Exists(rutaArchivo))
            {
                mensajeError = "El archivo a probar no existe en el disco.";
                return false;
            }

            try
            {
                string carpetaContenedora = Path.GetDirectoryName(rutaArchivo);
                string archivoWsbTemp = Path.Combine(Path.GetTempPath(), "SecureGate_Test.wsb");

                // Crear la configuración XML (.wsb) para Windows Sandbox
                string contenidoWsb = $@"<Configuration>
  <VGpu>Enable</VGpu>
  <Networking>Disable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>{carpetaContenedora}</HostFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>explorer.exe C:\Users\WDAGUtilityUser\Desktop</Command>
  </LogonCommand>
</Configuration>";

                File.WriteAllText(archivoWsbTemp, contenidoWsb);

                // Lanzar Windows Sandbox con el archivo de configuración
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{archivoWsbTemp}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                mensajeError = $"Error al iniciar Sandbox: {ex.Message}";
                return false;
            }
        }
    }
}