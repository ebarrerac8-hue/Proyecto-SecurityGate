using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SecureGate.Servidor;

// Credencial compartida del prototipo. No identifica usuarios ni asigna roles.
public sealed class AccesoCliente
{
    private readonly byte[] _huella;

    public AccesoCliente(string? token = null)
    {
        token = (token ?? Environment.GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN") ?? "").Trim();
        if (string.IsNullOrEmpty(token)) token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
        byte[] bytes;
        try { bytes = Convert.FromBase64String(token); }
        catch (FormatException) { throw new InvalidOperationException("Configurá SECUREGATE_CLIENT_TOKEN antes de iniciar el servidor."); }
        if (bytes.Length != 32 || token != Convert.ToBase64String(bytes))
            throw new InvalidOperationException("La clave de acceso debe contener 32 bytes aleatorios en Base64.");
        _huella = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        CryptographicOperations.ZeroMemory(bytes);
    }

    public async Task ProcesarAsync(HttpContext contexto, Func<Task> siguiente)
    {
        contexto.Response.Headers.CacheControl = "no-store";
        if (!contexto.Request.IsHttps)
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await contexto.Response.WriteAsJsonAsync(new { codigo = "HTTPS_REQUERIDO", mensaje = "La API requiere HTTPS." });
            return;
        }
        var encabezado = contexto.Request.Headers.Authorization;
        bool valido = false;
        if (encabezado.Count == 1)
        {
            string valor = encabezado[0] ?? "";
            if (valor.Length == 51 && valor.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                byte[] recibida = SHA256.HashData(Encoding.UTF8.GetBytes(valor[7..]));
                valido = CryptographicOperations.FixedTimeEquals(_huella, recibida);
            }
        }
        if (!valido)
        {
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            contexto.Response.Headers.WWWAuthenticate = "Bearer";
            await contexto.Response.WriteAsJsonAsync(new { codigo = "CLIENTE_NO_AUTORIZADO", mensaje = "Falta una clave de acceso válida para SecureGate." });
            return;
        }
        await siguiente();
    }
}
