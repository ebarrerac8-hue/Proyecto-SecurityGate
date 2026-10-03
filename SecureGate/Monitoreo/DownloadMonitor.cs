using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows; 

namespace SecureGate.Monitoreo
{
    public class DownloadMonitor
    {
        private FileSystemWatcher _watcher;

        
        public event Action<string> ArchivoDetectado;

        public void IniciarMonitoreo()
        {
            // Obtener automáticamente la ruta de Descargas 
            string rutaDescargas = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );

            // Si no encuentra la carpeta, avisar en lugar de apagarse en silencio
            if (!Directory.Exists(rutaDescargas))
            {
                MessageBox.Show($"SecureGate no pudo encontrar la carpeta de descargas en:\n{rutaDescargas}\n\nEl monitoreo automático está desactivado.",
                                "SecureGate - Error de Configuración", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _watcher = new FileSystemWatcher(rutaDescargas)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };

            //  Primero conectar los eventos...
            _watcher.Created += OnArchivoCambio;
            _watcher.Renamed += OnArchivoRenombrado;

            //  Despues encender el monitor
            _watcher.EnableRaisingEvents = true;
        }

        public void DetenerMonitoreo()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
        }

        private void OnArchivoCambio(object sender, FileSystemEventArgs e)
        {
            _ = ProcesarArchivoAsync(e.FullPath);
        }

        private void OnArchivoRenombrado(object sender, RenamedEventArgs e)
        {
            _ = ProcesarArchivoAsync(e.FullPath);
        }

        private async Task ProcesarArchivoAsync(string rutaArchivo)
        {
            string extension = Path.GetExtension(rutaArchivo).ToLower();

            // Ignorar archivos temporales de navegadores
            if (extension == ".crdownload" || extension == ".tmp" || extension == ".part")
            {
                return;
            }

            // Esperar a que el navegador suelte el archivo y se pueda leer
            bool listo = await EsperarArchivoDisponibleAsync(rutaArchivo);

            if (listo && File.Exists(rutaArchivo))
            {
                // Notificar que hay un archivo listo para ser analizado
                ArchivoDetectado?.Invoke(rutaArchivo);
            }
        }

        private async Task<bool> EsperarArchivoDisponibleAsync(string ruta)
        {
            // Reintentar hasta 10 veces (5 segundos) mientras el navegador escribe el archivo
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using (FileStream stream = File.Open(ruta, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        if (stream.Length > 0)
                        {
                            return true; // Archivo listo y con contenido
                        }
                    }
                    //  Si el archivo aún tiene 0 bytes, debe pausar antes de volver a intentar
                    await Task.Delay(500);
                }
                catch (IOException)
                {
                    // Si el archivo está bloqueado, espera medio segundo
                    await Task.Delay(500);
                }
            }
            return false;
        }
    }
}