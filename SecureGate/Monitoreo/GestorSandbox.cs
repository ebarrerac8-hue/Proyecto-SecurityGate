using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Xml;
using System.Xml.Linq;

namespace SecureGate.Monitoreo
{
    public class GestorSandbox
    {
        public bool TienePermisosAdministrador()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return false;

            using (WindowsIdentity identidad = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal usuario = new WindowsPrincipal(identidad);

                return usuario.IsInRole(
                    WindowsBuiltInRole.Administrator
                );
            }
        }

        public string BuscarLanzador()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                return null;

            string windows = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows
            );

            string sistema =
                Environment.Is64BitOperatingSystem &&
                !Environment.Is64BitProcess
                    ? "Sysnative"
                    : "System32";

            string ruta = Path.Combine(
                windows,
                sistema,
                "WindowsSandbox.exe"
            );

            return File.Exists(ruta) ? ruta : null;
        }

        public ResultadoSandbox Iniciar(
            string rutaConfiguracion,
            bool autorizadoPorUsuario)
        {
            if (!autorizadoPorUsuario)
            {
                return Resultado(
                    EstadoSandbox.Cancelado,
                    "El usuario no autorizó la prueba."
                );
            }

            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return Resultado(
                    EstadoSandbox.WindowsRequerido,
                    "Esta función necesita Windows."
                );
            }

            try
            {
                string lanzador = BuscarLanzador();

                if (lanzador == null)
                {
                    return Resultado(
                        EstadoSandbox.LanzadorNoEncontrado,
                        "No se encontró Windows Sandbox. " +
                        "Revisá su instalación y activación."
                    );
                }

                if (string.IsNullOrWhiteSpace(rutaConfiguracion))
                {
                    return Resultado(
                        EstadoSandbox.ConfiguracionInvalida,
                        "No se proporcionó una configuración."
                    );
                }

                string ruta = Path.GetFullPath(
                    rutaConfiguracion.Trim().Trim('"')
                );

                if (!string.Equals(
                    Path.GetExtension(ruta),
                    ".wsb",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return Resultado(
                        EstadoSandbox.ConfiguracionInvalida,
                        "El gestor necesita una configuración .wsb."
                    );
                }

                if (!File.Exists(ruta))
                {
                    return Resultado(
                        EstadoSandbox.ConfiguracionInvalida,
                        "No se encontró la configuración."
                    );
                }

                string problema = ValidarConfiguracion(ruta);

                if (problema != null)
                {
                    return Resultado(
                        EstadoSandbox.ConfiguracionInvalida,
                        problema
                    );
                }

                ProcessStartInfo inicio = new ProcessStartInfo
                {
                    FileName = lanzador,

                   
                    Arguments = "\"" + ruta + "\"",

                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(ruta)
                };

                using (Process proceso = Process.Start(inicio))
                {
                    return new ResultadoSandbox
                    {
                        Estado = EstadoSandbox.InicioSolicitado,
                        Mensaje =
                            "Se solicitó la apertura de Windows Sandbox. " +
                            "Comprobá que aparezca su ventana.",
                        IdProceso = proceso == null
                            ? (int?)null
                            : proceso.Id
                    };
                }
            }
            catch (UnauthorizedAccessException)
            {
                return Resultado(
                    EstadoSandbox.AccesoDenegado,
                    "Windows denegó el acceso a la configuración o al lanzador."
                );
            }
            catch (Win32Exception error)
            {
                EstadoSandbox estado = EstadoSandbox.Error;

                if (error.NativeErrorCode == 5)
                    estado = EstadoSandbox.AccesoDenegado;

                if (error.NativeErrorCode == 1223)
                    estado = EstadoSandbox.Cancelado;

                return Resultado(
                    estado,
                    "Windows no pudo iniciar Sandbox: " + error.Message
                );
            }
            catch (Exception error) when (
                error is IOException ||
                error is XmlException ||
                error is ArgumentException ||
                error is NotSupportedException ||
                error is InvalidOperationException)
            {
                return Resultado(
                    EstadoSandbox.Error,
                    "No se pudo preparar la prueba: " + error.Message
                );
            }
        }

        private static string ValidarConfiguracion(string ruta)
        {
            XmlReaderSettings ajustes = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1000000
            };

            using (XmlReader lector = XmlReader.Create(ruta, ajustes))
            {
                XDocument documento = XDocument.Load(lector);
                XElement raiz = documento.Root;

                if (raiz == null || raiz.Name != "Configuration")
                    return "La raíz del XML debe ser Configuration.";

                var opcionesRed = raiz.Elements("Networking").ToList();

                if (opcionesRed.Count != 1 ||
                    !string.Equals(
                        opcionesRed[0].Value.Trim(),
                        "Disable",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return "La configuración debe desactivar la red.";
                }

                foreach (XElement carpeta in raiz
                    .Elements("MappedFolders")
                    .Elements("MappedFolder"))
                {
                    var permisos = carpeta.Elements("ReadOnly").ToList();

                    if (permisos.Count != 1 ||
                        !string.Equals(
                            permisos[0].Value.Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return "Las carpetas compartidas deben ser de solo lectura.";
                    }
                }
            }

            return null;
        }

        private static ResultadoSandbox Resultado(
            EstadoSandbox estado,
            string mensaje)
        {
            return new ResultadoSandbox
            {
                Estado = estado,
                Mensaje = mensaje
            };
        }
    }
}