namespace SecureGate.Contratos;

// Indica en qué etapa está el trabajo de análisis.
public enum EstadoAnalisis
{
    Pendiente = 0,
    EnCola = 1,
    EnProceso = 2,
    Completado = 3,
    Fallido = 4,
    Cancelado = 5
}

// Indica qué se concluyó sobre el archivo.
public enum EvaluacionRiesgo
{
    Incompleto = 0,
    SinIndicadoresDetectados = 1,
    Sospechoso = 2,
    AmenazaDetectada = 3
}