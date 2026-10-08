using System.Text;

public static class ConfiguradorWsb
{
    public static string GenerarConfiguracion(string carpetaMuestraHost, bool permitirRed)
    {
        string networking = permitirRed ? "Default" : "Disable";

        var wsbContent = new StringBuilder();
        wsbContent.AppendLine("<Configuration>");
        wsbContent.AppendLine($"  <Networking>{networking}</Networking>");
        wsbContent.AppendLine("  <MappedFolders>");
        wsbContent.AppendLine("    <MappedFolder>");
        wsbContent.AppendLine($"      <HostFolder>{carpetaMuestraHost}</HostFolder>");
        wsbContent.AppendLine("      <SandboxFolder>C:\\Muestra</SandboxFolder>");
        wsbContent.AppendLine("      <ReadOnly>false</ReadOnly>");
        wsbContent.AppendLine("    </MappedFolder>");
        wsbContent.AppendLine("  </MappedFolders>");
        wsbContent.AppendLine("  <LogonCommand>");
        wsbContent.AppendLine("    <Command>powershell.exe -ExecutionPolicy Bypass -File C:\\Muestra\\Observador.ps1</Command>");
        wsbContent.AppendLine("  </LogonCommand>");
        wsbContent.AppendLine("</Configuration>");

        return wsbContent.ToString();
    }
}