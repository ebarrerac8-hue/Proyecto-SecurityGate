using SecureGate.Contratos;
using SecureGate.Sandbox.Services;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace SecureGate.Sandbox;

public class GestorSandbox : IAnalizadorSandbox
{
    public async Task<InformeSandbox> EjecutarAsync(
        SolicitudPruebaSandbox solicitud,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset inicioUtc = DateTimeOffset.UtcNow;

        var informe = new InformeSandbox
        {
            AnalisisId = solicitud.AnalisisId,
            ArchivoId = solicitud.ArchivoId,
            Sha256 = solicitud.Sha256,
            InicioUtc = inicioUtc,
            Estado = EstadoComprobacion.NoRealizada,
            MuestraEjecutada = false,
            ObservadorIniciado = false,
            RedHabilitada = false
        };

        if (!VerificadorEntorno.EsSandboxDisponible())
        {
            informe.Estado = EstadoComprobacion.NoDisponible;
            informe.CodigoError = "SANDBOX_NO_INSTALADO";
            informe.Limitaciones.Add("Windows Sandbox no está habilitado o instalado en la computadora servidor.");
            informe.FinUtc = DateTimeOffset.UtcNow;
            return informe;
        }

        if (!VerificadorEntorno.VerificarRecursosSuficientes())
        {
            informe.Estado = EstadoComprobacion.Fallida;
            informe.CodigoError = "RECURSOS_INSUFICIENTES";
            informe.Limitaciones.Add("Memoria RAM insuficiente en el servidor para iniciar el entorno aislado.");
            informe.FinUtc = DateTimeOffset.UtcNow;
            return informe;
        }

        if (!File.Exists(solicitud.RutaMuestraServidor))
        {
            informe.Estado = EstadoComprobacion.Fallida;
            informe.CodigoError = "MUESTRA_NO_ENCONTRADA";
            informe.Limitaciones.Add($"No se encontró la muestra en la ruta especificada: {solicitud.RutaMuestraServidor}");
            informe.FinUtc = DateTimeOffset.UtcNow;
            return informe;
        }

        string hashCalculado = ObtenerSha256(solicitud.RutaMuestraServidor);
        if (!string.Equals(hashCalculado, solicitud.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            informe.Estado = EstadoComprobacion.Fallida;
            informe.CodigoError = "HASH_NO_COINCIDE";
            informe.Limitaciones.Add("El hash SHA-256 calculado del archivo no coincide con el hash esperado.");
            informe.FinUtc = DateTimeOffset.UtcNow;
            return informe;
        }

        string carpetaMuestra = Path.GetDirectoryName(solicitud.RutaMuestraServidor) ?? Path.GetTempPath();

        string rutaObservadorBase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Guest", "Observador.ps1");
        string rutaObservadorDestino = Path.Combine(carpetaMuestra, "Observador.ps1");

        if (File.Exists(rutaObservadorBase))
        {
            File.Copy(rutaObservadorBase, rutaObservadorDestino, overwrite: true);
            informe.ObservadorIniciado = true;
        }
        else
        {
            informe.Limitaciones.Add("No se encontró el script Observador.ps1 en los Assets del sistema.");
        }

        string rutaWsb = Path.Combine(carpetaMuestra, $"analisis_{solicitud.AnalisisId}.wsb");

        try
        {
            string contenidoWsb = ConfiguradorWsb.GenerarConfiguracion(carpetaMuestra, permitirRed: false);
            await File.WriteAllTextAsync(rutaWsb, contenidoWsb, cancellationToken);

            var psi = new ProcessStartInfo
            {
                FileName = rutaWsb,
                UseShellExecute = true
            };

            using var proceso = Process.Start(psi);
            if (proceso == null)
            {
                informe.Estado = EstadoComprobacion.Fallida;
                informe.CodigoError = "ERROR_INICIO_SANDBOX";
                informe.Limitaciones.Add("No se pudo arrancar el proceso de Windows Sandbox.");
                informe.FinUtc = DateTimeOffset.UtcNow;
                return informe;
            }

            informe.MuestraEjecutada = true;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(solicitud.TiempoObservacionSegundos));

            try
            {
                await proceso.WaitForExitAsync(cts.Token);
                informe.Estado = EstadoComprobacion.Completada;
            }
            catch (OperationCanceledException)
            {
                informe.Estado = EstadoComprobacion.Completada;
                informe.Limitaciones.Add($"La prueba finalizó por alcanzar el tiempo máximo configurado ({solicitud.TiempoObservacionSegundos}s).");

                if (!proceso.HasExited)
                {
                    proceso.Kill(entireProcessTree: true);
                }
            }

            string rutaEvidencias = Path.Combine(carpetaMuestra, "evidencias.json");
            if (File.Exists(rutaEvidencias))
            {
                string json = await File.ReadAllTextAsync(rutaEvidencias, cancellationToken);
                var eventos = JsonSerializer.Deserialize<List<EventoSandbox>>(json);
                if (eventos != null)
                {
                    informe.Eventos.AddRange(eventos);
                }
            }
            else
            {
                informe.Limitaciones.Add("No se encontró el archivo de reporte de evidencias (evidencias.json) al finalizar.");
            }
        }
        catch (Exception ex)
        {
            informe.Estado = EstadoComprobacion.Fallida;
            informe.CodigoError = "EXCEPCION_EJECUCION";
            informe.Limitaciones.Add($"Ocurrió un error inesperado en la prueba: {ex.Message}");
        }
        finally
        {
            informe.FinUtc = DateTimeOffset.UtcNow;

            if (File.Exists(rutaWsb))
            {
                try { File.Delete(rutaWsb); } catch { }
            }
        }

        return informe;
    }

    private static string ObtenerSha256(string rutaArchivo)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(rutaArchivo);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}