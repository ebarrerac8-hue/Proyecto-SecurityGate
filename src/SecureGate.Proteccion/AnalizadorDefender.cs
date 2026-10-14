using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

internal static class AnalizadorDefender
{
    private static readonly SemaphoreSlim Turno = new(1, 1);
    private const string Preparacion = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        try {
            $s = Defender\Get-MpComputerStatus -ErrorAction Stop
            if (!$s.AMServiceEnabled -or !$s.AntivirusEnabled -or $s.AMRunningMode -ne 'Normal') {
                throw 'Microsoft Defender no esta activo en modo Normal.'
            }
            $raiz = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'Microsoft\Windows Defender\Platform'
            $ruta = $null
            if (Test-Path -LiteralPath $raiz) {
                $versiones = Get-ChildItem -LiteralPath $raiz -Directory | Sort-Object -Property @{
                    Expression = { try { [version](($_.Name -split '-')[0]) } catch { [version]'0.0' } }
                } -Descending
                foreach ($v in $versiones) {
                    $c = Join-Path $v.FullName 'MpCmdRun.exe'
                    if (Test-Path -LiteralPath $c -PathType Leaf) { $ruta = $c; break }
                }
            }
            if (!$ruta) { $ruta = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'Windows Defender\MpCmdRun.exe' }
            $firma = Microsoft.PowerShell.Security\Get-AuthenticodeSignature -LiteralPath $ruta
            if ($firma.Status -ne 'Valid' -or $null -eq $firma.SignerCertificate -or
                $firma.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)') {
                throw 'No se confirmo firma de Microsoft en MpCmdRun.exe.'
            }
            [pscustomobject]@{ Disponible = $true; Ruta = $ruta;
                Version = [string]$s.AntivirusSignatureVersion;
                CodigoPagina = [Globalization.CultureInfo]::CurrentCulture.TextInfo.OEMCodePage;
                Detalle = '' } | ConvertTo-Json -Compress
        } catch {
            [pscustomobject]@{ Disponible = $false; Ruta = ''; Version = '';
                CodigoPagina = 0; Detalle = [string]$_.Exception.Message } | ConvertTo-Json -Compress
        }
        """;

    internal static async Task<ResultadoAntivirus> AnalizarAsync(string ruta, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return InterpretadorDefender.Error("DEFENDER_WINDOWS_REQUERIDO", "Defender requiere Windows.",
                EstadoComprobacion.NoDisponible);
        await Turno.WaitAsync(ct);
        try
        {
            AlmacenCuarentena.ComprobarRuta(ruta);
            var datos = await PrepararAsync(ct);
            if (!datos.Disponible)
                return InterpretadorDefender.Error("DEFENDER_NO_DISPONIBLE", datos.Detalle,
                    EstadoComprobacion.NoDisponible);
            AlmacenCuarentena.ComprobarRuta(datos.Ruta);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var inicio = new ProcessStartInfo(datos.Ruta)
            {
                WorkingDirectory = Path.GetDirectoryName(datos.Ruta)!,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(datos.CodigoPagina),
                StandardErrorEncoding = Encoding.GetEncoding(datos.CodigoPagina)
            };
            foreach (string arg in new[] { "-Scan", "-ScanType", "3", "-File", ruta,
                "-DisableRemediation", "-ReturnHR" }) inicio.ArgumentList.Add(arg);
            var lectura = await EjecutarAsync(inicio, TimeSpan.FromSeconds(90), ct);
            var resultado = InterpretadorDefender.Interpretar(lectura.Codigo,
                lectura.Salida.Texto, lectura.Errores.Texto,
                lectura.Salida.Truncada || lectura.Errores.Truncada);
            resultado.Detalle += " Definiciones: " + datos.Version + ".";
            return resultado;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return InterpretadorDefender.Error("DEFENDER_TIEMPO_AGOTADO", "Se excedio el tiempo de preparacion o escaneo."); }
        catch (Win32Exception)
        { return InterpretadorDefender.Error("DEFENDER_INICIO_ERROR", "Windows no permitio iniciar el verificador o Defender."); }
        catch (JsonException)
        { return InterpretadorDefender.Error("DEFENDER_PREPARACION_INVALIDA", "La comprobacion de disponibilidad no devolvio datos validos."); }
        catch (IOException)
        { return InterpretadorDefender.Error("DEFENDER_LECTURA_ERROR", "No se pudo leer la muestra o la salida de Defender."); }
        catch (UnauthorizedAccessException)
        { return InterpretadorDefender.Error("DEFENDER_PERMISOS", "Faltan permisos para completar el escaneo."); }
        catch (ArgumentException)
        { return InterpretadorDefender.Error("DEFENDER_CONFIGURACION_INVALIDA", "No se pudo interpretar la configuracion de Defender."); }
        finally { Turno.Release(); }
    }

    private static async Task<Disponibilidad> PrepararAsync(CancellationToken ct)
    {
        string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(exe)) return new() { Detalle = "Windows PowerShell no esta disponible." };
        var inicio = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(Preparacion)) }) inicio.ArgumentList.Add(arg);
        var r = await EjecutarAsync(inicio, TimeSpan.FromSeconds(20), ct);
        if (r.Codigo != 0 || r.Salida.Truncada || r.Errores.Truncada ||
            !string.IsNullOrWhiteSpace(r.Errores.Texto))
            return new() { Detalle = "No se pudo consultar el estado y firma de Defender." };
        return JsonSerializer.Deserialize<Disponibilidad>(r.Salida.Texto)
            ?? throw new JsonException("Preparacion vacia.");
    }

    private static async Task<LecturaProceso> EjecutarAsync(ProcessStartInfo inicio,
        TimeSpan tiempo, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(tiempo);
        using var proceso = Process.Start(inicio) ?? throw new IOException("No se inicio el proceso.");
        Task<SalidaLimitada> salida = LeerAsync(proceso.StandardOutput, limite.Token);
        Task<SalidaLimitada> errores = LeerAsync(proceso.StandardError, limite.Token);
        try
        {
            await Task.WhenAll(salida, errores, proceso.WaitForExitAsync(limite.Token));
            return new(proceso.ExitCode, await salida, await errores);
        }
        finally
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
        }
    }

    private static async Task<SalidaLimitada> LeerAsync(StreamReader lector, CancellationToken ct)
    {
        const int maximo = 65536;
        var texto = new StringBuilder();
        var buffer = new char[4096];
        bool truncada = false;
        int cantidad;
        while ((cantidad = await lector.ReadAsync(buffer.AsMemory(), ct)) != 0)
        {
            int guardar = Math.Min(cantidad, maximo - texto.Length);
            texto.Append(buffer, 0, guardar);
            truncada |= guardar != cantidad;
        }
        return new(texto.ToString(), truncada);
    }

    private sealed class Disponibilidad
    {
        public Disponibilidad() { }
        public bool Disponible { get; set; }
        public string Ruta { get; set; } = "";
        public string Version { get; set; } = "";
        public int CodigoPagina { get; set; }
        public string Detalle { get; set; } = "";
    }
    private sealed record SalidaLimitada(string Texto, bool Truncada);
    private sealed record LecturaProceso(int Codigo, SalidaLimitada Salida, SalidaLimitada Errores);
}
