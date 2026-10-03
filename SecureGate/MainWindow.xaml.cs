using System.Windows;
using SecureGate.Analisis;
using SecureGate.Monitoreo;

namespace SecureGate
{
    public partial class MainWindow : Window
    {
        private DownloadMonitor _monitor;

        public MainWindow()
        {
            InitializeComponent();

            // Inicializar el monitor de descargas en segundo plano
            _monitor = new DownloadMonitor();
            _monitor.ArchivoDetectado += OnNuevoArchivoDescargado;
            _monitor.IniciarMonitoreo();
        }

        private void OnNuevoArchivoDescargado(string rutaArchivo)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 1. Analizar metadatos, SHA-256 y firma digital
                    var info = FileAnalyzer.AnalizarArchivo(rutaArchivo);

                    // 2. Evaluar si se considera potencialmente riesgoso
                    bool esEjecutable = info.Extension == ".exe" || info.Extension == ".bat" || info.Extension == ".ps1" || info.Extension == ".msi";
                    bool esSospechoso = !info.TieneFirma || esEjecutable;

                    string reporte = $"¡NUEVO ARCHIVO DETECTADO EN DESCARGAS!\n\n" +
                                     $"Nombre: {info.Nombre}\n" +
                                     $"Extensión: {info.Extension}\n" +
                                     $"Tamaño: {info.TamañoFormateado}\n\n" +
                                     $"--- FIRMA DIGITAL ---\n" +
                                     $"Estado: {info.EstadoFirma}\n" +
                                     $"Firmante: {info.Firmante}\n\n" +
                                     $"--- HASH SHA-256 ---\n" +
                                     $"{info.HashSHA256}\n\n";

                    if (esSospechoso)
                    {
                        reporte += "⚠️ ATENCIÓN: El archivo no tiene firma digital o es un ejecutable.\n\n" +
                                   "¿Deseas probarlo de forma aislada dentro de Windows Sandbox?";

                        MessageBoxResult respuesta = MessageBox.Show(reporte, "SecureGate - Alerta Preventiva",
                                                                     MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        // 3. Si el usuario acepta, se invoca el SandboxLauncher
                        if (respuesta == MessageBoxResult.Yes)
                        {
                            string error;
                            bool exito = SandboxLauncher.EjecutarEnSandbox(info.RutaCompleta, out error);

                            if (exito)
                            {
                                MessageBox.Show("Generando entorno aislado de pruebas en Windows Sandbox...",
                                                "SecureGate - Sandbox", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show($"No se pudo iniciar el Sandbox: {error}",
                                                "SecureGate - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                    else
                    {
                        reporte += "✅ El archivo posee una firma digital válida.";
                        MessageBox.Show(reporte, "SecureGate - Análisis Completo",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Error al analizar archivo detectado: {ex.Message}");
                }
            });
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _monitor.DetenerMonitoreo();
            base.OnClosed(e);
        }
    }
}