using SecureGate.Contratos;

namespace SecureGate.Proteccion;

public sealed class GestorCuarentena : IGestorCuarentena
{
    private readonly AlmacenCuarentena _almacen;
    public GestorCuarentena() : this(null) { }
    public GestorCuarentena(string? carpeta) => _almacen = new(carpeta);

    public async Task<ResultadoOperacionArchivo> RetenerAsync(string rutaArchivo,
        CancellationToken cancellationToken = default)
    {
        await using var sesion = await _almacen.AbrirSesionAsync(cancellationToken);
        string origen = Path.GetFullPath(rutaArchivo);
        AlmacenCuarentena.ComprobarRuta(origen);
        string relativa = Path.GetRelativePath(_almacen.Carpeta, origen);
        if (!Path.IsPathRooted(relativa) && relativa != ".." &&
            !relativa.StartsWith(".." + Path.DirectorySeparatorChar))
            throw new InvalidDataException("No se vuelve a retener una muestra de la cuarentena.");
        if (!File.Exists(origen))
        {
            foreach (var id in _almacen.Identificadores())
            {
                var anterior = await _almacen.RecuperarAsync(id, cancellationToken);
                if (anterior.Estado == EstadoArchivo.Retenido &&
                    anterior.RutaOriginal.Equals(origen, StringComparison.OrdinalIgnoreCase))
                    return Resultado(anterior, true, "Archivo ya retenido; se conserva el identificador.");
            }
            throw new FileNotFoundException("No se encontro el archivo original.", origen);
        }
        PermisosCuarentena.ValidarOrigen(origen, _almacen.Carpeta);
        var info = new FileInfo(origen);
        var r = new RegistroCuarentena
        {
            ArchivoId = Guid.NewGuid(), RutaOriginal = origen,
            SddlOriginal = PermisosCuarentena.CapturarOriginal(origen),
            NombreOriginal = info.Name, Extension = info.Extension,
            TamanoBytes = info.Length,
            Sha256 = await AlmacenCuarentena.HashAsync(origen, cancellationToken),
            FechaRegistroUtc = DateTimeOffset.UtcNow,
            FechaActualizacionUtc = DateTimeOffset.UtcNow
        };
        if (r.TamanoBytes <= 0) throw new InvalidDataException("Archivo vacio.");
        _almacen.Guardar(r); // Registrar antes de mover; permite recuperar un cierre intermedio.
        cancellationToken.ThrowIfCancellationRequested();
        // Denegar ejecucion directa antes de mover; la deteccion previa sigue teniendo un intervalo.
        PermisosCuarentena.RestringirArchivo(origen);
        File.Move(origen, _almacen.Muestra(r.ArchivoId), overwrite: false);
        await _almacen.ValidarAsync(r, cancellationToken);
        PermisosCuarentena.RestringirArchivo(_almacen.Muestra(r.ArchivoId));
        r.PermisosAplicados = true;
        r.Estado = EstadoArchivo.Retenido;
        r.OperacionPendiente = "";
        r.FechaActualizacionUtc = DateTimeOffset.UtcNow;
        _almacen.Guardar(r);
        return Resultado(r, true, "Archivo retenido con restriccion de ejecucion directa NTFS. No implica bloqueo global.");
    }
    public async Task<IReadOnlyList<RegistroCuarentena>> RecuperarPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var sesion = await _almacen.AbrirSesionAsync(cancellationToken);
        var pendientes = new List<RegistroCuarentena>();
        foreach (var id in _almacen.Identificadores())
        {
            var r = await _almacen.RecuperarAsync(id, cancellationToken);
            if (r.Estado != EstadoArchivo.Eliminado && r.Estado != EstadoArchivo.Liberado)
                pendientes.Add(r);
        }
        return pendientes;
    }
    public async Task<ResultadoOperacionArchivo> EliminarAsync(Guid archivoId,
        CancellationToken cancellationToken = default)
    {
        await using var sesion = await _almacen.AbrirSesionAsync(cancellationToken);
        var r = await _almacen.RecuperarAsync(archivoId, cancellationToken);
        if (r.Estado == EstadoArchivo.Eliminado)
            return Resultado(r, false, "El archivo ya fue eliminado.", "ARCHIVO_YA_ELIMINADO");
        if (r.Estado != EstadoArchivo.Retenido)
            return Resultado(r, false, "No se puede eliminar en este estado.", "ESTADO_NO_ADMITIDO");
        await _almacen.ValidarAsync(r, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        r.OperacionPendiente = "Eliminar";
        _almacen.Guardar(r);
        File.Delete(_almacen.Muestra(archivoId));
        r.Estado = EstadoArchivo.Eliminado;
        r.OperacionPendiente = "";
        r.FechaActualizacionUtc = DateTimeOffset.UtcNow;
        _almacen.Guardar(r);
        return Resultado(r, true, "Muestra eliminada; se conserva el registro.");
    }
    public async Task<ResultadoOperacionArchivo> LiberarAsync(SolicitudLiberacion solicitud,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var sesion = await _almacen.AbrirSesionAsync(cancellationToken);
        var r = await _almacen.RecuperarAsync(solicitud.ArchivoId, cancellationToken);
        if (r.Estado != EstadoArchivo.Retenido)
            return Resultado(r, false, "El archivo no esta retenido.", "ESTADO_NO_ADMITIDO");
        if (!r.Sha256.Equals(solicitud.Sha256Esperado, StringComparison.OrdinalIgnoreCase))
            return Resultado(r, false, "El hash esperado no coincide.", "HASH_NO_COINCIDE");
        // Un motivo enviado por la interfaz no demuestra autorizacion ni evaluacion favorable.
        return Resultado(r, false,
            "Liberacion deshabilitada hasta integrar autorizacion y evaluacion verificables.",
            "LIBERACION_NO_AUTORIZADA");
    }
    private static ResultadoOperacionArchivo Resultado(RegistroCuarentena r, bool realizada,
        string mensaje, string? codigo = null) => new()
    {
        ArchivoId = r.ArchivoId, Archivo = r.Informacion(), Realizada = realizada,
        EstadoActual = r.Estado, Mensaje = mensaje, CodigoError = codigo
    };
}
