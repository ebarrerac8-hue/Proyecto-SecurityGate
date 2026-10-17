using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Win32;

// Muestra benigna de laboratorio. Nunca borra copias, registros de eventos ni datos del usuario.
internal static class MuestraControlada
{
    [STAThread]
    private static int Main()
    {
        string propia = Path.GetFullPath(Process.GetCurrentProcess().MainModule.FileName);
        bool invitado = Environment.UserName.Equals("WDAGUtilityAccount", StringComparison.OrdinalIgnoreCase) &&
            propia.Equals(@"C:\Prueba\muestra.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(@"C:\Entrada\solicitud.json");
#if SECUREGATE_QA
        // Solo la compilación de CI permite un anfitrión de prueba; no se distribuye ese binario.
        invitado = propia.Equals(@"C:\Prueba\muestra.exe", StringComparison.OrdinalIgnoreCase) &&
            Environment.GetEnvironmentVariable("SECUREGATE_OBSERVADOR_QA") == "1";
#endif
        if (!invitado) return 10; // Abrir por accidente en el equipo normal no realiza acciones.
        string marcador = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SecureGate_PruebaComportamiento.txt");
        const string rutaRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        string nombre = "SecureGate_PruebaControlada_" + Guid.NewGuid().ToString("N");
        Process hijo = null;
        try
        {
            Thread.Sleep(1500); // Permitir que el observador documente la raíz antes del hijo.
            using (RegistryKey clave = Registry.CurrentUser.CreateSubKey(rutaRun))
                clave.SetValue(nombre, "\"" + propia + "\" --prueba-controlada", RegistryValueKind.String);
            string orden = "Start-Sleep -Seconds 3; [IO.File]::WriteAllText('" + marcador.Replace("'", "''") +
                "', 'PRUEBA CONTROLADA SECUREGATE - SIN MALWARE'); Start-Sleep -Seconds 7";
            string codificada = Convert.ToBase64String(Encoding.Unicode.GetBytes(orden));
            hijo = Process.Start(new ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -EncodedCommand " + codificada)
                { UseShellExecute = false, CreateNoWindow = true });
            if (hijo != null) hijo.WaitForExit(20000);
            Thread.Sleep(1500);
            return 0;
        }
        finally
        {
            if (hijo != null) { if (!hijo.HasExited) hijo.Kill(); hijo.Dispose(); }
            using (RegistryKey clave = Registry.CurrentUser.OpenSubKey(rutaRun, true))
                if (clave != null) clave.DeleteValue(nombre, false);
            if (File.Exists(marcador)) File.Delete(marcador);
        }
    }
}
