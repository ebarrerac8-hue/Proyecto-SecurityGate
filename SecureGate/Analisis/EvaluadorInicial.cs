using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace SecureGate.Analisis
{
    // 1. Clase resultado de tu evaluación
    public class ResultadoEvaluacion
    {
        public bool RequiereAtencion { get; set; }
        public string NivelRiesgo { get; set; } = string.Empty; // "Seguro", "Sospechoso", "Peligroso"
        public string Motivo { get; set; } = string.Empty;
    }

    // 2. Simulación temporal de la Reputación (hasta que se integre el cliente TCP)
    public class Reputacion
    {
        public bool EsMalicioso { get; set; }
        public bool EsDesconocido { get; set; }
        public string Detalles { get; set; } = string.Empty;
    }

    // 3. Módulo principal
    public class EvaluadorInicial
    {
        // Lista de extensiones que el proyecto considera de interés/riesgo
        private readonly List<string> extensionesRiesgosas = new List<string>
        {
            ".exe", ".msi", ".bat", ".ps1", ".vbs", ".cmd"
        };
        public ResultadoEvaluacion Evaluar(InformacionArchivo infoArchivo, Reputacion reputacion = null)
        {
            ResultadoEvaluacion resultado = new ResultadoEvaluacion();

            // Aún no hay conexión TCP, asumimos que el archivo es desconocido en la red
            if (reputacion == null)
            {
                reputacion = new Reputacion { EsDesconocido = true, EsMalicioso = false };
            }

            bool esExtensionRiesgosa = extensionesRiesgosas.Contains(infoArchivo.Extension);

            // Regla A: El servidor externo dice que es un virus/malware
            if (reputacion.EsMalicioso)
            {
                resultado.RequiereAtencion = true;
                resultado.NivelRiesgo = "Peligroso";
                resultado.Motivo = "El servidor de reputación marcó este archivo como malicioso.";
            }
            // Regla B: Es un ejecutable o script y NO tiene firma digital (Muy común en descargas peligrosas)
            else if (esExtensionRiesgosa && !infoArchivo.TieneFirma)
            {
                resultado.RequiereAtencion = true;
                resultado.NivelRiesgo = "Sospechoso";
                resultado.Motivo = $"El archivo es un {infoArchivo.Extension} sin firma digital. Alto riesgo de ejecución.";
            }
            // Regla C: Es ejecutable, tiene firma, pero nadie lo conoce (es nuevo o raro)
            else if (esExtensionRiesgosa && reputacion.EsDesconocido)
            {
                resultado.RequiereAtencion = true;
                resultado.NivelRiesgo = "Sospechoso";
                resultado.Motivo = "Archivo con reputación desconocida en la red. Se recomienda probar en Windows Sandbox.";
            }
            // Regla D: Parece seguro (está firmado por una entidad y no hay reportes negativos)
            else
            {
                resultado.RequiereAtencion = false;
                resultado.NivelRiesgo = "Seguro";
                resultado.Motivo = infoArchivo.TieneFirma
                    ? $"Archivo firmado válidamente por {infoArchivo.Firmante}."
                    : "El formato de archivo no representa un riesgo de ejecución directa.";
            }

            return resultado;
        }
    }
}