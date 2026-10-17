using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Analisis;
using SecureGate.Cliente;
using SecureGate.Contratos;
using SecureGate.Monitoreo;
using SecureGate.Proteccion;
using ArchivoContrato = SecureGate.Contratos.InformacionArchivo;

namespace SecureGate;

public partial class Form1
{
    private readonly DownloadMonitor _monitor = new();
    private readonly SemaphoreSlim _cola = new(1, 1);
    private readonly CancellationTokenSource _salida = new();
    private readonly AlmacenEnviosCliente _envios = new(Path.Combine(EntornoCliente.CarpetaCliente, "Envios"));
    private readonly Dictionary<Guid, ListViewItem> _filas = new();
    private readonly Dictionary<string, ClienteAnalisisHttp> _clientes = new();
    private readonly HistorialPersistencia _historialPersistencia = new(EntornoCliente.CarpetaHistorial);
    private PreparadorEnviosProtegidos? _preparadorProteccion;
    private bool _retencionAutomatica = true;
    private CancellationTokenSource? _operacion;
    private EnvioCliente? _actual;
    private NotifyIcon? _bandeja;
    private bool _salir;
    private bool _cerrado;
    private bool _historialDisponible;
    private string _servidor = "https://localhost:5443/";
    private string? _carpetaMonitoreo;
    private readonly Button _reanudar = new() { Text = "🔄 Reanudar pendientes", AutoSize = true };
    private readonly Button _pausar = new() { Text = "Detener consulta", AutoSize = true, Enabled = false };
    private readonly Button _reintentar = new() { Text = "🔁 Recuperar resultado", AutoSize = true, Enabled = false };
    private readonly Button _eliminar = new() { Text = "🗑️  Eliminar", AutoSize = true, Enabled = false };
    private readonly Button _liberar = new() { Text = "Decidir sobre archivo…", AutoSize = true, Enabled = false };
    private readonly Button _exportar = new() { Text = "Exportar informe…", AutoSize = true, Enabled = false };
    private static readonly JsonSerializerOptions JsonInforme = CrearJsonInforme();

    private void PrepararIntegracion()
    {
        MinimumSize = new Size(1000, 620);
        panelRight.AutoScroll = true;
        groupBoxSummary.Height = 330;
        tableSummary.ColumnStyles.Clear();
        tableSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tableSummary.RowStyles.Clear();
        for (int i = 0; i < 6; i++) tableSummary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (var etiqueta in new[] { lblName, lblType, lblSize, lblHash, lblSignature, lblReputation })
        {
            etiqueta.MaximumSize = new Size(270, 0);
            etiqueta.AutoSize = true;
            etiqueta.Margin = new Padding(5);
        }



        groupBoxSandbox.Text = "Resultado del análisis";
        groupBoxSandbox.Dock = DockStyle.Top;
        groupBoxSandbox.Height = 270;
        lblSandboxTitle.SetBounds(12, 25, 270, 45);
        lblSandboxStatus.AutoSize = false;
        lblSandboxStatus.SetBounds(12, 75, 270, 130);
        btnConfigureSandbox.SetBounds(12, 220, 250, 35);
        btnConfigureSandbox.Text = "Ver informe completo";
        btnConfigureSandbox.Enabled = false;
        groupBoxSandbox.Controls.AddRange(new Control[]
        {
            lblSandboxTitle, lblSandboxStatus, btnConfigureSandbox
        });
        panelRight.Controls.Add(groupBoxSandbox);
        panelRight.Controls.SetChildIndex(groupBoxSummary, 0);

        // Panel de acciones mejorado con 5 botones
        var acciones = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 55,
            AutoSize = true,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Aplicar estilos a los botones
        foreach (var btn in new[] { _reanudar, _pausar, _reintentar, _eliminar, _liberar, _exportar })
        {
            btn.Height = 38;
            btn.Font = new Font("Segoe UI", 9F);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(37, 99, 235);
            btn.ForeColor = Color.White;
            btn.Margin = new Padding(4);
        }

        acciones.Controls.AddRange(new Control[] { _reanudar, _pausar, _reintentar, _eliminar, _liberar, _exportar });
        panelRight.Controls.Add(acciones);
        panelRight.Controls.SetChildIndex(acciones, 0);

        OrganizarPantalla(acciones);

        // Event handlers
        _reanudar.Click += async (_, _) => await ReanudarPendientesAsync();
        _pausar.Click += (_, _) => _operacion?.Cancel();
        _reintentar.Click += async (_, _) => await ReiniciarAnalisisAsync();
        _eliminar.Click += async (_, _) => await EliminarArchivoAsync();
        _liberar.Click += async (_, _) => await LiberarArchivoAsync();
        _exportar.Click += (_, _) => ExportarInforme();
        btnConfigureSandbox.Click += (_, _) => MostrarInforme();
        lblSandboxTitle.Text = "Seleccioná un archivo";
        lblSandboxStatus.Text =
            "El servidor consultará su reputación y realizará la prueba aislada disponible.";
        openFileDialog1.Filter = "Ejecutables de Windows (*.exe)|*.exe";
        btnAnalyze.Click += selectFileButton_Click;
        btnHome.Click += (_, _) => { RestaurarVentana(); selectFileButton.Focus(); };
        btnHistory.Click += (_, _) => { RestaurarVentana(); ShowHistory(); };
        btnReports.Click += (_, _) => { RestaurarVentana(); ShowReports(); };
        dragPanel.AllowDrop = true;
        foreach (Control control in dragPanel.Controls)
        {
            control.AllowDrop = true;
            control.DragEnter += dragPanel_DragEnter;
            control.DragDrop += dragPanel_DragDrop;
        }
        dragPanel.Paint += dragPanel_Paint;
        dragPanel.Resize += dragPanel_Resize;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir SecureGate", null, (_, _) => RestaurarVentana());
        menu.Items.Add("Reanudar pendientes", null, async (_, _) => await ReanudarPendientesAsync());
        menu.Items.Add("Salir", null, (_, _) => { _salir = true; Close(); });
        _bandeja = new NotifyIcon
        {
            Icon = SystemIcons.Shield, Text = "SecureGate", Visible = true, ContextMenuStrip = menu
        };
        _bandeja.DoubleClick += (_, _) => RestaurarVentana();
        _bandeja.BalloonTipClicked += (_, _) => RestaurarVentana();
        _monitor.ArchivoDetectado += ruta => EnInterfaz(() => { _ = AnalizarAsync(ruta, true); });
        _monitor.ErrorMonitoreo += mensaje => EnInterfaz(() => Notificar(mensaje));
        Shown += async (_, _) => await IniciarClienteAsync();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosing += (_, e) =>
        {
            if (!_salir && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                Notificar("SecureGate continúa activo. Elegí Salir desde su icono junto al reloj para cerrarlo.");
            }
        };
        FormClosed += (_, _) =>
        {
            _cerrado = true;
            _salida.Cancel();
            _operacion?.Cancel();
            _monitor.Dispose();
            foreach (var cliente in _clientes.Values) cliente.Dispose();
            if (_bandeja is not null)
            {
                _bandeja.Visible = false;
                _bandeja.ContextMenuStrip?.Dispose();
                _bandeja.Dispose();
            }
        };
    }



    private void ShowReports()
    {
        try
        {
            var items = listViewRecent.Items;
            using var ventana = new ReportsForm(items);
            ventana.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo abrir la ventana de reportes: " + ex.Message);
        }
    }

    private void ShowHistory()
    {
        try
        {
            var items = listViewRecent.Items;
            using var ventana = new HistoryForm(items);
            ventana.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo abrir el historial: " + ex.Message);
        }
    }

    private async Task IniciarClienteAsync()
    {
        // Anchor: no-op insertion point for future automated patches.
        try
        {
            string ajustes = RutaAjustes();
            if (File.Exists(ajustes))
            {
                var guardado = JsonSerializer.Deserialize<AjustesCliente>(
                    await File.ReadAllTextAsync(ajustes), JsonInforme);
                if (guardado is not null)
                {
                    _servidor = NormalizarServidor(guardado.Servidor);
                    _carpetaMonitoreo = guardado.CarpetaMonitoreo;
                    _retencionAutomatica = guardado.RetencionAutomatica;
                }
            }

            if (PaqueteCliente.Cargar() is { } paquete)
            {
                _servidor = NormalizarServidor(paquete.Servidor);
                _carpetaMonitoreo = DownloadMonitor.ObtenerDescargas();
                _retencionAutomatica = true;
                while (!await paquete.VerificarConexionAsync(_salida.Token))
                {
                    if (MessageBox.Show("No se puede conectar con SecureGate.\nEl monitoreo todavía no se ha iniciado.",
                        "SecureGate", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry) return;
                }
                Hide();
            }

            foreach (var envio in (await _envios.ListarAsync(_salida.Token)).Reverse())
            {
                await SincronizarEstadoArchivoAsync(envio, _salida.Token);
                ActualizarFila(envio);
            }

            // Cargar historial persistente desde JSON
            var registrosHistorial = await _historialPersistencia.CargarAsync();
            System.Diagnostics.Debug.WriteLine($"Historial cargado: {registrosHistorial.Count} registros");

            if (listViewRecent.Items.Count > 0)
                listViewRecent.Items[0].Selected = true;
            _preparadorProteccion = new PreparadorEnviosProtegidos(_envios, EntornoCliente.CarpetaCuarentena);
            _preparadorProteccion.RestauracionPendiente += (ruta, hash) => _monitor.RegistrarRestauracion(ruta, hash);
            _historialDisponible = true;
            IniciarMonitor(_carpetaMonitoreo);
            if (_retencionAutomatica) await RecuperarProteccionAsync();
            else await ReanudarPendientesAsync();
            foreach (var envio in await _envios.ListarAsync(_salida.Token)) EncolarDecision(envio);
        }
        catch (OperationCanceledException) when (_salida.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_cerrado)
                MessageBox.Show(this, "No se pudo cargar el historial del cliente: " + error.Message,
                    "SecureGate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task AnalizarAsync(string ruta, bool automatico)
    {
        bool adquirido = false;
        EnvioCliente? envio = null;
        ProgresoArchivoForm? progresoArchivo = null;
        try
        {
            if (!_historialDisponible)
                throw new InvalidOperationException("El historial todavía no está disponible.");
            if (!AlcanceAnalisis.AdmiteExtension(Path.GetExtension(ruta)))
                throw new InvalidDataException("Esta versión analiza únicamente ejecutables .exe.");
            ResultadoOperacionArchivo? retencion = null;
            if (_retencionAutomatica)
            {
                EntornoCliente.ValidarRutaPrueba(ruta);
                retencion = await _preparadorProteccion!.RetenerRutaAsync(ruta, _salida.Token);
                progresoArchivo = new ProgresoArchivoForm(retencion.Archivo?.NombreOriginal ?? Path.GetFileName(ruta));
                progresoArchivo.Show();
                if (automatico) Notificar("Archivo retenido para analizar: " + retencion.Archivo?.NombreOriginal);
            }
            await _cola.WaitAsync(_salida.Token);
            adquirido = true;
            if (_cerrado) return;
            PrepararEspera();
            _progresoActual = progresoArchivo;
            progresoArchivo?.Actualizar("Comprobando firma y antivirus…");
            _actual = null;
            btnConfigureSandbox.Enabled = false;
            lblSandboxTitle.Text = "Preparando análisis";
            lblSandboxStatus.Text = Path.GetFileName(ruta);
            if (retencion is not null)
            {
                envio = await _preparadorProteccion!.PrepararRetenidoAsync(
                    retencion.ArchivoId, _servidor, _operacion!.Token);
                await _historialPersistencia.ActualizarRegistroAsync(envio);
            }
            else
            {
            var archivo = new FileInfo(ruta);
            if (!AlcanceAnalisis.AdmiteExtension(archivo.Extension))
                throw new InvalidDataException("Esta versión analiza únicamente ejecutables .exe.");
            if (archivo.Length <= 0 || archivo.Length > 100L * 1024 * 1024)
                throw new InvalidDataException("El archivo debe tener entre 1 byte y 100 MiB.");

            lblReputation.Text = "Calculando SHA-256 y leyendo información local...";
            DateTimeOffset inicio = DateTimeOffset.UtcNow;
            var local = await Task.Run(() => FileAnalyzer.AnalizarArchivo(ruta), _operacion!.Token);
            _operacion.Token.ThrowIfCancellationRequested();

            // Retomar un envío pendiente del mismo archivo y del mismo servidor.
            envio = (await _envios.ListarAsync(_operacion.Token)).FirstOrDefault(e =>
                !e.Terminado && e.Servidor == _servidor &&
                string.Equals(e.RutaArchivo, local.RutaCompleta, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.Solicitud.AnalisisLocal.Archivo.Sha256, local.HashSHA256,
                    StringComparison.OrdinalIgnoreCase));

            if (envio is null)
            {
                var informacion = new ArchivoContrato
                {
                    ArchivoId = Guid.NewGuid(),
                    NombreOriginal = local.Nombre,
                    Extension = local.Extension,
                    TamanoBytes = local.TamañoBytes,
                    Sha256 = local.HashSHA256,
                    FechaRegistroUtc = DateTimeOffset.UtcNow
                };
                envio = new EnvioCliente
                {
                    Solicitud = new SolicitudAnalisis
                    {
                        SolicitudId = Guid.NewGuid(),
                        AnalisisLocal = new ResultadoAnalisisLocal
                        {
                            Archivo = informacion,
                            InicioUtc = inicio,
                            FinUtc = DateTimeOffset.UtcNow,
                            FirmaDigital = new ResultadoFirmaDigital
                            {
                                Estado = EstadoComprobacion.NoRealizada,
                                Firma = EstadoFirmaDigital.Indeterminada,
                                Firmante = local.TieneFirma ? local.Firmante : null,
                                Detalle = local.EstadoFirma +
                                    " La validez Authenticode no fue comprobada."
                            },
                            Antivirus = new ResultadoAntivirus
                            {
                                Estado = EstadoComprobacion.NoRealizada,
                                Deteccion = DeteccionAntivirus.NoDeterminada,
                                Detalle = "Este adaptador no realizó un escaneo antivirus local."
                            }
                        }
                    },
                    RutaArchivo = local.RutaCompleta,
                    Servidor = _servidor
                };
                // Persistir la SolicitudId antes del primer envío.
                await _envios.GuardarAsync(envio, _operacion.Token);
                await _historialPersistencia.AgregarRegistroAsync(envio); // Guardar en historial JSON
            }

            }

            _actual = envio;
            ActualizarFila(envio);
            MostrarRegistro(envio);
            await AtenderEnvioAsync(envio, _operacion!.Token);
            if (automatico && !_cerrado)
                Notificar(envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal + ": " +
                    (envio.Resultado is null ? envio.EstadoLocal : PresentacionAnalisis.Resultado(envio)) +
                    (_retencionAutomatica ? ". El archivo sigue retenido. Abrí SecureGate para ver el informe." : ". Abrí SecureGate para ver el informe."));
        }
        catch (OperationCanceledException)
        {
            await RegistrarPausaAsync(envio);
        }
        catch (Exception error)
        {
            await RegistrarErrorAsync(envio, error);
            if (!_cerrado && automatico)
            {
                Notificar("No se pudo completar el análisis. No se liberó el archivo.");
                if (envio is null) MessageBox.Show("No se pudo preparar el análisis: " + error.Message +
                    "\nNo se devolvió ningún archivo. Revisá SecureGate.", "SecureGate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (!_cerrado && !automatico)
                MessageBox.Show(this, error.Message, "Análisis",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            progresoArchivo?.Close();
            progresoArchivo?.Dispose();
            if (adquirido)
            {
                _progresoActual = null;
                TerminarEspera();
                _cola.Release();
                if (envio is not null) EncolarDecision(envio);
                ProcesarDecisiones();
                if (!_cerrado && _actual is not null) MostrarRegistro(_actual);
            }
        }
    }

    private async Task RecuperarProteccionAsync()
    {
        if (_cerrado || !_historialDisponible || _preparadorProteccion is null) return;
        try
        {
            var recuperados = await _preparadorProteccion.RecuperarAsync(_servidor, _salida.Token);
            foreach (var envio in recuperados)
            {
                ActualizarFila(envio);
                await _historialPersistencia.ActualizarRegistroAsync(envio);
            }
            await ReanudarPendientesAsync();
        }
        catch (OperationCanceledException) when (_salida.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_cerrado) MessageBox.Show(this, "No se pudo recuperar la cuarentena: " + error.Message);
        }
    }

    private async Task ReanudarPendientesAsync()
    {
        if (_cerrado || !_historialDisponible || _cola.CurrentCount == 0) return;
        try
        {
            var pendientes = (await _envios.ListarAsync(_salida.Token))
                .Where(e => !e.Terminado).OrderBy(e => e.FechaCreacionUtc).ToList();
            foreach (var envio in pendientes)
                await ReanudarEnvioAsync(envio);
        }
        catch (OperationCanceledException) when (_salida.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (!_cerrado) MessageBox.Show(this, "No se pudieron reanudar los envíos: " + error.Message);
        }
    }

    private async Task ReanudarEnvioAsync(EnvioCliente envio)
    {
        bool adquirido = false;
        ProgresoArchivoForm? progreso = null;
        try
        {
            await _cola.WaitAsync(_salida.Token);
            adquirido = true;
            if (_cerrado) return;
            envio.DecisionUsuarioPendiente = true;
            if (EsMuestraRetenida(envio))
            {
                progreso = new ProgresoArchivoForm(envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal);
                progreso.Show();
            }
            _progresoActual = progreso;
            PrepararEspera();
            _actual = envio;
            MostrarRegistro(envio);
            await AtenderEnvioAsync(envio, _operacion!.Token);
        }
        catch (OperationCanceledException) { await RegistrarPausaAsync(envio); }
        catch (Exception error) { await RegistrarErrorAsync(envio, error); }
        finally
        {
            progreso?.Close();
            progreso?.Dispose();
            if (adquirido)
            {
                _progresoActual = null;
                TerminarEspera();
                _cola.Release();
                EncolarDecision(envio);
                ProcesarDecisiones();
                if (!_cerrado && _actual is not null) MostrarRegistro(_actual);
            }
        }
    }

    private async Task AtenderEnvioAsync(EnvioCliente envio, CancellationToken ct)
    {
        string direccion = NormalizarServidor(envio.Servidor);
        if (!_clientes.TryGetValue(direccion, out var cliente))
        {
            cliente = new ClienteAnalisisHttp(new Uri(direccion));
            _clientes.Add(direccion, cliente);
        }

        if (envio.AnalisisId is null)
        {
            _progresoActual?.Actualizar("Enviando al servidor…");
            envio.EstadoLocal = "Enviando";
            await _envios.GuardarAsync(envio, ct);
            ActualizarFila(envio);
            var aceptado = await cliente.EnviarArchivoAsync(envio.Solicitud, envio.RutaArchivo, ct);
            envio.AnalisisId = aceptado.AnalisisId;
            envio.EstadoLocal = "Registrado";
            await _envios.GuardarAsync(envio, ct);
        }

        var ventanaProgreso = _progresoActual;
        var progreso = new Progress<ResultadoAnalisis>(resultado =>
        {
            if (_cerrado) return;
            ventanaProgreso?.Actualizar(EstadoAmigable(resultado.Estado));
            envio.EstadoLocal = EstadoAmigable(resultado.Estado);
            ActualizarFila(envio);
            if (_actual?.Solicitud.SolicitudId == envio.Solicitud.SolicitudId)
            {
                lblSandboxTitle.Text = envio.EstadoLocal;
                lblSandboxStatus.Text = resultado.Resumen;
            }
        });

        envio.Resultado = await cliente.EsperarResultadoAsync(
            envio.AnalisisId.Value, progreso, cancellationToken: ct);
        envio.EstadoLocal = EstadoAmigable(envio.Resultado.Estado);
        await SincronizarEstadoArchivoAsync(envio, ct);
        envio.UltimoError = null;
        await _envios.GuardarAsync(envio, ct);
        await _historialPersistencia.ActualizarRegistroAsync(envio); // Persistir en historial JSON
        ActualizarFila(envio);
        if (_actual?.Solicitud.SolicitudId == envio.Solicitud.SolicitudId)
            MostrarRegistro(envio);
    }

    private async Task RegistrarPausaAsync(EnvioCliente? envio)
    {
        if (envio is null) return;
        envio.EstadoLocal = envio.Terminado && envio.Resultado is not null
            ? EstadoAmigable(envio.Resultado.Estado) : "Consulta pausada";
        envio.UltimoError = envio.Terminado ? null :
            "La espera local se pausó. El trabajo puede continuar en el servidor.";
        await GuardarEstadoLocalAsync(envio);
    }

    private async Task RegistrarErrorAsync(EnvioCliente? envio, Exception error)
    {
        if (envio is not null)
        {
            envio.EstadoLocal = "Pendiente de revisión";
            envio.UltimoError = error.Message;
            await GuardarEstadoLocalAsync(envio);
        }
        else if (!_cerrado) lblReputation.Text = "No se pudo preparar el análisis: " + error.Message;
    }

    private async Task GuardarEstadoLocalAsync(EnvioCliente envio)
    {
        try
        {
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _envios.GuardarAsync(envio, limite.Token);
        }
        catch (Exception error)
        {
            if (!_cerrado) MessageBox.Show(this, "No se pudo guardar el estado local: " + error.Message);
        }
        if (!_cerrado)
        {
            ActualizarFila(envio);
            if (_actual?.Solicitud.SolicitudId == envio.Solicitud.SolicitudId)
                MostrarRegistro(envio);
        }
    }

    private void PrepararEspera()
    {
        _operacion = CancellationTokenSource.CreateLinkedTokenSource(_salida.Token);
        _pausar.Enabled = true;
        _reanudar.Enabled = false;
        _reintentar.Enabled = false;
    }

    private void TerminarEspera()
    {
        _operacion?.Dispose();
        _operacion = null;
        if (!_cerrado)
        {
            _pausar.Enabled = false;
            _reanudar.Enabled = true;
            _reintentar.Enabled = _actual is not null;
        }
    }

    private void ActualizarFila(EnvioCliente envio)
    {
        if (_cerrado) return;
        var archivo = envio.Solicitud.AnalisisLocal.Archivo;
        if (!_filas.TryGetValue(envio.Solicitud.SolicitudId, out var fila))
        {
            fila = new ListViewItem(new[]
            {
                archivo.NombreOriginal, envio.RutaArchivo,
                envio.FechaCreacionUtc.ToLocalTime().ToString("g"), ""
            });
            _filas.Add(envio.Solicitud.SolicitudId, fila);
            listViewRecent.Items.Insert(0, fila);
        }
        fila.Tag = envio;
        fila.ToolTipText = archivo.NombreOriginal + "\n" + envio.RutaArchivo + "\n" + envio.EstadoLocal;
        fila.SubItems[3].Text = envio.EstadoLocal.StartsWith("Archivo liberado") || envio.EstadoLocal.StartsWith("Archivo eliminado")
            ? envio.EstadoLocal : envio.Terminado && envio.Resultado is not null
            ? PresentacionAnalisis.Resultado(envio) : envio.EstadoLocal;
    }

    private void MostrarSeleccion()
    {
        if (listViewRecent.SelectedItems.Count > 0 &&
            listViewRecent.SelectedItems[0].Tag is EnvioCliente envio)
            MostrarRegistro(envio);
    }

    private void MostrarRegistro(EnvioCliente envio)
    {
        if (_cerrado) return;
        _actual = envio;
        var archivo = envio.Solicitud.AnalisisLocal.Archivo;
        lblName.Text = "Nombre: " + archivo.NombreOriginal;
        lblType.Text = "Tipo: " + archivo.Extension;
        lblSize.Text = $"Tamaño: {archivo.TamanoBytes / (1024.0 * 1024.0):F2} MiB";
        lblHash.Text = "SHA-256: " + archivo.Sha256;
        lblSignature.Text = "Firma: " + TextoFirma(envio.Solicitud.AnalisisLocal.FirmaDigital) +
            (string.IsNullOrWhiteSpace(envio.Solicitud.AnalisisLocal.FirmaDigital.Firmante)
                ? "" : "\nCertificado: " + envio.Solicitud.AnalisisLocal.FirmaDigital.Firmante);
        lblReputation.Text = envio.Resultado?.Reputacion?.Detalle ??
            "Consulta de reputación a cargo del servidor.";
        lblSandboxTitle.Text = PresentacionAnalisis.Procesamiento(envio);
        lblSandboxStatus.Text = "Resultado: " + PresentacionAnalisis.Resultado(envio) + "\n\n" +
            PresentacionAnalisis.Informe(envio) + "\n\n" + PresentacionAnalisis.Alcance(envio) +
            (string.IsNullOrWhiteSpace(envio.UltimoError) ? "" : "\n\nÚltimo intento: " + envio.UltimoError);
        btnConfigureSandbox.Enabled = true;

        // Habilitar/deshabilitar botones según estado
        bool enProceso = envio.Resultado?.Estado == EstadoAnalisis.EnProceso || (envio.EstadoLocal == "Pendiente" && envio.Resultado == null);
        bool completado = envio.Resultado?.Estado == EstadoAnalisis.Completado;
        bool fallido = envio.Resultado?.Estado == EstadoAnalisis.Fallido;

        _pausar.Enabled = _operacion is not null && !_operacion.IsCancellationRequested;
        _reintentar.Enabled = !enProceso && _cola.CurrentCount > 0;
        _eliminar.Enabled = EsMuestraRetenida(envio) && _cola.CurrentCount > 0;
        _exportar.Enabled = true;
        _liberar.Enabled = EsMuestraRetenida(envio) && completado && _cola.CurrentCount > 0 &&
            !_decisionVisible;
    }

    private void MostrarInforme()
    {
        if (_actual is not null) MostrarInforme(_actual);
    }

    private void MostrarInforme(EnvioCliente envio)
    {
        using var ventana = new InformeVisualForm(envio);
        ventana.TopMost = _decisionVisible;
        ventana.ShowDialog();
    }

    private void ExportarInforme()
    {
        if (_actual is null)
        {
            MessageBox.Show(this, "Seleccioná un análisis del historial.");
            return;
        }
        using var dialogo = new SaveFileDialog
        {
            Filter = "Informe de texto|*.txt|Resultado estructurado|*.json",
            FileName = "SecureGate_Informe.txt"
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (dialogo.FilterIndex == 2 && _actual.Resultado is null)
                throw new InvalidOperationException("Todavía no hay un resultado para exportar como JSON.");
            string contenido = dialogo.FilterIndex == 2
                ? JsonSerializer.Serialize(_actual.Resultado, JsonInforme)
                : CrearInforme(_actual);
            File.WriteAllText(dialogo.FileName, contenido, Encoding.UTF8);
        }
        catch (Exception error) { MessageBox.Show(this, "No se pudo exportar: " + error.Message); }
    }

    internal static string CrearInforme(EnvioCliente envio)
    {
        var texto = new StringBuilder();
        var archivo = envio.Solicitud.AnalisisLocal.Archivo;
        texto.AppendLine("SECUREGATE — INFORME");
        texto.AppendLine("Archivo: " + archivo.NombreOriginal);
        texto.AppendLine("SHA-256: " + archivo.Sha256);
        texto.AppendLine("SolicitudId: " + envio.Solicitud.SolicitudId);
        texto.AppendLine("AnalisisId: " + envio.AnalisisId);
        texto.AppendLine("Estado local: " + envio.EstadoLocal);
        texto.AppendLine("Decisión del usuario: " + (envio.DecisionUsuario ?? "Pendiente o anterior a esta versión"));
        texto.AppendLine("Fecha de decisión: " + envio.FechaDecisionUsuarioUtc?.ToString("O"));
        var firmaLocal = envio.Solicitud.AnalisisLocal.FirmaDigital;
        texto.AppendLine("Firma local: " + firmaLocal.Estado + "; " + firmaLocal.Firma);
        texto.AppendLine("Firmante: " + firmaLocal.Firmante);
        texto.AppendLine("Detalle de firma: " + firmaLocal.Detalle);
        texto.AppendLine("Antivirus local: " + envio.Solicitud.AnalisisLocal.Antivirus.Estado + "; " +
            envio.Solicitud.AnalisisLocal.Antivirus.Deteccion);
        texto.AppendLine(envio.Solicitud.AnalisisLocal.Antivirus.Detalle);
        if (EsMuestraRetenida(envio))
            texto.AppendLine("La muestra se almacena en cuarentena; este informe no autoriza liberarla.");
        if (!string.IsNullOrWhiteSpace(envio.UltimoError)) texto.AppendLine(envio.UltimoError);
        if (envio.Resultado is null)
        {
            texto.AppendLine("Todavía no se recibió un informe final.");
            return texto.ToString();
        }
        var resultado = envio.Resultado;
        texto.AppendLine("Estado: " + EstadoAmigable(resultado.Estado));
        texto.AppendLine("Procesamiento: " + PresentacionAnalisis.Procesamiento(envio));
        texto.AppendLine("Informe: " + PresentacionAnalisis.Informe(envio));
        texto.AppendLine("Resultado visible: " + PresentacionAnalisis.Resultado(envio));
        texto.AppendLine("Alcance: " + PresentacionAnalisis.Alcance(envio));
        texto.AppendLine("Evaluación técnica original: " + resultado.Evaluacion);
        if (envio.Resultado?.Sandbox?.DemostracionComportamiento == true)
            texto.AppendLine("DEMOSTRACIÓN CONTROLADA: cmd.exe de Windows con acciones benignas predeterminadas. No es malware ni un instalador.");
        foreach (var hallazgo in AnalizadorComportamientoSandbox.Analizar(resultado))
            texto.AppendLine("COMPORTAMIENTO SANDBOX [" + hallazgo.Codigo + "]: " + hallazgo.Descripcion +
                " Eventos: " + string.Join(", ", hallazgo.Eventos));
        texto.AppendLine("Estado técnico de evaluación: " + resultado.Evaluacion);
        texto.AppendLine("Resumen: " + resultado.Resumen);
        texto.AppendLine("Reputación: " + resultado.Reputacion?.Detalle);
        texto.AppendLine("Código de error: " + resultado.CodigoError);
        if (resultado.FechaEliminacionMuestraServidorUtc is { } retirada)
            texto.AppendLine("Copia del servidor no disponible desde: " + retirada.ToString("O") +
                ". El informe se conserva; reenviar la misma solicitud recupera este trabajo sin repetirlo.");
        if (resultado.Ia is not null)
        {
            var ia = resultado.Ia;
            texto.AppendLine("\nEXPLICACIÓN DE IA — APOYO, SIN AUTORIZACIÓN DE EJECUCIÓN");
            texto.AppendLine($"Proveedor: {ia.Proveedor}; modelo: {ia.Modelo}; estado: {ia.Estado}");
            texto.AppendLine($"Consulta: {ia.FechaConsultaUtc:o}; finalización: {ia.FechaFinalizacionUtc:o}");
            texto.AppendLine("Código de error IA: " + ia.CodigoError);
            texto.AppendLine(ia.Resumen);
            texto.AppendLine("Evaluación de comportamiento IA: " + ia.EvaluacionComportamiento);
            foreach (var observacion in ia.ObservacionesComportamiento)
                texto.AppendLine("- Comportamiento IA: " + observacion.Texto + " [" + string.Join(", ", observacion.Referencias) + "]");
            texto.AppendLine("Referencias del resumen: " + string.Join(", ", ia.ReferenciasResumen));
            foreach (var observacion in ia.Observaciones)
                texto.AppendLine("- " + observacion.Texto + " [" +
                    string.Join(", ", observacion.Referencias) + "]");
            texto.AppendLine("\nLIMITACIONES DE IA");
            foreach (string limitacion in ia.Limitaciones) texto.AppendLine("- " + limitacion);
            texto.AppendLine("\nEVIDENCIAS ENVIADAS A IA");
            foreach (var evidencia in ia.EvidenciasEnviadas)
                texto.AppendLine($"{evidencia.Id} | {evidencia.Tipo} | {evidencia.Descripcion}");
        }
        texto.AppendLine("\nMOTIVOS");
        foreach (string motivo in resultado.Motivos) texto.AppendLine("- " + motivo);
        texto.AppendLine("\nLIMITACIONES");
        foreach (string limitacion in resultado.Limitaciones) texto.AppendLine("- " + limitacion);
        if (firmaLocal.Estado != EstadoComprobacion.Completada)
            texto.AppendLine("- La firma Authenticode no pudo comprobarse completamente.");
        if (envio.Solicitud.AnalisisLocal.Antivirus.Estado != EstadoComprobacion.Completada)
            texto.AppendLine("- El análisis antivirus local sigue pendiente o no pudo completarse.");
        if (resultado.Sandbox is not null)
        {
            texto.AppendLine("\nSANDBOX");
            if (resultado.Sandbox.AnalisisOrigenId is { } origen)
            {
                texto.AppendLine($"Observación histórica reutilizada. Análisis de origen: {origen}");
                texto.AppendLine($"Archivo de origen: {resultado.Sandbox.NombreArchivoOrigen}; ID: {resultado.Sandbox.ArchivoOrigenId}");
                texto.AppendLine($"Reutilización: {resultado.Sandbox.FechaReutilizacionUtc:o}. No se ejecutó de nuevo en esta solicitud.");
            }
            texto.AppendLine($"Estado: {resultado.Sandbox.Estado}");
            texto.AppendLine($"Muestra ejecutada: {resultado.Sandbox.MuestraEjecutada}");
            texto.AppendLine($"Observador iniciado: {resultado.Sandbox.ObservadorIniciado}");
            texto.AppendLine($"Red habilitada: {resultado.Sandbox.RedHabilitada}");
            texto.AppendLine($"Inicio: {resultado.Sandbox.InicioUtc:o}; fin: {resultado.Sandbox.FinUtc:o}");
            texto.AppendLine("\nEVENTOS");
            foreach (var evento in resultado.Sandbox.Eventos)
                texto.AppendLine($"{evento.FechaUtc:o} | {evento.Tipo} | PID {evento.ProcesoId} | " +
                    $"{evento.NombreProceso} | {evento.Recurso} | {evento.Detalle}");
        }
        return texto.ToString();
    }

    private void MostrarConfiguracion()
    {
        using var ventana = new Form
        {
            Text = "Configuración de SecureGate", StartPosition = FormStartPosition.CenterParent,
            AutoScaleMode = AutoScaleMode.Font, Font = new Font("Segoe UI", 10F),
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(620, 0), FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false
        };
        var tabla = new TableLayoutPanel
        { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(18) };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var servidor = new TextBox { Text = _servidor, Dock = DockStyle.Fill, MinimumSize = new Size(540, 0) };
        var ruta = new TextBox { ReadOnly = true, Dock = DockStyle.Fill,
            Text = _carpetaMonitoreo ?? DownloadMonitor.ObtenerDescargas() };
        var buscar = new Button { Text = "Elegir carpeta…", AutoSize = true, Padding = new Padding(8, 5, 8, 5) };
        buscar.Click += (_, _) =>
        {
            using var dialogo = new FolderBrowserDialog();
            if (dialogo.ShowDialog(ventana) == DialogResult.OK) ruta.Text = dialogo.SelectedPath;
        };
        var retener = new CheckBox { Text = "Retener nuevas descargas .exe antes de analizarlas",
            AutoSize = true, Checked = _retencionAutomatica, Margin = new Padding(3, 16, 3, 6) };
        void Fila(Control control, bool completa = true)
        {
            int fila = tabla.RowCount++;
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.Controls.Add(control, 0, fila);
            if (completa) tabla.SetColumnSpan(control, 2);
        }
        Fila(new Label { Text = "Dirección HTTPS del servidor", AutoSize = true });
        Fila(servidor);
        Fila(new Label { Text = "Carpeta monitoreada", AutoSize = true, Margin = new Padding(3, 15, 3, 6) });
        Fila(ruta, false);
        tabla.Controls.Add(buscar, 1, tabla.RowCount - 1);
        Fila(retener);
        Fila(new Label { AutoSize = true, MaximumSize = new Size(570, 0),
            Text = "Esta versión monitorea ejecutables .exe. La retención mueve el archivo a cuarentena. Todavía no constituye un bloqueo global de ejecución. La devolución exige una decisión explícita. Si hay riesgo, requiere una segunda confirmación; no cambia la evaluación." });
        Fila(new Label { AutoSize = true, MaximumSize = new Size(570, 0), Margin = new Padding(3, 12, 3, 12),
            Text = "Los envíos existentes conservan su servidor. Cambiar esta opción no libera archivos retenidos." });
        var botones = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var guardar = new Button { Text = "Guardar", AutoSize = true, Padding = new Padding(12, 6, 12, 6) };
        var cancelar = new Button { Text = "Cancelar", AutoSize = true, Padding = new Padding(12, 6, 12, 6), DialogResult = DialogResult.Cancel };
        botones.Controls.AddRange(new Control[] { guardar, cancelar });
        Fila(botones);
        ventana.Controls.Add(tabla);
        ventana.AcceptButton = guardar;
        ventana.CancelButton = cancelar;
        guardar.Click += (_, _) =>
        {
            try
            {
                string direccion = NormalizarServidor(servidor.Text.Trim());
                if (!Directory.Exists(ruta.Text)) throw new DirectoryNotFoundException("La carpeta no existe.");
                EntornoCliente.ValidarRutaPrueba(ruta.Text);
                Directory.CreateDirectory(EntornoCliente.CarpetaCliente);
                File.WriteAllText(RutaAjustes(), JsonSerializer.Serialize(new AjustesCliente
                { Servidor = direccion, CarpetaMonitoreo = ruta.Text, RetencionAutomatica = retener.Checked }, JsonInforme));
                _servidor = direccion;
                _retencionAutomatica = retener.Checked;
                IniciarMonitor(ruta.Text);
                ventana.DialogResult = DialogResult.OK;
            }
            catch (Exception error) { MessageBox.Show(ventana, "No se pudo guardar: " + error.Message); }
        };
        if (ventana.ShowDialog(this) == DialogResult.OK && _retencionAutomatica)
            _ = RecuperarProteccionAsync();
    }

    private void IniciarMonitor(string? carpeta = null)
    {
        try
        {
            EntornoCliente.ValidarRutaPrueba(carpeta ?? DownloadMonitor.ObtenerDescargas());
            _monitor.IniciarMonitoreo(carpeta);
            _carpetaMonitoreo = _monitor.CarpetaActual;
            lblTitle.Text = _retencionAutomatica ? "SecureGate — retención y monitoreo activos" : "SecureGate — monitoreo sin retención";
            EntornoCliente.MonitorListo(_monitor.CarpetaActual);
        }
        catch (Exception error)
        {
            lblTitle.Text = "SecureGate — monitoreo desactivado";
            MessageBox.Show(this, error.Message + "\nPodés elegir otra carpeta en Configuración.");
        }
    }

    private void EnInterfaz(Action accion)
    {
        if (_cerrado || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(() => { if (!_cerrado) accion(); })); }
        catch (InvalidOperationException) { }
    }

    private void RestaurarVentana() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void Notificar(string texto)
    {
        if (!_cerrado)
            _bandeja?.ShowBalloonTip(5000, "SecureGate", texto, ToolTipIcon.Warning);
    }

    private static string NormalizarServidor(string texto)
    {
        var uri = new Uri(texto, UriKind.Absolute);
        // Compatibilidad con los registros locales anteriores a HTTPS.
        if (uri.Scheme == "http" && uri.IsLoopback && uri.Port == 5080 &&
            uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo))
            return "https://localhost:5443/";
        if (uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("Ingresá una dirección HTTPS válida.");
        return uri.AbsoluteUri.TrimEnd('/') + "/";
    }

    private static string RutaAjustes() => Path.Combine(EntornoCliente.CarpetaCliente, "configuracion.json");

    private static string TextoFirma(ResultadoFirmaDigital firma) => firma.Estado != EstadoComprobacion.Completada
        ? "No comprobada" : firma.Firma switch
        {
            EstadoFirmaDigital.Valida => "Válida",
            EstadoFirmaDigital.SinFirma => "Sin firma",
            EstadoFirmaDigital.Invalida => "Inválida",
            _ => "Indeterminada"
        };

    private static string EstadoAmigable(EstadoAnalisis estado) => estado switch
    {
        EstadoAnalisis.EnCola => "En espera",
        EstadoAnalisis.EnProceso => "Analizando",
        EstadoAnalisis.Completado => "Procesamiento terminado",
        EstadoAnalisis.Fallido => "Procesamiento con errores",
        EstadoAnalisis.Cancelado => "Cancelado",
        _ => "Pendiente"
    };

    private static string RiesgoAmigable(EvaluacionRiesgo riesgo) => riesgo switch
    {
        EvaluacionRiesgo.AmenazaDetectada => "Amenaza reportada",
        EvaluacionRiesgo.Sospechoso => "Requiere precaución",
        EvaluacionRiesgo.SinIndicadoresDetectados => "Sin indicadores detectados",
        _ => "Requiere revisión"
    };

    private static JsonSerializerOptions CrearJsonInforme()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        opciones.Converters.Add(new JsonStringEnumConverter());
        return opciones;
    }

    // Métodos de acciones para los botones
    private async Task ReiniciarAnalisisAsync()
    {
        if (_actual is null || _cola.CurrentCount == 0) return;
        // Recuperar el trabajo existente con su identidad y estado originales.
        // ReanudarEnvioAsync prepara el token y coordina el semaforo del cliente.
        await ReanudarEnvioAsync(_actual);
    }

    private async Task SincronizarEstadoArchivoAsync(EnvioCliente envio, CancellationToken ct)
    {
        string carpeta = EntornoCliente.CarpetaCuarentena ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureGate", "Cuarentena");
        Guid id = envio.Solicitud.AnalisisLocal.Archivo.ArchivoId;
        if (!Path.GetFullPath(envio.RutaArchivo).Equals(Path.GetFullPath(Path.Combine(carpeta, id + ".bytes")),
                StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(carpeta, id + ".json"))) return;
        var r = await new GestorCuarentena(carpeta).ConsultarRegistroAsync(id, ct);
        if (r.Estado == EstadoArchivo.Liberado) envio.EstadoLocal = "Archivo liberado bajo responsabilidad; informe conservado";
        else if (r.Estado == EstadoArchivo.Eliminado) envio.EstadoLocal = "Archivo eliminado; informe conservado";
        else return;
        envio.DecisionUsuarioPendiente = false;
        envio.DecisionUsuario ??= r.Estado == EstadoArchivo.Eliminado ? "Eliminar" :
            r.MetodoAutorizacion == "ConfirmacionExcepcional" ? "LiberarConRiesgo" : "Devolver";
        envio.FechaDecisionUsuarioUtc ??= r.FechaAutorizacionLiberacionUtc ?? r.FechaActualizacionUtc;
        await _envios.GuardarAsync(envio, ct);
    }

    private static bool EsMuestraRetenida(EnvioCliente envio)
    {
        string carpeta = EntornoCliente.CarpetaCuarentena ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecureGate", "Cuarentena");
        string esperada = Path.Combine(carpeta,
            envio.Solicitud.AnalisisLocal.Archivo.ArchivoId + ".bytes");
        return Path.GetFullPath(envio.RutaArchivo).Equals(Path.GetFullPath(esperada),
            StringComparison.OrdinalIgnoreCase) && File.Exists(esperada);
    }

    private async Task EliminarArchivoAsync()
    {
        if (_actual is { } envio) await AplicarDecisionAsync(envio, AccionDecisionArchivo.Eliminar);
    }

    private async Task LiberarArchivoAsync()
    {
        if (_actual is { } envio && !_decisionVisible)
            await MostrarDecisionAsync(envio);
    }

    private sealed class AjustesCliente
    {
        public string Servidor { get; init; } = "https://localhost:5443/";
        public string? CarpetaMonitoreo { get; init; }
        public bool RetencionAutomatica { get; init; } = true;
    }
}
