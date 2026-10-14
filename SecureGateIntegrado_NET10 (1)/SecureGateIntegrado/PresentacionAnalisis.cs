using SecureGate.Cliente;
using SecureGate.Contratos;

namespace SecureGate;

// Traduce las evidencias a textos de interfaz sin modificar el informe ni sus permisos.
internal static class PresentacionAnalisis
{
    internal const string SinAmenazas = "Sin amenazas reportadas";
    internal const string Precaucion = "Indicadores que requieren precaución";
    internal const string Amenaza = "Amenaza reportada";
    internal const string Pendientes = "Comprobaciones pendientes";
    internal const string EnCurso = "Sin resultado final";

    internal static ResultadoAnalisisLocal Local(EnvioCliente envio) =>
        envio.Resultado?.AnalisisLocal ?? envio.Solicitud.AnalisisLocal;

    internal static List<string> Faltantes(EnvioCliente envio)
    {
        var faltantes = new List<string>();
        var local = Local(envio);
        if (local.FirmaDigital.Estado != EstadoComprobacion.Completada) faltantes.Add("firma Authenticode");
        if (local.Antivirus.Estado != EstadoComprobacion.Completada ||
            local.Antivirus.Deteccion == DeteccionAntivirus.NoDeterminada) faltantes.Add("antivirus local");
        if (envio.Resultado?.Reputacion?.Estado != EstadoComprobacion.Completada) faltantes.Add("consulta VirusTotal");
        var sandbox = envio.Resultado?.Sandbox;
        if (sandbox?.Estado != EstadoComprobacion.Completada || !sandbox.MuestraEjecutada ||
            !sandbox.ObservadorIniciado) faltantes.Add("ejecución y observación Sandbox");
        if (envio.Resultado?.Ia?.Estado != EstadoComprobacion.Completada) faltantes.Add("explicación de IA");
        return faltantes;
    }

    internal static string Resultado(EnvioCliente envio)
    {
        var informe = envio.Resultado;
        var local = Local(envio);
        if (informe?.Evaluacion == EvaluacionRiesgo.AmenazaDetectada ||
            local.Antivirus.Deteccion == DeteccionAntivirus.AmenazaDetectada ||
            local.Antivirus.Amenazas.Count > 0 || informe?.Reputacion?.MotoresMaliciosos > 0)
            return Amenaza;
        if (informe?.Evaluacion == EvaluacionRiesgo.Sospechoso ||
            informe?.Reputacion?.MotoresSospechosos > 0 ||
            local.FirmaDigital.Firma == EstadoFirmaDigital.Invalida) return Precaucion;
        if (informe?.Estado != EstadoAnalisis.Completado) return EnCurso;
        if (Faltantes(envio).Count > 0) return Pendientes;
        return SinAmenazas;
    }

    internal static string Procesamiento(EnvioCliente envio) => envio.Resultado?.Estado switch
    {
        EstadoAnalisis.Completado => "Análisis finalizado",
        EstadoAnalisis.EnProceso => "Análisis en curso",
        EstadoAnalisis.EnCola => "En cola de análisis",
        EstadoAnalisis.Fallido => "Análisis finalizado con errores",
        EstadoAnalisis.Cancelado => "Análisis cancelado",
        _ => "Análisis pendiente"
    };

    internal static string Informe(EnvioCliente envio) =>
        envio.Resultado?.Estado != EstadoAnalisis.Completado ? "Informe final aún no disponible"
        : Faltantes(envio).Count == 0 ? "Informe completo disponible"
        : "Informe generado con comprobaciones pendientes";

    internal static string Alcance(EnvioCliente envio)
    {
        var faltantes = Faltantes(envio);
        var s = envio.Resultado?.Sandbox;
        string historial = s?.AnalisisOrigenId is not null
            ? $"Observación Sandbox reutilizada del {s.FinUtc?.ToLocalTime():g}; no se ejecutó de nuevo para esta solicitud.\n"
            : "";
        return historial + (faltantes.Count == 0 ? "" : "Pendientes o fallidas: " + string.Join(", ", faltantes) + ".\n") +
            "La ausencia de amenazas reportadas no garantiza seguridad. " +
            "Sandbox observa el archivo durante un tiempo limitado y sin red; el detalle conserva sus limitaciones.";
    }

    internal static string EstadoArchivo(EnvioCliente envio)
    {
        if (envio.EstadoLocal.StartsWith("Archivo eliminado")) return "Eliminado";
        if (envio.EstadoLocal.StartsWith("Archivo liberado")) return "Liberado";
        if (Path.GetExtension(envio.RutaArchivo).Equals(".bytes", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(envio.RutaArchivo)) return "Retenido";
        return "Sin retención";
    }
}
