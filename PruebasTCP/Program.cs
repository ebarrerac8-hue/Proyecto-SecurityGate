using Seguregate;

var gestor = new GestorSandbox();
var virusTotal = new VirusTotalClient();

while (true)
{
    Console.WriteLine();
    Console.WriteLine("=== PRUEBAS SECUREGATE ===");
    Console.WriteLine("1. Consultar un SHA-256 en VirusTotal");
    Console.WriteLine("2. Probar el gestor con un Sandbox vacío");
    Console.WriteLine("3. Abrir una configuración .wsb del equipo");
    Console.WriteLine("0. Salir");
    Console.Write("Elegí una opción: ");

    string opcion = Console.ReadLine()?.Trim() ?? "";

    if (opcion == "0")
        break;

    switch (opcion)
    {
        case "1":
            await ProbarVirusTotalAsync(virusTotal);
            break;

        case "2":
            ProbarSandboxVacio(gestor);
            break;

        case "3":
            Console.Write(
                "Pegá la ruta completa de la configuración .wsb: "
            );

            string ruta = Console.ReadLine() ?? "";

            SolicitarApertura(gestor, ruta);
            break;

        default:
            Console.WriteLine("Opción no válida.");
            break;
    }
}

static async Task ProbarVirusTotalAsync(VirusTotalClient cliente)
{
    Console.Write("Ingresá tu clave de VirusTotal: ");
    string clave = LeerClaveOculta();

    Console.Write("Ingresá el SHA-256: ");
    string hash = Console.ReadLine() ?? "";

    try
    {
        Console.WriteLine("Consultando VirusTotal...");

        ResultadoReputacion resultado =
            await cliente.ConsultarHashAsync(hash, clave);

        Console.WriteLine($"Estado: {resultado.Estado}");
        Console.WriteLine(resultado.Mensaje);

        if (resultado.Estado == EstadoConsulta.InformeDisponible)
        {
            Console.WriteLine(
                $"Motores con detección maliciosa: {resultado.Maliciosos}"
            );

            Console.WriteLine(
                $"Motores con resultado sospechoso: {resultado.Sospechosos}"
            );

            Console.WriteLine(
                $"Motores sin detección: {resultado.SinDeteccion}"
            );
        }
    }
    catch (ArgumentException error)
    {
        Console.WriteLine(error.Message);
    }
}

static void ProbarSandboxVacio(GestorSandbox gestor)
{
    try
    {
        string carpeta = Path.Combine(
            Path.GetTempPath(),
            "SecureGate",
            "Pruebas"
        );

        Directory.CreateDirectory(carpeta);

        string ruta = Path.Combine(
            carpeta,
            $"prueba-{Guid.NewGuid():N}.wsb"
        );

        // Esta prueba no comparte archivos ni ejecuta programas.
        string configuracion = """
            <Configuration>
                <Networking>Disable</Networking>
                <ClipboardRedirection>Disable</ClipboardRedirection>
                <AudioInput>Disable</AudioInput>
                <VideoInput>Disable</VideoInput>
                <PrinterRedirection>Disable</PrinterRedirection>
                <vGPU>Disable</vGPU>
            </Configuration>
            """;

        File.WriteAllText(ruta, configuracion);

        SolicitarApertura(gestor, ruta);
    }
    catch (Exception error) when (
        error is IOException ||
        error is UnauthorizedAccessException)
    {
        Console.WriteLine(
            $"No se pudo crear la configuración de prueba: {error.Message}"
        );
    }
}

static void SolicitarApertura(GestorSandbox gestor, string ruta)
{
    if (OperatingSystem.IsWindows())
    {
        Console.WriteLine(
            "SecureGate tiene privilegios de administrador: " +
            (gestor.TienePermisosAdministrador() ? "Sí" : "No")
        );
    }

    Console.Write("¿Autorizás abrir Sandbox? Escribí S para continuar: ");

    bool autorizado = string.Equals(
        Console.ReadLine()?.Trim(),
        "S",
        StringComparison.OrdinalIgnoreCase
    );

    ResultadoSandbox resultado = gestor.Iniciar(ruta, autorizado);

    Console.WriteLine($"Estado: {resultado.Estado}");
    Console.WriteLine(resultado.Mensaje);

    if (resultado.IdProceso is int identificador)
    {
        Console.WriteLine(
            $"Identificador del proceso lanzador: {identificador}"
        );
    }
}

static string LeerClaveOculta()
{
    string clave = "";

    while (true)
    {
        ConsoleKeyInfo tecla = Console.ReadKey(intercept: true);

        if (tecla.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return clave;
        }

        if (tecla.Key == ConsoleKey.Backspace)
        {
            if (clave.Length > 0)
                clave = clave[..^1];
        }
        else if (!char.IsControl(tecla.KeyChar))
        {
            clave += tecla.KeyChar;
        }
    }
}