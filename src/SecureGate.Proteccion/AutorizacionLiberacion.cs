using System.Security.Principal;
using SecureGate.Cliente;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

// Capacidad de un solo uso, creada solo tras consultar el servidor y autenticar al usuario.
public sealed class AutorizacionLiberacion
{
    internal Guid ArchivoId { get; }
    internal Guid AnalisisId { get; }
    internal string Sha256 { get; }
    internal EvaluacionRiesgo Evaluacion { get; }
    internal string UsuarioSid { get; }
    internal DateTimeOffset FechaUtc { get; }
    internal string Metodo { get; }
    internal bool RiesgoAceptado { get; }
    private int _consumida;
    private AutorizacionLiberacion(ResultadoAnalisis r, string sid,
        string metodo = "CredUI+LogonUser", bool riesgoAceptado = false)
    {
        ArchivoId = r.Archivo.ArchivoId; AnalisisId = r.AnalisisId;
        Sha256 = r.Archivo.Sha256; Evaluacion = r.Evaluacion;
        UsuarioSid = sid; FechaUtc = DateTimeOffset.UtcNow;
        Metodo = metodo; RiesgoAceptado = riesgoAceptado;
    }

    // La decisión explícita sustituye las credenciales en el cliente. No es autenticación adicional.
    public static async Task<AutorizacionLiberacion?> SolicitarConfirmacionAsync(
        ClienteAnalisisHttp cliente, Guid archivoId, Guid analisisId, string sha256,
        bool excepcion, Func<ResultadoAnalisis, bool> confirmar, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        ArgumentNullException.ThrowIfNull(confirmar);
        var resultado = await ConsultarConLimiteAsync(cliente, analisisId, ct);
        ValidarDecision(resultado, archivoId, analisisId, sha256, excepcion);
        var tipo = PoliticaDecisionAnalisis.Clasificar(resultado);
        ct.ThrowIfCancellationRequested();
        if (!confirmar(resultado)) return null;
        // Volver a consultar tras la decisión. Un resultado cambiado requiere otro diálogo.
        resultado = await ConsultarConLimiteAsync(cliente, analisisId, ct);
        ValidarDecision(resultado, archivoId, analisisId, sha256, excepcion);
        if (PoliticaDecisionAnalisis.Clasificar(resultado) != tipo)
            throw new InvalidOperationException("El resultado cambió. Revisá el informe antes de decidir nuevamente.");
        ct.ThrowIfCancellationRequested();
        using var identidad = WindowsIdentity.GetCurrent();
        string sid = identidad.User?.Value ?? throw new UnauthorizedAccessException("No se obtuvo la cuenta actual.");
        return new(resultado, sid, excepcion ? "ConfirmacionExcepcional" : "ConfirmacionExplicita", excepcion);
    }

    internal static void ValidarDecision(ResultadoAnalisis r, Guid archivo, Guid analisis, string hash, bool excepcion)
    {
        PoliticaDecisionAnalisis.ValidarIdentidad(r, archivo, analisis, hash);
        if (!excepcion && PoliticaDecisionAnalisis.Clasificar(r) != TipoResultadoDecision.SinAmenazasReportadas)
            throw new UnauthorizedAccessException("El resultado requiere aceptación explícita de riesgo. La muestra sigue retenida.");
    }

    public static async Task<AutorizacionLiberacion?> SolicitarWindowsAsync(
        ClienteAnalisisHttp cliente, Guid archivoId, Guid analisisId, string sha256,
        bool riesgoAceptado, IntPtr ventana, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        if (!riesgoAceptado) throw new UnauthorizedAccessException("Falta aceptar explicitamente el riesgo.");
        var resultado = await ConsultarConLimiteAsync(cliente, analisisId, ct);
        ValidarInforme(resultado, archivoId, analisisId, sha256);
        ct.ThrowIfCancellationRequested();
        string? sid = CredencialesLiberacion.VerificarUsuarioActual(ventana);
        if (sid is null) return null;
        // Revalidar despues de la interaccion; no confiar en el historial local editable.
        resultado = await ConsultarConLimiteAsync(cliente, analisisId, ct);
        ValidarInforme(resultado, archivoId, analisisId, sha256);
        return new(resultado, sid);
    }
    private static async Task<ResultadoAnalisis> ConsultarConLimiteAsync(
        ClienteAnalisisHttp cliente, Guid id, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(30));
        try { return await cliente.ConsultarAsync(id, limite.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new TimeoutException("No se pudo confirmar el resultado del servidor en 30 segundos. No se libero el archivo."); }
    }

    internal static void ValidarInforme(ResultadoAnalisis r, Guid archivo, Guid analisis, string hash)
    {
        if (archivo == Guid.Empty || analisis == Guid.Empty || r.AnalisisId != analisis ||
            r.Archivo.ArchivoId != archivo || !r.Archivo.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase) ||
            r.Estado != EstadoAnalisis.Completado || !Enum.IsDefined(r.Evaluacion))
            throw new InvalidDataException("El informe no corresponde a un analisis terminado de este archivo.");
        var av = r.AnalisisLocal?.Antivirus;
        if (r.AnalisisLocal?.Archivo.ArchivoId != archivo ||
            !string.Equals(r.AnalisisLocal?.Archivo.Sha256, hash, StringComparison.OrdinalIgnoreCase) ||
            av?.Estado != EstadoComprobacion.Completada || av.Deteccion != DeteccionAntivirus.SinDetecciones)
            throw new UnauthorizedAccessException("Se requiere antivirus local completado sin detecciones.");
        if (r.Evaluacion == EvaluacionRiesgo.AmenazaDetectada || r.Reputacion?.MotoresMaliciosos is > 0)
            throw new UnauthorizedAccessException("No se permite liberar una amenaza detectada desde esta accion.");
    }

    internal void Consumir(RegistroCuarentena r)
    {
        using var identidad = WindowsIdentity.GetCurrent();
        if (r.ArchivoId != ArchivoId || !r.Sha256.Equals(Sha256, StringComparison.OrdinalIgnoreCase) ||
            identidad.User?.Value != UsuarioSid || DateTimeOffset.UtcNow - FechaUtc > TimeSpan.FromMinutes(2) ||
            Interlocked.CompareExchange(ref _consumida, 1, 0) != 0)
            throw new UnauthorizedAccessException("La autorizacion no corresponde, vencio o ya fue utilizada.");
    }
}
