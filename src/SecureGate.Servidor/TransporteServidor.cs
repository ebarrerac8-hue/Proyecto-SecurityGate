using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace SecureGate.Servidor;

public static class TransporteServidor
{
    public static IPAddress ValidarDireccion(string texto)
    {
        if (!IPAddress.TryParse(texto, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            throw new InvalidOperationException("Configurá una dirección IPv4 de la red privada.");
        var b = ip.GetAddressBytes();
        if (!(b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)))
            throw new InvalidOperationException("Esta configuración admite únicamente una red privada de laboratorio.");
        return ip;
    }

    public static void ValidarCertificado(X509Certificate2 certificado, string direccion)
    {
        if (!certificado.HasPrivateKey || certificado.NotAfter.ToUniversalTime() <= DateTime.UtcNow ||
            certificado.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
            !certificado.MatchesHostname(direccion, allowWildcards: false, allowCommonName: false) ||
            !certificado.MatchesHostname("localhost", allowWildcards: false, allowCommonName: false))
            throw new InvalidOperationException("El certificado no tiene clave privada, venció o no corresponde a la dirección configurada y localhost.");
    }

    public static void Configurar(KestrelServerOptions kestrel)
    {
        string direccion = (Environment.GetEnvironmentVariable("SECUREGATE_BIND_IP") ?? "").Trim();
        if (direccion.Length == 0)
        {
            kestrel.ListenLocalhost(5443, e => e.UseHttps());
            return;
        }
        IPAddress ip = ValidarDireccion(direccion);
        string huella = (Environment.GetEnvironmentVariable("SECUREGATE_CERT_THUMBPRINT") ?? "").Trim();
        if (huella.Length != 40 || !huella.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Falta la huella del certificado remoto de SecureGate.");
        using var almacen = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        almacen.Open(OpenFlags.ReadOnly);
        var coincidencias = almacen.Certificates.Find(X509FindType.FindByThumbprint, huella, validOnly: false);
        if (coincidencias.Count != 1)
            throw new InvalidOperationException("No se encontró el certificado del servidor en el usuario Windows actual.");
        var certificado = coincidencias[0];
        ValidarCertificado(certificado, direccion);
        kestrel.ListenLocalhost(5443, e => e.UseHttps(certificado));
        kestrel.Listen(ip, 5443, e => e.UseHttps(certificado));
    }
}
