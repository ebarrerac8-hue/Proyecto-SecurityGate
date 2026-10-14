using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate;
using SecureGate.Cliente;
using SecureGate.Contratos;
using SecureGate.Proteccion;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        PruebasAsync().GetAwaiter().GetResult();
        ApplicationConfiguration.Initialize();
        ProbarDialogos();
        Console.WriteLine("Pruebas de decisión y diálogos aprobadas; sin claves, malware ni apertura real de ejecutables.");
    }

    private static void Exigir(bool condicion, string detalle)
    {
        if (!condicion) throw new InvalidOperationException(detalle);
        Console.WriteLine("CORRECTO: " + detalle);
    }

    private static ResultadoAnalisis Informe(InformacionArchivo archivo, Guid id) => new()
    {
        Archivo = archivo, AnalisisId = id, Estado = EstadoAnalisis.Completado, Evaluacion = EvaluacionRiesgo.Incompleto,
        AnalisisLocal = new ResultadoAnalisisLocal
        {
            Archivo = archivo,
            Antivirus = new() { Estado = EstadoComprobacion.Completada, Deteccion = DeteccionAntivirus.SinDetecciones },
            FirmaDigital = new() { Estado = EstadoComprobacion.Completada, Firma = EstadoFirmaDigital.Valida }
        },
        Reputacion = new() { Sha256 = archivo.Sha256, Estado = EstadoComprobacion.Completada, MotoresMaliciosos = 0, MotoresSospechosos = 0 },
        Sandbox = new() { AnalisisId = id, ArchivoId = archivo.ArchivoId, Sha256 = archivo.Sha256,
            Estado = EstadoComprobacion.Completada, MuestraEjecutada = true, ObservadorIniciado = true, RedHabilitada = false },
        Ia = new() { Estado = EstadoComprobacion.Completada }
    };

    private static async Task PruebasAsync()
    {
        string raiz = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecureGate", "PruebasDecisiones", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);
        var gestor = new GestorCuarentena(Path.Combine(raiz, "Cuarentena"));
        string origen = Path.Combine(raiz, "Inerte.exe");
        await File.WriteAllTextAsync(origen, "Contenido inerte para probar movimientos; no es un ejecutable.");
        var retenido = await gestor.RetenerAsync(origen);
        var archivo = retenido.Archivo!;
        var id = Guid.NewGuid();
        var informe = Informe(archivo, id);
        var servidor = new ServidorSimulado(() => informe);
        using var http = new HttpClient(servidor);
        using var cliente = new ClienteAnalisisHttp(http, new Uri("https://pruebas.invalid/"), new string('a', 44));
        Exigir(PoliticaDecisionAnalisis.Clasificar(informe) == TipoResultadoDecision.SinAmenazasReportadas &&
            informe.Evaluacion == EvaluacionRiesgo.Incompleto, "presentación favorable no cambia la evaluación técnica.");
        var cancelada = await AutorizacionLiberacion.SolicitarConfirmacionAsync(cliente, archivo.ArchivoId, id, archivo.Sha256,
            false, _ => false);
        Exigir(cancelada is null && servidor.Consultas == 1 && !File.Exists(origen), "cancelar conserva retención y no crea permiso.");

        informe.AnalisisLocal!.Antivirus.Deteccion = DeteccionAntivirus.AmenazaDetectada;
        Exigir(PoliticaDecisionAnalisis.Clasificar(informe) == TipoResultadoDecision.Riesgo,
            "detección local no queda oculta por Incompleto.");
        bool confirmo = false;
        try
        {
            await AutorizacionLiberacion.SolicitarConfirmacionAsync(cliente, archivo.ArchivoId, id, archivo.Sha256,
                false, _ => { confirmo = true; return true; });
            throw new Exception("Se esperaba rechazo.");
        }
        catch (UnauthorizedAccessException) { }
        Exigir(!confirmo && !File.Exists(origen), "riesgo no se libera por la ruta normal.");
        informe.AnalisisLocal.Antivirus.Deteccion = DeteccionAntivirus.SinDetecciones;
        informe.Ia!.Estado = EstadoComprobacion.Fallida;
        Exigir(PoliticaDecisionAnalisis.Clasificar(informe) == TipoResultadoDecision.Incompleto,
            "capas fallidas no generan mensaje sin amenazas.");
        informe.Ia.Estado = EstadoComprobacion.Completada;

        servidor.Respuesta = () => Informe(archivo, id);
        bool cambio = false;
        try
        {
            await AutorizacionLiberacion.SolicitarConfirmacionAsync(cliente, archivo.ArchivoId, id, archivo.Sha256,
                false, _ => { servidor.Respuesta = () => { var r = Informe(archivo, id); r.Evaluacion = EvaluacionRiesgo.AmenazaDetectada; return r; }; return true; });
        }
        catch (UnauthorizedAccessException) { cambio = true; }
        Exigir(cambio && !File.Exists(origen), "reconsulta detecta un resultado cambiado después de decidir.");
        servidor.Respuesta = () => Informe(archivo, id);
        int antes = servidor.Consultas;
        var permiso = await AutorizacionLiberacion.SolicitarConfirmacionAsync(cliente, archivo.ArchivoId, id, archivo.Sha256, false, _ => true);
        Exigir(permiso is not null && servidor.Consultas == antes + 2, "decisión explícita reconsulta sin pedir credenciales Windows.");
        await File.WriteAllTextAsync(origen, "No sobrescribir");
        var ocupado = await gestor.LiberarAutorizadoAsync(permiso!);
        Exigir(!ocupado.Realizada && ocupado.CodigoError == "DESTINO_OCUPADO" && await File.ReadAllTextAsync(origen) == "No sobrescribir",
            "destino ocupado conserva ambos archivos.");
        File.Delete(origen);
        var liberado = await gestor.LiberarAutorizadoAsync(permiso!);
        var registro = await gestor.ConsultarRegistroAsync(archivo.ArchivoId);
        Exigir(liberado.Realizada && registro.Estado == EstadoArchivo.Liberado && registro.MetodoAutorizacion == "ConfirmacionExplicita" &&
            registro.EvaluacionAlLiberar == EvaluacionRiesgo.Incompleto, "devolver conserva evaluación y registra la decisión.");
        var otra = await gestor.LiberarAutorizadoAsync(permiso!);
        Exigir(!otra.Realizada, "una autorización no devuelve dos veces.");
        bool usada = false;
        try { permiso!.Consumir(registro); } catch (UnauthorizedAccessException) { usada = true; }
        Exigir(usada, "capacidad consumida no se reutiliza.");

        int aperturas = 0;
        AbridorArchivoLiberado.AbrirVerificado(registro, _ => aperturas++);
        Exigir(aperturas == 1, "abrir verifica identidad y bytes con ejecutor simulado.");
        await File.AppendAllTextAsync(origen, "Cambio");
        bool alterado = false;
        try { AbridorArchivoLiberado.AbrirVerificado(registro, _ => aperturas++); }
        catch (InvalidDataException) { alterado = true; }
        Exigir(alterado && aperturas == 1, "archivo alterado no se abre.");

        string riesgoOrigen = Path.Combine(raiz, "RiesgoSimulado.exe");
        await File.WriteAllTextAsync(riesgoOrigen, "Contenido inerte con informe de amenaza SIMULADO.");
        var riesgoRetenido = await gestor.RetenerAsync(riesgoOrigen);
        var riesgoArchivo = riesgoRetenido.Archivo!;
        var riesgoId = Guid.NewGuid();
        servidor.Respuesta = () => { var r = Informe(riesgoArchivo, riesgoId); r.Evaluacion = EvaluacionRiesgo.AmenazaDetectada; return r; };
        var excepcion = await AutorizacionLiberacion.SolicitarConfirmacionAsync(cliente, riesgoArchivo.ArchivoId, riesgoId,
            riesgoArchivo.Sha256, true, _ => true);
        await gestor.LiberarAutorizadoAsync(excepcion!);
        var riesgoRegistro = await gestor.ConsultarRegistroAsync(riesgoArchivo.ArchivoId);
        Exigir(riesgoRegistro.MetodoAutorizacion == "ConfirmacionExcepcional" && riesgoRegistro.RiesgoAceptadoAlLiberar &&
            riesgoRegistro.EvaluacionAlLiberar == EvaluacionRiesgo.AmenazaDetectada, "excepción registra aceptación sin ocultar amenaza simulada.");
        bool aperturaRiesgo = false;
        try { AbridorArchivoLiberado.AbrirVerificado(riesgoRegistro, _ => aperturas++); }
        catch (UnauthorizedAccessException) { aperturaRiesgo = true; }
        Exigir(aperturaRiesgo && aperturas == 1, "excepción riesgosa nunca ofrece apertura automática.");
        // Reproducir cierre tras el movimiento y antes de confirmar permisos/estado.
        riesgoRegistro.Estado = EstadoArchivo.Retenido; riesgoRegistro.OperacionPendiente = "Liberar";
        await File.WriteAllTextAsync(Path.Combine(raiz, "Cuarentena", riesgoArchivo.ArchivoId + ".json"), JsonSerializer.Serialize(riesgoRegistro));
        var recuperado = await new GestorCuarentena(Path.Combine(raiz, "Cuarentena")).ConsultarRegistroAsync(riesgoArchivo.ArchivoId);
        Exigir(recuperado.Estado == EstadoArchivo.Liberado && recuperado.OperacionPendiente == "" &&
            recuperado.EvaluacionAlLiberar == EvaluacionRiesgo.AmenazaDetectada, "recuperación completa la excepción sin ejecutar ni pedir otra confirmación.");

        var envios = new AlmacenEnviosCliente(Path.Combine(raiz, "Envios"));
        var envio = new EnvioCliente { Solicitud = new() { SolicitudId = Guid.NewGuid(), AnalisisLocal = Informe(archivo, id).AnalisisLocal! },
            RutaArchivo = origen, Servidor = "https://pruebas.invalid/", AnalisisId = id, Resultado = Informe(archivo, id), DecisionUsuarioPendiente = true };
        await envios.GuardarAsync(envio);
        Exigir((await new AlmacenEnviosCliente(Path.Combine(raiz, "Envios")).ListarAsync()).Single().DecisionUsuarioPendiente,
            "reinicio conserva decisión pendiente del informe terminado.");
        var cola = new ColaDecisionesCliente();
        var segundo = new EnvioCliente { Solicitud = new() { SolicitudId = Guid.NewGuid(), AnalisisLocal = envio.Solicitud.AnalisisLocal },
            RutaArchivo = origen, Servidor = envio.Servidor, Resultado = envio.Resultado, DecisionUsuarioPendiente = true };
        Exigir(cola.Encolar(envio) && !cola.Encolar(envio) && cola.Encolar(segundo) && cola.Count == 2,
            "varios archivos se encolan sin duplicar decisiones.");
        Exigir(cola.TryTomar(out var primero) && ReferenceEquals(primero, envio) && !cola.Encolar(envio),
            "el cuadro abierto conserva su identidad y no se duplica.");
        cola.Finalizar(envio);
        Exigir(cola.TryTomar(out var siguiente) && ReferenceEquals(siguiente, segundo),
            "la segunda decisión conserva el archivo correcto.");
        envio.DecisionUsuarioPendiente = false; envio.DecisionUsuario = "MantenerRetenido"; envio.FechaDecisionUsuarioUtc = DateTimeOffset.UtcNow;
        await envios.GuardarAsync(envio);
        var guardado = (await envios.ListarAsync()).Single();
        Exigir(!guardado.DecisionUsuarioPendiente && guardado.DecisionUsuario == "MantenerRetenido" && guardado.FechaDecisionUsuarioUtc is not null,
            "mantener queda persistido para no repetir el diálogo.");
        Directory.Delete(raiz, true);
    }

    private static IEnumerable<Control> Controles(Control c) => c.Controls.Cast<Control>().SelectMany(h => new[] { h }.Concat(Controles(h)));
    private static void ProbarDialogos()
    {
        foreach (var tipo in Enum.GetValues<TipoResultadoDecision>())
        {
            using var dialogo = new DecisionArchivoForm("Prueba.exe", tipo, () => { });
            var botones = Controles(dialogo).OfType<Button>().ToList();
            Exigir(botones.Any(b => b.Name == "DevolverYAbrir") == (tipo == TipoResultadoDecision.SinAmenazasReportadas),
                "abrir solo aparece en el resultado favorable: " + tipo);
            using var reloj = new System.Windows.Forms.Timer { Interval = 50 };
            reloj.Tick += (_, _) => { reloj.Stop(); dialogo.Close(); };
            dialogo.Shown += (_, _) => { Capturar(dialogo, tipo.ToString()); reloj.Start(); };
            dialogo.ShowDialog();
            Exigir(dialogo.Accion == AccionDecisionArchivo.Mantener, "cerrar el cuadro conserva retención: " + tipo);
        }
        using var confirmar = new ConfirmarRiesgoForm("Prueba.exe", TipoResultadoDecision.Riesgo);
        var boton = Controles(confirmar).OfType<Button>().Single(b => b.Name == "Confirmar");
        var casilla = Controles(confirmar).OfType<CheckBox>().Single();
        Exigir(!boton.Enabled, "la excepción no puede aceptarse sin marcar la casilla.");
        casilla.Checked = true;
        Exigir(boton.Enabled, "aceptación explícita habilita devolución excepcional.");
        confirmar.Show();
        Application.DoEvents();
        Capturar(confirmar, "ConfirmacionRiesgo");
        confirmar.Close();
    }

    private static void Capturar(Form ventana, string nombre)
    {
        string? destino = Environment.GetEnvironmentVariable("SECUREGATE_DECISION_QA_DIR");
        if (string.IsNullOrEmpty(destino)) return;
        Directory.CreateDirectory(destino);
        using var imagen = new Bitmap(ventana.Width, ventana.Height);
        ventana.DrawToBitmap(imagen, new Rectangle(Point.Empty, imagen.Size));
        imagen.Save(Path.Combine(destino, nombre + ".png"));
    }

    private sealed class ServidorSimulado(Func<ResultadoAnalisis> respuesta) : HttpMessageHandler
    {
        public int Consultas { get; private set; }
        public Func<ResultadoAnalisis> Respuesta { get; set; } = respuesta;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Consultas++;
            var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            json.Converters.Add(new JsonStringEnumConverter());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(JsonSerializer.Serialize(Respuesta(), json)) });
        }
    }
}
