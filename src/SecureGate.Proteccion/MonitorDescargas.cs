using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecureGate.Contratos;

namespace SecureGate.Proteccion
{
    public class MonitorDescargas : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly IGestorCuarentena _gestorCuarentena;
        private readonly IAnalizadorLocal _analizadorLocal;
        private readonly ConcurrentDictionary<string, DateTime> _archivosEnProceso = new();
        private readonly string[] _extensionesPermitidas = { ".exe", ".msi", ".bat", ".cmd", ".ps1" };

        // Eventos para notificar a la aplicación principal (interfaz de Osbin)
        public event Action<ResultadoOperacionArchivo, ResultadoAnalisisLocal>? OnArchivoProcesado;
        public event Action<string, Exception>? OnError;

        public MonitorDescargas(IGestorCuarentena gestorCuarentena, IAnalizadorLocal analizadorLocal, string? rutaCarpeta = null)
        {
            _gestorCuarentena = gestorCuarentena;
            _analizadorLocal = analizadorLocal;

            // Ruta por defecto: C:\Users\<Usuario>\Downloads
            string carpetaDescargas = rutaCarpeta ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads"
            );

            if (!Directory.Exists(carpetaDescargas))
            {
                Directory.CreateDirectory(carpetaDescargas);
            }

            _watcher = new FileSystemWatcher(carpetaDescargas)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };

            _watcher.Created += OnEventoArchivo;
            _watcher.Changed += OnEventoArchivo;
            _watcher.Renamed += OnEventoRenombrado;
        }

        public void Iniciar()
        {
            _watcher.EnableRaisingEvents = true;
        }

        public void Detener()
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void OnEventoRenombrado(object sender, RenamedEventArgs e)
        {
            _ = ProcesarArchivoAsync(e.FullPath);
        }

        private void OnEventoArchivo(object sender, FileSystemEventArgs e)
        {
            _ = ProcesarArchivoAsync(e.FullPath);
        }

        private async Task ProcesarArchivoAsync(string ruta)
        {
            string extension = Path.GetExtension(ruta).ToLowerInvariant();

            // 1. Filtrar solo las extensiones permitidas
            if (Array.IndexOf(_extensionesPermitidas, extension) < 0)
                return;

            // 2. Control de eventos duplicados (ignorar si se detectó el mismo archivo hace menos de 5 segundos)
            if (_archivosEnProceso.TryGetValue(ruta, out var ultimaVez))
            {
                if ((DateTime.UtcNow - ultimaVez).TotalSeconds < 5)
                    return;
            }

            _archivosEnProceso[ruta] = DateTime.UtcNow;

            try
            {
                // 3. Esperar a que el navegador termine de escribir el archivo
                bool listo = await EsperarCompletarDescargaAsync(ruta, TimeSpan.FromSeconds(30));
                if (!listo)
                {
                    OnError?.Invoke(ruta, new TimeoutException("El archivo no terminó de descargarse dentro del tiempo límite."));
                    return;
                }

                // 4. Retener el archivo en cuarentena (bloqueo FileShare.None)
                var resultadoRetencion = await _gestorCuarentena.RetenerAsync(ruta);

                // 5. Analizar localmente (SHA-256, Firma Digital, Microsoft Defender)
                var resultadoAnalisis = await _analizadorLocal.AnalizarAsync(resultadoRetencion.ArchivoId);

                // 6. Emitir evento con los resultados para el resto del sistema
                OnArchivoProcesado?.Invoke(resultadoRetencion, resultadoAnalisis);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ruta, ex);
            }
            finally
            {
                _archivosEnProceso.TryRemove(ruta, out _);
            }
        }

        private async Task<bool> EsperarCompletarDescargaAsync(string ruta, TimeSpan timeout)
        {
            var inicio = DateTime.UtcNow;

            while (DateTime.UtcNow - inicio < timeout)
            {
                if (File.Exists(ruta))
                {
                    try
                    {
                        // Intentamos abrirlo de forma exclusiva; si lo logramos, la descarga finalizó
                        using (var stream = new FileStream(ruta, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            if (stream.Length > 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch (IOException)
                    {
                        // El archivo sigue bloqueado por el navegador
                    }
                }

                await Task.Delay(500); // Reintentar cada medio segundo
            }

            return false;
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }

}