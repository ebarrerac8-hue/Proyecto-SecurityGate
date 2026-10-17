using SecureGate.Contratos;

namespace SecureGate.Servidor;

public static class PreparadorEvidenciasIa
{
    public static List<EvidenciaIa> Preparar(ResultadoAnalisis resultado)
    {
        var lista = new List<EvidenciaIa>();
        void Agregar(string tipo, string descripcion) => lista.Add(new EvidenciaIa
        {
            Id = "E" + (lista.Count + 1).ToString("D4"), Tipo = tipo, Descripcion = descripcion
        });

        Agregar("Reglas", "Evaluación fijada por el servidor: " + resultado.Evaluacion +
            ". La IA explica evidencias y no concede permisos de ejecución ni liberación. " +
            "Una evaluación Incompleto no equivale a una amenaza detectada ni determina por sí sola si puede liberarse. " +
            "La liberación excepcional es una acción separada del cliente: exige análisis terminado, identidad e integridad " +
            "verificadas y aceptación explícita del riesgo en un segundo diálogo. No solicita contraseñas. " +
            "Puede devolver un archivo con señales de riesgo solo si el usuario confirma la excepción; no lo ejecuta. " +
            "No declara seguro el archivo ni cambia la evaluación. " +
            "Este informe no demuestra que esa autorización se haya solicitado o concedido.");
        Agregar("Archivo", $"Extensión: {resultado.Archivo.Extension}; tamaño: {resultado.Archivo.TamanoBytes} bytes.");
        var local = resultado.AnalisisLocal;
        Agregar("Firma", local is null ? "Sin análisis local." :
            $"Comprobación: {local.FirmaDigital.Estado}; firma: {local.FirmaDigital.Firma}.");
        Agregar("Antivirus", local is null ? "Sin análisis local." :
            $"Comprobación: {local.Antivirus.Estado}; detección: {local.Antivirus.Deteccion}.");
        var rep = resultado.Reputacion;
        Agregar("Reputacion", rep is null ? "No se consultó reputación." :
            $"Estado: {rep.Estado}; informe encontrado: {rep.InformeEncontrado}; " +
            $"motores maliciosos: {rep.MotoresMaliciosos}; sospechosos: {rep.MotoresSospechosos}; " +
            $"sin detección: {rep.MotoresSinDeteccion}. " +
            "Informe encontrado=False significa sin información, no un análisis limpio.");
        var sandbox = resultado.Sandbox;
        Agregar("Sandbox", sandbox is null ? "No se realizó prueba Sandbox." :
            $"Estado: {sandbox.Estado}; muestra ejecutada: {sandbox.MuestraEjecutada}; " +
            $"observador iniciado: {sandbox.ObservadorIniciado}; red: {sandbox.RedHabilitada}; " +
            $"inicio: {sandbox.InicioUtc:o}; fin: {sandbox.FinUtc:o}; eventos: {sandbox.Eventos.Count}.");
        if (sandbox?.DemostracionComportamiento == true)
            Agregar("Demostracion", "Laboratorio explícito: cmd.exe de Windows con acciones benignas predeterminadas. No es malware ni un instalador; no generalizar esta demostración a otros archivos.");
        Agregar("Cobertura", "Comprobaciones locales y reputación: " +
            $"firma={local?.FirmaDigital.Estado}; antivirus={local?.Antivirus.Estado}; reputación={rep?.Estado}. " +
            "Incompleto no implica que falten comprobaciones: puede expresar límites de la observación. No inventar capas faltantes.");
        foreach (var h in AnalizadorComportamientoSandbox.Analizar(resultado).Take(12))
            Agregar("Comportamiento", $"Regla {h.Codigo}: {h.Descripcion} Eventos observados: {string.Join(", ", h.Eventos)}.");
        if (sandbox?.AnalisisOrigenId is { } origen)
            Agregar("ReutilizacionSandbox", $"Observación histórica reutilizada; análisis de origen: {origen}; " +
                $"archivo de origen: {NombreMinimo(sandbox.NombreArchivoOrigen)}; reutilización: {sandbox.FechaReutilizacionUtc:o}. " +
                "No se ejecutó de nuevo el archivo para esta solicitud. Sus fechas y eventos corresponden a la observación original.");
        Agregar("Limitacion",
            "La observación es parcial. Compara únicamente Run/RunOnce del usuario, no todo el registro ni toda la red; puede perder procesos breves. " +
            "Los eventos de archivo no identifican su autor. Las evidencias del invitado no son resistentes a manipulación.");
        Agregar("Limitacion",
            "Ausencia de detecciones no demuestra seguridad. Una demostración de acciones predeterminadas no es un análisis de un instalador.");
        if (sandbox is not null)
        {
            // Conservar algunos eventos de cada tipo para que el ruido no ocupe toda la solicitud.
            var seleccion = sandbox.Eventos.GroupBy(e => e.Tipo)
                .SelectMany(g => g.OrderBy(e => e.FechaUtc).Take(20))
                .OrderBy(e => e.FechaUtc).Take(120).ToList();
            foreach (var e in seleccion)
                Agregar("Evento", $"{e.FechaUtc:o}; tipo: {e.Tipo}; PID: {e.ProcesoId}; " +
                    $"proceso: {NombreMinimo(e.NombreProceso)}; recurso: {NombreMinimo(e.Recurso)}. " +
                    "No se atribuye este evento a la muestra sin evidencia adicional.");
            if (seleccion.Count < sandbox.Eventos.Count)
                Agregar("Limitacion", $"Se enviaron {seleccion.Count} de {sandbox.Eventos.Count} eventos. " +
                    "El informe completo permanece en el servidor.");
        }
        return lista;
    }

    private static string NombreMinimo(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "(no identificado)";
        // No enviar directorios, URL, argumentos ni textos libres del observador.
        if (texto.Contains("://", StringComparison.Ordinal)) return "(URL omitida)";
        string nombre = texto.Replace('\\', '/').Split('/').Last();
        nombre = new string(nombre.Where(c => !char.IsControl(c)).Take(100).ToArray());
        return string.IsNullOrWhiteSpace(nombre) ? "(omitido)" : nombre;
    }
}
