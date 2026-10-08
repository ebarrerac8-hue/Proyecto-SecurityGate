using System.Runtime.InteropServices;

namespace SecureGate.Sandbox.Services;

public static class VerificadorEntorno
{
    public static bool EsSandboxDisponible()
    {
        string ejecutable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsSandbox.exe");
        return File.Exists(ejecutable);
    }

    public static bool VerificarRecursosSuficientes()
    {
        var memoria = new Memoria { Longitud = (uint)Marshal.SizeOf<Memoria>() };
        return GlobalMemoryStatusEx(ref memoria) &&
            memoria.FisicaDisponible >= 2UL * 1024 * 1024 * 1024;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref Memoria memoria);

    [StructLayout(LayoutKind.Sequential)]
    private struct Memoria
    {
        public uint Longitud;
        public uint Carga;
        public ulong FisicaTotal;
        public ulong FisicaDisponible;
        public ulong PaginaTotal;
        public ulong PaginaDisponible;
        public ulong VirtualTotal;
        public ulong VirtualDisponible;
        public ulong VirtualExtendida;
    }
}
