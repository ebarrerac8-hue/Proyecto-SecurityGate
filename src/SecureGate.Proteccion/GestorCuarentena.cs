using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SecureGate.Contratos;

namespace SecureGate.Proteccion
{
    public class GestorCuarentena : IGestorCuarentena
    {
        private readonly string _directorioCuarentena;

        public GestorCuarentena()
        {
            _directorioCuarentena = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecureGate",
                "Cuarentena"
            );

            if (!Directory.Exists(_directorioCuarentena))
            {
                Directory.CreateDirectory(_directorioCuarentena);
            }
        }

        public Task<ResultadoOperacionArchivo> RetenerAsync(string rutaArchivo, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!File.Exists(rutaArchivo))
                    throw new FileNotFoundException("El archivo original no existe.");

                Guid nuevoId = Guid.NewGuid();
                string rutaCuarentena = Path.Combine(_directorioCuarentena, $"{nuevoId}.bytes");
                File.Move(rutaArchivo, rutaCuarentena);

                return new ResultadoOperacionArchivo
                {
                    ArchivoId = nuevoId,
                    Realizada = true,
                    EstadoActual = EstadoArchivo.Retenido,
                    Mensaje = $"Archivo movido a cuarentena exitosamente."
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

                return new ResultadoOperacionArchivo
                {
                    ArchivoId = archivoId,
                    Realizada = eliminado,
                    EstadoActual = eliminado ? EstadoArchivo.Eliminado : EstadoArchivo.NoDeterminado,
                    Mensaje = eliminado ? "Archivo eliminado de cuarentena." : "Archivo no encontrado."
                };
            }, cancellationToken);
        }

        public Task<ResultadoOperacionArchivo> LiberarAsync(SolicitudLiberacion solicitud, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                string rutaCuarentena = Path.Combine(_directorioCuarentena, $"{solicitud.ArchivoId}.bytes");
                bool liberado = false;

                // Nota: Idealmente aquí se pediría la ruta original donde restaurarlo. 
                // Por ahora lo simulamos moviéndolo a una subcarpeta "Liberados"
                string dirLiberados = Path.Combine(_directorioCuarentena, "Liberados");
                if (!Directory.Exists(dirLiberados)) Directory.CreateDirectory(dirLiberados);

                if (File.Exists(rutaCuarentena))
                {
                    string rutaDestino = Path.Combine(dirLiberados, $"{solicitud.ArchivoId}_liberado.bin");
                    File.Move(rutaCuarentena, rutaDestino);
                    liberado = true;
                }

                return new ResultadoOperacionArchivo
                {
                    ArchivoId = solicitud.ArchivoId,
                    Realizada = liberado,
                    EstadoActual = liberado ? EstadoArchivo.Liberado : EstadoArchivo.Retenido,
                    Mensaje = liberado ? "Archivo liberado exitosamente." : "Error al liberar archivo."
                };
            }, cancellationToken);
        }
    }
}