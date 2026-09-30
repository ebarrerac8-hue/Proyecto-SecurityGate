using Seguregate;
using System.Net;


Console.Write("Ingresá tu clave de VirusTotal y presioná Enter: ");
string claveApi = LeerClaveOculta();


Console.Write("Ingresá el SHA-256 del archivo: ");
string hashPrueba = Console.ReadLine() ?? "";

var virusTotal = new VirusTotalClient();

try
{
    Console.WriteLine("Consultando VirusTotal...");

    ResultadoReputacion resultado =
        await virusTotal.ConsultarHashAsync(hashPrueba, claveApi);

    Console.WriteLine($"Estado: {resultado.Estado}");
    Console.WriteLine(resultado.Mensaje);

    if (resultado.Estado == EstadoConsulta.InformeDisponible)
    {
        Console.WriteLine($"Maliciosos: {resultado.Maliciosos}");
        Console.WriteLine($"Sospechosos: {resultado.Sospechosos}");
        Console.WriteLine($"Sin detección: {resultado.SinDeteccion}");
    }
}
catch (HttpRequestException error)
{
    if (error.StatusCode == HttpStatusCode.NotFound)
    {
        Console.WriteLine("VirusTotal respondió, pero no conoce esta huella.");
    }
    else if (error.StatusCode == HttpStatusCode.Unauthorized ||
             error.StatusCode == HttpStatusCode.Forbidden)
    {
        Console.WriteLine("VirusTotal rechazó el acceso. Revisá tu clave.");
    }
    else if (error.StatusCode == HttpStatusCode.TooManyRequests)
    {
        Console.WriteLine("Se alcanzó el límite de consultas. Intentá más tarde.");
    }
    else
    {
        Console.WriteLine($"Error de conexión: {error.Message}");
    }
}
catch (TaskCanceledException)
{
    Console.WriteLine("La consulta superó el tiempo de espera.");
}
catch (ArgumentException error)
{
    Console.WriteLine(error.Message);
}

Console.WriteLine("Presioná Enter para terminar.");
Console.ReadLine();

static string LeerClaveOculta()
{
    string clave = "";

    while (true)
    {
        var tecla = Console.ReadKey(intercept: true);

        if (tecla.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return clave;
        }

        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (clave.Length > 0)
                clave = clave.Substring(0, clave.Length - 1);
        }
        else if (!char.IsControl(tecla.KeyChar))
        {
            clave += tecla.KeyChar;
        }
    }
}