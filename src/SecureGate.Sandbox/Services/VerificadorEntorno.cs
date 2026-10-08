using System.Diagnostics;

namespace SecureGate.Sandbox.Services;

public static class VerificadorEntorno
{
    public static bool EsSandboxDisponible()
    {
      
        string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        string sandboxExe = Path.Combine(systemPath, "WindowsSandbox.exe");

        return File.Exists(sandboxExe);
    }

    public static bool VerificarRecursosSuficientes()
    {
       
        var gcInfo = GC.GetGCMemoryInfo();
        long memoriaDisponibleMb = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);

        return memoriaDisponibleMb >= 1024;
    }
}
