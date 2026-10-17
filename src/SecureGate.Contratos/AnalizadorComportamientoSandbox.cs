using System.Text.RegularExpressions;

namespace SecureGate.Contratos;

public sealed record HallazgoComportamiento(string Codigo, string Descripcion, IReadOnlyList<Guid> Eventos);

// Señales de precaución, no veredictos de malware. Los datos del invitado no son resistentes a manipulación.
public static class AnalizadorComportamientoSandbox
{
    public static IReadOnlyList<HallazgoComportamiento> Analizar(ResultadoAnalisis resultado)
    {
        var s = resultado.Sandbox;
        if (s is not { ObservadorIniciado: true, MuestraEjecutada: true, RedHabilitada: false } ||
            s.AnalisisId != resultado.AnalisisId || s.ArchivoId != resultado.Archivo.ArchivoId ||
            !string.Equals(s.Sha256, resultado.Archivo.Sha256, StringComparison.OrdinalIgnoreCase)) return [];
        if (s.InicioUtc is null || s.Eventos is null) return [];
        var hallazgos = new List<HallazgoComportamiento>();
        var eventos = s.Eventos.Where(e => e.EventoId != Guid.Empty && e.FechaUtc >= s.InicioUtc &&
            (s.FinUtc is null || e.FechaUtc <= s.FinUtc)).Take(2000).ToList();
        var raiz = eventos.FirstOrDefault(e => e.Tipo == TipoEventoSandbox.ProcesoIniciado &&
            e.EsProcesoMuestra && e.ProcesoId is > 0 &&
            string.Equals(e.Recurso, @"C:\Prueba\muestra.exe", StringComparison.OrdinalIgnoreCase));
        if (raiz is null) return [];
        var atribuibles = new HashSet<int> { raiz.ProcesoId!.Value };
        // Una cadena de padres documentada es necesaria además de la etiqueta del observador.
        foreach (var e in eventos.OrderBy(e => e.FechaUtc))
        {
            if (e.Tipo != TipoEventoSandbox.ProcesoIniciado || e.EsProcesoMuestra || !e.AtribuidoAMuestra ||
                e.ProcesoId is not > 0 || e.ProcesoPadreId is not > 0 ||
                !atribuibles.Contains(e.ProcesoPadreId.Value)) continue;
            atribuibles.Add(e.ProcesoId.Value);
            string nombre = (e.NombreProceso ?? "").ToLowerInvariant();
            string orden = (e.LineaComandos ?? "");
            if (orden.Length > 2048) continue;
            void Agregar(string codigo, string descripcion) => hallazgos.Add(new(codigo, descripcion, new[] { raiz.EventoId, e.EventoId }));
            if ((nombre is "powershell.exe" or "pwsh.exe") &&
                Regex.IsMatch(orden, @"(?i)(?:^|\s)-(?:encodedcommand|enc|enco|encod|encode|encoded|encodedc|encodedco|encodedcom|encodedcomm|encodedcomma|encodedcomman)\s+\S+", RegexOptions.CultureInvariant))
                Agregar("SG-B001", "Un proceso descendiente de la muestra inició PowerShell con una instrucción codificada. Puede ocultar el contenido de la orden; requiere revisión, pero no demuestra malware.");
            if ((nombre == "vssadmin.exe" && Regex.IsMatch(orden, @"(?i)\bdelete\s+shadows\b")) ||
                (nombre == "wevtutil.exe" && Regex.IsMatch(orden, @"(?i)\s(?:cl|clear-log)\s")) ||
                (nombre == "wbadmin.exe" && Regex.IsMatch(orden, @"(?i)\bdelete\s+(?:catalog|backup)\b")))
                Agregar("SG-B003", "Un proceso descendiente de la muestra solicitó borrar registros o copias de recuperación. Se observó la orden; no se confirma que tuviera éxito. Requiere revisión.");
        }
        foreach (var e in eventos.Where(e => e.Tipo == TipoEventoSandbox.RegistroModificado && e.CambioRegistroObservado))
        {
            if (!(e.Recurso ?? "").StartsWith(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\", StringComparison.OrdinalIgnoreCase) &&
                !(e.Recurso ?? "").StartsWith(@"HKCU\Software\Microsoft\Windows\CurrentVersion\RunOnce\", StringComparison.OrdinalIgnoreCase)) continue;
            if ((e.DatosRegistro?.Length ?? 0) <= 4096 && Regex.IsMatch(e.DatosRegistro ?? "", "(?i)^\\s*(?:\"C:\\\\Prueba\\\\muestra\\.exe\"|C:\\\\Prueba\\\\muestra\\.exe)(?:\\s|$)"))
                hallazgos.Add(new("SG-B002", "Se observó un cambio en el inicio automático del usuario que apunta a la muestra ejecutada. El destino se relaciona con la muestra; el autor de la modificación no está confirmado. Requiere revisión.", new[] { raiz.EventoId, e.EventoId }));
        }
        return hallazgos.DistinctBy(h => (h.Codigo, h.Eventos.Last())).ToList();
    }
}
