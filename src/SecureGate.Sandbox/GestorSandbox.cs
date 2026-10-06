using System.Diagnostics;
using System.Security.Cryptography;
using SecureGate.Contratos;

namespace SecureGate.Sandbox;

public class GestorSandbox
{
    public async Task<InformeSandbox> EjecutarAnalisisAsync(SolicitudSandbox solicitud, CancellationToken cancellationToken = default)
    {
        var informe = new InformeSandbox
        {
            SolicitudId = solicitud.SolicitudId,
            Estado = EstadoEjecucionSandbox.NoIniciado
        }; 

        if (!VerificadorEntorno.EsSandboxDisponible())
        {
            informe.Estado = EstadoEjecucionSandbox.ErrorArranque;
            informe.Observaciones = "Windows Sandbox no está disponible o instalado en el servidor.";
            return informe;
        }

        if (!VerificadorEntorno.VerificarRecursosSuficientes())
        {
            informe.Estado = EstadoEjecucionSandbox.FaltaRecursos;
            informe.Observaciones = "Recursos de memoria insuficientes en el servidor para iniciar la prueba.";
            return informe;
        }

        if (!File.Exists(solicitud.RutaMuestraServidor))
        {
            informe.Estado = EstadoEjecucionSandbox.ErrorArranque;
            informe.Observaciones = "El archivo de la muestra no existe en la ruta proporcionada.";
            return informe;
        }

        string hashCalculado = ObtenerSha256(solicitud.RutaMuestraServidor);
        if (!string.Equals(hashCalculado, solicitud.Sha256Esperado, StringComparison.OrdinalIgnoreCase))
        {
            informe.Estado = EstadoEjecucionSandbox.ErrorArranque;
            informe.Observaciones = "El hash SHA-256 de la muestra no coincide con el valor esperado.";
            return informe;
        }

        string carpetaMuestra = Path.GetDirectoryName(solicitud.RutaMuestraServidor) ?? Path.GetTempPath();
        string rutaWsb = Path.Combine(carpetaMuestra, $"analisis_{solicitud.SolicitudId}.wsb");

        try
        {
            string contenidoWsb = ConfiguradorWsb.GenerarConfiguracion(carpetaMuestra, solicitud.PermitirRed);
            await File.WriteAllTextAsync(rutaWsb, contenidoWsb, cancellationToken);

            var cronometro = Stopwatch.StartNew();

            var psi = new ProcessStartInfo
            {
                FileName = rutaWsb,
                UseShellExecute = true
            };

            using var proceso = Process.Start(psi);
            if (proceso == null)
            {
                informe.Estado = EstadoEjecucionSandbox.ErrorArranque;
                informe.Observaciones = "No se pudo iniciar el proceso de Windows Sandbox.";
                return informe;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(solicitud.TiempoMaximoEjecucion);

            try
            {
                await proceso.WaitForExitAsync(cts.Token);
                cronometro.Stop();

                informe.Estado = EstadoEjecucionSandbox.Completado;
                informe.DuracionEfectiva = cronometro.Elapsed;
                informe.Observaciones = "Prueba finalizada normalmente.";
            }
            catch (OperationCanceledException)
            {
                cronometro.Stop();
                informe.Estado = EstadoEjecucionSandbox.Timeout;
                informe.DuracionEfectiva = cronometro.Elapsed;
                informe.Observaciones = "La prueba alcanzó el tiempo máximo límite permitido.";

                if (!proceso.HasExited)
                {
                    proceso.Kill(entireProcessTree: true);
                }
            }

            informe.Eventos.Add(new EventoComportamiento
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                TipoEvento = "EntornoSandboxIniciado",
                Detalle = $"Sesión iniciada correctamente. Red habilitada: {solicitud.PermitirRed}"
            });

            return informe;
        }
        catch (Exception ex)
        {
            informe.Estado = EstadoEjecucionSandbox.ErrorArranque;
            informe.Observaciones = $"Error durante la ejecución del Sandbox: {ex.Message}";
            return informe;
        }
        finally
        {
            if (File.Exists(rutaWsb))
            {
                try { File.Delete(rutaWsb); } catch { }
            }
        }
    }

    private static string ObtenerSha256(string rutaArchivo)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(rutaArchivo);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
