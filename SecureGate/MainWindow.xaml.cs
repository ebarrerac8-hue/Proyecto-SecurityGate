using System.Windows;
using SecureGate.Analisis;
using SecureGate.Monitoreo;

namespace SecureGate
{
    public partial class MainWindow : Window
    {
        private DownloadMonitor _monitor;
        private EvaluadorInicial _evaluador; // Instancia de tu módulo

        public MainWindow()
        {
            InitializeComponent();

            // Inicializar módulos
            _monitor = new DownloadMonitor();
            _evaluador = new EvaluadorInicial();

            _monitor.ArchivoDetectado += OnNuevoArchivoDescargado;
            _monitor.IniciarMonitoreo();
        }

        private void OnNuevoArchivoDescargado(string rutaArchivo)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Analizar metadatos, SHA-256 y firma digital
                    var info = FileAnalyzer.AnalizarArchivo(rutaArchivo);

                    // Entra tu módulo en acción
                    var resultado = _evaluador.Evaluar(info);

                    string reporte = $"¡NUEVO ARCHIVO DETECTADO EN DESCARGAS!\n\n" +
                                     $"Nombre: {info.Nombre}\n" +
                                     $"Extensión: {info.Extension}\n" +
                                     $"Tamaño: {info.TamañoFormateado}\n\n" +
                                     $"--- FIRMA DIGITAL ---\n" +
                                     $"Estado: {info.EstadoFirma}\n" +
                                     $"Firmante: {info.Firmante}\n\n" +
                                     $"--- HASH SHA-256 ---\n" +
                                     $"{info.HashSHA256}\n\n" +
                                     $"--- EVALUACIÓN INICIAL ---\n" +
                                     $"Nivel de Riesgo: {resultado.NivelRiesgo}\n" +
                                     $"Motivo: {resultado.Motivo}\n\n";

                    // Evaluador decide si requiere atención y muestra el Sandbox
                    if (resultado.RequiereAtencion)
                    {
                        reporte += "¿Deseas probarlo de forma aislada dentro de Windows Sandbox?";

                        MessageBoxResult respuesta = MessageBox.Show(reporte, "SecureGate - Alerta Preventiva",
                                                                     MessageBoxButton.YesNo, MessageBoxImage.Warning);

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