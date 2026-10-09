using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SecureGate.Contratos;
using SecureGate.Cliente;

namespace SecureGate
{
    /// <summary>
    /// Gestiona la persistencia del historial de análisis en JSON.
    /// </summary>
    public class HistorialPersistencia
    {
        private readonly string _rutaHistorial;
        private static readonly JsonSerializerOptions JsonOpciones = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public HistorialPersistencia()
        {
            var carpetaApp = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecureGate"
            );

            if (!Directory.Exists(carpetaApp))
                Directory.CreateDirectory(carpetaApp);

            _rutaHistorial = Path.Combine(carpetaApp, "historial.json");
        }

        /// <summary>
        /// Carga el historial desde el archivo JSON.
        /// </summary>
        public async Task<List<RegistroHistorial>> CargarAsync()
        {
            if (!File.Exists(_rutaHistorial))
                return new List<RegistroHistorial>();

            try
            {
                var json = await File.ReadAllTextAsync(_rutaHistorial);
                return JsonSerializer.Deserialize<List<RegistroHistorial>>(json, JsonOpciones) 
                    ?? new List<RegistroHistorial>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando historial: {ex.Message}");
                return new List<RegistroHistorial>();
            }
        }

        /// <summary>
        /// Guarda el historial en el archivo JSON.
        /// </summary>
        public async Task GuardarAsync(List<RegistroHistorial> registros)
        {
            try
            {
                var json = JsonSerializer.Serialize(registros, JsonOpciones);
                await File.WriteAllTextAsync(_rutaHistorial, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando historial: {ex.Message}");
            }
        }

        /// <summary>
        /// Agrega un nuevo registro al historial y lo persiste.
        /// </summary>
        public async Task AgregarRegistroAsync(EnvioCliente envio)
        {
            try
            {
                var registros = await CargarAsync();
                var archivo = envio.Solicitud.AnalisisLocal.Archivo;

                var nuevoRegistro = new RegistroHistorial
                {
                    Id = envio.Solicitud.SolicitudId,
                    NombreArchivo = archivo.NombreOriginal,
                    TamanoBytes = archivo.TamanoBytes,
                    Sha256 = archivo.Sha256,
                    FechaAnalisis = DateTime.Now,
                    Estado = envio.Resultado?.Estado.ToString() ?? envio.EstadoLocal,
                    Evaluacion = envio.Resultado?.Evaluacion.ToString(),
                    Resumen = envio.Resultado?.Resumen,
                    UltimoError = envio.UltimoError
                };

                registros.Insert(0, nuevoRegistro);

                // Mantener solo los últimos 1000 registros
                if (registros.Count > 1000)
                    registros = registros.Take(1000).ToList();

                await GuardarAsync(registros);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error agregando registro: {ex.Message}");
            }
        }

        /// <summary>
        /// Actualiza un registro existente en el historial.
        /// </summary>
        public async Task ActualizarRegistroAsync(EnvioCliente envio)
        {
            try
            {
                var registros = await CargarAsync();
                var registro = registros.FirstOrDefault(r => r.Id == envio.Solicitud.SolicitudId);

                if (registro == null)
                {
                    await AgregarRegistroAsync(envio);
                    return;
                }

                registro.Estado = envio.Resultado?.Estado.ToString() ?? envio.EstadoLocal;
                registro.Evaluacion = envio.Resultado?.Evaluacion.ToString();
                registro.Resumen = envio.Resultado?.Resumen;
                registro.UltimoError = envio.UltimoError;
                registro.FechaActualizacion = DateTime.Now;

                await GuardarAsync(registros);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando registro: {ex.Message}");
            }
        }

        /// <summary>
        /// Exporta el historial a un archivo de texto.
        /// </summary>
        public async Task ExportarATxtAsync(string rutaDestino)
        {
            try
            {
                var registros = await CargarAsync();
                var texto = new System.Text.StringBuilder();

                texto.AppendLine("═════════════════════════════════════════════════════════════");
                texto.AppendLine("HISTORIAL DE SECUREGATE");
                texto.AppendLine($"Exportado el {DateTime.Now:g}");
                texto.AppendLine("═════════════════════════════════════════════════════════════");
                texto.AppendLine();

                foreach (var registro in registros)
                {
                    texto.AppendLine($"📄 {registro.NombreArchivo}");
                    texto.AppendLine($"   Fecha: {registro.FechaAnalisis:g}");
                    texto.AppendLine($"   Estado: {registro.Estado}");
                    texto.AppendLine($"   Evaluación: {registro.Evaluacion ?? "Pendiente"}");
                    texto.AppendLine($"   Tamaño: {registro.TamanoBytes / (1024.0 * 1024.0):F2} MiB");
                    texto.AppendLine($"   SHA-256: {registro.Sha256}");

                    if (!string.IsNullOrEmpty(registro.Resumen))
                        texto.AppendLine($"   Resumen: {registro.Resumen}");

                    if (!string.IsNullOrEmpty(registro.UltimoError))
                        texto.AppendLine($"   Error: {registro.UltimoError}");

                    texto.AppendLine();
                }

                texto.AppendLine("═════════════════════════════════════════════════════════════");
                texto.AppendLine($"Total de análisis: {registros.Count}");

                await File.WriteAllTextAsync(rutaDestino, texto.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exportando a TXT: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpia los registros más antiguos, manteniendo solo los N últimos.
        /// </summary>
        public async Task LimpiarAntiguosAsync(int cantidadMaxima = 1000)
        {
            try
            {
                var registros = await CargarAsync();
                if (registros.Count > cantidadMaxima)
                {
                    registros = registros.Take(cantidadMaxima).ToList();
                    await GuardarAsync(registros);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error limpiando historial: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Modelo de datos para un registro del historial.
    /// </summary>
    public class RegistroHistorial
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("nombreArchivo")]
        public string NombreArchivo { get; set; }

        [JsonPropertyName("tamanoBytes")]
        public long TamanoBytes { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; }

        [JsonPropertyName("fechaAnalisis")]
        public DateTime FechaAnalisis { get; set; }

        [JsonPropertyName("fechaActualizacion")]
        public DateTime? FechaActualizacion { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; }

        [JsonPropertyName("evaluacion")]
        public string? Evaluacion { get; set; }

        [JsonPropertyName("resumen")]
        public string? Resumen { get; set; }

        [JsonPropertyName("ultimoError")]
        public string? UltimoError { get; set; }
    }
}
