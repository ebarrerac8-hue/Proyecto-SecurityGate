using System.Diagnostics;
using System.Security.Cryptography;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

public static class AbridorArchivoLiberado
{
    // Solo se invoca tras elegir expresamente Devolver y abrir; nunca al recuperar un registro.
    public static void Abrir(RegistroCuarentena registro) => AbrirVerificado(registro, ruta =>
        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(ruta)! }));

    internal static void AbrirVerificado(RegistroCuarentena r, Action<string> abrir)
    {
        if (r.Estado != EstadoArchivo.Liberado || r.OperacionPendiente != "" ||
            r.MetodoAutorizacion != "ConfirmacionExplicita" ||
            !Path.GetExtension(r.RutaOriginal).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Solo se abre un ejecutable devuelto mediante la decisión sin señales de riesgo.");
        AlmacenCuarentena.ComprobarRuta(r.RutaOriginal);
        // Impedir cambios o reemplazo mientras se comprueba y se solicita la apertura a Windows.
        using var archivo = new FileStream(r.RutaOriginal, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (archivo.Length != r.TamanoBytes || !string.Equals(
            Convert.ToHexString(SHA256.HashData(archivo)), r.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El archivo cambió después de devolverlo. No se abrió.");
        abrir(r.RutaOriginal);
    }
}
