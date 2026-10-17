using SecureGate.Contratos;

namespace SecureGate.Proteccion;

public enum TipoResultadoDecision { SinAmenazasReportadas, Riesgo, Incompleto, NoFinalizado }

// Una presentación favorable exige evidencias completas; Incompleto técnico no se reescribe.
public static class PoliticaDecisionAnalisis
{
    public static TipoResultadoDecision Clasificar(ResultadoAnalisis r)
    {
        var local = r.AnalisisLocal;
        if (AnalizadorComportamientoSandbox.Analizar(r).Count > 0) return TipoResultadoDecision.Riesgo;
        if (r.Evaluacion == EvaluacionRiesgo.AmenazaDetectada ||
            r.Evaluacion == EvaluacionRiesgo.Sospechoso ||
            local?.Antivirus.Deteccion == DeteccionAntivirus.AmenazaDetectada ||
            local?.Antivirus.Amenazas.Count > 0 || r.Reputacion?.MotoresMaliciosos > 0 ||
            r.Reputacion?.MotoresSospechosos > 0 || local?.FirmaDigital.Firma == EstadoFirmaDigital.Invalida)
            return TipoResultadoDecision.Riesgo;
        if (r.Estado != EstadoAnalisis.Completado) return TipoResultadoDecision.NoFinalizado;
        if (!Enum.IsDefined(r.Evaluacion) || local?.Antivirus.Estado != EstadoComprobacion.Completada ||
            local.Antivirus.Deteccion != DeteccionAntivirus.SinDetecciones ||
            local.FirmaDigital.Estado != EstadoComprobacion.Completada ||
            local.FirmaDigital.Firma is not (EstadoFirmaDigital.Valida or EstadoFirmaDigital.SinFirma) ||
            r.Reputacion?.Estado != EstadoComprobacion.Completada ||
            r.Sandbox?.Estado != EstadoComprobacion.Completada || !r.Sandbox.MuestraEjecutada ||
            !r.Sandbox.ObservadorIniciado || r.Sandbox.RedHabilitada ||
            r.Ia?.Estado != EstadoComprobacion.Completada)
            return TipoResultadoDecision.Incompleto;
        return TipoResultadoDecision.SinAmenazasReportadas;
    }

    internal static void ValidarIdentidad(ResultadoAnalisis r, Guid archivo, Guid analisis, string hash)
    {
        if (archivo == Guid.Empty || analisis == Guid.Empty || hash.Length != 64 || !hash.All(Uri.IsHexDigit) ||
            r.AnalisisId != analisis || r.Archivo.ArchivoId != archivo ||
            !string.Equals(r.Archivo.Sha256, hash, StringComparison.OrdinalIgnoreCase) ||
            r.Estado != EstadoAnalisis.Completado || !Enum.IsDefined(r.Evaluacion) ||
            r.AnalisisLocal?.Archivo.ArchivoId != archivo ||
            !string.Equals(r.AnalisisLocal.Archivo.Sha256, hash, StringComparison.OrdinalIgnoreCase) ||
            r.AnalisisLocal.Archivo.TamanoBytes != r.Archivo.TamanoBytes ||
            !string.Equals(r.AnalisisLocal.Archivo.Extension, r.Archivo.Extension, StringComparison.OrdinalIgnoreCase) ||
            !Enum.IsDefined(r.AnalisisLocal.Antivirus.Estado) || !Enum.IsDefined(r.AnalisisLocal.Antivirus.Deteccion))
            throw new InvalidDataException("El informe no corresponde a un análisis terminado de este archivo.");
        if (r.Reputacion is { } reputacion && !string.Equals(reputacion.Sha256, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("La reputación corresponde a otro archivo.");
        if (r.Sandbox is { } sandbox && (sandbox.AnalisisId != analisis || sandbox.ArchivoId != archivo ||
            !string.Equals(sandbox.Sha256, hash, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("La observación corresponde a otro archivo.");
    }
}
