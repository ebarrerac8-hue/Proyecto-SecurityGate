using System;
using System.IO;
using System.Threading.Tasks;
using SecureGate.Proteccion;
using SecureGate.Contratos;

namespace SecureGate.Pruebas
{
    class Program
    {
        public static event Action<ResultadoOperacionArchivo, ResultadoAnalisisLocal>? OnArchivoProcesado;

        static async Task Main(string[] args)
        {
            Console.WriteLine("============================================");
            Console.WriteLine("  PRUEBA EN VIVO: SecureGate.Proteccion     ");
            Console.WriteLine("============================================");

            // 1. Inicializar tus clases
            var gestor = new GestorCuarentena();
            var analizador = new AnalizadorLocal();
            using var monitor = new MonitorDescargas(gestor, analizador);

            // 2. Escuchar los eventos del monitor
            monitor.OnArchivoProcesado += async (retencion, analisis) =>
            {
                Console.WriteLine($"[1/3] Archivo retenido con ID: {retencion.ArchivoId}");

                Console.WriteLine("[2/3] Iniciando analisis con Defender y Hash...");
                var resultadoAnalisis = await analizador.AnalizarAsync(retencion.ArchivoId);

                Console.WriteLine("[3/3] Analisis finalizado. Notificando evento...");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n[✓] ¡PROCESO COMPLETADO EXITOSAMENTE!");
                Console.ResetColor();

                Console.WriteLine($" ID Cuarentena : {retencion.ArchivoId}");
                Console.WriteLine($" Estado        : {retencion.EstadoActual}");
                Console.WriteLine($" Mensaje       : {retencion.Mensaje}");
                Console.WriteLine($" SHA-256       : {resultadoAnalisis.Archivo.Sha256}");
                Console.WriteLine($" Firma Digital : {resultadoAnalisis.FirmaDigital.Firma}");
                Console.WriteLine($" Antivirus     : {resultadoAnalisis.Antivirus.Motor} -> {resultadoAnalisis.Antivirus.Deteccion}");
                Console.WriteLine($" Detalle       : {resultadoAnalisis.Antivirus.Detalle}");
            };

            monitor.OnError += (ruta, ex) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[X] Error procesando {ruta}: {ex.Message}");
                Console.ResetColor();
            };

            // 3. Encender el monitor
            monitor.Iniciar();
            Console.WriteLine("\n-> Monitor activado. Esperando archivos en tu carpeta de Descargas...");
            Console.WriteLine("-> Copia o crea un archivo .exe, .bat o .ps1 en 'Downloads' para probar.\n");
            Console.WriteLine("Presiona ENTER para detener el programa en cualquier momento.");

            await Task.Delay(Timeout.Infinite);
            monitor.Detener();
        }
    }
}