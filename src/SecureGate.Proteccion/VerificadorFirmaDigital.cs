using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

internal static class VerificadorFirmaDigital
{
    // El comando consulta la firma: no abre ni ejecuta el archivo analizado.
    private const string Comando = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        $s = Microsoft.PowerShell.Security\Get-AuthenticodeSignature -LiteralPath $env:SECUREGATE_FIRMA_ARCHIVO
        $firmante = $null
        if ($null -ne $s.SignerCertificate) { $firmante = $s.SignerCertificate.Subject }
        [pscustomobject]@{ Estado = [string]$s.Status; Detalle = [string]$s.StatusMessage; Firmante = $firmante } | ConvertTo-Json -Compress
        """;

    internal static async Task<ResultadoFirmaDigital> VerificarAsync(string muestra,
        string extension, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return Error(EstadoComprobacion.NoDisponible, "FIRMA_WINDOWS_REQUERIDO", "Authenticode requiere Windows.");
        string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(powershell))
            return Error(EstadoComprobacion.NoDisponible, "FIRMA_VERIFICADOR_NO_DISPONIBLE", "Windows PowerShell no esta disponible.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(extension, @"^\.[a-zA-Z0-9]{1,10}$"))
            return Error(EstadoComprobacion.NoDisponible, "FIRMA_EXTENSION_NO_ADMITIDA", "La extension no permite esta comprobacion.");

        string carpeta = Path.Combine(Path.GetDirectoryName(muestra)!, "VerificacionFirma", Guid.NewGuid().ToString("N"));
        AlmacenCuarentena.ComprobarRuta(carpeta);
        Directory.CreateDirectory(carpeta);
        string consulta = Path.Combine(carpeta, "consulta" + extension);
        Process? proceso = null;
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            // Preservar la extension original para los proveedores SIP de Windows.
            await using (var entrada = new FileStream(muestra, FileMode.Open,
                FileAccess.Read, FileShare.None, 65536, useAsync: true))
            await using (var salida = new FileStream(consulta, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 65536, useAsync: true))
                await entrada.CopyToAsync(salida, limite.Token);

            var inicio = new ProcessStartInfo(powershell)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
                Convert.ToBase64String(Encoding.Unicode.GetBytes(Comando)) })
                inicio.ArgumentList.Add(arg);
            inicio.Environment["SECUREGATE_FIRMA_ARCHIVO"] = consulta;
            proceso = Process.Start(inicio) ?? throw new IOException("No se inicio el verificador.");
            Task<string> salidaJson = proceso.StandardOutput.ReadToEndAsync(limite.Token);
            Task<string> errores = proceso.StandardError.ReadToEndAsync(limite.Token);
            await Task.WhenAll(salidaJson, errores, proceso.WaitForExitAsync(limite.Token));
            if (proceso.ExitCode != 0)
                return Error(EstadoComprobacion.Fallida, "FIRMA_VERIFICADOR_ERROR", "Windows no pudo completar la consulta Authenticode.");
            string json = await salidaJson;
            if (json.Length > 16384)
                return Error(EstadoComprobacion.Fallida, "FIRMA_RESPUESTA_INVALIDA", "Respuesta demasiado extensa.");
            var dato = JsonSerializer.Deserialize<DatoFirma>(json)
                ?? throw new JsonException("Respuesta vacia.");
            return Interpretar(dato);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Error(EstadoComprobacion.Fallida, "FIRMA_TIEMPO_AGOTADO", "La comprobacion excedio 20 segundos."); }
        catch (Win32Exception)
        { return Error(EstadoComprobacion.NoDisponible, "FIRMA_VERIFICADOR_BLOQUEADO", "No se pudo iniciar Windows PowerShell."); }
        catch (JsonException)
        { return Error(EstadoComprobacion.Fallida, "FIRMA_RESPUESTA_INVALIDA", "Windows devolvio una respuesta no reconocida."); }
        catch (IOException)
        { return Error(EstadoComprobacion.Fallida, "FIRMA_LECTURA_ERROR", "No se pudo preparar o leer la consulta de firma."); }
        catch (UnauthorizedAccessException)
        { return Error(EstadoComprobacion.Fallida, "FIRMA_PERMISOS", "Faltan permisos para comprobar la firma."); }
        finally
        {
            if (proceso is not null)
            {
                try
                {
                    if (!proceso.HasExited)
                    {
                        proceso.Kill(entireProcessTree: true);
                        using var cierre = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await proceso.WaitForExitAsync(cierre.Token);
                    }
                }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
                catch (OperationCanceledException) { }
                finally { proceso.Dispose(); }
            }
            try { Directory.Delete(carpeta, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    private static ResultadoFirmaDigital Interpretar(DatoFirma d)
    {
        var r = new ResultadoFirmaDigital
        {
            Firmante = d.Firmante is { Length: > 1000 } ? d.Firmante[..1000] : d.Firmante,
            Detalle = "Authenticode de Windows: " + d.Estado + ". " +
                (d.Detalle.Length > 1000 ? d.Detalle[..1000] : d.Detalle)
        };
        switch (d.Estado)
        {
            case "Valid": r.Estado = EstadoComprobacion.Completada; r.Firma = EstadoFirmaDigital.Valida; break;
            case "NotSigned": r.Estado = EstadoComprobacion.Completada; r.Firma = EstadoFirmaDigital.SinFirma; break;
            case "HashMismatch":
            case "NotTrusted":
                r.Estado = EstadoComprobacion.Completada; r.Firma = EstadoFirmaDigital.Invalida;
                r.CodigoError = "FIRMA_" + d.Estado.ToUpperInvariant(); break;
            case "NotSupportedFileFormat":
            case "Incompatible":
                r.Estado = EstadoComprobacion.NoDisponible; r.Firma = EstadoFirmaDigital.Indeterminada;
                r.CodigoError = "FIRMA_FORMATO_NO_ADMITIDO"; break;
            default:
                r.Estado = EstadoComprobacion.Fallida; r.Firma = EstadoFirmaDigital.Indeterminada;
                r.CodigoError = "FIRMA_VERIFICACION_INDETERMINADA"; break;
        }
        return r;
    }
    private static ResultadoFirmaDigital Error(EstadoComprobacion estado, string codigo, string detalle) => new()
    { Estado = estado, Firma = EstadoFirmaDigital.Indeterminada, CodigoError = codigo, Detalle = detalle };
    private sealed class DatoFirma
    {
        public DatoFirma() { }
        public string Estado { get; set; } = "";
        public string Detalle { get; set; } = "";
        public string? Firmante { get; set; }
    }
}
