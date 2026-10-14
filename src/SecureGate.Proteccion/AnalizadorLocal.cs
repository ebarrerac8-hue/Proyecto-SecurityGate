using SecureGate.Contratos;

namespace SecureGate.Proteccion;

public sealed class AnalizadorLocal : IAnalizadorLocal
{
    private readonly AlmacenCuarentena _almacen;
    public AnalizadorLocal() : this(null) { }
    public AnalizadorLocal(string? carpeta) => _almacen = new(carpeta);

    public async Task<ResultadoAnalisisLocal> AnalizarAsync(Guid archivoId,
        CancellationToken cancellationToken = default)
    {
        var inicio = DateTimeOffset.UtcNow;
        await using var sesion = await _almacen.AbrirSesionAsync(cancellationToken);
        var r = await _almacen.RecuperarAsync(archivoId, cancellationToken);
        if (r.Estado != EstadoArchivo.Retenido)
            throw new InvalidOperationException("El archivo no esta retenido para analizar.");
        var firma = await VerificadorFirmaDigital.VerificarAsync(
            _almacen.Muestra(archivoId), r.Extension, cancellationToken);
        await _almacen.ValidarAsync(r, cancellationToken);
        var antivirus = await AnalizadorDefender.AnalizarAsync(
            _almacen.Muestra(archivoId), cancellationToken);
        await _almacen.ValidarAsync(r, cancellationToken);
        return new ResultadoAnalisisLocal
        {
            Archivo = r.Informacion(), InicioUtc = inicio, FinUtc = DateTimeOffset.UtcNow,
            FirmaDigital = firma,
            Antivirus = antivirus
        };
    }
}

