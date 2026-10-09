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
            ". La IA no autoriza ejecutar ni liberar el archivo.");
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
            $"sin detección: {rep.MotoresSinDeteccion}.");
        var sandbox = resultado.Sandbox;
        Agregar("Sandbox", sandbox is null ? "No se realizó prueba Sandbox." :
            $"Estado: {sandbox.Estado}; muestra ejecutada: {sandbox.MuestraEjecutada}; " +
            $"observador iniciado: {sandbox.ObservadorIniciado}; red: {sandbox.RedHabilitada}; " +
            $"inicio: {sandbox.InicioUtc:o}; fin: {sandbox.FinUtc:o}; eventos: {sandbox.Eventos.Count}.");
        Agregar("Limitacion",
            "La observación es parcial. No comprueba todo el registro, toda la red ni todos los procesos breves. " +
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

