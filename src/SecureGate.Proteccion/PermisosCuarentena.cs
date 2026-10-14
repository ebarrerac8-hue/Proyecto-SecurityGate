using System.Security.AccessControl;
using System.Security.Principal;

namespace SecureGate.Proteccion;

internal static class PermisosCuarentena
{
    private static SecurityIdentifier Usuario()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Los permisos requieren Windows.");
        using var identidad = WindowsIdentity.GetCurrent();
        return identidad.User ?? throw new UnauthorizedAccessException("No se obtuvo el usuario actual.");
    }
    private static void Ntfs(string ruta)
    {
        AlmacenCuarentena.ComprobarRuta(ruta);
        string raiz = Path.GetPathRoot(Path.GetFullPath(ruta))
            ?? throw new IOException("No se obtuvo el volumen.");
        if (!new DriveInfo(raiz).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Esta fase requiere un volumen NTFS.");
    }
    internal static void ProtegerDirectorio(string carpeta)
    {
        Ntfs(carpeta);
        var usuario = Usuario();
        var sistema = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var permisos = new DirectorySecurity();
        permisos.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in new[] { usuario, sistema })
            permisos.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        FileSystemAclExtensions.SetAccessControl(new DirectoryInfo(carpeta), permisos);
    }
    internal static void ValidarOrigen(string origen, string cuarentena)
    {
        Ntfs(origen);
        Ntfs(cuarentena);
        if (!string.Equals(Path.GetPathRoot(Path.GetFullPath(origen)),
            Path.GetPathRoot(Path.GetFullPath(cuarentena)), StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Esta fase solo retiene dentro del mismo volumen NTFS.");
        foreach (var especial in new[] { Environment.SpecialFolder.Windows,
            Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            string carpeta = Environment.GetFolderPath(especial);
            if (string.IsNullOrEmpty(carpeta)) continue;
            string relativa = Path.GetRelativePath(carpeta, origen);
            if (!Path.IsPathRooted(relativa) && relativa != ".." &&
                !relativa.StartsWith(".." + Path.DirectorySeparatorChar))
                throw new InvalidOperationException("No se retienen archivos de Windows o Program Files.");
        }
    }
    internal static string CapturarOriginal(string ruta)
    {
        Ntfs(ruta);
        return FileSystemAclExtensions.GetAccessControl(new FileInfo(ruta),
            AccessControlSections.Access | AccessControlSections.Owner | AccessControlSections.Group)
            .GetSecurityDescriptorSddlForm(AccessControlSections.Access |
                AccessControlSections.Owner | AccessControlSections.Group);
    }
    internal static void RestringirArchivo(string ruta)
    {
        Ntfs(ruta);
        var usuario = Usuario();
        var sistema = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var todos = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var permisos = new FileSecurity();
        permisos.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        permisos.AddAccessRule(new FileSystemAccessRule(todos, FileSystemRights.ExecuteFile, AccessControlType.Deny));
        foreach (var sid in new[] { usuario, sistema })
            permisos.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        FileSystemAclExtensions.SetAccessControl(new FileInfo(ruta), permisos);
        var efectivos = FileSystemAclExtensions.GetAccessControl(new FileInfo(ruta));
        bool denegacion = efectivos.GetAccessRules(true, false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().Any(r => r.IdentityReference.Equals(todos) &&
                r.AccessControlType == AccessControlType.Deny &&
                (r.FileSystemRights & FileSystemRights.ExecuteFile) != 0);
        if (!efectivos.AreAccessRulesProtected || !denegacion)
            throw new UnauthorizedAccessException("No se confirmo la restriccion de ejecucion.");
    }
}
