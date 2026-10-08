namespace SecureGate.Contratos;

// Indica si una comprobación pudo realizarse.
public enum EstadoComprobacion
{
    NoRealizada = 0,
    Completada = 1,
    Fallida = 2,
    NoDisponible = 3
}

// Resultado específico de la verificación de firma.
public enum EstadoFirmaDigital
{
    NoComprobada = 0,
    Valida = 1,
    SinFirma = 2,
    Invalida = 3,
    Indeterminada = 4
}

// Resultado específico del antivirus.
public enum DeteccionAntivirus
{
    NoDeterminada = 0,
    SinDetecciones = 1,
    AmenazaDetectada = 2
}

public sealed class ResultadoFirmaDigital
{
    public EstadoComprobacion Estado { get; set; }
        = EstadoComprobacion.NoRealizada;

    public EstadoFirmaDigital Firma { get; set; }
        = EstadoFirmaDigital.NoComprobada;

    public string? Firmante { get; set; }

    public string Detalle { get; set; } = string.Empty;

    public string? CodigoError { get; set; }
}

public sealed class ResultadoAntivirus
{
    public EstadoComprobacion Estado { get; set; }
        = EstadoComprobacion.NoRealizada;

    public DeteccionAntivirus Deteccion { get; set; }
        = DeteccionAntivirus.NoDeterminada;

    public string Motor { get; set; } = string.Empty;

    public List<string> Amenazas { get; set; } = new();

    public string Detalle { get; set; } = string.Empty;

    public string? CodigoError { get; set; }
}

// Reúne los resultados obtenidos en la computadora del usuario.
public sealed class ResultadoAnalisisLocal
{
    public required InformacionArchivo Archivo { get; init; }

    public ResultadoFirmaDigital FirmaDigital { get; set; } = new();

    public ResultadoAntivirus Antivirus { get; set; } = new();

    public DateTimeOffset InicioUtc { get; init; }

    public DateTimeOffset? FinUtc { get; set; }
}