using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SecureGate;

// Se genera en la computadora servidor, nunca se incluye en Git ni contiene claves de proveedores.
internal sealed class PaqueteCliente
{
    public string Servidor { get; init; } = "";
    public string ClaveCliente { get; init; } = "";
    internal static PaqueteCliente? Cargar(string? ruta = null)
    {
        if (ruta is null && EntornoCliente.EsPrueba) return null;
        ruta ??= Path.Combine(AppContext.BaseDirectory, "conexion-cliente.privado.json");
        if (!File.Exists(ruta)) return null;
        var paquete = JsonSerializer.Deserialize<PaqueteCliente>(File.ReadAllText(ruta))
            ?? throw new InvalidDataException("El paquete de conexión no es válido.");
        if (!Uri.TryCreate(paquete.Servidor, UriKind.Absolute, out var servidor) ||
            servidor.Scheme != "https" || servidor.IsLoopback || !string.IsNullOrEmpty(servidor.UserInfo) ||
            !string.IsNullOrEmpty(servidor.Query) || !string.IsNullOrEmpty(servidor.Fragment) || servidor.AbsolutePath != "/")
            throw new InvalidDataException("El paquete necesita una dirección pública HTTPS válida.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(paquete.ClaveCliente); }
        catch (FormatException) { throw new InvalidDataException("La clave del paquete no es válida."); }
        if (bytes.Length != 32 || paquete.ClaveCliente != Convert.ToBase64String(bytes))
            throw new InvalidDataException("La clave del paquete no es válida.");
        Environment.SetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN", paquete.ClaveCliente);
        return paquete;
    }
    internal async Task<bool> VerificarConexionAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var peticion = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(Servidor), "api/estado"));
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ClaveCliente);
        peticion.Headers.TryAddWithoutValidation("ngrok-skip-browser-warning", "SecureGate");
        try
        {
            using var respuesta = await http.SendAsync(peticion, ct);
            return respuesta.StatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (HttpRequestException) { return false; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return false; }
    }
}
