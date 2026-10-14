using System.Security.Cryptography;
using System.Text.Json;
using SecureGate.Contratos;

namespace SecureGate.Servidor;

public static class PoliticaReutilizacionSandbox
{
    public const string VersionActual = "securegate-sandbox-v1";

    public static TrabajoAnalisis? Seleccionar(IEnumerable<TrabajoAnalisis> trabajos,
        TrabajoAnalisis actual, OpcionesServidor opciones, DateTimeOffset ahora)
    {
        if (!opciones.ReutilizacionSandboxHabilitada ||
            opciones.VigenciaSandboxDias is < 1 or > 90 ||
            opciones.InformeRecienteHoras is < 1 or > 168 ||
            actual.Resultado.Reputacion is not { Estado: EstadoComprobacion.Completada } rep ||
            !Igual(rep.Sha256, actual.Resultado.Archivo.Sha256) ||
            rep.FechaConsultaUtc is not { } consulta || consulta > ahora.AddMinutes(5) ||
            ahora - consulta > TimeSpan.FromMinutes(5) ||
            rep.MotoresMaliciosos is > 0 || rep.MotoresSospechosos is > 0 ||
            actual.Resultado.AnalisisLocal is not { } local ||
            local.FirmaDigital.Estado != EstadoComprobacion.Completada ||
            local.FirmaDigital.Firma != EstadoFirmaDigital.Valida ||
            local.Antivirus.Estado != EstadoComprobacion.Completada ||
            local.Antivirus.Deteccion != DeteccionAntivirus.SinDetecciones ||
            local.Antivirus.Amenazas.Count != 0 ||
            local.FinUtc is not { } finLocal || finLocal > ahora.AddMinutes(5) ||
            ahora - finLocal > TimeSpan.FromHours(24) ||
            !IdentidadLocal(actual.Resultado)) return null;

        return trabajos.Where(t =>
        {
            var r = t.Resultado;
            var s = r.Sandbox;
            if (r.AnalisisId == actual.Resultado.AnalisisId ||
                r.Estado != EstadoAnalisis.Completado ||
                r.Evaluacion is EvaluacionRiesgo.AmenazaDetectada or EvaluacionRiesgo.Sospechoso ||
                t.VersionObservacionSandbox != VersionActual ||
                t.SegundosObservacionSandbox != opciones.TiempoObservacionSegundos ||
                !Igual(r.Archivo.Sha256, actual.Resultado.Archivo.Sha256) ||
                r.Archivo.TamanoBytes != actual.Resultado.Archivo.TamanoBytes ||
                !Igual(r.Archivo.Extension, actual.Resultado.Archivo.Extension) ||
                !IdentidadLocal(r) ||
                r.AnalisisLocal!.FirmaDigital.Estado != EstadoComprobacion.Completada ||
                r.AnalisisLocal.FirmaDigital.Firma != EstadoFirmaDigital.Valida ||
                r.Reputacion is not { Estado: EstadoComprobacion.Completada } reputacionAnterior ||
                !Igual(reputacionAnterior.Sha256, r.Archivo.Sha256) ||
                reputacionAnterior.MotoresMaliciosos is > 0 || reputacionAnterior.MotoresSospechosos is > 0 ||
                r.AnalisisLocal.Antivirus.Estado != EstadoComprobacion.Completada ||
                r.AnalisisLocal.Antivirus.Deteccion != DeteccionAntivirus.SinDetecciones ||
                r.AnalisisLocal.Antivirus.Amenazas.Count != 0 ||
                s is not { Estado: EstadoComprobacion.Completada, MuestraEjecutada: true,
                    ObservadorIniciado: true, RedHabilitada: false } ||
                s.CodigoError is not null ||
                s.AnalisisId != r.AnalisisId || s.ArchivoId != r.Archivo.ArchivoId ||
                !Igual(s.Sha256, r.Archivo.Sha256) ||
                s.InicioUtc is not { } inicio || s.FinUtc is not { } fin ||
                inicio >= fin || fin > ahora || ahora - fin > TimeSpan.FromDays(opciones.VigenciaSandboxDias))
                return false;
            // El reloj de vigencia es el de la observación original, no el de su última copia.
            if (ahora - fin > TimeSpan.FromHours(opciones.InformeRecienteHoras) &&
                (rep.InformeEncontrado != true || rep.MotoresMaliciosos != 0 || rep.MotoresSospechosos != 0))
                return false;
            return true;
        }).OrderByDescending(t => t.Resultado.Sandbox!.FinUtc).FirstOrDefault();
    }

    public static InformeSandbox Copiar(TrabajoAnalisis origen, TrabajoAnalisis destino, DateTimeOffset ahora)
    {
        var s = origen.Resultado.Sandbox ?? throw new InvalidOperationException("Falta observación de origen.");
        return new InformeSandbox
        {
            AnalisisId = destino.Resultado.AnalisisId,
            ArchivoId = destino.Resultado.Archivo.ArchivoId,
            Sha256 = destino.Resultado.Archivo.Sha256,
            Estado = s.Estado, MuestraEjecutada = s.MuestraEjecutada,
            ObservadorIniciado = s.ObservadorIniciado, RedHabilitada = s.RedHabilitada,
            InicioUtc = s.InicioUtc, FinUtc = s.FinUtc, CodigoError = s.CodigoError,
            Eventos = JsonSerializer.Deserialize<List<EventoSandbox>>(JsonSerializer.Serialize(s.Eventos))!,
            Limitaciones = new List<string>(s.Limitaciones),
            AnalisisOrigenId = s.AnalisisOrigenId ?? origen.Resultado.AnalisisId,
            ArchivoOrigenId = s.ArchivoOrigenId ?? origen.Resultado.Archivo.ArchivoId,
            NombreArchivoOrigen = s.NombreArchivoOrigen ?? origen.Resultado.Archivo.NombreOriginal,
            FechaReutilizacionUtc = ahora
        };
    }

    public static async Task ValidarMuestraAsync(TrabajoAnalisis trabajo, CancellationToken ct)
    {
        string ruta = trabajo.RutaMuestraServidor;
        if ((File.GetAttributes(ruta) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("La reutilización no admite muestras enlazadas.");
        await using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        if (archivo.Length != trabajo.Resultado.Archivo.TamanoBytes)
            throw new InvalidDataException("El tamaño de la muestra cambió.");
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(archivo, ct));
        if (!Igual(hash, trabajo.Resultado.Archivo.Sha256))
            throw new InvalidDataException("El SHA-256 de la muestra no corresponde a la solicitud.");
    }

    private static bool IdentidadLocal(ResultadoAnalisis r) =>
        r.AnalisisLocal is { } l && l.Archivo.ArchivoId == r.Archivo.ArchivoId &&
        l.Archivo.TamanoBytes == r.Archivo.TamanoBytes && Igual(l.Archivo.Sha256, r.Archivo.Sha256);
    private static bool Igual(string? a, string? b) => !string.IsNullOrWhiteSpace(a) &&
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
