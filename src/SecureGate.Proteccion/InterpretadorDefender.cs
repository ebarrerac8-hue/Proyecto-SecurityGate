using System.Text.RegularExpressions;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

internal static class InterpretadorDefender
{
    // -ReturnHR devuelve un HRESULT, no el codigo simplificado 0/2.
    // Sin un mensaje reconocible de la prueba, el resultado queda indeterminado.
    internal static ResultadoAntivirus Interpretar(int codigo, string salida,
        string errores, bool truncada)
    {
        if (truncada)
            return Error("DEFENDER_SALIDA_EXTENSA", "La salida excedio el limite; no se puede interpretar completa.");
        var nombres = Regex.Matches(salida,
            @"(?im)^\s*(?:Threat\s*Name|ThreatName|Threat)\s*[:=]\s*(\S[^\r\n]*)$")
            .Select(m => m.Groups[1].Value.Trim())
            .Where(n => Regex.IsMatch(n, @"^[A-Za-z][A-Za-z0-9]*:[^\s]+/[^\s]+$"))
            .Distinct().ToList();
        bool sinAmenazas = Regex.IsMatch(salida,
            @"(?im)^\s*Scanning\s+.+\s+found no threats\.\s*$");
        string hr = "0x" + unchecked((uint)codigo).ToString("X8");
        if (nombres.Count > 0 && !sinAmenazas)
            return new ResultadoAntivirus
            {
                Motor = "Microsoft Defender", Estado = EstadoComprobacion.Completada,
                Deteccion = DeteccionAntivirus.AmenazaDetectada,
                Amenazas = nombres.Select(n => n.Length > 300 ? n[..300] : n).Take(50).ToList(),
                Detalle = "Defender reporto amenazas en su salida. HRESULT: " + hr +
                    ". No se autorizo la liberacion del archivo."
            };
        if (codigo == 0 && sinAmenazas && nombres.Count == 0 &&
            string.IsNullOrWhiteSpace(errores) &&
            !Regex.IsMatch(salida, @"(?im)^\s*(?:ERROR|WARNING)\b"))
            return new ResultadoAntivirus
            {
                Motor = "Microsoft Defender", Estado = EstadoComprobacion.Completada,
                Deteccion = DeteccionAntivirus.SinDetecciones,
                Detalle = "Escaneo personalizado completado sin detecciones. No garantiza seguridad."
            };
        return Error(codigo == 0 ? "DEFENDER_RESPUESTA_NO_RECONOCIDA" : "DEFENDER_ESCANEO_ERROR",
            "No se confirmo un resultado de escaneo interpretable. HRESULT: " + hr +
            ". Salida: " + Resumen(salida) + ". Errores: " + Resumen(errores));
    }

    private static string Resumen(string texto) => texto.Length > 1500 ? texto[..1500] : texto;
    internal static ResultadoAntivirus Error(string codigo, string detalle,
        EstadoComprobacion estado = EstadoComprobacion.Fallida) => new()
    {
        Motor = "Microsoft Defender", Estado = estado,
        Deteccion = DeteccionAntivirus.NoDeterminada, CodigoError = codigo, Detalle = detalle
    };
}
