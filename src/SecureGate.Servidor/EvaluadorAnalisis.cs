using SecureGate.Contratos;

namespace SecureGate.Servidor;

public static class EvaluadorAnalisis
{
    public static void Evaluar(ResultadoAnalisis resultado)
    {
        resultado.Evaluacion = EvaluacionRiesgo.Incompleto;
        resultado.Motivos.Clear();
        bool amenaza = false;
        bool sospechoso = false;

        var antivirus = resultado.AnalisisLocal?.Antivirus;
        if (antivirus?.Estado == EstadoComprobacion.Completada &&
            antivirus.Deteccion == DeteccionAntivirus.AmenazaDetectada)
        {
            amenaza = true;
            resultado.Motivos.Add("El antivirus local reportó una amenaza.");
        }

        var reputacion = resultado.Reputacion;
        bool informeValido = reputacion is not null &&
            reputacion.Estado == EstadoComprobacion.Completada &&
            reputacion.InformeEncontrado == true &&
            string.Equals(reputacion.Sha256, resultado.Archivo.Sha256,
                StringComparison.OrdinalIgnoreCase);

        if (informeValido)
        {
            if (reputacion!.MotoresMaliciosos is > 0)
            {
                amenaza = true;
                resultado.Motivos.Add(
                    $"VirusTotal reportó {reputacion.MotoresMaliciosos} motores maliciosos.");
            }
            if (reputacion.MotoresSospechosos is > 0)
            {
                sospechoso = true;
                resultado.Motivos.Add(
                    $"VirusTotal reportó {reputacion.MotoresSospechosos} motores sospechosos.");
            }
        }

        var firma = resultado.AnalisisLocal?.FirmaDigital;
        if (firma?.Estado == EstadoComprobacion.Completada)
        {
            if (firma.Firma == EstadoFirmaDigital.Invalida)
            {
                sospechoso = true;
                resultado.Motivos.Add("La firma digital del archivo no es válida.");
            }
            else if (firma.Firma == EstadoFirmaDigital.SinFirma &&
                     EsEjecutable(resultado.Archivo.Extension))
            {
                sospechoso = true;
                resultado.Motivos.Add(
                    "El ejecutable no tiene firma digital. Requiere precaución; " +
                    "esto por sí solo no demuestra que sea malicioso.");
            }
        }

        if (amenaza)
        {
            resultado.Evaluacion = EvaluacionRiesgo.AmenazaDetectada;
            resultado.Resumen =
                "Se reportaron detecciones de amenaza. Le recomendamos no ejecutar ni instalar el archivo.";
        }
        else if (sospechoso)
        {
            resultado.Evaluacion = EvaluacionRiesgo.Sospechoso;
            resultado.Resumen =
                "Se encontraron señales que requieren precaución. " +
                "Le recomendamos no ejecutar ni instalar el archivo hasta completar la revisión.";
        }
        else if (resultado.Sandbox is { Estado: EstadoComprobacion.Completada,
                      ObservadorIniciado: true, MuestraEjecutada: true } sandbox &&
                 sandbox.AnalisisId == resultado.AnalisisId &&
                 sandbox.ArchivoId == resultado.Archivo.ArchivoId &&
                 string.Equals(sandbox.Sha256, resultado.Archivo.Sha256,
                     StringComparison.OrdinalIgnoreCase))
        {
            resultado.Resumen =
                "Se realizó una observación limitada en Sandbox. " +
                "La evaluación sigue incompleta: los eventos registrados no bastan para aprobar el archivo.";
        }
        else if (resultado.Sandbox is not null)
        {
            resultado.Resumen =
                "No se pudo completar la prueba en Sandbox. " +
                "La evaluación está incompleta; consulte las limitaciones del informe.";
        }
        else
        {
            resultado.Resumen = informeValido &&
                reputacion!.MotoresMaliciosos == 0 && reputacion.MotoresSospechosos == 0
                ? "VirusTotal no reportó detecciones maliciosas ni sospechosas. " +
                  "La evaluación sigue incompleta: falta la prueba en Sandbox."
                : "La evaluación está incompleta. Todavía falta la prueba en Sandbox.";
        }
    }

    private static bool EsEjecutable(string extension) =>
        extension.ToLowerInvariant() is ".exe" or ".msi" or ".bat" or ".cmd" or
            ".ps1" or ".com" or ".scr" or ".vbs";
}
