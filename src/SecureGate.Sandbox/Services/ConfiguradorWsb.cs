using System.Xml.Linq;

namespace SecureGate.Sandbox.Services;

public static class ConfiguradorWsb
{
    public static string GenerarConfiguracion(string entrada, string salida)
    {
        var xml = new XElement("Configuration",
            new XElement("VGpu", "Disable"),
            new XElement("Networking", "Disable"),
            new XElement("ClipboardRedirection", "Disable"),
            new XElement("AudioInput", "Disable"),
            new XElement("VideoInput", "Disable"),
            new XElement("PrinterRedirection", "Disable"),
            new XElement("MappedFolders",
                Carpeta(entrada, @"C:\Entrada", true),
                Carpeta(salida, @"C:\Salida", false)),
            new XElement("LogonCommand",
                new XElement("Command",
                    @"powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\Entrada\Observador.ps1")));

        return xml.ToString();
    }

    private static XElement Carpeta(string host, string guest, bool soloLectura)
    {
        return new XElement("MappedFolder",
            new XElement("HostFolder", Path.GetFullPath(host)),
            new XElement("SandboxFolder", guest),
            new XElement("ReadOnly", soloLectura ? "true" : "false"));
    }
}
