using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SecureGate.Contratos;

namespace SecureGate.Proteccion
{
    // Esta clase interna nos sirve para guardar la memoria en el disco
    public class RegistroArchivoEnCuarentena
    {
        public Guid ArchivoId { get; set; }
        public string RutaOriginal { get; set; } = string.Empty;
    }

    public class GestorCuarentena : IGestorCuarentena
    {
        private readonly string _directorioCuarentena;
        private readonly string _rutaRegistro;
        private ConcurrentDictionary<Guid, RegistroArchivoEnCuarentena> _registro;

        public GestorCuarentena()
        {
            _directorioCuarentena = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecureGate",
                "Cuarentena"
            );
            _rutaRegistro = Path.Combine(_directorioCuarentena, "registro_cuarentena.json");

            if (!Directory.Exists(_directorioCuarentena))
            {
                Directory.CreateDirectory(_directorioCuarentena);
            }

            CargarRegistro();
        }

        private void CargarRegistro()
        {
            if (File.Exists(_rutaRegistro))
            {
                string json = File.ReadAllText(_rutaRegistro);
                _registro = JsonSerializer.Deserialize<ConcurrentDictionary<Guid, RegistroArchivoEnCuarentena>>(json)
                            ?? new ConcurrentDictionary<Guid, RegistroArchivoEnCuarentena>();
            }
            else
            {
                _registro = new ConcurrentDictionary<Guid, RegistroArchivoEnCuarentena>();
            }
        }

        private void GuardarRegistro()
        {
            string json = JsonSerializer.Serialize(_registro, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_rutaRegistro, json);
        }

        public Task<ResultadoOperacionArchivo> RetenerAsync(string rutaArchivo, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(rutaArchivo))
                    throw new FileNotFoundException("El archivo original no existe.");

                Guid nuevoId = Guid.NewGuid();
                string rutaCuarentena = Path.Combine(_directorioCuarentena, $"{nuevoId}.bytes");

                // BLOQUEO CRÍTICO: Abrimos el archivo con FileShare.None para que nadie más pueda usarlo/ejecutarlo
                using (var sourceStream = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.None))
                using (var destStream = new FileStream(rutaCuarentena, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(destStream);
                }

                // Si la copia con bloqueo fue exitosa, eliminamos el original
                File.Delete(rutaArchivo);

                // Guardamos en la memoria (persistencia para reinicios)
                _registro.TryAdd(nuevoId, new RegistroArchivoEnCuarentena
                {
                    ArchivoId = nuevoId,
                    RutaOriginal = rutaArchivo
                });
                GuardarRegistro();

                return new ResultadoOperacionArchivo
                {
                    ArchivoId = nuevoId,
                    Realizada = true,
                    EstadoActual = EstadoArchivo.Retenido,
                    Mensaje = "Archivo retenido y bloqueado exitosamente."
                };
            }, cancellationToken);
        }

        public Task<ResultadoOperacionArchivo> EliminarAsync(Guid archivoId, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string rutaCuarentena = Path.Combine(_directorioCuarentena, $"{archivoId}.bytes");
                bool eliminado = false;

                if (File.Exists(rutaCuarentena))
                {
                    File.Delete(rutaCuarentena);
                    eliminado = true;
                }

                if (_registro.TryRemove(archivoId, out _))
                {
                    GuardarRegistro();
                }

                return new ResultadoOperacionArchivo
                {
                    ArchivoId = archivoId,
                    Realizada = eliminado,
                    EstadoActual = eliminado ? EstadoArchivo.Eliminado : EstadoArchivo.NoDeterminado,
                    Mensaje = eliminado ? "Archivo eliminado permanentemente." : "Archivo no encontrado."
                };
            }, cancellationToken);
        }

        public Task<ResultadoOperacionArchivo> LiberarAsync(SolicitudLiberacion solicitud, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string rutaCuarentena = Path.Combine(_directorioCuarentena, $"{solicitud.ArchivoId}.bytes");

                if (!File.Exists(rutaCuarentena) || !_registro.TryGetValue(solicitud.ArchivoId, out var datosOriginales))
                {
                    return new ResultadoOperacionArchivo
                    {
                        ArchivoId = solicitud.ArchivoId,
                        Realizada = false,
                        EstadoActual = EstadoArchivo.Retenido,
                        Mensaje = "No se encontró el archivo o su registro original."
                    };
                }

                // VALIDACIÓN DE SEGURIDAD: Verificar el SHA-256 antes de liberar
                string hashActual = CalcularSha256(rutaCuarentena);
                if (!hashActual.Equals(solicitud.Sha256Esperado, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Alerta de Seguridad: El hash del archivo en cuarentena no coincide con el esperado.");
                }

                // Restaurar a la ubicación original exacta
                File.Move(rutaCuarentena, datosOriginales.RutaOriginal);

                _registro.TryRemove(solicitud.ArchivoId, out _);
                GuardarRegistro();

                return new ResultadoOperacionArchivo
                {
                    ArchivoId = solicitud.ArchivoId,
                    Realizada = true,
                    EstadoActual = EstadoArchivo.Liberado,
                    Mensaje = $"Archivo restaurado exitosamente en {datosOriginales.RutaOriginal}"
                };
            }, cancellationToken);
        }

        private string CalcularSha256(string ruta)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(ruta))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}