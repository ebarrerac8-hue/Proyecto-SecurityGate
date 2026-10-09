using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Contratos;

namespace SecureGate.Cliente;

/// <summary>Cliente reutilizable para el servidor propio de SecureGate.</summary>
public sealed class ClienteAnalisisHttp : IClienteAnalisis, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _servidor;
    private readonly string _token;
    private readonly bool _propietarioHttp;
    private static readonly JsonSerializerOptions Json = CrearJson();

    public ClienteAnalisisHttp(Uri servidor)
        : this(new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AllowAutoRedirect = false
        }) { Timeout = Timeout.InfiniteTimeSpan }, servidor, true) { }

    /// <summary>No modifica ni elimina el HttpClient recibido.</summary>
    public ClienteAnalisisHttp(HttpClient http, Uri servidor, string? token = null)
        : this(http, servidor, false, token) { }

    private ClienteAnalisisHttp(HttpClient http, Uri servidor, bool propietario, string? token = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(servidor);
        if (!servidor.IsAbsoluteUri ||
            servidor.Scheme != "https" ||
            !string.IsNullOrEmpty(servidor.UserInfo) ||
            !string.IsNullOrEmpty(servidor.Query) ||
            !string.IsNullOrEmpty(servidor.Fragment))
            throw new ArgumentException("La dirección del servidor debe ser HTTPS sin credenciales, consulta ni fragmento.");

        _token = (token ?? Environment.GetEnvironmentVariable("SECUREGATE_CLIENT_TOKEN") ?? "").Trim();
        if (_token.Length != 44 || !_token.All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '/' or '='))
            throw new InvalidOperationException("No se configuró una clave de acceso válida para SecureGate.");
        _http = http;
        _servidor = new Uri(servidor.AbsoluteUri.TrimEnd('/') + "/");
        _propietarioHttp = propietario;
    }

    // Un envío individual. El stream pertenece a quien llama y queda abierto.
    public async Task<AnalisisAceptado> EnviarAsync(
        SolicitudAnalisis solicitud, Stream contenidoArchivo,
        CancellationToken cancellationToken = default)
    {
        ValidarSolicitud(solicitud);
        ArgumentNullException.ThrowIfNull(contenidoArchivo);
        if (!contenidoArchivo.CanRead)
            throw new ArgumentException("El contenido del archivo no permite lectura.");

        var archivo = solicitud.AnalisisLocal.Archivo;
        if (contenidoArchivo.CanSeek &&
            contenidoArchivo.Length - contenidoArchivo.Position != archivo.TamanoBytes)
            throw new ArgumentException("El tamaño disponible del stream no coincide con la solicitud.");

        using var formulario = new MultipartFormDataContent();
        formulario.Add(new StringContent(
            JsonSerializer.Serialize(solicitud, Json), Encoding.UTF8, "application/json"),
            "solicitud");

        // StreamContent elimina su stream al liberarse.
        // Este envoltorio evita eliminar el stream del llamador.
        formulario.Add(new StreamContent(new StreamPrestado(contenidoArchivo)),
            "archivo", archivo.NombreOriginal);

        using var peticion = new HttpRequestMessage(
            HttpMethod.Post, new Uri(_servidor, "api/analisis"))
        {
            Content = formulario
        };

        var aceptado = await EjecutarAsync<AnalisisAceptado>(
            peticion, TimeSpan.FromMinutes(5), HttpStatusCode.Accepted, cancellationToken);

        if (aceptado.AnalisisId == Guid.Empty ||
            aceptado.SolicitudId != solicitud.SolicitudId ||
            aceptado.ArchivoId != archivo.ArchivoId ||
            !Enum.IsDefined(aceptado.Estado))
            throw RespuestaInvalida("La aceptación no corresponde a la solicitud enviada.");

        return aceptado;
    }

    // Reabre el archivo en cada intento y conserva la misma solicitud.
    public async Task<AnalisisAceptado> EnviarArchivoAsync(
        SolicitudAnalisis solicitud, string rutaArchivo,
        CancellationToken cancellationToken = default)
    {
        ValidarSolicitud(solicitud);
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaArchivo);

        // Copia estable: las modificaciones externas no cambian un reintento.
        var copia = JsonSerializer.Deserialize<SolicitudAnalisis>(
            JsonSerializer.Serialize(solicitud, Json), Json)!;

        return await ConReintentosAsync(async ct =>
        {
            await using var archivo = new FileStream(rutaArchivo, FileMode.Open,
                FileAccess.Read, FileShare.Read, 81920, true);
            return await EnviarAsync(copia, archivo, ct);
        }, cancellationToken);
    }

    public async Task<ResultadoAnalisis> ConsultarAsync(
        Guid analisisId, CancellationToken cancellationToken = default)
    {
        if (analisisId == Guid.Empty)
            throw new ArgumentException("El identificador de análisis está vacío.");

        using var peticion = new HttpRequestMessage(HttpMethod.Get,
            new Uri(_servidor, "api/analisis/" + analisisId.ToString("D")));

        var resultado = await EjecutarAsync<ResultadoAnalisis>(
            peticion, TimeSpan.FromSeconds(30), HttpStatusCode.OK, cancellationToken);

        if (resultado.AnalisisId != analisisId || resultado.Archivo is null ||
            resultado.Archivo.ArchivoId == Guid.Empty ||
            string.IsNullOrWhiteSpace(resultado.Archivo.Sha256) ||
            resultado.Archivo.Sha256.Length != 64 ||
            !resultado.Archivo.Sha256.All(Uri.IsHexDigit) ||
            resultado.Motivos is null || resultado.Limitaciones is null ||
            !Enum.IsDefined(resultado.Estado) || !Enum.IsDefined(resultado.Evaluacion))
            throw RespuestaInvalida("El resultado recibido no corresponde al análisis solicitado.");

        return resultado;
    }

    /// <summary>
    /// Espera un estado terminal. Fallido y Cancelado se devuelven con su informe.
    /// Cancelar esta espera no cancela el trabajo que ya recibió el servidor.
    /// </summary>
    public async Task<ResultadoAnalisis> EsperarResultadoAsync(
        Guid analisisId, IProgress<ResultadoAnalisis>? progreso = null,
        TimeSpan? tiempoMaximo = null, CancellationToken cancellationToken = default)
    {
        var duracion = tiempoMaximo ?? TimeSpan.FromMinutes(10);
        if (duracion <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(tiempoMaximo));

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(duracion);

        try
        {
            while (true)
            {
                var resultado = await ConReintentosAsync(
                    ct => ConsultarAsync(analisisId, ct), limite.Token);
                progreso?.Report(resultado);

                if (resultado.Estado is EstadoAnalisis.Completado or
                    EstadoAnalisis.Fallido or EstadoAnalisis.Cancelado)
                    return resultado;

                await Task.Delay(TimeSpan.FromSeconds(2), limite.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ErrorClienteAnalisis("ESPERA_AGOTADA",
                "Se agotó la espera. El trabajo puede seguir en el servidor; conserve su AnalisisId.");
        }
    }

    private async Task<T> EjecutarAsync<T>(HttpRequestMessage peticion,
        TimeSpan tiempo, HttpStatusCode esperado, CancellationToken cancellationToken)
        where T : class
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limite.CancelAfter(tiempo);
        try
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var respuesta = await _http.SendAsync(
                peticion, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            string texto = await LeerContenidoAsync(respuesta.Content, limite.Token);

            if (!respuesta.IsSuccessStatusCode)
                throw CrearErrorHttp(respuesta.StatusCode, texto);

            if (respuesta.StatusCode != esperado)
                throw RespuestaInvalida("El servidor devolvió un código HTTP inesperado.");

            try
            {
                return JsonSerializer.Deserialize<T>(texto, Json)
                    ?? throw RespuestaInvalida("El servidor devolvió un resultado vacío.");
            }
            catch (JsonException error)
            {
                throw new ErrorClienteAnalisis("RESPUESTA_INVALIDA",
                    "La respuesta del servidor no tiene el formato esperado.",
                    innerException: error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException error)
        {
            throw new ErrorClienteAnalisis("TIEMPO_AGOTADO",
                "El servidor no respondió dentro del tiempo permitido.",
                reintentable: true, innerException: error);
        }
        catch (HttpRequestException error)
        {
            throw new ErrorClienteAnalisis("SERVIDOR_SIN_CONEXION",
                "No se pudo conectar con el servidor de SecureGate.",
                reintentable: true, innerException: error);
        }
        catch (IOException error)
        {
            throw new ErrorClienteAnalisis("TRANSMISION_INTERRUMPIDA",
                "La transmisión se interrumpió antes de completar la operación.",
                reintentable: true, innerException: error);
        }
    }

    private static async Task<T> ConReintentosAsync<T>(
        Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        for (int intento = 1; ; intento++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await operacion(ct); }
            catch (ErrorClienteAnalisis error) when (error.Reintentable && intento < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(intento), ct);
            }
        }
    }

    private static async Task<string> LeerContenidoAsync(HttpContent contenido, CancellationToken ct)
    {
        const int maximo = 16 * 1024 * 1024;
        await using var stream = await contenido.ReadAsStreamAsync(ct);
        using var memoria = new MemoryStream();
        byte[] buffer = new byte[8192];
        int cantidad;
        while ((cantidad = await stream.ReadAsync(buffer.AsMemory(), ct)) != 0)
        {
            if (memoria.Length + cantidad > maximo)
                throw RespuestaInvalida("La respuesta del servidor supera el tamaño permitido.");
            memoria.Write(buffer, 0, cantidad);
        }
        return Encoding.UTF8.GetString(memoria.ToArray());
    }

    private static ErrorClienteAnalisis CrearErrorHttp(HttpStatusCode estado, string texto)
    {
        string codigo = "HTTP_" + (int)estado;
        string mensaje = estado switch
        {
            HttpStatusCode.Conflict => "La solicitud ya pertenece a otro archivo.",
            HttpStatusCode.NotFound => "No se encontró el análisis solicitado.",
            HttpStatusCode.RequestEntityTooLarge => "El archivo supera el tamaño admitido.",
            _ => "El servidor rechazó la operación (HTTP " + (int)estado + ")."
        };

        try
        {
            using var json = JsonDocument.Parse(texto);
            var raiz = json.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("codigo", out var c) && c.ValueKind == JsonValueKind.String)
                    codigo = c.GetString() ?? codigo;
                if (raiz.TryGetProperty("mensaje", out var m) && m.ValueKind == JsonValueKind.String)
                    mensaje = m.GetString() ?? mensaje;
                else if (raiz.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                    mensaje = t.GetString() ?? mensaje;
            }
        }
        catch (JsonException) { }

        bool reintentable = estado is HttpStatusCode.RequestTimeout or
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
        return new ErrorClienteAnalisis(codigo, mensaje, estado, reintentable);
    }

    private static void ValidarSolicitud(SolicitudAnalisis solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var archivo = solicitud.AnalisisLocal?.Archivo;
        if (solicitud.SolicitudId == Guid.Empty || archivo is null ||
            archivo.ArchivoId == Guid.Empty || archivo.TamanoBytes <= 0 ||
            string.IsNullOrWhiteSpace(archivo.NombreOriginal) ||
            archivo.NombreOriginal.Contains('/') || archivo.NombreOriginal.Contains('\\') ||
            archivo.NombreOriginal.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(archivo.Sha256) ||
            archivo.Sha256.Length != 64 || !archivo.Sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("La solicitud no contiene datos válidos del archivo.");
    }

    private static ErrorClienteAnalisis RespuestaInvalida(string mensaje) =>
        new("RESPUESTA_INVALIDA", mensaje);

    private static JsonSerializerOptions CrearJson()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 64 };
        opciones.Converters.Add(new JsonStringEnumConverter());
        return opciones;
    }

    public void Dispose()
    {
        if (_propietarioHttp) _http.Dispose();
    }

    private sealed class StreamPrestado(Stream origen) : Stream
    {
        public override bool CanRead => origen.CanRead;
        public override bool CanSeek => origen.CanSeek;
        public override bool CanWrite => false;
        public override long Length => origen.Length;
        public override long Position { get => origen.Position; set => origen.Position = value; }
        public override int Read(byte[] buffer, int offset, int count) => origen.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            origen.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            origen.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin inicio) => origen.Seek(offset, inicio);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { }
    }
}
