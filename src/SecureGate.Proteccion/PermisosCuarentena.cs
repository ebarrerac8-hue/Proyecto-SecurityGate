using System.Security.AccessControl;
using System.Security.Principal;

namespace SecureGate.Proteccion;

internal static class PermisosCuarentena
{
    internal static void RestaurarAcceso(string ruta, string sddl)
    {
        Ntfs(ruta);
        var permisos = new FileSecurity();
        permisos.SetSecurityDescriptorSddlForm(sddl, AccessControlSections.Access);
        // Restaurar explicitamente la politica de herencia. Windows puede normalizar
        // los indicadores y el orden del SDDL sin cambiar los permisos.
        permisos.SetAccessRuleProtection(permisos.AreAccessRulesProtected, preserveInheritance: true);
        try
        {
            FileSystemAclExtensions.SetAccessControl(new FileInfo(ruta), permisos);
            var actuales = FileSystemAclExtensions.GetAccessControl(new FileInfo(ruta));
            if (!AccesoEquivalente(permisos, actuales))
                throw new UnauthorizedAccessException("Los permisos aplicados no equivalen a los originales.");
        }
        catch
        {
            // Si no se confirma la restauracion, conservar la restriccion de ejecucion.
            RestringirArchivo(ruta);
            throw;
        }
    }
    private static bool AccesoEquivalente(FileSecurity esperado, FileSecurity actual)
    {
        if (esperado.AreAccessRulesProtected != actual.AreAccessRulesProtected ||
            !esperado.AreAccessRulesCanonical || !actual.AreAccessRulesCanonical) return false;
        static Dictionary<string, int> Reglas(FileSecurity seguridad)
        {
            var reglas = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (FileSystemAccessRule r in seguridad.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                string clave = r.IdentityReference.Value + "|" + r.AccessControlType + "|" +
                    r.InheritanceFlags + "|" + r.PropagationFlags + "|" + r.IsInherited;
                reglas.TryGetValue(clave, out int derechos);
                reglas[clave] = derechos | (int)r.FileSystemRights;
            }
            return reglas;
        }
        var originales = Reglas(esperado);
        var aplicadas = Reglas(actual);
        return originales.Count == aplicadas.Count &&
            originales.All(r => aplicadas.TryGetValue(r.Key, out int derechos) && derechos == r.Value);
    }
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

