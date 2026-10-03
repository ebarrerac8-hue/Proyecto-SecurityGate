using SecureGate.Analisis;
using SecureGate.Monitoreo;
using Seguregate;

namespace SecureGate;

public partial class Form1
{
    private sealed record Registro(InformacionArchivo Archivo, ResultadoReputacion Reputacion,
        ResultadoEvaluacion Evaluacion, DateTime Fecha);
    private readonly DownloadMonitor _monitor = new();
    private readonly VirusTotalClient _virusTotal = new();
    private readonly SemaphoreSlim _cola = new(1, 1);
    private string _claveApi = "";
    private Registro? _actual;
    private NotifyIcon? _bandeja;
    private bool _salir;
    private bool _cerrado;

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
        groupBoxSandbox.Text = "Evaluación y prueba aislada";
        groupBoxSandbox.Dock = DockStyle.Top;
        groupBoxSandbox.Height = 210;
        lblSandboxTitle.SetBounds(12, 25, 270, 40);
        lblSandboxStatus.SetBounds(12, 67, 270, 90);
        btnConfigureSandbox.SetBounds(12, 162, 250, 35);
        btnConfigureSandbox.Text = "Probar en Sandbox";
        btnConfigureSandbox.Enabled = false;
        groupBoxSandbox.Controls.AddRange(new Control[] { lblSandboxTitle, lblSandboxStatus, btnConfigureSandbox });
        panelRight.Controls.Add(groupBoxSandbox);
        panelRight.Controls.SetChildIndex(groupBoxSummary, 0);
        lblSandboxTitle.Text = "Seleccioná un archivo";
        lblSandboxStatus.Text = "Configurá la clave de VirusTotal para consultar reputación.";
        btnConfigureSandbox.Click += async (_, _) => await ProbarSandboxAsync();
        btnAnalyze.Click += selectFileButton_Click;
        btnHome.Click += (_, _) => { RestaurarVentana(); selectFileButton.Focus(); };
        btnHistory.Click += (_, _) => { RestaurarVentana(); listViewRecent.Focus(); };
        btnReports.Click += (_, _) => ExportarInforme();
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
        menu.Items.Add("Salir", null, (_, _) => { _salir = true; Close(); });
        _bandeja = new NotifyIcon
        {
            Icon = SystemIcons.Shield, Text = "SecureGate", Visible = true, ContextMenuStrip = menu
        };
        _bandeja.DoubleClick += (_, _) => RestaurarVentana();
        _monitor.ArchivoDetectado += ruta => EnInterfaz(() => { _ = AnalizarAsync(ruta, true); });
        _monitor.ErrorMonitoreo += mensaje => EnInterfaz(() => Notificar(mensaje));
        Shown += (_, _) => IniciarMonitor();
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosing += (_, e) =>
        {
            if (!_salir && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                Notificar("SecureGate continúa monitoreando. Para cerrarlo, elegí Salir en su icono junto al reloj.");
            }
        };
        FormClosed += (_, _) =>
        {
            _cerrado = true;
            _monitor.Dispose();
            if (_bandeja != null) { _bandeja.Visible = false; _bandeja.ContextMenuStrip?.Dispose(); _bandeja.Dispose(); }
        };
    }
    private void EnInterfaz(Action accion)
    {
        if (_cerrado || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(new Action(() => { if (!_cerrado) accion(); })); }
        catch (InvalidOperationException) { }
    }
    private void RestaurarVentana()
    {
        Show(); WindowState = FormWindowState.Normal; Activate();
    }
    private void Notificar(string texto)
    {
        if (_cerrado) return;
        _bandeja?.ShowBalloonTip(5000, "SecureGate", texto, ToolTipIcon.Warning);
    }
    private void IniciarMonitor(string? carpeta = null)
    {
        try
        {
            _monitor.IniciarMonitoreo(carpeta);
            lblTitle.Text = "SecureGate — monitoreo activo";
        }
        catch (Exception ex)
        {
            lblTitle.Text = "SecureGate — monitoreo desactivado";
            MessageBox.Show(this, ex.Message + "\nPodés elegir otra carpeta en Configuración.", "Monitoreo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private async Task AnalizarAsync(string ruta, bool automatico)
    {
        await _cola.WaitAsync();
        try
        {
            if (_cerrado) return;
            btnConfigureSandbox.Enabled = false;
            lblReputation.Text = "Analizando archivo; consultando reputación cuando corresponda...";
            var info = await Task.Run(() => FileAnalyzer.AnalizarArchivo(ruta));
            if (_cerrado) return;
            string clave = _claveApi;
            ResultadoReputacion reputacion = string.IsNullOrWhiteSpace(clave)
                ? new() { Sha256 = info.HashSHA256, Estado = EstadoConsulta.NoDisponible, Mensaje = "Consulta no realizada: falta configurar la clave de VirusTotal." }
                : await _virusTotal.ConsultarHashAsync(info.HashSHA256, clave);
            if (_cerrado) return;
            var evaluacion = new EvaluadorInicial().Evaluar(info, reputacion);
            var registro = new Registro(info, reputacion, evaluacion, DateTime.Now);
            var item = new ListViewItem(new[] { info.Nombre, info.RutaCompleta, registro.Fecha.ToString("g"), evaluacion.NivelRiesgo }) { Tag = registro };
            listViewRecent.Items.Insert(0, item);
            MostrarRegistro(registro);
            if (automatico && evaluacion.RequiereAtencion)
                Notificar(info.Nombre + ": " + evaluacion.NivelRiesgo + ". Abrí SecureGate para ver los detalles.");
        }
        catch (Exception ex)
        {
            if (!_cerrado)
            {
                lblReputation.Text = "No se pudo analizar: " + ex.Message;
                // Nunca conservar habilitada la prueba de un resultado anterior tras un error.
                _actual = null;
                btnConfigureSandbox.Enabled = false;
                if (automatico) Notificar("No se pudo analizar " + Path.GetFileName(ruta));
                else MessageBox.Show(this, ex.Message, "Análisis", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally { _cola.Release(); }
    }
    private void MostrarSeleccion()
    {
        if (listViewRecent.SelectedItems.Count > 0 && listViewRecent.SelectedItems[0].Tag is Registro registro)
            MostrarRegistro(registro);
    }
    private void MostrarRegistro(Registro registro)
    {
        _actual = registro;
        var info = registro.Archivo;
        lblName.Text = "Nombre: " + info.Nombre;
        lblType.Text = "Tipo: " + info.Extension;
        lblSize.Text = "Tamaño: " + info.TamañoFormateado;
        lblHash.Text = "SHA-256: " + info.HashSHA256;
        lblSignature.Text = "Firma: " + info.EstadoFirma + (info.TieneFirma ? "\nFirmante: " + info.Firmante : "");
        var rep = registro.Reputacion;
        lblReputation.Text = rep.Estado == EstadoConsulta.InformeDisponible
            ? $"VirusTotal: {rep.Maliciosos} maliciosos, {rep.Sospechosos} sospechosos, {rep.SinDeteccion} sin detección."
            : rep.Mensaje;
        lblSandboxTitle.Text = registro.Evaluacion.NivelRiesgo;
        lblSandboxStatus.Text = registro.Evaluacion.Motivo;
        btnConfigureSandbox.Enabled = File.Exists(info.RutaCompleta);
    }
    private void MostrarConfiguracion()
    {
        using var ventana = new Form { Text = "Configuración de SecureGate", ClientSize = new Size(530, 230), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var etiqueta = new Label { Text = "Clave de VirusTotal (se conserva solo mientras la app esté abierta):", AutoSize = true, Location = new Point(12, 12) };
        var clave = new TextBox { UseSystemPasswordChar = true, Text = _claveApi, Location = new Point(12, 40), Width = 505 };
        var ruta = new TextBox { ReadOnly = true, Text = string.IsNullOrEmpty(_monitor.CarpetaActual) ? DownloadMonitor.ObtenerDescargas() : _monitor.CarpetaActual, Location = new Point(12, 110), Width = 390 };
        var buscar = new Button { Text = "Carpeta…", Location = new Point(410, 108), Width = 105 };
        buscar.Click += (_, _) => { using var dialogo = new FolderBrowserDialog(); if (dialogo.ShowDialog(ventana) == DialogResult.OK) ruta.Text = dialogo.SelectedPath; };
        var guardar = new Button { Text = "Aplicar", DialogResult = DialogResult.OK, Location = new Point(410, 175), Width = 105 };
        ventana.Controls.AddRange(new Control[] { etiqueta, clave, new Label { Text = "Carpeta que se monitorea:", AutoSize = true, Location = new Point(12, 85) }, ruta, buscar, guardar });
        ventana.AcceptButton = guardar;
        if (ventana.ShowDialog(this) != DialogResult.OK) return;
        _claveApi = clave.Text.Trim();
        IniciarMonitor(ruta.Text);
        if (_actual != null) lblSandboxStatus.Text = "Configuración aplicada. Volvé a analizar el archivo para actualizar su reputación.";
    }
    private async Task ProbarSandboxAsync()
    {
        var registro = _actual;
        if (registro == null) return;
        string aviso = "Se compartirá una copia de este archivo con Sandbox, de solo lectura y sin red.\nDentro de Sandbox podrás abrirlo manualmente. No se generará un diagnóstico automático de su comportamiento.\n\n¿Querés continuar?";
        if (MessageBox.Show(this, aviso, "Prueba aislada", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        btnConfigureSandbox.Enabled = false;
        try
        {
            var respuesta = await Task.Run(() =>
            {
                bool exito = SandboxLauncher.EjecutarEnSandbox(registro.Archivo.RutaCompleta, out string error, registro.Archivo.HashSHA256);
                return (exito, error);
            });
            if (_cerrado) return;
            MessageBox.Show(this, respuesta.exito
                ? "Se solicitó abrir Sandbox. Buscá la muestra en C:\\SecureGate\\Muestra dentro de Sandbox. Que abra correctamente no demuestra que el archivo sea seguro."
                : respuesta.error, "Windows Sandbox", MessageBoxButtons.OK, respuesta.exito ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            if (!_cerrado) MessageBox.Show(this, "No se pudo preparar Sandbox: " + ex.Message, "Sandbox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { if (!_cerrado) btnConfigureSandbox.Enabled = _actual != null && File.Exists(_actual.Archivo.RutaCompleta); }
    }
    private void ExportarInforme()
    {
        if (_actual == null) { MessageBox.Show(this, "Seleccioná un análisis del historial."); return; }
        using var dialogo = new SaveFileDialog { Filter = "Informe de texto|*.txt", FileName = "SecureGate_Informe.txt" };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;
        var r = _actual;
        try
        {
            File.WriteAllText(dialogo.FileName, $"SecureGate — análisis preventivo\nFecha: {r.Fecha:g}\nArchivo: {r.Archivo.Nombre}\nRuta: {r.Archivo.RutaCompleta}\nTamaño: {r.Archivo.TamañoFormateado}\nSHA-256: {r.Archivo.HashSHA256}\nFirma: {r.Archivo.EstadoFirma}\nFirmante: {r.Archivo.Firmante}\nReputación: {r.Reputacion.Mensaje}\nMotores maliciosos: {r.Reputacion.Maliciosos}\nMotores sospechosos: {r.Reputacion.Sospechosos}\nMotores sin detección: {r.Reputacion.SinDeteccion}\nEvaluación: {r.Evaluacion.NivelRiesgo}\nMotivo: {r.Evaluacion.Motivo}\n\nNo garantiza seguridad. No incluye observación automática del comportamiento dentro de Sandbox.\n");
        }
        catch (Exception ex) { MessageBox.Show(this, "No se pudo guardar: " + ex.Message); }
    }
}
