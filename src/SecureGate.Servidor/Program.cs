using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using SecureGate.Contratos;
using SecureGate.Servidor;

var builder = WebApplication.CreateBuilder(args);

// TLS local. No se abre un puerto HTTP.
var acceso = new AccesoCliente();
builder.WebHost.ConfigureKestrel(TransporteServidor.Configurar);

var opciones = builder.Configuration
    .GetSection("Servidor")
    .Get<OpcionesServidor>() ?? new OpcionesServidor();

if (opciones.TamanoMaximoArchivoBytes <= 0 ||
    opciones.TamanoMaximoArchivoBytes > 1024L * 1024 * 1024)
{
    throw new InvalidOperationException(
        "El límite de archivos debe ser mayor que cero y no superar 1 GiB.");
}

if (opciones.RetencionMuestrasDias is < 1 or > 365 ||
    opciones.TemporalesAbandonadosHoras is < 1 or > 168 ||
    opciones.IntervaloLimpiezaMinutos is < 5 or > 1440)
    throw new InvalidOperationException("Revisá los plazos de retención y limpieza del servidor.");

// Permitir el archivo y un margen para los datos de la solicitud.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize =
        opciones.TamanoMaximoArchivoBytes + 1024 * 1024;
});

builder.Services.Configure<FormOptions>(form =>
{
    form.MultipartBodyLengthLimit =
        opciones.TamanoMaximoArchivoBytes;

    form.ValueLengthLimit = 64 * 1024;
    form.ValueCountLimit = 10;
});

builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IOptions<OpcionesServidor>>(
    Options.Create(opciones));

builder.Services.AddSingleton<AlmacenMuestras>();
builder.Services.AddSingleton<RepositorioAnalisis>();
builder.Services.AddHttpClient(
    "VirusTotal",
    cliente =>
    {
        cliente.BaseAddress = new Uri("https://www.virustotal.com/api/v3/");
        cliente.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddSingleton<ClienteReputacion>(servicios =>
{
    var fabrica = servicios.GetRequiredService<IHttpClientFactory>();
    var configuracion = servicios.GetRequiredService<IConfiguration>();

    return new ClienteReputacion(
        fabrica.CreateClient("VirusTotal"),
        configuracion);
});
builder.Services.AddSingleton(OpcionesIa.DesdeEntorno());
builder.Services.AddHttpClient("Gemini", cliente =>
{
    cliente.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<ClienteGemini>(servicios =>
{
    var fabrica = servicios.GetRequiredService<IHttpClientFactory>();
    return new ClienteGemini(fabrica.CreateClient("Gemini"),
        servicios.GetRequiredService<OpcionesIa>());
});
builder.Services.AddSingleton<ColaAnalisis>();
builder.Services.AddHostedService<RecuperadorAnalisis>();
builder.Services.AddSingleton<SecureGate.Contratos.IAnalizadorSandbox, SecureGate.Sandbox.GestorSandbox>();
builder.Services.AddHostedService<ProcesadorAnalisis>();

// Coordinar el registro para evitar duplicados simultáneos.
builder.Services.AddSingleton(new SemaphoreSlim(1, 1));
builder.Services.AddSingleton<LimpiadorDatos>();
builder.Services.AddHostedService<ServicioLimpieza>();

var app = builder.Build();
app.Use((contexto, siguiente) => acceso.ProcesarAsync(contexto, siguiente));

var jsonEntrada = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    MaxDepth = 32
};

jsonEntrada.Converters.Add(new JsonStringEnumConverter());

var extensionesPermitidas = new HashSet<string>(
    StringComparer.OrdinalIgnoreCase)
{
    ".exe", ".msi", ".bat", ".cmd",
    ".ps1", ".com", ".scr", ".vbs"
};

app.MapGet("/", () => Results.Ok(new
{
    servicio = "SecureGate.Servidor",
    mensaje = "Servidor iniciado"
}));

app.MapGet("/api/estado", () => Results.Ok(new
{
    estado = "Disponible",
    fechaUtc = DateTimeOffset.UtcNow
}));

// Recibir una muestra y registrar su solicitud.
app.MapPost("/api/analisis", async Task<IResult> (
    HttpRequest peticion,
    AlmacenMuestras almacen,
    RepositorioAnalisis repositorio,
    ColaAnalisis cola,
    SemaphoreSlim controlRegistro,
    CancellationToken cancellationToken) =>
{
    bool bloqueoAdquirido = false;
    bool registroGuardado = false;
    string? rutaGuardada = null;

    try
    {
        if (peticion.ContentType is null ||
            !peticion.ContentType.StartsWith(
                "multipart/form-data",
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                codigo = "FORMATO_INVALIDO",
                mensaje = "Se requiere multipart/form-data."
            });
        }

        var formulario = await peticion.ReadFormAsync(
            cancellationToken);

        if (formulario.Count != 1 ||
            !formulario.ContainsKey("solicitud") ||
            formulario["solicitud"].Count != 1 ||
            formulario.Files.Count != 1 ||
            formulario.Files[0].Name != "archivo")
        {
            return Results.BadRequest(new
            {
                codigo = "CAMPOS_INVALIDOS",
                mensaje = "Enviá un campo solicitud y un archivo llamado archivo."
            });
        }

        var solicitud = JsonSerializer.Deserialize<SolicitudAnalisis>(
            formulario["solicitud"].ToString(),
            jsonEntrada);

        if (solicitud is null ||
            solicitud.SolicitudId == Guid.Empty ||
            solicitud.AnalisisLocal is null ||
            solicitud.AnalisisLocal.Archivo is null)
        {
            return Results.BadRequest(new
            {
                codigo = "SOLICITUD_INVALIDA",
                mensaje = "Faltan datos o identificadores."
            });
        }

        var info = solicitud.AnalisisLocal.Archivo;
        var archivoRecibido = formulario.Files[0];

        if (info.ArchivoId == Guid.Empty ||
            string.IsNullOrWhiteSpace(info.NombreOriginal) ||
            info.NombreOriginal.Length > 255 ||
            info.NombreOriginal.Contains('/') ||
            info.NombreOriginal.Contains('\\') ||
            info.NombreOriginal.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(info.Extension) ||
            !extensionesPermitidas.Contains(info.Extension) ||
            !string.Equals(
                Path.GetExtension(info.NombreOriginal),
                info.Extension,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                codigo = "ARCHIVO_NO_ADMITIDO",
                mensaje = "El nombre o la extensión no están permitidos."
            });
        }

        if (info.TamanoBytes <= 0 ||
            info.TamanoBytes > opciones.TamanoMaximoArchivoBytes ||
            archivoRecibido.Length != info.TamanoBytes ||
            string.IsNullOrWhiteSpace(info.Sha256) ||
            info.Sha256.Length != 64 ||
            !info.Sha256.All(Uri.IsHexDigit))
        {
            return Results.BadRequest(new
            {
                codigo = "DATOS_ARCHIVO_INVALIDOS",
                mensaje = "Revisá el tamaño y el SHA-256."
            });
        }

        await controlRegistro.WaitAsync(cancellationToken);
        bloqueoAdquirido = true;

        var existente = await repositorio.BuscarPorSolicitudAsync(
            solicitud.SolicitudId,
            cancellationToken);

        if (existente is not null)
        {
            var anterior = existente.Resultado.Archivo;

            bool coincide =
                anterior.ArchivoId == info.ArchivoId &&
                anterior.TamanoBytes == info.TamanoBytes &&
                anterior.NombreOriginal == info.NombreOriginal &&
                string.Equals(
                    anterior.Extension,
                    info.Extension,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    anterior.Sha256,
                    info.Sha256,
                    StringComparison.OrdinalIgnoreCase);

            if (!coincide)
            {
                return Results.Conflict(new
                {
                    codigo = "SOLICITUD_REUTILIZADA",
                    mensaje = "Ese identificador ya pertenece a otro archivo."
                });
            }

            if (existente.Resultado.Estado is
                EstadoAnalisis.Pendiente or
                EstadoAnalisis.EnCola or
                EstadoAnalisis.EnProceso)
            {
                // Encolar solo si no está esperando o siendo atendido.
                if (cola.Encolar(existente.Resultado.AnalisisId))
                {
                    app.Logger.LogInformation(
                        "ENCOLADO: {AnalisisId}",
                        existente.Resultado.AnalisisId);
                }
            }

            // Devolver el trabajo previo sin crear otra muestra.
            return Results.Accepted(
                $"/api/analisis/{existente.Resultado.AnalisisId}",
                CrearRespuesta(existente));
        }

        await using var contenido = archivoRecibido.OpenReadStream();

        rutaGuardada = await almacen.GuardarAsync(
            contenido,
            info,
            cancellationToken);

        var resultado = new ResultadoAnalisis
        {
            AnalisisId = Guid.NewGuid(),
            Archivo = info,
            AnalisisLocal = solicitud.AnalisisLocal,
            Estado = EstadoAnalisis.EnCola,
            Evaluacion = EvaluacionRiesgo.Incompleto,
            Resumen = "Solicitud registrada. En espera de procesamiento.",
            FechaCreacionUtc = DateTimeOffset.UtcNow
        };

        var trabajo = new TrabajoAnalisis
        {
            SolicitudId = solicitud.SolicitudId,
            RutaMuestraServidor = rutaGuardada,
            Resultado = resultado
        };

        await repositorio.GuardarAsync(
            trabajo,
            cancellationToken);

        registroGuardado = true;

        if (cola.Encolar(resultado.AnalisisId))
        {
            app.Logger.LogInformation(
                "ENCOLADO: {AnalisisId}",
                resultado.AnalisisId);
        }

        return Results.Accepted(
            $"/api/analisis/{resultado.AnalisisId}",
            CrearRespuesta(trabajo));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new
        {
            codigo = "JSON_INVALIDO",
            mensaje = "Los datos de la solicitud no tienen el formato esperado."
        });
    }
    catch (InvalidDataException error)
    {
        Console.WriteLine("RECEPCION RECHAZADA: " + error.Message);

        return Results.BadRequest(new
        {
            codigo = "RECEPCION_RECHAZADA",
            mensaje = error.Message
        });
    }
    catch (BadHttpRequestException error)
    {
        return Results.Problem(
            title: "Petición rechazada",
            statusCode: error.StatusCode);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception error)
    {
        app.Logger.LogError(error, "Falló el registro del análisis.");

        return Results.Problem(
            title: "No se pudo registrar el análisis.",
            statusCode: 500);
    }
    finally
    {
        if (!registroGuardado && rutaGuardada is not null)
        {
            try
            {
                File.Delete(rutaGuardada);
            }
            catch (Exception errorLimpieza)
            {
                app.Logger.LogWarning(
                    errorLimpieza,
                    "No se pudo retirar una muestra sin registro.");
            }
        }

        if (bloqueoAdquirido)
            controlRegistro.Release();
    }
});

// Consultar el estado y el resultado público.
app.MapGet("/api/analisis/{id:guid}", async Task<IResult> (
    Guid id,
    RepositorioAnalisis repositorio,
    CancellationToken cancellationToken) =>
{
    try
    {
        var trabajo = await repositorio.ObtenerAsync(
            id,
            cancellationToken);

        if (trabajo is null)
        {
            return Results.NotFound(new
            {
                codigo = "ANALISIS_NO_ENCONTRADO",
                mensaje = "No existe ese análisis."
            });
        }

        return Results.Ok(trabajo.Resultado);
    }
    catch (OperationCanceledException)
        when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception error)
    {
        app.Logger.LogError(error, "Falló la consulta del análisis.");

        return Results.Problem(
            title: "No se pudo consultar el análisis.",
            statusCode: 500);
    }
});

app.Run();

static AnalisisAceptado CrearRespuesta(TrabajoAnalisis trabajo)
{
    return new AnalisisAceptado
    {
        SolicitudId = trabajo.SolicitudId,
        AnalisisId = trabajo.Resultado.AnalisisId,
        ArchivoId = trabajo.Resultado.Archivo.ArchivoId,
        Estado = trabajo.Resultado.Estado,
        FechaRegistroUtc = trabajo.Resultado.FechaCreacionUtc,
        FechaEliminacionMuestraServidorUtc = trabajo.Resultado.FechaEliminacionMuestraServidorUtc
    };
}
