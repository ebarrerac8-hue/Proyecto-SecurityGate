using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SecureGate.Contratos;
using SecureGate.Sandbox.Services;

namespace SecureGate.Sandbox;

public sealed class GestorSandbox : IAnalizadorSandbox
{
    private readonly SemaphoreSlim _sesiones = new(1, 1);

    public async Task<InformeSandbox> EjecutarAsync(
        SolicitudPruebaSandbox solicitud,
        CancellationToken cancellationToken = default)
    {
        await _sesiones.WaitAsync(cancellationToken);
        Process? proceso = null;
        var informe = new InformeSandbox
        {
            AnalisisId = solicitud.AnalisisId,
            ArchivoId = solicitud.ArchivoId,
            Sha256 = solicitud.Sha256,
            InicioUtc = DateTimeOffset.UtcNow,
            RedHabilitada = false
        };

        try
        {
            if (solicitud.AnalisisId == Guid.Empty ||
                solicitud.ArchivoId == Guid.Empty ||
                solicitud.Sha256.Length != 64 ||
                !solicitud.Sha256.All(Uri.IsHexDigit) ||
                solicitud.TiempoObservacionSegundos < 10 ||
                solicitud.TiempoObservacionSegundos > 300)
                return Fallo(informe, "SANDBOX_SOLICITUD_INVALIDA", "Solicitud Sandbox inválida.");

            if (!string.Equals(solicitud.ExtensionMuestra, ".exe",
                    StringComparison.OrdinalIgnoreCase))
                return Fallo(informe, "SANDBOX_EXTENSION_NO_ADMITIDA",
                    "Esta versión del observador ejecuta únicamente archivos .exe.",
                    EstadoComprobacion.NoDisponible);

            string ejecutableSandbox = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe");

            if (!File.Exists(ejecutableSandbox))
                return Fallo(informe, "SANDBOX_NO_INSTALADO",
                    "Windows Sandbox no está disponible en el servidor.",
                    EstadoComprobacion.NoDisponible);

            if (HayOtraSesion())
                return Fallo(informe, "SANDBOX_OCUPADO",
                    "Hay otra sesión de Windows Sandbox abierta. Ciérrela antes de la prueba.",
                    EstadoComprobacion.NoDisponible);

            if (!VerificadorEntorno.VerificarRecursosSuficientes())
                return Fallo(informe, "SANDBOX_RECURSOS_INSUFICIENTES",
                    "Se requieren al menos 2 GiB de memoria física disponible para esta prueba.",
                    EstadoComprobacion.NoDisponible);

            string observador = Path.Combine(AppContext.BaseDirectory,
                "Assets", "Guest", "Observador.ps1");

            if (!File.Exists(observador))
                return Fallo(informe, "SANDBOX_OBSERVADOR_AUSENTE",
                    "No se encontró Assets/Guest/Observador.ps1 en la salida del servidor.");

            string sesion = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecureGate", "Servidor", "SesionesSandbox",
                solicitud.AnalisisId.ToString("N"), Guid.NewGuid().ToString("N"));
            string raizDisco = Path.GetPathRoot(Path.GetFullPath(sesion))!;
            if (new DriveInfo(raizDisco).AvailableFreeSpace < 1024L * 1024 * 1024)
                return Fallo(informe, "SANDBOX_DISCO_INSUFICIENTE",
                    "Se requiere al menos 1 GiB libre para preparar esta prueba.",
                    EstadoComprobacion.NoDisponible);

            string entrada = Path.Combine(sesion, "Entrada");
            string salida = Path.Combine(sesion, "Salida");
            Directory.CreateDirectory(entrada);
            Directory.CreateDirectory(salida);

            string copia = Path.Combine(entrada, "muestra.exe");
            await using (var origen = new FileStream(solicitud.RutaMuestraServidor,
                FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            await using (var destino = new FileStream(copia,
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await origen.CopyToAsync(destino, cancellationToken);
            }

            await using (var archivo = File.OpenRead(copia))
            {
                string hash = Convert.ToHexString(
                    await SHA256.HashDataAsync(archivo, cancellationToken));
                if (!string.Equals(hash, solicitud.Sha256, StringComparison.OrdinalIgnoreCase))
                    return Fallo(informe, "SANDBOX_HASH_NO_COINCIDE",
                        "La copia preparada no corresponde al SHA-256 esperado.");
            }

            File.Copy(observador, Path.Combine(entrada, "Observador.ps1"));
            // Demostración solo si la muestra coincide con cmd.exe del anfitrión.
            // No se permite convertir otro ejecutable en muestra de demostración.
            bool demostracion = false;
            string cmdWindows = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (File.Exists(cmdWindows))
            {
                await using var cmd = File.OpenRead(cmdWindows);
                string hashCmd = Convert.ToHexString(
                    await SHA256.HashDataAsync(cmd, cancellationToken));
                demostracion = string.Equals(hashCmd, solicitud.Sha256,
                    StringComparison.OrdinalIgnoreCase);
            }

            var datosInvitado = new
            {
                solicitud.AnalisisId,
                solicitud.ArchivoId,
                solicitud.Sha256,
                solicitud.TiempoObservacionSegundos,
                ModoDemostracion = demostracion
            };
            await File.WriteAllTextAsync(Path.Combine(entrada, "solicitud.json"),
                JsonSerializer.Serialize(datosInvitado), cancellationToken);

            string wsb = Path.Combine(sesion, "prueba.wsb");
            await File.WriteAllTextAsync(wsb,
                ConfiguradorWsb.GenerarConfiguracion(entrada, salida), cancellationToken);

            proceso = Process.Start(new ProcessStartInfo
            {
                FileName = ejecutableSandbox,
                Arguments = "\"" + wsb + "\"",
                UseShellExecute = false
            });

            if (proceso is null)
                return Fallo(informe, "SANDBOX_ERROR_ARRANQUE",
                    "No se pudo iniciar Windows Sandbox.");

            // Incluye tiempo adicional para arrancar Windows Sandbox.
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(TimeSpan.FromSeconds(solicitud.TiempoObservacionSegundos + 90));

            string reporte = Path.Combine(salida, "informe.json");
            string inicio = Path.Combine(salida, "inicio.json");

            while (!File.Exists(reporte))
            {
                if (File.Exists(inicio))
                {
                    var confirmacion = await LeerInformeAsync(inicio, limite.Token);
                    ValidarIdentidad(confirmacion, solicitud);
                    informe.ObservadorIniciado = confirmacion.ObservadorIniciado;
                }

                await Task.Delay(500, limite.Token);
            }

            var obtenido = await LeerInformeAsync(reporte, limite.Token);
            ValidarIdentidad(obtenido, solicitud);
            informe = obtenido;

            informe.Limitaciones.Add(
                "Observación parcial de procesos y archivos del perfil del usuario. " +
                "No se observaron registro ni conexiones de red; pueden perderse procesos breves.");
            informe.Limitaciones.Add(
                "Las evidencias provienen del entorno que ejecuta la muestra y no son " +
                "una prueba resistente a manipulación por software hostil.");

            if (informe.Estado == EstadoComprobacion.Completada &&
                (!informe.ObservadorIniciado || !informe.MuestraEjecutada ||
                 informe.Eventos.Count == 0))
                return Fallo(informe, "SANDBOX_EVIDENCIA_INSUFICIENTE",
                    "El informe no confirma observación y ejecución de la muestra.");

            return informe;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Fallo(informe, "SANDBOX_TIEMPO_AGOTADO",
                "No llegó un informe final dentro del tiempo de arranque y observación.");
        }
        catch (Exception error)
        {
            return Fallo(informe, "SANDBOX_ERROR",
                "La prueba Sandbox falló: " + error.Message);
        }
        finally
        {
            if (proceso is not null)
            {
                try
                {
                    if (!proceso.HasExited)
                    {
                        proceso.Kill(entireProcessTree: true);
                        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        await proceso.WaitForExitAsync(cierre.Token);
                    }
                }
                catch (Exception)
                {
                    Fallo(informe, "SANDBOX_CIERRE_NO_CONFIRMADO",
                        "No se pudo confirmar el cierre del proceso iniciado.");
                }
                proceso.Dispose();
            }

            // Dar tiempo al cliente para terminar el cierre de su invitado.
            if (proceso is not null)
            {
                var finCierre = DateTimeOffset.UtcNow.AddSeconds(10);
                while (HayOtraSesion() && DateTimeOffset.UtcNow < finCierre)
                    await Task.Delay(250);
            }

            if (proceso is not null && HayOtraSesion())
                Fallo(informe, "SANDBOX_SESION_SIGUE_ABIERTA",
                    "Hay una sesión Sandbox que sigue abierta. No se iniciará otra hasta cerrarla.");

            informe.FinUtc ??= DateTimeOffset.UtcNow;
            _sesiones.Release();
        }
    }

    private static bool HayOtraSesion()
    {
        foreach (string nombre in new[] { "WindowsSandbox", "WindowsSandboxClient" })
        {
            var procesos = Process.GetProcessesByName(nombre);
            bool existe = procesos.Length > 0;
            foreach (var proceso in procesos) proceso.Dispose();
            if (existe) return true;
        }
        return false;
    }

    private static async Task<InformeSandbox> LeerInformeAsync(string ruta, CancellationToken ct)
    {
        var info = new FileInfo(ruta);
        if (info.Length > 8 * 1024 * 1024 ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Archivo de evidencias no admitido.");

        string json = await File.ReadAllTextAsync(ruta, ct);
        return JsonSerializer.Deserialize<InformeSandbox>(json)
            ?? throw new InvalidDataException("Informe de evidencias vacío.");
    }

    private static void ValidarIdentidad(
        InformeSandbox informe, SolicitudPruebaSandbox solicitud)
    {
        if (informe.AnalisisId != solicitud.AnalisisId ||
            informe.ArchivoId != solicitud.ArchivoId ||
            !string.Equals(informe.Sha256, solicitud.Sha256, StringComparison.OrdinalIgnoreCase) ||
            informe.RedHabilitada || informe.Eventos is null || informe.Limitaciones is null)
            throw new InvalidDataException("Las evidencias no corresponden a esta prueba.");
    }

    private static InformeSandbox Fallo(InformeSandbox informe, string codigo,
        string detalle, EstadoComprobacion estado = EstadoComprobacion.Fallida)
    {
        informe.Estado = estado;
        informe.CodigoError = codigo;
        informe.Limitaciones.Add(detalle);
        informe.FinUtc = DateTimeOffset.UtcNow;
        return informe;
    }
}
