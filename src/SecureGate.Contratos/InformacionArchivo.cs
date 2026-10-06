namespace SecureGate.Contratos;

public sealed class InformacionArchivo
{
    public Guid ArchivoId { get; init; }

    public required string NombreOriginal { get; init; }

    public required string Extension { get; init; }

    public long TamanoBytes { get; init; }

    public required string Sha256 { get; init; }

    public DateTimeOffset FechaRegistroUtc { get; init; }
}