using Seguregate;
namespace SecureGate.Analisis;

public class ResultadoEvaluacion
{
    public bool RequiereAtencion { get; set; }
    public string NivelRiesgo { get; set; } = "";
    public string Motivo { get; set; } = "";
}

public class EvaluadorInicial
{
    public ResultadoEvaluacion Evaluar(InformacionArchivo archivo, ResultadoReputacion reputacion)
    {
        if (reputacion.Estado != EstadoConsulta.InformeDisponible)
            return Crear("Confianza no determinada", reputacion.Mensaje, true);
        if (reputacion.Maliciosos > 0)
            return Crear("Detecciones de malware", "VirusTotal registra detecciones. Se recomienda no ejecutar el archivo en tu computadora.", true);
        if (reputacion.Sospechosos > 0)
            return Crear("Sospechoso", "VirusTotal registra detecciones sospechosas. Se recomienda no ejecutarlo en tu computadora.", true);
        if (!archivo.TieneFirma)
            return Crear("Requiere revisión", "Sin detecciones reportadas, pero no se pudo identificar un certificado de firma. Esto no demuestra que sea malware ni que sea seguro.", true);
        return Crear("Sin detecciones reportadas", "El informe consultado no registra detecciones de malware o sospechosas. Hay un certificado de firma, cuya validez no se ha comprobado. No es una garantía de seguridad.", false);
    }
    private static ResultadoEvaluacion Crear(string nivel, string motivo, bool atencion) =>
        new() { NivelRiesgo = nivel, Motivo = motivo, RequiereAtencion = atencion };
}
