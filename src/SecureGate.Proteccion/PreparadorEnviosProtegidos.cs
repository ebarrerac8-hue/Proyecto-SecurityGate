using SecureGate.Cliente;
using SecureGate.Contratos;

namespace SecureGate.Proteccion;

// Puente entre cuarentena y transporte. No realiza consultas de red por si mismo.
public sealed class PreparadorEnviosProtegidos
{
    public event Action<string, string>? RestauracionPendiente;
    private readonly AlmacenCuarentena _almacen;
    private readonly GestorCuarentena _gestor;
    private readonly AnalizadorLocal _analizador;
    private readonly AlmacenEnviosCliente _envios;
    private static readonly HashSet<string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".com", ".scr", ".vbs" };

    public PreparadorEnviosProtegidos(AlmacenEnviosCliente? envios = null,
        string? carpetaCuarentena = null)
    {
        _envios = envios ?? new AlmacenEnviosCliente();
        _almacen = new(carpetaCuarentena);
        _gestor = new(carpetaCuarentena);
        _analizador = new(carpetaCuarentena);
    }
    private static string Servidor(string servidor)
    {
        if (!Uri.TryCreate(servidor, UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Servidor HTTPS sin credenciales, consulta ni fragmento requerido.");
        return uri.AbsoluteUri.TrimEnd('/') + "/";
    }
    public async Task<ResultadoOperacionArchivo> RetenerRutaAsync(string ruta,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        string original = Path.GetFullPath(ruta);
        if (!Extensiones.Contains(Path.GetExtension(original)))
            throw new InvalidDataException("Extension no admitida para el envio.");
        if (File.Exists(original))
        {
            long tamano = new FileInfo(original).Length;
            if (tamano <= 0 || tamano > 100L * 1024 * 1024)
                throw new InvalidDataException("Se admiten archivos de 1 byte hasta 100 MiB.");
        }
        return await _gestor.RetenerAsync(original, ct);
    }
    public async Task<EnvioCliente> PrepararAsync(string ruta, string servidor,
        CancellationToken ct = default)
    {
        string normalizado = Servidor(servidor);
        var retenido = await RetenerRutaAsync(ruta, ct);
        return await PrepararRetenidoAsync(retenido.ArchivoId, normalizado, ct);
    }
    public async Task<EnvioCliente> PrepararRetenidoAsync(Guid archivoId, string servidor,
        CancellationToken ct = default)
    {
        string normalizado = Servidor(servidor);
        await using var sesion = await _almacen.AbrirSesionAsync(ct, ".preparacion-envios.lock");
        return await PrepararRegistroAsync(archivoId, normalizado, ct);
    }
    private async Task<EnvioCliente> PrepararRegistroAsync(Guid archivoId, string servidor,
        CancellationToken ct)
    {
        RegistroCuarentena r;
        await using (var sesion = await _almacen.AbrirSesionAsync(ct))
            r = await _almacen.RecuperarAsync(archivoId, ct);
        if (r.Estado != EstadoArchivo.Retenido)
            throw new InvalidOperationException("La muestra no esta retenida.");
        if (!Extensiones.Contains(r.Extension) || r.TamanoBytes > 100L * 1024 * 1024)
            throw new InvalidDataException("La muestra registrada no es admitida por el servidor.");

        var existentes = await _envios.ListarAsync(ct);
        var mismo = existentes.FirstOrDefault(e =>
            e.Solicitud.AnalisisLocal.Archivo.ArchivoId == archivoId &&
            Servidor(e.Servidor) == servidor);
        if (mismo is not null)
        {
            var a = mismo.Solicitud.AnalisisLocal.Archivo;
            if (a.Sha256 != r.Sha256 || a.TamanoBytes != r.TamanoBytes ||
                a.NombreOriginal != r.NombreOriginal || a.Extension != r.Extension ||
                !Path.GetFullPath(mismo.RutaArchivo).Equals(_almacen.Muestra(archivoId),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El envio guardado no coincide con la muestra retenida.");
            return mismo; // Conservar solicitud, resultado y fechas; no repetir comprobaciones.
        }
        var local = await _analizador.AnalizarAsync(archivoId, ct);
        var envio = new EnvioCliente
        {
            Solicitud = new SolicitudAnalisis
            { SolicitudId = Guid.NewGuid(), AnalisisLocal = local },
            RutaArchivo = _almacen.Muestra(archivoId), Servidor = servidor,
            EstadoLocal = "Retenido; preparado para envio", DecisionUsuarioPendiente = true
        };
        // Antes de cualquier POST. Un cierre posterior conserva la misma SolicitudId.
        await _envios.GuardarAsync(envio, ct);
        return envio;
    }
    public async Task<IReadOnlyList<EnvioCliente>> RecuperarAsync(string servidor,
        CancellationToken ct = default)
    {
        string normalizado = Servidor(servidor);
        await using var sesion = await _almacen.AbrirSesionAsync(ct, ".preparacion-envios.lock");
        foreach (var id in _almacen.Identificadores())
        {
            var previo = _almacen.Leer(id);
            if (previo.OperacionPendiente == "Liberar")
                RestauracionPendiente?.Invoke(previo.RutaOriginal, previo.Sha256);
        }
        var registros = await _gestor.RecuperarPendientesAsync(ct);
        var recuperados = new List<EnvioCliente>();
        foreach (var r in registros)
        {
            // Nunca enviar un registro incompleto o sin muestra confirmada.
            if (r.Estado != EstadoArchivo.Retenido)
                throw new InvalidDataException("Retencion sin terminar: requiere revisar el registro " + r.ArchivoId);
            // Si ya existe un envio para otro servidor, respetar su destino original.
            var anteriores = (await _envios.ListarAsync(ct)).Where(e =>
                e.Solicitud.AnalisisLocal.Archivo.ArchivoId == r.ArchivoId).ToList();
            if (anteriores.Count > 0)
                foreach (var previo in anteriores)
                    recuperados.Add(await PrepararRegistroAsync(r.ArchivoId, Servidor(previo.Servidor), ct));
            else
                recuperados.Add(await PrepararRegistroAsync(r.ArchivoId, normalizado, ct));
        }
        return recuperados;
    }
}

