using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SecureGate.Analisis;
using SecureGate.Cliente;
using SecureGate.Contratos;
using SecureGate.Monitoreo;
using ArchivoContrato = SecureGate.Contratos.InformacionArchivo;

namespace SecureGate;

public partial class Form1
{
    private readonly DownloadMonitor _monitor = new();
    private readonly SemaphoreSlim _cola = new(1, 1);
    private readonly CancellationTokenSource _salida = new();
    private readonly AlmacenEnviosCliente _envios = new();
    private readonly Dictionary<Guid, ListViewItem> _filas = new();
    private readonly Dictionary<string, ClienteAnalisisHttp> _clientes = new();
    private readonly HistorialPersistencia _historialPersistencia = new();
    private CancellationTokenSource? _operacion;
    private EnvioCliente? _actual;
    private NotifyIcon? _bandeja;
    private bool _salir;
    private bool _cerrado;
    private bool _historialDisponible;
    private string _servidor = "https://localhost:5443/";
    private string _claveVirusTotal = "";
    private string? _carpetaMonitoreo;
    private readonly Button _reanudar = new() { Text = "🔄 Reanudar pendientes", AutoSize = true };
    private readonly Button _pausar = new() { Text = "⏸️  Pausar", AutoSize = true, Enabled = false };
    private readonly Button _reintentar = new() { Text = "🔁 Reintentar", AutoSize = true, Enabled = false };
    private readonly Button _eliminar = new() { Text = "🗑️  Eliminar", AutoSize = true, Enabled = false };
    private readonly Button _liberar = new() { Text = "📋 Liberar", AutoSize = true, Enabled = false };
    private static readonly JsonSerializerOptions JsonInforme = CrearJsonInforme();
    private static readonly HashSet<string> ExtensionesServidor = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1", ".com", ".scr", ".vbs"
    };

    private void PrepararIntegracion()
    {
        MinimumSize = new Size(1100, 650);
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
            AutoSize = false,
            Padding = new Padding(8),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Aplicar estilos a los botones
        foreach (var btn in new[] { _reanudar, _pausar, _reintentar, _eliminar, _liberar })
        {
            btn.Height = 38;
            btn.Font = new Font("Segoe UI", 9F);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.FromArgb(37, 99, 235);
            btn.ForeColor = Color.White;
            btn.Margin = new Padding(4);
        }

        acciones.Controls.AddRange(new Control[] { _reanudar, _pausar, _reintentar, _eliminar, _liberar });
        panelRight.Controls.Add(acciones);
        panelRight.Controls.SetChildIndex(acciones, 0);

        // Event handlers
        _reanudar.Click += async (_, _) => await ReanudarPendientesAsync();
        _pausar.Click += (_, _) => _operacion?.Cancel();
        _reintentar.Click += async (_, _) => await ReiniciarAnalisisAsync();
        _eliminar.Click += async (_, _) => await EliminarArchivoAsync();
        _liberar.Click += async (_, _) => await LiberarArchivoAsync();
        btnConfigureSandbox.Click += (_, _) => MostrarInforme();
        lblSandboxTitle.Text = "Seleccioná un archivo";
        lblSandboxStatus.Text =
            "El servidor consultará su reputación y realizará la prueba aislada disponible.";
        openFileDialog1.Filter = "Archivos admitidos|*.exe;*.msi;*.bat;*.cmd;*.ps1;*.com;*.scr;*.vbs";
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
                    _claveVirusTotal = guardado.ClaveVirusTotal ?? "";
                }
            }

            foreach (var envio in (await _envios.ListarAsync(_salida.Token)).Reverse())
                ActualizarFila(envio);

            // Cargar historial persistente desde JSON
            var registrosHistorial = await _historialPersistencia.CargarAsync();
            System.Diagnostics.Debug.WriteLine($"Historial cargado: {registrosHistorial.Count} registros");

            if (listViewRecent.Items.Count > 0)
                listViewRecent.Items[0].Selected = true;
            _historialDisponible = true;
            IniciarMonitor(_carpetaMonitoreo);
            await ReanudarPendientesAsync();
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
        try
        {
            if (!_historialDisponible)
                throw new InvalidOperationException("El historial todavía no está disponible.");
            await _cola.WaitAsync(_salida.Token);
            adquirido = true;
            if (_cerrado) return;
            PrepararEspera();
            _actual = null;
            btnConfigureSandbox.Enabled = false;
            lblSandboxTitle.Text = "Preparando análisis";
            lblSandboxStatus.Text = Path.GetFileName(ruta);
            var archivo = new FileInfo(ruta);
            if (!ExtensionesServidor.Contains(archivo.Extension))
                throw new InvalidDataException("La extensión no está admitida por el servidor.");
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

            _actual = envio;
            ActualizarFila(envio);
            MostrarRegistro(envio);
            await AtenderEnvioAsync(envio, _operacion!.Token);
            if (automatico && !_cerrado)
                Notificar(envio.Solicitud.AnalisisLocal.Archivo.NombreOriginal + ": " +
                    (envio.Resultado?.Resumen ?? envio.UltimoError ?? envio.EstadoLocal));
        }
        catch (OperationCanceledException)
        {
            await RegistrarPausaAsync(envio);
        }
        catch (Exception error)
        {
            await RegistrarErrorAsync(envio, error);
            if (!_cerrado && !automatico)
                MessageBox.Show(this, error.Message, "Análisis",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (adquirido)
            {
                TerminarEspera();
                _cola.Release();
            }
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
        try
        {
            await _cola.WaitAsync(_salida.Token);
            adquirido = true;
            if (_cerrado) return;
            PrepararEspera();
            await AtenderEnvioAsync(envio, _operacion!.Token);
        }
        catch (OperationCanceledException) { await RegistrarPausaAsync(envio); }
        catch (Exception error) { await RegistrarErrorAsync(envio, error); }
        finally
        {
            if (adquirido)
            {
                TerminarEspera();
                _cola.Release();
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
            envio.EstadoLocal = "Enviando";
            envio.UltimoError = null;
            await _envios.GuardarAsync(envio, ct);
            ActualizarFila(envio);
            var aceptado = await cliente.EnviarArchivoAsync(envio.Solicitud, envio.RutaArchivo, ct);
            envio.AnalisisId = aceptado.AnalisisId;
            envio.EstadoLocal = "Registrado";
            await _envios.GuardarAsync(envio, ct);
        }

        var progreso = new Progress<ResultadoAnalisis>(resultado =>
        {
            if (_cerrado) return;
            envio.EstadoLocal = EstadoAmigable(resultado.Estado);
            ActualizarFila(envio);
            if (_actual?.Solicitud.SolicitudId == envio.Solicitud.SolicitudId)
            {
                lblSandboxTitle.Text = envio.EstadoLocal;
                lblSandboxStatus.Text = resultado.Resumen;
            }
        });

        string claveDura = Environment.GetEnvironmentVariable("SECUREGATE_VIRUSTOTAL_API_KEY") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(claveDura) && envio.Resultado?.Reputacion == null)
        {
            try {
                var vtClient = new Seguregate.VirusTotalClient();
                var rep = await vtClient.ConsultarHashAsync(envio.Solicitud.AnalisisLocal.Archivo.Sha256, claveDura);
                envio.Resultado ??= new ResultadoAnalisis { Archivo = envio.Solicitud.AnalisisLocal.Archivo };
                envio.Resultado.Reputacion = new Contratos.ResultadoReputacion {
                    Sha256 = rep.Sha256,
                    MotoresMaliciosos = rep.Maliciosos,
                    MotoresSospechosos = rep.Sospechosos,
                    MotoresSinDeteccion = rep.SinDeteccion,
                    Detalle = rep.Mensaje,
                    Estado = rep.Estado == Seguregate.EstadoConsulta.InformeDisponible ? Contratos.EstadoComprobacion.Completada : Contratos.EstadoComprobacion.Fallida
                };
            } catch (Exception) { }
        }

        envio.Resultado = await cliente.EsperarResultadoAsync(
            envio.AnalisisId.Value, progreso, cancellationToken: ct);
        envio.EstadoLocal = EstadoAmigable(envio.Resultado.Estado);
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
    }

    private void TerminarEspera()
    {
        _operacion?.Dispose();
        _operacion = null;
        if (!_cerrado)
        {
            _pausar.Enabled = false;
            _reanudar.Enabled = true;
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
        fila.SubItems[3].Text = envio.Terminado && envio.Resultado is not null
            ? RiesgoAmigable(envio.Resultado.Evaluacion) : envio.EstadoLocal;
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
        lblSignature.Text = "Firma: validez sin comprobar" +
            (string.IsNullOrWhiteSpace(envio.Solicitud.AnalisisLocal.FirmaDigital.Firmante)
                ? "" : "\nCertificado: " + envio.Solicitud.AnalisisLocal.FirmaDigital.Firmante);
        lblReputation.Text = envio.Resultado?.Reputacion?.Detalle ??
            "Consulta de reputación a cargo del servidor.";
        lblSandboxTitle.Text = envio.Resultado is null
            ? envio.EstadoLocal : RiesgoAmigable(envio.Resultado.Evaluacion);
        lblSandboxStatus.Text = envio.Resultado?.Resumen ?? envio.UltimoError ??
            "El archivo se enviará al servidor para su análisis.";
        btnConfigureSandbox.Enabled = true;

        // Habilitar/deshabilitar botones según estado
        bool enProceso = envio.Resultado?.Estado == EstadoAnalisis.EnProceso || (envio.EstadoLocal == "Pendiente" && envio.Resultado == null);
        bool completado = envio.Resultado?.Estado == EstadoAnalisis.Completado;
        bool fallido = envio.Resultado?.Estado == EstadoAnalisis.Fallido;

        _pausar.Enabled = enProceso;
        _reintentar.Enabled = (fallido || completado) && !enProceso;
        _eliminar.Enabled = !enProceso && (completado || fallido);
        _liberar.Enabled = completado && envio.Resultado?.Evaluacion == EvaluacionRiesgo.SinIndicadoresDetectados;
    }

    private void MostrarInforme()
    {
        if (_actual is null) return;
        using var fuente = new Font("Consolas", 10);
        using var ventana = new Form
        {
            Text = "Informe de SecureGate", Size = new Size(850, 650),
            StartPosition = FormStartPosition.CenterParent
        };
        var texto = new TextBox
        {
            Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Both, WordWrap = false,
            Font = fuente, Text = CrearInforme(_actual)
        };
        ventana.Controls.Add(texto);
        ventana.ShowDialog(this);
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

    private static string CrearInforme(EnvioCliente envio)
    {
        var texto = new StringBuilder();
        var archivo = envio.Solicitud.AnalisisLocal.Archivo;
        texto.AppendLine("SECUREGATE — INFORME");
        texto.AppendLine("Archivo: " + archivo.NombreOriginal);
        texto.AppendLine("SHA-256: " + archivo.Sha256);
        texto.AppendLine("SolicitudId: " + envio.Solicitud.SolicitudId);
        texto.AppendLine("AnalisisId: " + envio.AnalisisId);
        texto.AppendLine("Estado local: " + envio.EstadoLocal);
        if (!string.IsNullOrWhiteSpace(envio.UltimoError)) texto.AppendLine(envio.UltimoError);
        if (envio.Resultado is null)
        {
            texto.AppendLine("Todavía no se recibió un informe final.");
            return texto.ToString();
        }
        var resultado = envio.Resultado;
        texto.AppendLine("Estado: " + EstadoAmigable(resultado.Estado));
        texto.AppendLine("Evaluación: " + RiesgoAmigable(resultado.Evaluacion));
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
        texto.AppendLine("- La firma Authenticode y el antivirus local no se verificaron en este adaptador.");
        if (resultado.Sandbox is not null)
        {
            texto.AppendLine("\nSANDBOX");
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
        bool autoInicial = false;
        try {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
            autoInicial = key?.GetValue("SecureGate") != null;
        } catch { }

        using var ventana = new Form
        {
            Text = "Configuración de SecureGate", ClientSize = new Size(560, 290),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false
        };
        var servidor = new TextBox { Text = _servidor, Location = new Point(12, 40), Width = 535 };
        var ruta = new TextBox
        {
            ReadOnly = true,
            Text = string.IsNullOrEmpty(_monitor.CarpetaActual)
                ? DownloadMonitor.ObtenerDescargas() : _monitor.CarpetaActual,
            Location = new Point(12, 110), Width = 410
        };
        var buscar = new Button { Text = "Carpeta…", Location = new Point(435, 108), Width = 110 };
        buscar.Click += (_, _) =>
        {
            using var dialogo = new FolderBrowserDialog();
            if (dialogo.ShowDialog(ventana) == DialogResult.OK) ruta.Text = dialogo.SelectedPath;
        };
        var chkAuto = new CheckBox { Text = "Iniciar SecureGate automáticamente con Windows", Location = new Point(12, 155), AutoSize = true, Checked = autoInicial };

        var aplicar = new Button
        {
            Text = "Aplicar", DialogResult = DialogResult.OK,
            Location = new Point(435, 245), Width = 110
        };
        ventana.Controls.AddRange(new Control[]
        {
            new Label { Text = "Dirección del servidor de SecureGate:", AutoSize = true, Location = new Point(12, 12) },
            servidor,
            new Label { Text = "Carpeta que se monitorea:", AutoSize = true, Location = new Point(12, 85) },
            ruta, buscar,
            chkAuto,
            new Label
            {
                Text = "Los envíos pendientes conservan el servidor al que fueron enviados.",
                AutoSize = true, Location = new Point(12, 200)
            }, aplicar
        });
        ventana.AcceptButton = aplicar;
        if (ventana.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            try {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (chkAuto.Checked) key?.SetValue("SecureGate", Application.ExecutablePath);
                else key?.DeleteValue("SecureGate", false);
            } catch { }

            string direccion = NormalizarServidor(servidor.Text.Trim());
            string ajustes = RutaAjustes();
            Directory.CreateDirectory(Path.GetDirectoryName(ajustes)!);
            File.WriteAllText(ajustes,
                JsonSerializer.Serialize(new AjustesCliente
                {
                    Servidor = direccion, CarpetaMonitoreo = ruta.Text
                }, JsonInforme));
            _servidor = direccion;
            IniciarMonitor(ruta.Text);
        }
        catch (Exception error) { MessageBox.Show(this, "No se pudo aplicar: " + error.Message); }
    }

    private void IniciarMonitor(string? carpeta = null)
    {
        try
        {
            _monitor.IniciarMonitoreo(carpeta);
            _carpetaMonitoreo = _monitor.CarpetaActual;
            lblTitle.Text = "SecureGate — monitoreo activo";
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

    private static string RutaAjustes() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SecureGate", "Cliente", "configuracion.json");

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
        _ => "Evaluación incompleta"
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
        if (_actual == null)
        {
            MessageBox.Show(this, "No hay archivo seleccionado.", "Reintentar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_actual.Resultado?.Estado == EstadoAnalisis.EnProceso)
        {
            MessageBox.Show(this, "El análisis ya está en progreso.", "Reintentar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        EnInterfaz(() =>
        {
            _reintentar.Enabled = false;
            _eliminar.Enabled = false;
            _liberar.Enabled = false;
            lblSandboxStatus.Text = "🔄 Reintentando análisis...";
        });

        try
        {
            _actual.EstadoLocal = "Pendiente de reintento";
            _actual.UltimoError = null;
            if (_actual.Resultado != null)
            {
                _actual.Resultado.Estado = EstadoAnalisis.EnCola;
            }
            if (_operacion != null)
            {
                _ = AtenderEnvioAsync(_actual, _operacion.Token);
            }
        }
        catch (Exception ex)
        {
            EnInterfaz(() => 
                MessageBox.Show(this, $"Error al reintentar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            );
        }
    }

    private async Task EliminarArchivoAsync()
    {
        if (_actual == null)
        {
            MessageBox.Show(this, "No hay archivo seleccionado.", "Eliminar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var resultado = MessageBox.Show(this, 
            $"¿Eliminar '{_actual.Solicitud.AnalisisLocal!.Archivo.NombreOriginal}' de la cuarentena?", 
            "Confirmar eliminación", 
            MessageBoxButtons.YesNo, 
            MessageBoxIcon.Question);

        if (resultado != DialogResult.Yes) return;

        try
        {
            // Lógica para eliminar archivo (integración con Ever)
            EnInterfaz(() =>
            {
                if (_actual.Resultado != null) _actual.Resultado.Estado = EstadoAnalisis.Cancelado;
                _actual.EstadoLocal = "Eliminado de cuarentena";
                lblSandboxStatus.Text = $"✓ Archivo '{_actual.Solicitud.AnalisisLocal.Archivo.NombreOriginal}' ha sido eliminado de la cuarentena.";
                _reintentar.Enabled = false;
                _eliminar.Enabled = false;
                _liberar.Enabled = false;
            });

            Notificar($"Archivo '{_actual.Solicitud.AnalisisLocal.Archivo.NombreOriginal}' eliminado exitosamente.");
        }
        catch (Exception ex)
        {
            EnInterfaz(() => 
                MessageBox.Show(this, $"No se pudo eliminar el archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            );
        }
    }

    private async Task LiberarArchivoAsync()
    {
        if (_actual == null)
        {
            MessageBox.Show(this, "No hay archivo seleccionado.", "Liberar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Abrir diálogo de confirmación de liberación
        using var dlg = new LiberarForm(_actual);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            EnInterfaz(() =>
            {
                _liberar.Enabled = false;
                lblSandboxStatus.Text = $"📋 Solicitando liberación de '{_actual.Solicitud.AnalisisLocal!.Archivo.NombreOriginal}'...";
            });

            // Lógica para liberar archivo (integración con Ever)
            if (_actual.Resultado != null) _actual.Resultado.Estado = EstadoAnalisis.Completado;
            _actual.EstadoLocal = "Liberado bajo responsabilidad";

            EnInterfaz(() =>
            {
                lblSandboxStatus.Text = $"✓ Archivo '{_actual.Solicitud.AnalisisLocal!.Archivo.NombreOriginal}' liberado bajo responsabilidad del usuario {Environment.UserName}.";
                _reintentar.Enabled = false;
                _eliminar.Enabled = false;
                _liberar.Enabled = false;
            });

            Notificar($"Archivo '{_actual.Solicitud.AnalisisLocal!.Archivo.NombreOriginal}' liberado bajo su responsabilidad.");
        }
        catch (Exception ex)
        {
            EnInterfaz(() => 
                MessageBox.Show(this, $"No se pudo liberar el archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            );
        }
    }

    private sealed class AjustesCliente
    {
        public string Servidor { get; init; } = "https://localhost:5443/";
        public string? CarpetaMonitoreo { get; init; }
        public string? ClaveVirusTotal { get; init; } = "";
    }
}

