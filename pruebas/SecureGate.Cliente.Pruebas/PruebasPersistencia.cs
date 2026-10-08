using System.Text.Json;
using SecureGate.Cliente;
using SecureGate.Contratos;

internal static class PruebasPersistencia
{
    public static async Task EjecutarAsync()
    {
        string carpeta = Path.Combine(Path.GetTempPath(), "SecureGate_" + Guid.NewGuid().ToString("N"));
        try
        {
            var envio = new EnvioCliente
            {
                Servidor = "http://127.0.0.1:5080/",
                RutaArchivo = Path.Combine(carpeta, "prueba.exe"),
                Solicitud = new SolicitudAnalisis
                {
                    SolicitudId = Guid.NewGuid(),
                    AnalisisLocal = new ResultadoAnalisisLocal
                    {
                        Archivo = new InformacionArchivo
                        {
                            ArchivoId = Guid.NewGuid(),
                            NombreOriginal = "prueba.exe",
                            Extension = ".exe",
                            Sha256 = new string('a', 64),
                            TamanoBytes = 3
                        }
                    }
                }
            };

            await new AlmacenEnviosCliente(carpeta).GuardarAsync(envio);
            var recuperado = (await new AlmacenEnviosCliente(carpeta).ListarAsync()).Single();
            if (recuperado.Solicitud.SolicitudId != envio.Solicitud.SolicitudId ||
                recuperado.AnalisisId is not null || recuperado.Terminado ||
                recuperado.Servidor != envio.Servidor || recuperado.RutaArchivo != envio.RutaArchivo)
                throw new Exception("No se recuperó la solicitud pendiente.");
            Console.WriteLine("CORRECTO: conserva SolicitudId, ruta y servidor antes de recibir la aceptación.");

            recuperado.AnalisisId = Guid.NewGuid();
            recuperado.EstadoLocal = "Registrado";
            await new AlmacenEnviosCliente(carpeta).GuardarAsync(recuperado);
            var registrado = (await new AlmacenEnviosCliente(carpeta).ListarAsync()).Single();
            if (registrado.AnalisisId != recuperado.AnalisisId ||
                Directory.GetFiles(carpeta, "*.json").Length != 1)
                throw new Exception("No conservó AnalisisId o duplicó el registro.");
            Console.WriteLine("CORRECTO: conserva AnalisisId sin duplicar el registro local.");

            registrado.Resultado = new ResultadoAnalisis
            {
                AnalisisId = registrado.AnalisisId!.Value,
                Archivo = registrado.Solicitud.AnalisisLocal.Archivo,
                Estado = EstadoAnalisis.Completado,
                Evaluacion = EvaluacionRiesgo.Incompleto,
                Resumen = "Resultado de prueba."
            };
            await new AlmacenEnviosCliente(carpeta).GuardarAsync(registrado);
            var terminado = (await new AlmacenEnviosCliente(carpeta).ListarAsync()).Single();
            if (!terminado.Terminado || terminado.Resultado?.Resumen != "Resultado de prueba." ||
                Directory.GetFiles(carpeta, "*.tmp").Length != 0)
                throw new Exception("No conservó el resultado terminal o dejó un temporal.");
            Console.WriteLine("CORRECTO: conserva el informe final y reconoce el trabajo terminado.");

            await File.WriteAllTextAsync(Path.Combine(carpeta, "corrupto.json"), "{");
            try
            {
                await new AlmacenEnviosCliente(carpeta).ListarAsync();
                throw new Exception("Ignoró un registro corrupto.");
            }
            catch (JsonException)
            {
                Console.WriteLine("CORRECTO: detecta un registro corrupto sin descartarlo silenciosamente.");
            }
            Console.WriteLine("LAS CUATRO PRUEBAS DE PERSISTENCIA TERMINARON CORRECTAMENTE.");
        }
        finally
        {
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
        }
    }
}

