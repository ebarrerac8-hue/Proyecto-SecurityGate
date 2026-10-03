using System;
using System.IO;
using System.Threading.Tasks;

namespace SecureGate.Monitoreo
{
    public class DownloadMonitor
    {
        private FileSystemWatcher _watcher;

        // Evento que se dispara cuando se detecta un archivo nuevo y completo
        public event Action<string> ArchivoDetectado;

        public void IniciarMonitoreo()
        {
            // Obtener automáticamente la ruta C:\Users\<Usuario>\Downloads
            string rutaDescargas = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );

            if (!Directory.Exists(rutaDescargas))
            {
                return;
            }

            _watcher = new FileSystemWatcher(rutaDescargas)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            // Detectar creación directa o cuando el navegador renombre el archivo al terminar
            _watcher.Created += OnArchivoCambio;
            _watcher.Renamed += OnArchivoRenombrado;
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

            // Ignorar archivos temporales de navegadores (Chrome, Edge, Firefox)
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
                            return true;
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(500);
                }
            }
            return false;
        }
    }
}