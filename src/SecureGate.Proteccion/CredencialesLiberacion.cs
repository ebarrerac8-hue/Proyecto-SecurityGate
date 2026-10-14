using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace SecureGate.Proteccion;

internal static class CredencialesLiberacion
{
    internal static string? VerificarUsuarioActual(IntPtr ventana)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Autorizacion requiere Windows.");
        var info = new CredUiInfo
        {
            Tamano = Marshal.SizeOf<CredUiInfo>(), Ventana = ventana,
            Titulo = "SecureGate - autorizar liberacion",
            Mensaje = "Confirma las credenciales de tu cuenta de Windows para liberar el archivo bajo tu responsabilidad."
        };
        uint paquete = 0;
        bool guardar = false;
        IntPtr buffer = IntPtr.Zero, usuario = IntPtr.Zero, dominio = IntPtr.Zero, clave = IntPtr.Zero;
        uint longitud = 0, nu = 0, nd = 0, nc = 0;
        int bu = 0, bd = 0, bc = 0;
        try
        {
            uint codigo = CredUIPromptForWindowsCredentialsW(ref info, 0, ref paquete,
                IntPtr.Zero, 0, out buffer, out longitud, ref guardar, 1); // GENERIC, sin guardar.
            if (codigo == 1223) return null;
            if (codigo != 0) throw new Win32Exception((int)codigo);
            if (longitud > 1024 * 1024) throw new InvalidDataException("Respuesta de credenciales demasiado grande.");
            CredUnPackAuthenticationBufferW(0, buffer, longitud,
                IntPtr.Zero, ref nu, IntPtr.Zero, ref nd, IntPtr.Zero, ref nc);
            if (Marshal.GetLastWin32Error() != 122 || nu is 0 or > 32768 || nd > 32768 || nc is 0 or > 32768)
                throw new UnauthorizedAccessException("Formato de credenciales no compatible. Usa la contrasena de Windows, no el PIN.");
            bu = checked((int)nu * 2); bd = checked((int)Math.Max(nd, 1) * 2); bc = checked((int)nc * 2);
            usuario = Marshal.AllocHGlobal(bu); dominio = Marshal.AllocHGlobal(bd); clave = Marshal.AllocHGlobal(bc);
            if (!CredUnPackAuthenticationBufferW(0, buffer, longitud,
                usuario, ref nu, dominio, ref nd, clave, ref nc))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            string nombre = Marshal.PtrToStringUni(usuario) ?? "";
            string? grupo = Marshal.PtrToStringUni(dominio);
            if (nombre.Contains('\\'))
            {
                var partes = nombre.Split('\\', 2);
                grupo = partes[0]; nombre = partes[1];
            }
            else if (nombre.Contains('@')) grupo = null;
            else if (string.IsNullOrEmpty(grupo)) grupo = ".";
            // La contrasena se mantiene en memoria no administrada, nunca en strings ni registros.
            if (!LogonUserW(nombre, grupo, clave, 2, 0, out var token))
            {
                int errorWindows = Marshal.GetLastWin32Error();
                token?.Dispose();
                string motivo = errorWindows switch
                {
                    1326 => "Windows rechazo el usuario o la contrasena; el PIN no sirve para este metodo.",
                    1385 => "La cuenta no tiene permitido el tipo de inicio de sesion requerido.",
                    1331 => "La cuenta esta deshabilitada.",
                    1909 => "La cuenta esta bloqueada. No repitas intentos de contrasena.",
                    1327 => "Windows aplica una restriccion a esta cuenta.",
                    _ => new Win32Exception(errorWindows).Message
                };
                throw new UnauthorizedAccessException(
                    $"Windows no autorizo la liberacion. Codigo Win32: {errorWindows}. {motivo} No se libero el archivo.");
            }
            using (token)
            using (var verificada = new WindowsIdentity(token.DangerousGetHandle()))
            using (var actual = WindowsIdentity.GetCurrent())
            {
                if (verificada.User is null || actual.User is null || !verificada.User.Equals(actual.User))
                    throw new UnauthorizedAccessException("Debe autorizar la misma cuenta que administra esta cuarentena.");
                return verificada.User.Value;
            }
        }
        finally
        {
            Borrar(usuario, bu, false); Borrar(dominio, bd, false); Borrar(clave, bc, false);
            Borrar(buffer, checked((int)longitud), true);
        }
    }
    private static void Borrar(IntPtr p, int bytes, bool com)
    {
        if (p == IntPtr.Zero) return;
        for (int i = 0; i < bytes; i++) Marshal.WriteByte(p, i, 0);
        if (com) Marshal.FreeCoTaskMem(p); else Marshal.FreeHGlobal(p);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CredUiInfo
    {
        public int Tamano;
        public IntPtr Ventana;
        public string Mensaje;
        public string Titulo;
        public IntPtr Imagen;
    }
    [DllImport("credui.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint CredUIPromptForWindowsCredentialsW(ref CredUiInfo info, uint error,
        ref uint paquete, IntPtr entrada, uint tamanoEntrada, out IntPtr salida,
        out uint tamanoSalida, [MarshalAs(UnmanagedType.Bool)] ref bool guardar, uint flags);
    [DllImport("credui.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredUnPackAuthenticationBufferW(uint flags, IntPtr buffer, uint tamano,
        IntPtr usuario, ref uint nu, IntPtr dominio, ref uint nd, IntPtr clave, ref uint nc);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUserW(string usuario, string? dominio, IntPtr clave,
        int tipo, int proveedor, out SafeAccessTokenHandle token);
}
